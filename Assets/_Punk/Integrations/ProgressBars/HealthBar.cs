using INab.UI;
using PunkGame.Core.Combat;
using PunkGame.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PunkGame.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProceduralProgressBar), typeof(WorldToScreenFollower))]
    public sealed class HealthBar : MonoBehaviour
    {
        [SerializeField] private Health _owner;

        public Health Owner => _owner;

        [SerializeField] private ProceduralProgressBar _progressBar;

        [SerializeField] private WorldToScreenFollower _follower;

        [SerializeField] private float _barAnimationTime = .15f;

        [SerializeField] private bool _destroyOnDeath;

        private bool _isSubscribed;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            if (_owner != null) Refresh();
            var image = GetComponent<Image>();
            if (image)
            {
                _progressBar.progressBarMaterial = Instantiate(_progressBar.progressBarMaterial);
                image.material = _progressBar.progressBarMaterial;
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (_follower != null) _follower.SetVisible(false);
        }

        public void Bind(Health owner)
        {
            CacheReferences();

            if (_owner != owner)
            {
                Unsubscribe();
                _owner = owner;
            }

            if (_follower != null) _follower.SetTarget(_owner != null ? _owner.transform : null);

            if (!isActiveAndEnabled)
            {
                if (_follower != null) _follower.SetVisible(_owner != null && _owner.IsAlive);
                return;
            }

            Subscribe();
        }

        private void Subscribe()
        {
            if (_owner == null)
            {
                if (_follower != null) _follower.SetVisible(false);
                return;
            }

            if (!_isSubscribed)
            {
                _owner.HealthChanged += OnHealthChanged;
                _owner.Died += OnOwnerDied;
                _isSubscribed = true;
            }

            if (_follower != null) _follower.SetTarget(_owner.transform);
            Refresh();
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed) return;

            if (_owner != null)
            {
                _owner.HealthChanged -= OnHealthChanged;
                _owner.Died -= OnOwnerDied;
            }

            _isSubscribed = false;
        }

        private void OnHealthChanged(float currentHealth, float delta)
        {
            if (_owner == null) return;

            if (delta < 0) _progressBar.BarLoss(Mathf.Abs(delta / _owner.Max), _barAnimationTime);
            else _progressBar.BarFill(delta / _owner.Max, _barAnimationTime);
            _follower.SetVisible(_owner.IsAlive);
        }

        private void OnOwnerDied()
        {
            if (_destroyOnDeath)
            {
                Destroy(gameObject);
                return;
            }

            _follower.SetVisible(false);
        }

        private void Refresh()
        {
            _progressBar.UpdateBarFillAmount(_owner.Normalized);
            _follower.SetVisible(_owner.IsAlive);
        }

        private void CacheReferences()
        {
            if (_progressBar == null) _progressBar = GetComponent<ProceduralProgressBar>();
            if (_follower == null) _follower = GetComponent<WorldToScreenFollower>();
        }
    }
}