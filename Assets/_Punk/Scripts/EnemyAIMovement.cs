using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

namespace PunkGame
{
    // Composition adapter: owns NavMesh path finding and patrol, while CharacterMovement
    // remains responsible for CharacterController movement, gravity, root motion and rotation.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(CharacterMovement))]
    public class EnemyAIMovement : MonoBehaviour
    {
        private const float MinDirectionSqrMagnitude = 0.0001f;
        private const float MinRotateWhileMovingSqrSpeed = 0.01f;
        private const float PatrolRetryDelay = 0.5f;

        [FoldoutGroup("References")]
        [Required, SerializeField]
        private NavMeshAgent _agent;

        [FoldoutGroup("References")]
        [Required, SerializeField]
        private CharacterMovement _movement;

        [FoldoutGroup("Patrol")]
        [SerializeField, MinValue(0f)]
        private float _patrolRadius = 8f;

        [FoldoutGroup("Patrol")]
        [SerializeField, MinMaxSlider(0f, 10f)]
        private Vector2 _patrolWaitRange = new Vector2(1f, 3f);

        [FoldoutGroup("Patrol")]
        [SerializeField, MinValue(0.05f)]
        private float _patrolArrivalDistance = 0.5f;

        [FoldoutGroup("Patrol")]
        [SerializeField, MinValue(0f)]
        private float _patrolMinDistance = 2f;

        [FoldoutGroup("Patrol")]
        [SerializeField, MinValue(1)]
        private int _patrolSampleAttempts = 8;

        [FoldoutGroup("Patrol")]
        [SerializeField, MinValue(0.1f)]
        private float _patrolSampleMaxDistance = 2f;

        [FoldoutGroup("Chase")]
        [SerializeField, MinValue(0.05f)]
        private float _repathInterval = 0.2f;

        [FoldoutGroup("Chase")]
        [SerializeField, MinValue(0f)]
        private float _stoppingDistance = 1.5f;

        [FoldoutGroup("Chase")]
        [SerializeField, MinValue(0f)]
        private float _turnSpeed = 10f;

        [FoldoutGroup("Debug")]
        [SerializeField]
        private bool _debugLogs;

        [FoldoutGroup("Debug")]
        [SerializeField]
        private bool _drawGizmos = true;

        private NavMeshPath _path;
        private Vector3 _home;
        private Vector3 _destination;
        private bool _hasDestination;
        private bool _isWaiting;
        private float _waitUntil;
        private float _nextRepathTime;
        private bool _warnedOffNavMesh;

        public float StoppingDistance => _stoppingDistance;

        private void Reset()
        {
            _agent = GetComponent<NavMeshAgent>();
            _movement = GetComponent<CharacterMovement>();
        }

        private void Awake()
        {
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
            if (_movement == null) _movement = GetComponent<CharacterMovement>();

            _path = new NavMeshPath();
            _home = transform.position;
            ConfigureAgent();
        }

        private void OnEnable()
        {
            ConfigureAgent();
            ResetPatrol();
            _warnedOffNavMesh = false;
        }

        private void OnDisable()
        {
            Stop();
        }

        private void Update()
        {
            SyncAgentToBody();
        }

        private void ConfigureAgent()
        {
            if (_agent == null) return;
            _agent.updatePosition = false;
            _agent.updateRotation = false;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        }

        public void ResetPatrol()
        {
            _isWaiting = false;
            _hasDestination = false;
            Stop();
        }

        public void TickPatrol()
        {
            if (_isWaiting)
            {
                _movement.SetMoveDirection(Vector3.zero);
                if (Time.time < _waitUntil) return;

                if (TryPickPatrolPoint()) _isWaiting = false;
                else _waitUntil = Time.time + PatrolRetryDelay;
                return;
            }

            if (!_hasDestination)
            {
                if (!TryPickPatrolPoint()) StartWaiting(PatrolRetryDelay);
                return;
            }

            if (FlatDistance(transform.position, _destination) <= _patrolArrivalDistance)
            {
                StartWaiting(Random.Range(_patrolWaitRange.x, _patrolWaitRange.y));
                return;
            }

            SteerAlongPath();
        }

        public void TickChase(Transform target)
        {
            if (target == null)
            {
                Stop();
                return;
            }

            Vector3 flatDirection = FlatVector(target.position - transform.position);
            float distance = flatDirection.magnitude;

            if (Time.time >= _nextRepathTime)
            {
                _nextRepathTime = Time.time + _repathInterval;
                SetAgentDestination(target.position);
            }

            if (distance > _stoppingDistance) SteerAlongPath();
            else _movement.SetMoveDirection(Vector3.zero);

            RotateTowards(flatDirection);
        }

        public void Stop()
        {
            if (_movement != null) _movement.SetMoveDirection(Vector3.zero);
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.ResetPath();
        }

        public void RotateTowards(Vector3 flatDirection)
        {
            if (!_movement.CanRotate) return;
            if (flatDirection.sqrMagnitude < MinDirectionSqrMagnitude) return;
            if (_movement.DesiredMoveSpeed.sqrMagnitude > MinRotateWhileMovingSqrSpeed) return;

            Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * _turnSpeed);
        }

        public bool IsFacing(Vector3 worldDirection, float maxAngle)
        {
            Vector3 flatDirection = FlatVector(worldDirection);
            if (flatDirection.sqrMagnitude < MinDirectionSqrMagnitude) return true;
            return Vector3.Angle(transform.forward, flatDirection) <= maxAngle;
        }

        private void SyncAgentToBody()
        {
            if (_agent == null) return;

            if (!_agent.enabled || !_agent.isOnNavMesh)
            {
                if (_debugLogs && !_warnedOffNavMesh)
                {
                    _warnedOffNavMesh = true;
                    Debug.LogWarning($"[EnemyAIMovement] {name} is not on a NavMesh.", this);
                }
                return;
            }

            _agent.nextPosition = transform.position;
        }

        private void SteerAlongPath()
        {
            if (_agent == null || !_agent.isOnNavMesh)
            {
                _movement.SetMoveDirection(Vector3.zero);
                return;
            }

            Vector3 steer = FlatVector(_agent.steeringTarget - transform.position);
            if (steer.sqrMagnitude < MinDirectionSqrMagnitude)
            {
                _movement.SetMoveDirection(Vector3.zero);
                return;
            }

            _movement.SetMoveDirection(steer.normalized);
        }

        private void SetAgentDestination(Vector3 worldPosition)
        {
            if (_agent != null && _agent.isOnNavMesh) _agent.SetDestination(worldPosition);
        }

        private bool TryPickPatrolPoint()
        {
            if (_agent == null || !_agent.isOnNavMesh) return false;

            for (int i = 0; i < _patrolSampleAttempts; i++)
            {
                Vector2 offset = Random.insideUnitCircle * _patrolRadius;
                Vector3 candidate = _home + new Vector3(offset.x, 0f, offset.y);

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, _patrolSampleMaxDistance, _agent.areaMask)) continue;
                if (FlatDistance(transform.position, hit.position) < _patrolMinDistance) continue;
                if (!_agent.CalculatePath(hit.position, _path) || _path.status != NavMeshPathStatus.PathComplete) continue;

                _agent.SetPath(_path);
                _destination = hit.position;
                _hasDestination = true;

                if (_debugLogs) Debug.Log($"[EnemyAIMovement] {name} patrols to {hit.position}.", this);
                return true;
            }

            return false;
        }

        private void StartWaiting(float seconds)
        {
            _isWaiting = true;
            _hasDestination = false;
            _waitUntil = Time.time + seconds;
            _movement.SetMoveDirection(Vector3.zero);
            Stop();
        }

        private void OnValidate()
        {
            if (_patrolWaitRange.y < _patrolWaitRange.x) _patrolWaitRange.y = _patrolWaitRange.x;
        }

        private static Vector3 FlatVector(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            Vector3 difference = a - b;
            difference.y = 0f;
            return difference.magnitude;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_drawGizmos) return;

            Vector3 position = transform.position;
            Vector3 home = Application.isPlaying ? _home : position;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(home, _patrolRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(position, _stoppingDistance);

            if (Application.isPlaying && _hasDestination)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(position, _destination);
                Gizmos.DrawSphere(_destination, 0.2f);
            }
        }
#endif
    }
}
