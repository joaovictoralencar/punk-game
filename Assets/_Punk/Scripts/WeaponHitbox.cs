using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace PunkGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider), typeof(Rigidbody))]
    public class WeaponHitbox : MonoBehaviour
    {
        [FoldoutGroup("References")]
        [Required, SerializeField]
        private Health _ownerHealth;

        [FoldoutGroup("References")]
        [Required, SerializeField]
        private Collider _collider;

        [FoldoutGroup("Debug")]
        [SerializeField]
        private bool _drawGizmos = true;

        [FoldoutGroup("Debug")]
        [SerializeField]
        private bool _debugLogs;

        [FoldoutGroup("Debug")]
        [SerializeField, MinValue(0f)]
        private float _debugDamage = 10f;

        [FoldoutGroup("Debug")]
        [SerializeField, MinValue(0f)]
        private float _debugKnockback = 5f;

        [FoldoutGroup("Debug")]
        [SerializeField]
        private HitStrength _debugStrength = HitStrength.Light;

        private readonly HashSet<IDamageable> _alreadyHit = new HashSet<IDamageable>();
        private float _currentDamage;
        private float _currentKnockback;
        private HitStrength _currentStrength;

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public bool IsActive { get; private set; }

        public event Action<IDamageable, DamageInfo, DamageResult> Hit;

        private void Reset()
        {
            _collider = GetComponent<Collider>();
            _ownerHealth = GetComponentInParent<Health>();

            if (_collider != null) _collider.isTrigger = true;

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        private void Awake()
        {
            if (_collider == null) _collider = GetComponent<Collider>();
            if (_ownerHealth == null) _ownerHealth = GetComponentInParent<Health>();

            if (_collider != null)
            {
                _collider.isTrigger = true;
                _collider.enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_ownerHealth != null) _ownerHealth.Died += End;
        }

        private void OnDisable()
        {
            if (_ownerHealth != null) _ownerHealth.Died -= End;
            End();
        }

        private void OnTriggerEnter(Collider other) => TryHit(other);
        private void OnTriggerStay(Collider other) => TryHit(other);

        public void Begin(float damage, float knockback, HitStrength strength)
        {
            _currentDamage = damage;
            _currentKnockback = knockback;
            _currentStrength = strength;

            _alreadyHit.Clear();
            IsActive = true;
            if (_collider != null) _collider.enabled = true;

            if (_debugLogs)
            {
                Debug.Log($"[WeaponHitbox] {name} Begin ({damage} dmg, {strength}).", this);
            }
        }

        public void End()
        {
            IsActive = false;
            if (_collider != null) _collider.enabled = false;
            _alreadyHit.Clear();
        }

        private void TryHit(Collider other)
        {
            if (!IsActive || _ownerHealth == null) return;

            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null || ReferenceEquals(target, _ownerHealth) || target.Team == _ownerHealth.Team) return;
            if (!_alreadyHit.Add(target) || !target.IsAlive) return;

            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 hitDirection = ComputeHorizontalDirection(target, other);

            var info = new DamageInfo(
                _currentDamage,
                _ownerHealth.Team,
                _currentStrength,
                _ownerHealth.gameObject,
                hitPoint,
                hitDirection,
                _currentKnockback);

            DamageResult result = target.TakeDamage(in info);
            if (result == DamageResult.Ignored) return;

            if (_debugLogs)
            {
                Debug.Log($"[WeaponHitbox] {name} hit {other.name} -> {result}.", this);
            }

            Hit?.Invoke(target, info, result);
        }

        private Vector3 ComputeHorizontalDirection(IDamageable target, Collider fallbackCollider)
        {
            Vector3 targetPos = target is Component comp ? comp.transform.position : fallbackCollider.transform.position;
            Vector3 dir = targetPos - _ownerHealth.transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f) return dir.normalized;

            Vector3 forward = _ownerHealth.transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/Actions"), Button("Begin")]
        [EnableIf("@UnityEngine.Application.isPlaying && !IsActive")]
        private void DebugBegin() => Begin(_debugDamage, _debugKnockback, _debugStrength);

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/Actions"), Button("End")]
        [EnableIf("@UnityEngine.Application.isPlaying && IsActive")]
        private void DebugEnd() => End();

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_drawGizmos || !IsActive || _collider == null) return;

            Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.4f);
            Gizmos.DrawCube(_collider.bounds.center, _collider.bounds.size);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(_collider.bounds.center, _collider.bounds.size);
        }
#endif
    }
}