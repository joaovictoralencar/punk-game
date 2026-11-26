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
        [SerializeField] private RectTransform _damageNumbersHolder;

        private void Reset()
        {
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _health.Damaged += OnHealthChanged;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnHealthChanged;
        }

        private void OnHealthChanged(DamageInfo damageInfo)
        {
            if (damageInfo.Amount > 0)
            {
                DamageNumber damageNumber = DamageNumber.GetComponent<DamageNumberGUI>().SpawnGUI(_damageNumbersHolder, new Vector2(0, 0), damageInfo.Amount.ToString());
            }
        }
    }
}