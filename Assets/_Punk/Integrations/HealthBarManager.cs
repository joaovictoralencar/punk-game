using System.Collections.Generic;
using PunkGame.Core.Combat;
using PunkGame.UI;
using UnityEngine;

namespace PunkGame.Integrations
{
    [DisallowMultipleComponent]
    public sealed class HealthBarManager : MonoBehaviour
    {
        [SerializeField] private EnemySpawnManager _enemySpawnManager;
        [SerializeField] private HealthBar _healthBarPrefab;

        private readonly Dictionary<Health, HealthBar> _healthBars = new Dictionary<Health, HealthBar>();
        private bool _referencesValidated;
        private bool _hasRequiredReferences;

        private void OnEnable()
        {
            if (_enemySpawnManager == null) return;

            _enemySpawnManager.EnemyRegistered += HandleEnemyRegistered;
            _enemySpawnManager.EnemyUnregistered += HandleEnemyUnregistered;
            SynchronizeAliveEnemies();
        }

        private void Start()
        {
            SynchronizeAliveEnemies();
        }

        private void OnDisable()
        {
            if (_enemySpawnManager == null) return;

            _enemySpawnManager.EnemyRegistered -= HandleEnemyRegistered;
            _enemySpawnManager.EnemyUnregistered -= HandleEnemyUnregistered;
        }

        public HealthBar RegisterEnemy(Health enemy)
        {
            if (enemy == null || !HasRequiredReferences()) return null;

            if (enemy.gameObject.scene != gameObject.scene)
            {
                Debug.LogError($"[{nameof(HealthBarManager)}] Cannot register an enemy from another scene.", this);
                return null;
            }

            if (_healthBars.TryGetValue(enemy, out HealthBar registeredBar) && registeredBar != null)
                return registeredBar;

            HealthBar healthBar = FindHealthBar(enemy);
            if (healthBar == null)
                healthBar = Instantiate(_healthBarPrefab, _enemySpawnManager.Context.ScreenSpaceRoot, false);

            healthBar.Bind(enemy);
            _healthBars[enemy] = healthBar;
            return healthBar;
        }

        public void UnregisterEnemy(Health enemy)
        {
            if (ReferenceEquals(enemy, null) || !_healthBars.TryGetValue(enemy, out HealthBar healthBar)) return;

            _healthBars.Remove(enemy);
            if (healthBar != null) Destroy(healthBar.gameObject);
        }

        private void HandleEnemyRegistered(Health enemy)
        {
            RegisterEnemy(enemy);
        }

        private void HandleEnemyUnregistered(Health enemy)
        {
            UnregisterEnemy(enemy);
        }

        private void SynchronizeAliveEnemies()
        {
            if (_enemySpawnManager == null) return;

            foreach (Health enemy in _enemySpawnManager.AliveEnemies)
                RegisterEnemy(enemy);
        }

        private HealthBar FindHealthBar(Health owner)
        {
            HealthBar[] bars = _enemySpawnManager.Context.ScreenSpaceRoot.GetComponentsInChildren<HealthBar>(true);
            foreach (HealthBar bar in bars)
            {
                if (bar.Owner == owner) return bar;
            }

            return null;
        }

        private bool HasRequiredReferences()
        {
            if (_referencesValidated) return _hasRequiredReferences;

            _referencesValidated = true;
            _hasRequiredReferences = _enemySpawnManager != null &&
                                     _enemySpawnManager.Context != null &&
                                     _enemySpawnManager.Context.ScreenSpaceRoot != null &&
                                     _healthBarPrefab != null;

            if (!_hasRequiredReferences)
            {
                Debug.LogError(
                    $"[{nameof(HealthBarManager)}] Assign an EnemySpawnManager with a valid GameplayContext and a health-bar prefab.",
                    this);
            }

            return _hasRequiredReferences;
        }
    }
}
