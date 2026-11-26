using System;
using DamageNumbersPro;
using PunkGame.Core.Combat;
using UnityEngine;

namespace PunkGame
{
    [RequireComponent(typeof(Health))]
    public class DamageNumbersManager : MonoBehaviour
    {
        [SerializeField] private GameObject DamageNumber;
        [SerializeField] private Health _health;
        [SerializeField] private GameplayContext _context;

        private DamageNumberGUI _damageNumberPrefab;

        private void Reset()
        {
            _health = GetComponent<Health>();
        }

        private void Awake()
        {
            if (_health == null) _health = GetComponent<Health>();
            if (DamageNumber != null) _damageNumberPrefab = DamageNumber.GetComponent<DamageNumberGUI>();
        }

        private void OnEnable()
        {
            _health.Damaged += OnHealthChanged;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Damaged -= OnHealthChanged;
        }

        public void BindContext(GameplayContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            _context = context;
        }

        private void OnHealthChanged(DamageInfo damageInfo)
        {
            if (damageInfo.Amount <= 0f) return;

            if (_damageNumberPrefab == null ||
                _context == null ||
                _context.ScreenSpaceRoot == null ||
                _context.OutputCamera == null)
            {
                Debug.LogError($"[{nameof(DamageNumbersManager)}] Assign a DamageNumberGUI prefab and a valid GameplayContext.", this);
                return;
            }

            RectTransform screenSpaceRoot = _context.ScreenSpaceRoot;
            Vector3 screenPosition = _context.OutputCamera.WorldToScreenPoint(damageInfo.HitPoint);
            if (screenPosition.z <= 0f) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    screenSpaceRoot,
                    screenPosition,
                    null,
                    out Vector2 anchoredPosition))
            {
                return;
            }

            _damageNumberPrefab.SpawnGUI(screenSpaceRoot, anchoredPosition, damageInfo.Amount.ToString());
        }
    }
}