using UnityEngine;

namespace PunkGame
{
    public class PlayerAnimations : CharacterAnimations
    {
        [SerializeField] private CharacterDodge _dodge;
        
        private static readonly int Dodge = Animator.StringToHash("Dodge");

        protected override void OnEnable()
        {
            base.OnEnable();
            _dodge.Dodged += OnDodge;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _dodge.Dodged -= OnDodge;
        }

        private void OnDodge(Vector3 direction)
        {
            _animator.SetTrigger(Dodge);
        }
    }
}