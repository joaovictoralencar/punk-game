using UnityEngine;
using PunkGame.Combat;
using PunkGame.Core.Combat;

namespace PunkGame.Enemies
{
    public interface IEnemyCommandReceiver
    {
        void Engage(Transform target);
        void ClearTarget();
        void ReturnToPatrol();
    }

    public enum EnemyState
    {
        Patrol,
        Chase,
        Attack
    }

    // Plain decision logic; Unity lifecycle and external commands live in EnemyBrain.
    public sealed class EnemyStateMachine
    {
        private readonly Transform _owner;
        private readonly EnemyAIMovement _movement;
        private readonly CharacterAttack _attack;
        private readonly Health _health;
        private readonly float _attackRange;
        private readonly float _leashRange;
        private readonly float _facingAngle;
        private readonly int _maxComboSteps;
        private readonly float _attackCooldown;
        private readonly float _cooldownVariance;
        private readonly bool _debugLogs;

        private IEnemyTargetSense _sense;
        private Transform _commandTarget;
        private bool _hasCommandTarget;
        private float _nextAttackTime;

        public EnemyState State { get; private set; } = EnemyState.Patrol;
        public Transform Target { get; private set; }

        public EnemyStateMachine(
            Transform owner,
            EnemyAIMovement movement,
            CharacterAttack attack,
            Health health,
            IEnemyTargetSense sense,
            float attackRange,
            float leashRange,
            float facingAngle,
            int maxComboSteps,
            float attackCooldown,
            float cooldownVariance,
            bool debugLogs)
        {
            _owner = owner;
            _movement = movement;
            _attack = attack;
            _health = health;
            _sense = sense;
            _attackRange = Mathf.Max(0f, attackRange);
            _leashRange = Mathf.Max(_attackRange, leashRange);
            _facingAngle = Mathf.Clamp(facingAngle, 1f, 180f);
            _maxComboSteps = maxComboSteps;
            _attackCooldown = Mathf.Max(0f, attackCooldown);
            _cooldownVariance = Mathf.Max(0f, cooldownVariance);
            _debugLogs = debugLogs;

            if (_attack != null) _attack.AttackEnded += OnAttackEnded;
        }

        public void Tick()
        {
            if (_health != null && !_health.IsAlive)
            {
                Stop();
                return;
            }

            Target = ResolveTarget();

            switch (State)
            {
                case EnemyState.Patrol:
                    TickPatrol();
                    break;
                case EnemyState.Chase:
                    TickChase();
                    break;
                case EnemyState.Attack:
                    TickAttack();
                    break;
            }
        }

        public void SetTarget(Transform target)
        {
            _commandTarget = target;
            _hasCommandTarget = true;
        }

        public void ClearTarget()
        {
            _commandTarget = null;
            _hasCommandTarget = false;
        }

        public void SetSense(IEnemyTargetSense sense)
        {
            _sense = sense;
        }

        public void ReturnToPatrol()
        {
            _commandTarget = null;
            _hasCommandTarget = true;
            SetState(EnemyState.Patrol);
        }

        public void Stop()
        {
            if (_movement != null) _movement.Stop();
        }

        public void Dispose()
        {
            if (_attack != null) _attack.AttackEnded -= OnAttackEnded;
            Stop();
        }

        private Transform ResolveTarget()
        {
            if (_hasCommandTarget)
            {
                return IsTargetValid(_commandTarget) ? _commandTarget : null;
            }

            if (_sense != null && _sense.TryGetTarget(out Transform sensedTarget) && IsTargetValid(sensedTarget))
            {
                return sensedTarget;
            }

            return null;
        }

        private bool IsTargetValid(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            Health targetHealth = target.GetComponent<Health>();
            return targetHealth == null || targetHealth.IsAlive;
        }

        private void TickPatrol()
        {
            if (Target != null)
            {
                SetState(EnemyState.Chase);
                return;
            }

            if (_movement != null) _movement.TickPatrol();
        }

        private void TickChase()
        {
            if (Target == null)
            {
                SetState(EnemyState.Patrol);
                return;
            }

            if (_movement == null || _attack == null) return;

            Vector3 toTarget = FlatVector(Target.position - _owner.position);
            float distance = toTarget.magnitude;
            _movement.TickChase(Target);

            if (distance <= _attackRange
                && Time.time >= _nextAttackTime
                && !_attack.IsAttacking
                && _movement.IsFacing(toTarget, _facingAngle)
                && _attack.StartCombo(_maxComboSteps))
            {
                SetState(EnemyState.Attack);
            }
        }

        private void TickAttack()
        {
            if (_attack != null && !_attack.IsAttacking)
            {
                SetState(Target == null ? EnemyState.Patrol : EnemyState.Chase);
                return;
            }

            if (Target == null)
            {
                if (_attack != null) _attack.StopChaining();
                return;
            }

            if (_attack != null && FlatDistance(_owner.position, Target.position) > _leashRange)
            {
                _attack.StopChaining();
            }
        }

        private void OnAttackEnded(bool finishedNaturally)
        {
            _nextAttackTime = Time.time + _attackCooldown + Random.Range(0f, _cooldownVariance);
            if (State == EnemyState.Attack) SetState(Target == null ? EnemyState.Patrol : EnemyState.Chase);
        }

        private void SetState(EnemyState next)
        {
            if (State == next) return;

            if (_debugLogs) Debug.Log($"[EnemyStateMachine] {_owner.name}: {State} -> {next}.", _owner);
            State = next;

            if (next == EnemyState.Patrol && _movement != null) _movement.ResetPatrol();
            else if (next == EnemyState.Attack && _movement != null) _movement.Stop();
        }

        private static Vector3 FlatVector(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            Vector3 difference = b - a;
            difference.y = 0f;
            return difference.magnitude;
        }
    }
}
