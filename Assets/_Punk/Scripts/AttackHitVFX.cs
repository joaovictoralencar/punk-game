using UnityEngine;

namespace PunkGame
{
    public class AttackHitVFX : MonoBehaviour
    {
        [SerializeField] private WeaponHitbox _hitbox;
        [SerializeField] private GameObject _hitVFXPrefab;

        private void OnEnable()
        {
            _hitbox.Hit += OnHit;
        }

        private void OnDisable()
        {
            _hitbox.Hit -= OnHit;
        }

        private void Reset()
        {
            _hitbox = GetComponent<WeaponHitbox>();
        }

        private void OnHit(IDamageable damageable, DamageInfo damageInfo, DamageResult result)
        {
            var hit = Instantiate(_hitVFXPrefab, damageInfo.HitPoint, Quaternion.LookRotation(damageInfo.HitDirection * -1));
            Destroy(hit, 2f);
        }
    }
}