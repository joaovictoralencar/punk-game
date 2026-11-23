using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PunkGame
{
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputHandler : MonoBehaviour
    {
        #region Inspector - References

        [FoldoutGroup("References")] [SerializeField]
        private PlayerInput _input;

        #endregion

        #region Runtime State

        private Vector2 _lastInputMovement;

        #endregion

        #region Properties

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public Vector2 InputMovement { get; private set; }

        #endregion

        #region Events

        /// <summary>Raised when the movement input changes.</summary>
        public event Action<Vector2> InputMovementChanged;

        /// <summary>Raised once each time the button is pressed. Releases are ignored.</summary>
        public event Action JumpPerformed;

        public event Action<Vector2> DodgePerformed;
        public event Action AttackPerformed;

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            _input = GetComponent<PlayerInput>();
        }

        private void Awake()
        {
            if (!_input) TryGetComponent(out _input);
        }

        private void OnDisable()
        {
            // Prevents stuck input (e.g. the character walking on its own) when the object is disabled mid-press.
            SetMovement(Vector2.zero);
        }

        #endregion

        #region Input Callbacks (PlayerInput - Send Messages)

        public void OnMove(InputValue value) => SetMovement(value.Get<Vector2>());

        public void OnJump(InputValue value)
        {
            if (value.isPressed) JumpPerformed?.Invoke();
        }

        public void OnDodge(InputValue value)
        {
            if (value.isPressed) DodgePerformed?.Invoke(InputMovement);
        }

        public void OnAttack(InputValue value)
        {
            if (value.isPressed) AttackPerformed?.Invoke();
        }

        #endregion

        #region State Setters

        private void SetMovement(Vector2 movement)
        {
            InputMovement = movement;
            if (_lastInputMovement == InputMovement) return;

            _lastInputMovement = InputMovement;
            InputMovementChanged?.Invoke(InputMovement);
        }

        #endregion
    }
}