using Sirenix.OdinInspector;
using UnityEngine;

namespace PunkGame
{
    // Unity-facing command API and composition root. Keep game-specific orchestration here;
    // the state machine and target-sense implementations remain replaceable plain C# objects.
    [DefaultExecutionOrder(0)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAIMovement), typeof(CharacterAttack))]
    public class EnemyBrain : MonoBehaviour, IEnemyCommandReceiver
    {
        [FoldoutGroup("References")]
        [SerializeField]
        private EnemyAIMovement _movement;

        [FoldoutGroup("References")]
        [SerializeField]
        private CharacterAttack _attack;

        [FoldoutGroup("References")]
        [Tooltip("Optional. The brain stops making decisions when this enemy dies.")]
        [SerializeField]
        private Health _health;

        [FoldoutGroup("Targeting")]
        [Tooltip("Optional initial target. When empty, the scene PlayerController is used.")]
        [SerializeField]
        private Health _initialTarget;

        [FoldoutGroup("Targeting")]
        [SerializeField, MinValue(0f)]
        private float _sightRange = 12f;

        [FoldoutGroup("Targeting")]
        [SerializeField, MinValue(0f)]
        private float _loseRange = 18f;

        [FoldoutGroup("Attack")]
        [SerializeField, MinValue(0f)]
        private float _attackRange = 2f;

        [FoldoutGroup("Attack")]
        [SerializeField, MinValue(0f)]
        private float _leashRange = 3f;

        [FoldoutGroup("Attack")]
        [SerializeField, Range(1f, 180f)]
        private float _facingAngle = 30f;

        [FoldoutGroup("Attack")]
        [SerializeField, MinValue(-1)]
        private int _maxComboSteps = 1;

        [FoldoutGroup("Attack")]
        [SerializeField, MinValue(0f)]
        private float _attackCooldown = 1f;

        [FoldoutGroup("Attack")]
        [SerializeField, MinValue(0f)]
        private float _cooldownVariance = 0.5f;

        [FoldoutGroup("Debug")]
        [SerializeField]
        private bool _debugLogs;

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public EnemyState State => _stateMachine != null ? _stateMachine.State : EnemyState.Patrol;

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public Transform Target => _stateMachine != null ? _stateMachine.Target : null;

        private EnemyStateMachine _stateMachine;
        private IEnemyTargetSense _targetSense;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
            CreateStateMachine();
        }

        private void OnEnable()
        {
            if (_stateMachine == null) CreateStateMachine();
        }

        private void OnDisable()
        {
            if (_stateMachine == null) return;
            _stateMachine.Dispose();
            _stateMachine = null;
        }

        private void Update()
        {
            if (_health != null && !_health.IsAlive) return;
            _stateMachine?.Tick();
        }

        /// <summary>Assigns a target directly, bypassing autonomous sensing until cleared.</summary>
        public void Engage(Transform target)
        {
            EnsureStateMachine();
            _stateMachine.SetTarget(target);
        }

        /// <summary>Clears the command target and resumes autonomous sensing.</summary>
        public void ClearTarget()
        {
            EnsureStateMachine();
            _stateMachine.ClearTarget();
        }

        /// <summary>Clears any command target and resumes patrol.</summary>
        public void ReturnToPatrol()
        {
            EnsureStateMachine();
            _stateMachine.ReturnToPatrol();
        }

        /// <summary>Replaces the default sense with another implementation or a package adapter.</summary>
        public void SetTargetSense(IEnemyTargetSense sense)
        {
            _targetSense = sense;
            EnsureStateMachine();
            _stateMachine.SetSense(sense);
        }

        private void CacheReferences()
        {
            if (_movement == null) _movement = GetComponent<EnemyAIMovement>();
            if (_attack == null) _attack = GetComponent<CharacterAttack>();
            if (_health == null) _health = GetComponent<Health>();
        }

        private void EnsureStateMachine()
        {
            if (_stateMachine != null) return;
            CacheReferences();
            CreateStateMachine();
        }

        private void CreateStateMachine()
        {
            if (_stateMachine != null || _movement == null || _attack == null) return;

            IEnemyTargetSense sense = _targetSense ?? CreateDefaultSense();
            _stateMachine = new EnemyStateMachine(
                transform,
                _movement,
                _attack,
                _health,
                sense,
                _attackRange,
                _leashRange,
                _facingAngle,
                _maxComboSteps,
                _attackCooldown,
                _cooldownVariance,
                _debugLogs);
        }

        private IEnemyTargetSense CreateDefaultSense()
        {
            Health target = _initialTarget;
            if (target == null)
            {
                PlayerController player = FindAnyObjectByType<PlayerController>();
                if (player != null) target = player.GetComponent<Health>();
            }

            Transform targetTransform = target != null ? target.transform : null;
            return new EnemyVisionSense(transform, targetTransform, _sightRange, _loseRange);
        }

        private void OnValidate()
        {
            _loseRange = Mathf.Max(_loseRange, _sightRange);
            _leashRange = Mathf.Max(_leashRange, _attackRange);
        }
    }
}
