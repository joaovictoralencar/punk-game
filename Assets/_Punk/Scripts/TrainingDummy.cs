using Sirenix.OdinInspector;
using UnityEngine;

namespace PunkGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class TrainingDummy : MonoBehaviour
    {
        [BoxGroup("Configuration")]
        [Tooltip("Seconds before the dummy respawns after dying.")]
        [SerializeField, MinValue(0.1f)]
        private float _respawnDelay = 3f;

        [BoxGroup("Configuration")]
        [Tooltip("Duration of the hit flash in seconds.")]
        [SerializeField, MinValue(0.01f)]
        private float _flashDuration = 0.12f;

        [BoxGroup("Configuration")]
        [Tooltip("Color applied to renderers when taking damage.")]
        [SerializeField]
        private Color _flashColor = Color.white;

        [BoxGroup("Configuration")]
        [Tooltip("Optional child root for visuals/colliders. If empty, all renderers and colliders on this hierarchy are toggled on death.")]
        [SerializeField]
        private GameObject _bodyRoot;

        [BoxGroup("Knockback")]
        [Tooltip("Enables horizontal push when hit by attacks with knockback force.")]
        [SerializeField]
        private bool _enableKnockback = true;

        [BoxGroup("Knockback")]
        [Tooltip("Duration of the knockback push in seconds.")]
        [SerializeField, MinValue(0.01f), EnableIf(nameof(_enableKnockback))]
        private float _knockbackDuration = 0.2f;

        [BoxGroup("Knockback")]
        [Tooltip("Scales incoming DamageInfo.Knockback force.")]
        [SerializeField, MinValue(0f), EnableIf(nameof(_enableKnockback))]
        private float _knockbackMultiplier = 1f;

        [BoxGroup("Knockback")]
        [Tooltip("Velocity falloff over the normalized knockback duration (0 to 1).")]
        [SerializeField, EnableIf(nameof(_enableKnockback))]
        private AnimationCurve _knockbackDecay = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [BoxGroup("Knockback")]
        [Tooltip("Returns the dummy to its initial spawn position when respawning.")]
        [SerializeField, EnableIf(nameof(_enableKnockback))]
        private bool _resetPositionOnRespawn = true;

        [BoxGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public bool IsRespawning => _respawnTimer > 0f;

        [BoxGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public bool IsKnockedBack => _isKnockedBack;

        [BoxGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        private float _respawnTimer;

        [FoldoutGroup("Debug")]
        [Tooltip("Logs incoming damage amount, strength, knockback, and death/respawn events to the Console.")]
        [SerializeField]
        private bool _logEvents = true;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Health _health;
        private CharacterController _characterController;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private MaterialPropertyBlock _propertyBlock;

        private Vector3 _spawnPosition;
        private float _flashTimer;
        private bool _isFlashing;

        private bool _isKnockedBack;
        private Vector3 _knockbackDirection;
        private float _knockbackInitialSpeed;
        private float _knockbackElapsed;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _characterController = GetComponent<CharacterController>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            _propertyBlock = new MaterialPropertyBlock();
            _spawnPosition = transform.position;
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
            SetFlash(false);
            _isKnockedBack = false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_isFlashing)
            {
                _flashTimer -= dt;
                if (_flashTimer <= 0f)
                {
                    SetFlash(false);
                }
            }

            if (_isKnockedBack)
            {
                TickKnockback(dt);
            }

            if (_respawnTimer > 0f)
            {
                _respawnTimer -= dt;
                if (_respawnTimer <= 0f)
                {
                    Respawn();
                }
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            if (_logEvents)
            {
                string sourceName = info.Source != null ? info.Source.name : "Unknown";
                Debug.Log($"[TrainingDummy] {name} took {info.Amount} ({info.Strength}) damage (Knockback: {info.Knockback:0.##}) from {sourceName} [{info.Team}].", this);
            }

            _flashTimer = _flashDuration;
            SetFlash(true);

            if (_enableKnockback)
            {
                ApplyKnockback(info.Direction, info.Knockback);
            }
        }

        private void OnDied()
        {
            if (_logEvents)
            {
                Debug.Log($"[TrainingDummy] {name} died. Respawning in {_respawnDelay:0.##}s.", this);
            }

            _isKnockedBack = false;
            SetFlash(false);
            SetDummyActive(false);
            _respawnTimer = _respawnDelay;
        }

        private void Respawn()
        {
            _respawnTimer = 0f;
            _isKnockedBack = false;

            if (_resetPositionOnRespawn)
            {
                TeleportToSpawn();
            }

            _health.ResetHealth();
            SetDummyActive(true);

            if (_logEvents)
            {
                Debug.Log($"[TrainingDummy] {name} respawned.", this);
            }
        }

        public void ApplyKnockback(Vector3 direction, float force)
        {
            direction.y = 0f;

            if (force <= 0f || _knockbackMultiplier <= 0f || _knockbackDuration <= 0f || direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            _knockbackDirection = direction.normalized;
            _knockbackInitialSpeed = force * _knockbackMultiplier;
            _knockbackElapsed = 0f;
            _isKnockedBack = true;
        }

        private void TickKnockback(float dt)
        {
            _knockbackElapsed += dt;
            float normalizedTime = Mathf.Clamp01(_knockbackElapsed / _knockbackDuration);
            float currentSpeed = _knockbackInitialSpeed * _knockbackDecay.Evaluate(normalizedTime);
            Vector3 displacement = _knockbackDirection * (currentSpeed * dt);

            if (_characterController != null && _characterController.enabled)
            {
                _characterController.Move(displacement);
            }
            else
            {
                transform.position += displacement;
            }

            if (normalizedTime >= 1f)
            {
                _isKnockedBack = false;
            }
        }

        private void TeleportToSpawn()
        {
            if (_characterController != null && _characterController.enabled)
            {
                _characterController.enabled = false;
                transform.position = _spawnPosition;
                _characterController.enabled = true;
            }
            else
            {
                transform.position = _spawnPosition;
            }
        }

        private void SetFlash(bool active)
        {
            _isFlashing = active;

            if (_renderers == null) return;

            if (active)
            {
                _propertyBlock.SetColor(BaseColorId, _flashColor);
                _propertyBlock.SetColor(ColorId, _flashColor);

                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null)
                    {
                        _renderers[i].SetPropertyBlock(_propertyBlock);
                    }
                }
            }
            else
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null)
                    {
                        _renderers[i].SetPropertyBlock(null);
                    }
                }
            }
        }

        private void SetDummyActive(bool active)
        {
            if (_bodyRoot != null)
            {
                _bodyRoot.SetActive(active);

                for (int i = 0; i < _colliders.Length; i++)
                {
                    if (_colliders[i] != null && _colliders[i].gameObject == gameObject)
                    {
                        _colliders[i].enabled = active;
                    }
                }

                return;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = active;
                }
            }

            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                {
                    _colliders[i].enabled = active;
                }
            }
        }

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/Actions"), Button("Test Flash")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void DebugTestFlash()
        {
            _flashTimer = _flashDuration;
            SetFlash(true);
        }

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/Actions"), Button("Test Knockback")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void DebugTestKnockback()
        {
            ApplyKnockback(-transform.forward, 8f);
        }

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/Actions"), Button("Respawn Now")]
        [EnableIf("@UnityEngine.Application.isPlaying && IsRespawning")]
        private void DebugRespawnNow()
        {
            Respawn();
        }
    }
}