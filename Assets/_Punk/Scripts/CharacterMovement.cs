using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace PunkGame
{
// Runs after controllers (order 0) and action drivers like Dodge/Attack (order 5)
// so velocity overrides set this frame are applied on the exact same frame.
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : MonoBehaviour
    {
        #region Constants

        private const float GroundedStickForce = -2f;
        private const float MinDirectionSqrMagnitude = 0.0001f;
        private const float MinRotationSqrMagnitude = 0.01f;

        #endregion

        #region Inspector - References

        [FoldoutGroup("References")] [Tooltip("The CharacterController that actually moves this character.")] [SerializeField]
        private CharacterController _characterController;

        #endregion

        #region Inspector - Movement

        [FoldoutGroup("Movement")] [Tooltip("Maximum movement speed in units per second.")] [SerializeField, MinValue(0f)]
        private float _movementSpeed = 5f;

        [FoldoutGroup("Movement")] [Tooltip("How fast the character turns toward the movement direction. Higher = snappier.")] [SerializeField, MinValue(0f)]
        private float _rotationSpeed = 15f;

        #endregion

        #region Inspector - Jump

        [FoldoutGroup("Jump")] [Tooltip("Desired jump height in world units.")] [SerializeField, MinValue(0f)]
        private float _jumpHeight = 1.5f;

        [FoldoutGroup("Jump")] [Tooltip("Multiplies Physics.gravity for this character. Higher = snappier, less floaty jumps.")] [SerializeField, MinValue(0.1f)]
        private float _gravityMultiplier = 2f;

        #endregion

        #region Inspector - Ground Check

        [FoldoutGroup("Ground Check")] [Tooltip("Layers that count as ground.")] [SerializeField]
        private LayerMask _groundLayer;

        [FoldoutGroup("Ground Check")] [Tooltip("Point (usually at the feet) where the ground check sphere is centered.")] [SerializeField]
        private Transform _groundedTransform;

        [FoldoutGroup("Ground Check")] [Tooltip("Radius of the ground check sphere.")] [SerializeField, MinValue(0.01f)]
        private float _groundedRadius = .3f;

        #endregion

        #region Inspector - Debug

        [FoldoutGroup("Debug")] [Tooltip("Draws desired direction (red), velocity (cyan), root motion (magenta), and status overlay.")] [SerializeField]
        private bool _debugMovement;

        #endregion

        #region Runtime State

        private Vector3 _moveDirection;
        private Vector3 _velocity;
        private bool _isGrounded;

        // Locks are counters so several systems (Dodge, Attack, Stun) can lock without overriding each other.
        private int _moveLocks;
        private int _rotateLocks;
        private int _jumpLocks;

        // External horizontal velocity override (used by Dodge, Attack lunges, Knockback, etc.)
        private Vector3? _overrideHorizontalVelocity;

        // External horizontal root motion displacement (fed by AnimationEventRelay from OnAnimatorMove)
        private bool _isRootMotionActive;
        private Vector3 _pendingRootDelta;
        private Vector3 _lastAppliedRootDelta;

        #endregion

        #region Properties

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public Vector3 Velocity => _velocity;

        /// <summary>Actual horizontal velocity reported by the CharacterController after collisions.</summary>
        public Vector3 RealHorizontalVelocity
        {
            get
            {
                if (!_characterController) return Vector3.zero;
                Vector3 vel = _characterController.velocity;
                vel.y = 0f;
                return vel;
            }
        }

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public bool IsGrounded => _isGrounded;

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public bool IsRootMotionActive => _isRootMotionActive;

        /// <summary>World-space velocity the character wants to walk at (before gravity). Zero while locked or driven by root motion.</summary>
        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public Vector3 DesiredMoveSpeed { get; private set; }

        public bool CanMove => _moveLocks == 0;
        public bool CanRotate => _rotateLocks == 0;
        public bool CanJump => _jumpLocks == 0;

        #endregion

        #region Events

        public event Action<Vector3> SpeedChanged;
        public event Action<bool> IsGroundedChanged;
        public event Action Jumped;

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            _characterController = GetComponent<CharacterController>();
        }

        private void Awake()
        {
            if (!_characterController) _characterController = GetComponent<CharacterController>();
        }

        private void OnDisable()
        {
            _moveDirection = Vector3.zero;
            _overrideHorizontalVelocity = null;
            SetRootMotion(false);
        }

        private void Update()
        {
            UpdateGrounded();
            UpdateDesiredMovement();
            ApplyHorizontalVelocity();
            ApplyGravity();
            Move();
            RotateToMovement();
        }

        #endregion

        #region Public API

        /// <summary>Sets the world-space direction to walk toward. Magnitude is clamped to 1. Zero = stop.</summary>
        public void SetMoveDirection(Vector3 worldDirection)
        {
            Vector3 flat = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
            _moveDirection = Vector3.ClampMagnitude(flat, 1f);
        }

        /// <summary>Jumps if grounded and not locked.</summary>
        public void Jump()
        {
            if (!CanMove || !CanJump || !_isGrounded) return;

            float gravity = Physics.gravity.y * _gravityMultiplier;
            _velocity.y = Mathf.Sqrt(_jumpHeight * -2f * gravity);
            Jumped?.Invoke();
        }

        /// <summary>
        /// Overrides walking velocity with an external world-space horizontal velocity (e.g. Dodge, Attack lunge).
        /// Gravity still applies normally.
        /// </summary>
        public void SetHorizontalVelocityOverride(Vector3 worldVelocity)
        {
            _overrideHorizontalVelocity = new Vector3(worldVelocity.x, 0f, worldVelocity.z);
        }

        /// <summary>Clears any active horizontal velocity override, returning control to standard walking.</summary>
        public void ClearHorizontalVelocityOverride()
        {
            _overrideHorizontalVelocity = null;
        }

        /// <summary>Enables or disables external root motion displacement and clears any accumulated delta.</summary>
        public void SetRootMotion(bool active)
        {
            _isRootMotionActive = active;
            _pendingRootDelta = Vector3.zero;
            _lastAppliedRootDelta = Vector3.zero;
        }

        /// <summary>Accumulates horizontal world-space root motion displacement to be applied on the next Move().</summary>
        public void AddRootMotionDelta(Vector3 worldDelta)
        {
            if (!_isRootMotionActive) return;

            Vector3 flat = Vector3.ProjectOnPlane(worldDelta, Vector3.up);
            _pendingRootDelta += flat;
        }

        /// <summary>Immediately snaps the character's Y-axis rotation to face a world-space direction.</summary>
        public void SetRotationImmediate(Vector3 worldDirection)
        {
            Vector3 flat = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
            if (flat.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            }
        }

        // Every Add must be paired with exactly one Remove by the same system.
        public void AddMoveLock() => _moveLocks++;
        public void RemoveMoveLock() => _moveLocks = Mathf.Max(0, _moveLocks - 1);

        public void AddRotateLock() => _rotateLocks++;
        public void RemoveRotateLock() => _rotateLocks = Mathf.Max(0, _rotateLocks - 1);

        public void AddJumpLock() => _jumpLocks++;
        public void RemoveJumpLock() => _jumpLocks = Mathf.Max(0, _jumpLocks - 1);

        #endregion

        #region Movement

        private void UpdateDesiredMovement()
        {
            Vector3 desired = Vector3.zero;

            if (CanMove && !_isRootMotionActive && _moveDirection.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                desired = _moveDirection * _movementSpeed;
            }

            if (desired == DesiredMoveSpeed) return;

            DesiredMoveSpeed = desired;
            SpeedChanged?.Invoke(DesiredMoveSpeed);
        }

        private void ApplyHorizontalVelocity()
        {
            if (_overrideHorizontalVelocity.HasValue)
            {
                _velocity.x = _overrideHorizontalVelocity.Value.x;
                _velocity.z = _overrideHorizontalVelocity.Value.z;
            }
            else
            {
                _velocity.x = DesiredMoveSpeed.x;
                _velocity.z = DesiredMoveSpeed.z;
            }
        }

        private void Move()
        {
            if (!_characterController) return;

            Vector3 displacement = _velocity * Time.deltaTime;

            if (_isRootMotionActive)
            {
                displacement += _pendingRootDelta;
                _lastAppliedRootDelta = _pendingRootDelta;
                _pendingRootDelta = Vector3.zero;
            }
            else
            {
                _lastAppliedRootDelta = Vector3.zero;
            }

            _characterController.Move(displacement);
        }

        #endregion

        #region Rotation

        private void RotateToMovement()
        {
            if (!CanRotate) return;
            if (DesiredMoveSpeed.sqrMagnitude <= MinRotationSqrMagnitude) return;

            Quaternion targetRotation = Quaternion.LookRotation(DesiredMoveSpeed.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * _rotationSpeed);
        }

        #endregion

        #region Gravity

        private void ApplyGravity()
        {
            if (_isGrounded && _velocity.y < 0f)
            {
                _velocity.y = GroundedStickForce;
            }

            _velocity.y += Physics.gravity.y * _gravityMultiplier * Time.deltaTime;
        }

        #endregion

        #region Grounding

        private void UpdateGrounded()
        {
            if (!_groundedTransform) return;

            bool lastIsGrounded = _isGrounded;
            _isGrounded = Physics.CheckSphere(
                _groundedTransform.position,
                _groundedRadius,
                _groundLayer,
                QueryTriggerInteraction.Ignore);

            if (_isGrounded != lastIsGrounded) IsGroundedChanged?.Invoke(_isGrounded);
        }

        #endregion

        #region Debug & Gizmos

        [FoldoutGroup("Debug")]
        [Button("Toggle Root Motion")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void DebugToggleRootMotion()
        {
            SetRootMotion(!_isRootMotionActive);
        }

        private void OnGUI()
        {
            if (!_debugMovement) return;

            GUI.Label(new Rect(12f, 12f, 220f, 24f), $"Root motion: {(_isRootMotionActive ? "on" : "off")}");
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_groundedTransform) return;

            Gizmos.color = _isGrounded ? new Color(1f, 0.55f, 0f) : Color.yellow;
            Gizmos.DrawSphere(_groundedTransform.position, _groundedRadius);
        }

        private void OnDrawGizmos()
        {
            if (!_debugMovement) return;

            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, DesiredMoveSpeed);
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, _velocity);

            if (_isRootMotionActive && _lastAppliedRootDelta.sqrMagnitude > 0f)
            {
                Gizmos.color = Color.magenta;
                Vector3 rootVelocity = Time.deltaTime > 0f ? _lastAppliedRootDelta / Time.deltaTime : _lastAppliedRootDelta;
                Vector3 origin = transform.position + Vector3.up * 0.1f;
                Vector3 tip = origin + rootVelocity;

                Gizmos.DrawLine(origin, tip);

                Vector3 dir = rootVelocity.normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir) * 0.15f;
                Gizmos.DrawLine(tip, tip - dir * 0.25f + right);
                Gizmos.DrawLine(tip, tip - dir * 0.25f - right);
            }
        }
#endif

        #endregion
    }
}