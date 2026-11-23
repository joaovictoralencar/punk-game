using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace PunkGame
{
// Runs after PlayerController (order 0) and before CharacterMovement (order 10)
// so dodge velocity overrides are applied on the exact frame the dodge starts.
    [DefaultExecutionOrder(5)]
    [RequireComponent(typeof(CharacterMovement))]
    public class CharacterDodge : MonoBehaviour
    {
        #region Constants

        private const float MinDirectionSqrMagnitude = 0.0001f;

        // A dodge is considered blocked (wall) when the real speed drops below this ratio of the intended speed
        private const float DodgeBlockedSpeedRatio = 0.2f;

        // A dodge is considered blocked (wall) when the real speed drops below this ratio of the intended speed for at least this long (seconds)...
        private const float DodgeBlockedTimeout = 0.1f;

        // A dodge is considered blocked (wall) when the real speed drops below this ratio of the intended speed and only when the intended speed is meaningful (ignores the slow start of the curve).
        private const float DodgeBlockedMinIntendedSpeed = 1f;

        #endregion

        #region Inspector - References

        [FoldoutGroup("References")] [Tooltip("The CharacterMovement component this dodge drives.")] [SerializeField]
        private CharacterMovement _movement;

        [FoldoutGroup("References")] [Tooltip("Optional Health component toggled during the dodge invulnerability window.")] [SerializeField]
        private Health _health;

        #endregion

        #region Inspector - Dodge

        [FoldoutGroup("Dodge")] [Tooltip("Total distance covered by a dodge, in world units.")] [SerializeField, MinValue(0f)]
        private float _dodgeDistance = 4f;

        [FoldoutGroup("Dodge")] [Tooltip("How long a dodge lasts, in seconds. Should match the dodge animation length.")] [SerializeField, MinValue(0.05f)]
        private float _dodgeDuration = 0.4f;

        [FoldoutGroup("Dodge")] [Tooltip("Progress over time (X = normalized time, Y = normalized distance, 0 to 1). Controls how the distance is spent: slow start / fast middle / slow end, etc.")] [SerializeField]
        private AnimationCurve _dodgeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [FoldoutGroup("Dodge")] [Tooltip("Time in seconds after a dodge ends before another one can start.")] [SerializeField, MinValue(0f)]
        private float _dodgeCooldown = 0.5f;

        [FoldoutGroup("Dodge")] [Tooltip("A dodge pressed this many seconds before it becomes available is remembered and fired as soon as possible. 0 disables buffering.")] [SerializeField, MinValue(0f)]
        private float _dodgeBufferTime = 0.15f;

        [FoldoutGroup("Dodge")] [Tooltip("Normalized time window (0 to 1) of the dodge during which the character cannot take damage.")] [SerializeField, MinMaxSlider(0f, 1f)]
        private Vector2 _invulnerableWindow = new Vector2(0.1f, 0.6f);

        [FoldoutGroup("Dodge")] [Tooltip("If disabled, dodging is only possible while grounded.")] [SerializeField]
        private bool _allowAirDodge;

        #endregion

        #region Inspector - Debug

        [FoldoutGroup("Debug")] [Tooltip("Draws the active dodge direction (green) as a gizmo.")] [SerializeField]
        private bool _debugDodge;

        #endregion

        #region Runtime State

        private bool _canDodge = true;
        private bool _isDodging;
        private bool _invulnerabilityApplied;
        private float _dodgeTimer;
        private float _lastDodgeProgress;
        private float _nextDodgeTime;
        private float _dodgeBlockedTime;
        private float _currentIntendedSpeed;
        private Vector3 _dodgeDirection;

        // Dodge buffer
        private bool _hasBufferedDodge;
        private Vector3 _bufferedDodgeDirection;
        private float _bufferExpireTime;

        #endregion

        #region Properties

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public bool IsDodging => _isDodging;

        /// <summary>True while the dodge is inside its invulnerability window.</summary>
        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public bool IsInvulnerable => _invulnerabilityApplied;

        public bool CanDodge => _canDodge;

        #endregion

        #region Events

        /// <summary>Raised when a dodge starts. Parameter is the world-space dodge direction.</summary>
        public event Action<Vector3> Dodged;

        /// <summary>Raised when a dodge ends (finished, blocked by a wall, or interrupted).</summary>
        public event Action DodgeEnded;

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            _movement = GetComponent<CharacterMovement>();
            _health = GetComponent<Health>();
        }

        private void Awake()
        {
            if (!_movement) _movement = GetComponent<CharacterMovement>();
            if (!_health) _health = GetComponent<Health>();
        }

        private void OnDisable()
        {
            _hasBufferedDodge = false;
            if (_isDodging) EndDodge();
            ReleaseInvulnerability();
        }

        private void Update()
        {
            ConsumeBufferedDodge();

            if (!_isDodging) return;

            // Check if the previous frame's dodge movement was blocked by a wall.
            CheckDodgeBlocked();

            if (_isDodging)
            {
                ApplyDodgeVelocity();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Dodges toward a world-space direction. A zero (or near zero) direction dodges forward.
        /// If the dodge can't start right now, it is buffered for a short window.
        /// </summary>
        public void Dodge(Vector3 worldDirection)
        {
            if (TryStartDodge(worldDirection)) return;
            if (_dodgeBufferTime <= 0f) return;

            _bufferedDodgeDirection = worldDirection;
            _bufferExpireTime = Time.time + _dodgeBufferTime;
            _hasBufferedDodge = true;
        }

        /// <summary>Allows external systems (e.g. taking damage/stagger) to interrupt an active dodge.</summary>
        public void CancelDodge()
        {
            if (_isDodging) EndDodge();
        }

        public void SetCanDodge(bool value) => _canDodge = value;

        #endregion

        #region Dodge Execution

        private bool CanStartDodge()
        {
            if (!_movement) return false;

            return _canDodge
                   && !_isDodging
                   && Time.time >= _nextDodgeTime
                   && (_movement.IsGrounded || _allowAirDodge);
        }

        private bool TryStartDodge(Vector3 worldDirection)
        {
            if (!CanStartDodge()) return false;

            Vector3 direction = Vector3.ProjectOnPlane(worldDirection, Vector3.up);

            if (direction.sqrMagnitude < MinDirectionSqrMagnitude)
            {
                direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }

            _dodgeDirection = direction.normalized;

            // Lock standard locomotion and snap facing to the dodge direction.
            _movement.AddMoveLock();
            _movement.AddRotateLock();
            _movement.AddJumpLock();
            _movement.SetRotationImmediate(_dodgeDirection);

            _isDodging = true;
            _dodgeTimer = 0f;
            _lastDodgeProgress = 0f;
            _dodgeBlockedTime = 0f;
            _currentIntendedSpeed = 0f;
            _hasBufferedDodge = false;

            UpdateInvulnerability(0f);

            Dodged?.Invoke(_dodgeDirection);
            return true;
        }

        private void ConsumeBufferedDodge()
        {
            if (!_hasBufferedDodge) return;

            if (Time.time > _bufferExpireTime)
            {
                _hasBufferedDodge = false;
                return;
            }

            TryStartDodge(_bufferedDodgeDirection);
        }

        private void ApplyDodgeVelocity()
        {
            if (Time.deltaTime <= 0f) return;

            _dodgeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_dodgeTimer / _dodgeDuration);

            UpdateInvulnerability(t);

            float progress = _dodgeCurve.Evaluate(t);
            float step = (progress - _lastDodgeProgress) * _dodgeDistance;
            _lastDodgeProgress = progress;

            _currentIntendedSpeed = step / Time.deltaTime;
            _movement.SetHorizontalVelocityOverride(_dodgeDirection * _currentIntendedSpeed);

            if (t >= 1f) EndDodge();
        }

        private void CheckDodgeBlocked()
        {
            if (!_isDodging || !_movement) return;

            float actualSpeed = _movement.RealHorizontalVelocity.magnitude;

            bool blocked = _currentIntendedSpeed > DodgeBlockedMinIntendedSpeed
                           && actualSpeed < _currentIntendedSpeed * DodgeBlockedSpeedRatio;

            _dodgeBlockedTime = blocked ? _dodgeBlockedTime + Time.deltaTime : 0f;

            if (_dodgeBlockedTime >= DodgeBlockedTimeout) EndDodge();
        }

        private void EndDodge()
        {
            if (!_isDodging) return;

            _isDodging = false;
            _nextDodgeTime = Time.time + _dodgeCooldown;

            ReleaseInvulnerability();

            if (_movement)
            {
                _movement.ClearHorizontalVelocityOverride();
                _movement.RemoveMoveLock();
                _movement.RemoveRotateLock();
                _movement.RemoveJumpLock();
            }

            DodgeEnded?.Invoke();
        }

        #endregion

        #region Invulnerability

        private void UpdateInvulnerability(float normalizedTime)
        {
            bool insideWindow = _isDodging
                                && normalizedTime >= _invulnerableWindow.x
                                && normalizedTime <= _invulnerableWindow.y;

            if (insideWindow)
            {
                ApplyInvulnerability();
            }
            else
            {
                ReleaseInvulnerability();
            }
        }

        private void ApplyInvulnerability()
        {
            if (_invulnerabilityApplied) return;

            _invulnerabilityApplied = true;
            if (_health) _health.SetCanTakeDamage(false);
        }

        private void ReleaseInvulnerability()
        {
            if (!_invulnerabilityApplied) return;

            _invulnerabilityApplied = false;
            if (_health) _health.SetCanTakeDamage(true);
        }

        #endregion

        #region Gizmos

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_debugDodge || !_isDodging) return;

            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, _dodgeDirection * _dodgeDistance);
        }
#endif

        #endregion
    }
}