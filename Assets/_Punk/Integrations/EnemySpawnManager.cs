using System;
using System.Collections.Generic;
using PunkGame;
using PunkGame.Core.Combat;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

namespace PunkGame.Integrations
{
    [DisallowMultipleComponent]
    public sealed class EnemySpawnManager : MonoBehaviour
    {
        private const int SpawnPositionAttempts = 20;
        private const int GizmoCircleSegments = 48;

        [SerializeField] private GameplayContext _context;

        [SerializeField]
        private List<Health> _placedEnemies = new List<Health>();

        [FoldoutGroup("Spawn")]
        [Required, SerializeField]
        private GameObject _enemyPrefab;

        [FoldoutGroup("Spawn")]
        [SerializeField, MinValue(0f)]
        private float _spawnRadius = 8f;

        [FoldoutGroup("Spawn")]
        [SerializeField, MinValue(1)]
        private int _spawnCount = 5;

        [FoldoutGroup("Spawn")]
        [SerializeField, MinValue(0.1f)]
        private float _navMeshSampleDistance = 2f;
        
        [FoldoutGroup("Spawn")]
        [Required, SerializeField]
        private bool _spawnBatchOnStart;

        private readonly HashSet<Health> _aliveEnemies = new HashSet<Health>();
        private readonly Dictionary<Health, Action> _deathHandlers = new Dictionary<Health, Action>();

        public GameplayContext Context => _context;
        public int AliveEnemyCount => _aliveEnemies.Count;
        public IReadOnlyCollection<Health> AliveEnemies => _aliveEnemies;

        public event Action<Health> EnemyRegistered;
        public event Action<Health> EnemyUnregistered;

        private void Start()
        {
            if (!HasRequiredContext()) return;

            foreach (Health enemy in _placedEnemies)
                RegisterEnemy(enemy);

            if (_spawnBatchOnStart)
            {
                SpawnBatchInRange();
            }
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<Health, Action> entry in _deathHandlers)
            {
                if (entry.Key != null) entry.Key.Died -= entry.Value;
            }
        }

        public Health SpawnEnemy(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (!HasRequiredContext())
                throw new InvalidOperationException($"[{nameof(EnemySpawnManager)}] A valid GameplayContext is required.");

            GameObject instance = Instantiate(prefab, position, rotation);
            Health enemy = instance.GetComponent<Health>();
            if (enemy == null || enemy.Team != Team.Enemy)
            {
                Destroy(instance);
                throw new InvalidOperationException(
                    $"[{nameof(EnemySpawnManager)}] Spawned prefab '{prefab.name}' must have an enemy Health component on its root.");
            }

            if (!RegisterEnemy(enemy))
            {
                Destroy(instance);
                throw new InvalidOperationException(
                    $"[{nameof(EnemySpawnManager)}] Failed to register spawned enemy '{instance.name}'.");
            }

            return enemy;
        }

        public int SpawnEnemiesInRange(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0) return 0;
            if (_enemyPrefab == null)
                throw new InvalidOperationException($"[{nameof(EnemySpawnManager)}] Assign an enemy prefab.");
            if (_spawnRadius < 0f)
                throw new InvalidOperationException($"[{nameof(EnemySpawnManager)}] Spawn radius cannot be negative.");
            if (_navMeshSampleDistance <= 0f)
                throw new InvalidOperationException(
                    $"[{nameof(EnemySpawnManager)}] NavMesh sample distance must be greater than zero.");
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[{nameof(EnemySpawnManager)}] Enemies can only be spawned in Play Mode.", this);
                return 0;
            }
            if (!HasRequiredContext()) return 0;

            int spawnedCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (!TryGetSpawnPosition(out Vector3 position))
                {
                    Debug.LogWarning(
                        $"[{nameof(EnemySpawnManager)}] No NavMesh position found within {_spawnRadius:0.##} units.",
                        this);
                    continue;
                }

                SpawnEnemy(_enemyPrefab, position, transform.rotation);
                spawnedCount++;
            }

            return spawnedCount;
        }

        [ButtonGroup("Spawn/Actions"), Button("Spawn One")]
        private void SpawnOneInRange()
        {
            SpawnEnemiesInRange(1);
        }

        [ButtonGroup("Spawn/Actions"), Button("Spawn Batch")]
        private void SpawnBatchInRange()
        {
            SpawnEnemiesInRange(_spawnCount);
        }

        public void DespawnEnemy(Health enemy)
        {
            if (ReferenceEquals(enemy, null)) return;

            UntrackEnemy(enemy);
            if (enemy != null) Destroy(enemy.gameObject);
        }

        public bool RegisterEnemy(Health enemy)
        {
            if (enemy == null || !HasRequiredContext()) return false;

            if (enemy.Team != Team.Enemy)
            {
                Debug.LogError($"[{nameof(EnemySpawnManager)}] Only enemy-team Health components can be registered.", enemy);
                return false;
            }

            if (enemy.gameObject.scene != gameObject.scene)
            {
                Debug.LogError($"[{nameof(EnemySpawnManager)}] Cannot register an enemy from another scene.", enemy);
                return false;
            }

            DamageNumbersManager damageNumbers = enemy.GetComponent<DamageNumbersManager>();
            if (damageNumbers != null) damageNumbers.BindContext(_context);

            if (!enemy.IsAlive) return true;

            TrackEnemy(enemy);
            return true;
        }

        private void TrackEnemy(Health enemy)
        {
            if (!_aliveEnemies.Add(enemy)) return;

            Action deathHandler = null;
            deathHandler = () => UntrackEnemy(enemy);

            _deathHandlers.Add(enemy, deathHandler);
            enemy.Died += deathHandler;
            EnemyRegistered?.Invoke(enemy);
        }

        private void UntrackEnemy(Health enemy)
        {
            _aliveEnemies.Remove(enemy);

            if (!_deathHandlers.TryGetValue(enemy, out Action deathHandler)) return;

            if (enemy != null) enemy.Died -= deathHandler;
            _deathHandlers.Remove(enemy);
            EnemyUnregistered?.Invoke(enemy);
        }

        private bool HasRequiredContext()
        {
            if (_context != null &&
                _context.ScreenSpaceRoot != null &&
                _context.OutputCamera != null)
            {
                return true;
            }

            Debug.LogError(
                $"[{nameof(EnemySpawnManager)}] Assign a valid GameplayContext with a camera and screen-space root.",
                this);
            return false;
        }

        private bool TryGetSpawnPosition(out Vector3 position)
        {
            float radius = Mathf.Max(0f, _spawnRadius);
            float radiusWithTolerance = radius + 0.05f;
            float radiusWithToleranceSqr = radiusWithTolerance * radiusWithTolerance;

            for (int attempt = 0; attempt < SpawnPositionAttempts; attempt++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;
                Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(
                        candidate,
                        out NavMeshHit hit,
                        _navMeshSampleDistance,
                        NavMesh.AllAreas))
                {
                    continue;
                }

                Vector3 horizontalOffset = hit.position - transform.position;
                horizontalOffset.y = 0f;
                if (horizontalOffset.sqrMagnitude > radiusWithToleranceSqr)
                    continue;

                position = hit.position;
                return true;
            }

            position = default;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Color previousColor = Gizmos.color;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);

            float radius = Mathf.Max(0f, _spawnRadius);
            Vector3 previousPoint = transform.position + Vector3.right * radius;
            for (int i = 1; i <= GizmoCircleSegments; i++)
            {
                float angle = i * (Mathf.PI * 2f / GizmoCircleSegments);
                Vector3 nextPoint = transform.position + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }

            Gizmos.color = previousColor;
        }
    }
}
