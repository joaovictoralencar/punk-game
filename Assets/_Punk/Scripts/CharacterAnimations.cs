using UnityEngine;

namespace PunkGame
{
    public class CharacterAnimations : MonoBehaviour
    {
        private static readonly int Jump = Animator.StringToHash("Jump");
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int Grounded = Animator.StringToHash("Grounded");
        private static readonly int Dodge = Animator.StringToHash("Dodge");
        [SerializeField] protected Animator _animator;
        [SerializeField] protected CharacterMovement _characterMovement;

        protected virtual void OnEnable()
        {
            _characterMovement.SpeedChanged += OnSpeedChanged;
            _characterMovement.IsGroundedChanged += OnGroundedChanged;
            _characterMovement.Jumped += OnJump;
        }

        protected virtual void OnDisable()
        {
            _characterMovement.SpeedChanged -= OnSpeedChanged;
            _characterMovement.IsGroundedChanged -= OnGroundedChanged;
            _characterMovement.Jumped -= OnJump;
        }

        private void OnGroundedChanged(bool isGrounded)
        {
            _animator.SetBool(Grounded, isGrounded);
            if (isGrounded) _animator.ResetTrigger(Jump);
        }

        private void OnSpeedChanged(Vector3 speedVector)
        {
            float speed = speedVector.magnitude;

            if (Mathf.Approximately(speed, 0f))
            {
                speed = 0;
            }

            _animator.SetFloat(Speed, speed);
        }

        private void OnJump()
        {
            _animator.SetTrigger(Jump);
        }

        private void OnDodge(Vector2 direction)
        {
            _animator.SetTrigger(Dodge);
        }
    }
}