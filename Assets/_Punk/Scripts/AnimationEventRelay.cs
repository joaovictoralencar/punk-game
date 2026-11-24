using Sirenix.OdinInspector;
using UnityEngine;

namespace PunkGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class AnimationEventRelay : MonoBehaviour
    {
        #region Inspector - References

        [FoldoutGroup("References")]
        [Tooltip("Animator on this child model object.")]
        [Required, SerializeField]
        private Animator _animator;

        [FoldoutGroup("References")]
        [Tooltip("CharacterAttack component on the character root.")]
        [Required, SerializeField]
        private CharacterAttack _attack;

        [FoldoutGroup("References")]
        [Tooltip("Optional CharacterMovement component on the character root for root motion.")]
        [SerializeField]
        private CharacterMovement _movement;

        #endregion

        #region Inspector - Debug

        [FoldoutGroup("Debug")]
        [Tooltip("Logs forwarded and ignored animation events to the Console.")]
        [SerializeField]
        private bool _debugLogs = true;

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            _animator = GetComponent<Animator>();
            _attack = GetComponentInParent<CharacterAttack>();
            _movement = GetComponentInParent<CharacterMovement>();

            if (_animator != null)
            {
                _animator.applyRootMotion = true;
            }
        }

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_attack == null) _attack = GetComponentInParent<CharacterAttack>();
            if (_movement == null) _movement = GetComponentInParent<CharacterMovement>();

            if (_animator != null)
            {
                _animator.applyRootMotion = true;
            }
        }

        private void OnAnimatorMove()
        {
            if (_animator == null || _movement == null) return;

            if (_movement.IsRootMotionActive)
            {
                _movement.AddRootMotionDelta(_animator.deltaPosition);
            }
        }

        #endregion

        #region Animation Events

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/Hitbox"), Button("Hitbox On")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        public void HitboxOn()
        {
            if (!CanForward(nameof(HitboxOn))) return;
            _attack.OnHitboxOn();
        }

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/Hitbox"), Button("Hitbox Off")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        public void HitboxOff()
        {
            if (!CanForward(nameof(HitboxOff))) return;
            _attack.OnHitboxOff();
        }

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/RootMotion"), Button("Root Motion On")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        public void RootMotionOn()
        {
            if (!CanForward(nameof(RootMotionOn))) return;
            _attack.OnRootMotionOn();
        }

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/RootMotion"), Button("Root Motion Off")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        public void RootMotionOff()
        {
            if (!CanForward(nameof(RootMotionOff))) return;
            _attack.OnRootMotionOff();
        }

        [FoldoutGroup("Debug")]
        [Button("Attack End")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        public void AttackEnd()
        {
            if (!CanForward(nameof(AttackEnd))) return;
            _attack.OnAttackEnd();
        }

        #endregion

        #region Helpers

        private bool CanForward(string eventName)
        {
            if (_attack == null || !_attack.IsAttacking)
            {
                if (_debugLogs)
                {
                    Debug.Log($"[AnimationEventRelay] Ignored '{eventName}' (not attacking).", this);
                }

                return false;
            }

            if (_debugLogs)
            {
                Debug.Log($"[AnimationEventRelay] Forwarded '{eventName}'.", this);
            }

            return true;
        }

        #endregion
    }
}