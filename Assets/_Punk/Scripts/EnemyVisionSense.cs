using UnityEngine;

namespace PunkGame
{
    public interface IEnemyTargetSense
    {
        bool TryGetTarget(out Transform target);
    }

    // Default distance-based sense. It knows only about its owner, target, and ranges;
    // target discovery and any future sensing package stay outside this class.
    public sealed class EnemyVisionSense : IEnemyTargetSense
    {
        private readonly Transform _owner;
        private readonly Transform _target;
        private readonly float _sightRange;
        private readonly float _loseRange;
        private bool _acquired;

        public EnemyVisionSense(Transform owner, Transform target, float sightRange, float loseRange)
        {
            _owner = owner;
            _target = target;
            _sightRange = Mathf.Max(0f, sightRange);
            _loseRange = Mathf.Max(_sightRange, loseRange);
        }

        public bool TryGetTarget(out Transform target)
        {
            target = null;
            if (_owner == null || _target == null || !_target.gameObject.activeInHierarchy) return false;

            float distance = FlatDistance(_owner.position, _target.position);
            if (!_acquired)
            {
                if (distance > _sightRange) return false;
                _acquired = true;
            }
            else if (distance > _loseRange)
            {
                _acquired = false;
                return false;
            }

            target = _target;
            return true;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            Vector3 difference = a - b;
            difference.y = 0f;
            return difference.magnitude;
        }
    }
}
