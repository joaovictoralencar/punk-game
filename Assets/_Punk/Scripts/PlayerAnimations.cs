using Sirenix.OdinInspector;
using UnityEngine;

namespace PunkGame
{
    public class PlayerAnimations : CharacterAnimations
    {
        private static readonly int DodgeHash = Animator.StringToHash("Dodge");

        [FoldoutGroup("References")]
        [Tooltip("Optional CharacterDodge component for dodge animation triggers.")]
        [SerializeField]
        private CharacterDodge _dodge;

        protected override void Reset()
        {
            base.Reset();
            if (!_dodge) _dodge = GetComponentInParent<CharacterDodge>();
        }

        protected override void Awake()
        {
            base.Awake();
            if (!_dodge) _dodge = GetComponentInParent<CharacterDodge>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_dodge != null) _dodge.Dodged += OnDodge;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_dodge != null) _dodge.Dodged -= OnDodge;
        }

        private void OnDodge(Vector3 direction)
        {
            if (!_animator) return;

            _animator.SetTrigger(DodgeHash);
        }
    }
}