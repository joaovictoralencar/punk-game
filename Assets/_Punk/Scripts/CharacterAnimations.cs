using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace PunkGame
{
    public class CharacterAnimations : MonoBehaviour
    {
        #region Animator Hashes

        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DieHash = Animator.StringToHash("Die");

        #endregion

        #region Inspector - References

        [FoldoutGroup("References")]
        [Tooltip("Animator driven by this component.")]
        [Required, SerializeField]
        protected Animator _animator;

        [FoldoutGroup("References")]
        [Tooltip("Optional CharacterMovement component for locomotion, jump, and grounded states.")]
        [SerializeField]
        protected CharacterMovement _characterMovement;

        [FoldoutGroup("References")]
        [Tooltip("Optional CharacterAttack component for cross-fading attack states.")]
        [SerializeField]
        protected CharacterAttack _attack;

        [FoldoutGroup("References")]
        [Tooltip("Optional Health component for hit reaction and death triggers.")]
        [SerializeField]
        protected Health _health;

        #endregion

        #region Inspector - Combat

        [FoldoutGroup("Combat")]
        [Tooltip("Animator layer index containing the Attacks sub-state machine.")]
        [SerializeField, MinValue(0)]
        protected int _attackLayerIndex;

        #endregion

        #region Runtime State

        private readonly Dictionary<string, int> _stateHashCache = new Dictionary<string, int>();

        #endregion

        #region Unity Lifecycle

        protected virtual void Reset()
        {
            if (!_animator) _animator = GetComponentInChildren<Animator>();
            if (!_characterMovement) _characterMovement = GetComponentInParent<CharacterMovement>();
            if (!_attack) _attack = GetComponentInParent<CharacterAttack>();
            if (!_health) _health = GetComponentInParent<Health>();
        }

        protected virtual void Awake()
        {
            if (!_animator) _animator = GetComponentInChildren<Animator>();
            if (!_characterMovement) _characterMovement = GetComponentInParent<CharacterMovement>();
            if (!_attack) _attack = GetComponentInParent<CharacterAttack>();
            if (!_health) _health = GetComponentInParent<Health>();
        }

        protected virtual void OnEnable()
        {
            if (_characterMovement != null)
            {
                _characterMovement.SpeedChanged += OnSpeedChanged;
                _characterMovement.IsGroundedChanged += OnGroundedChanged;
                _characterMovement.Jumped += OnJump;
            }

            if (_attack != null)
            {
                _attack.AttackStarted += OnAttackStarted;
            }

            if (_health != null)
            {
                _health.Damaged += OnDamaged;
                _health.Died += OnDied;
            }
        }

        protected virtual void OnDisable()
        {
            if (_characterMovement != null)
            {
                _characterMovement.SpeedChanged -= OnSpeedChanged;
                _characterMovement.IsGroundedChanged -= OnGroundedChanged;
                _characterMovement.Jumped -= OnJump;
            }

            if (_attack != null)
            {
                _attack.AttackStarted -= OnAttackStarted;
            }

            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.Died -= OnDied;
            }
        }

        #endregion

        #region Locomotion Handlers

        protected virtual void OnGroundedChanged(bool isGrounded)
        {
            if (!_animator) return;

            _animator.SetBool(GroundedHash, isGrounded);
            if (isGrounded) _animator.ResetTrigger(JumpHash);
        }

        protected virtual void OnSpeedChanged(Vector3 speedVector)
        {
            if (!_animator) return;

            float speed = speedVector.magnitude;
            if (Mathf.Approximately(speed, 0f)) speed = 0f;

            _animator.SetFloat(SpeedHash, speed);
        }

        protected virtual void OnJump()
        {
            if (!_animator) return;

            _animator.SetTrigger(JumpHash);
        }

        #endregion

        #region Combat Handlers

        protected virtual void OnAttackStarted(AttackStep step, int index)
        {
            if (!_animator || step == null) return;

            string stateName = step.StateName;
            if (string.IsNullOrEmpty(stateName)) return;

            _animator.CrossFadeInFixedTime(GetStateHash(stateName), step.BlendDuration, _attackLayerIndex, 0f);
        }

        protected virtual void OnDamaged(DamageInfo info)
        {
            if (!_animator) return;

            // Skip hit flinch if this blow was lethal so the Hit trigger doesn't stay queued over Die.
            if (_health != null && !_health.IsAlive) return;

            _animator.SetTrigger(HitHash);
        }

        protected virtual void OnDied()
        {
            if (!_animator) return;

            _animator.ResetTrigger(HitHash);
            _animator.SetTrigger(DieHash);
        }

        #endregion

        #region Helpers

        protected int GetStateHash(string stateName)
        {
            if (!_stateHashCache.TryGetValue(stateName, out int hash))
            {
                hash = Animator.StringToHash(stateName);
                _stateHashCache[stateName] = hash;
            }

            return hash;
        }

        #endregion
    }
}