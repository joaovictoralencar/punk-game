using Sirenix.OdinInspector;
using UnityEngine;

namespace PunkGame
{
    public class PlayerController : MonoBehaviour
    {
        #region Constants

        private const float MinInputSqrMagnitude = 0.0001f;

        // Dodge input below this magnitude (0.2) counts as "no direction" and dodges forward instead.
        private const float MinDodgeInputSqrMagnitude = 0.04f;

        #endregion

        #region Inspector - References

        [FoldoutGroup("References")] [Tooltip("Provides the raw 2D movement input and the jump/dodge events.")] [SerializeField]
        private PlayerInputHandler _inputHandler;

        [FoldoutGroup("References")] [Tooltip("The movement component this controller drives.")] [SerializeField]
        private CharacterMovement _movement;

        [FoldoutGroup("References")] [Tooltip("The dodge component.")] [SerializeField]
        private CharacterDodge _dodge;


        [FoldoutGroup("References")] [Tooltip("Optional. Transform movement is relative to. If empty, Camera.main is used (the camera driven by the Cinemachine Brain).")] [SerializeField]
        private Transform _cameraTransform;

        #endregion

        #region Runtime State

        private Vector2 _moveInput;

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            _inputHandler = GetComponent<PlayerInputHandler>();
            _movement = GetComponent<CharacterMovement>();
        }

        private void Awake()
        {
            if (!_cameraTransform && Camera.main) _cameraTransform = Camera.main.transform;
        }

        private void OnEnable()
        {
            if (!_inputHandler) return;

            _inputHandler.InputMovementChanged += OnInputMovementChanged;
            _inputHandler.JumpPerformed += OnJump;
            _inputHandler.DodgePerformed += OnDodge;
        }

        private void OnDisable()
        {
            if (_inputHandler)
            {
                _inputHandler.InputMovementChanged -= OnInputMovementChanged;
                _inputHandler.JumpPerformed -= OnJump;
                _inputHandler.DodgePerformed -= OnDodge;
            }

            _moveInput = Vector2.zero;
            if (_movement) _movement.SetMoveDirection(Vector3.zero);
        }

        private void Update()
        {
            if (!_movement) return;

            // Recomputed every frame so the direction follows the camera even if the stick doesn't change.
            _movement.SetMoveDirection(GetCameraRelativeDirection(_moveInput));
        }

        #endregion

        #region Input

        private void OnInputMovementChanged(Vector2 moveInput)
        {
            _moveInput = moveInput;
        }

        private void OnJump()
        {
            if (_movement) _movement.Jump();
        }

        private void OnDodge(Vector2 input)
        {
            if (!_movement) return;

            Vector3 direction = input.sqrMagnitude >= MinDodgeInputSqrMagnitude
                ? GetCameraRelativeDirection(input)
                : Vector3.zero; // Zero makes the movement component dodge forward.

            _dodge.Dodge(direction);
        }

        #endregion

        #region Camera Relative Direction

        private Vector3 GetCameraRelativeDirection(Vector2 input)
        {
            if (input.sqrMagnitude < MinInputSqrMagnitude) return Vector3.zero;

            if (!_cameraTransform)
            {
                if (!Camera.main) return new Vector3(input.x, 0f, input.y);
                _cameraTransform = Camera.main.transform;
            }

            // Flatten the camera forward onto the ground plane so the camera pitch (isometric) doesn't slow movement.
            Vector3 forward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up);
            if (forward.sqrMagnitude < MinInputSqrMagnitude)
            {
                // Camera looking straight down: use its up vector instead.
                forward = Vector3.ProjectOnPlane(_cameraTransform.up, Vector3.up);
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            Vector3 direction = forward * input.y + right * input.x;
            return Vector3.ClampMagnitude(direction, 1f);
        }

        #endregion
    }
}