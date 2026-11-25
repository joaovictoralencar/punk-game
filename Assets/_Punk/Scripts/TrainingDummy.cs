using Sirenix.OdinInspector;
using UnityEngine;

namespace PunkGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(Rigidbody))]
    public class TrainingDummy : MonoBehaviour
    {
        private const float WallSkinWidth = 0.02f;
        private const float MaxWalkableWallNormalY = 0.5f;

        [FoldoutGroup("Configuration")]
        [Tooltip("Seconds before the dummy respawns after dying.")]
        [SerializeField, MinValue(0.1f)]
        private float _respawnDelay = 3f;

        [FoldoutGroup("Configuration")]
        [Tooltip("Duration of the hit flash in seconds.")]
        [SerializeField, MinValue(0.01f)]
        private float _flashDuration = 0.12f;

        [FoldoutGroup("Configuration")]
        [Tooltip("Color applied to renderers when taking damage.")]
        [SerializeField]
        private Color _flashColor = Color.white;

        [FoldoutGroup("Configuration")]
        [Tooltip("Brightness multiplier for the hit flash. Values above 1 make white flashes visible on textured materials.")]
        [SerializeField, MinValue(1f)]
        private float _flashIntensity = 3f;
        
        [FoldoutGroup("Configuration")]
        [Tooltip("Optional child root for visuals/colliders. If empty, all renderers and colliders on this hierarchy are toggled on death.")]
        [SerializeField]
        private GameObject _bodyRoot;

        [FoldoutGroup("Knockback")]
        [Tooltip("Enables deterministic kinematic knockback when hit by attacks with knockback force.")]
        [SerializeField]
        private bool _enableKnockback = true;

        [FoldoutGroup("Knockback")]
        [Tooltip("Duration of the knockback slide in seconds.")]
        [SerializeField, MinValue(0.01f), EnableIf(nameof(_enableKnockback))]
        private float _knockbackDuration = 0.2f;

        [FoldoutGroup("Knockback")]
        [Tooltip("Scales incoming DamageInfo.Knockback speed.")]
        [SerializeField, MinValue(0f), EnableIf(nameof(_enableKnockback))]
        private float _knockbackMultiplier = 1f;

        [FoldoutGroup("Knockback")]
        [Tooltip("Speed falloff over the normalized knockback duration (0 to 1).")]
        [SerializeField, EnableIf(nameof(_enableKnockback))]
        private AnimationCurve _knockbackDecay = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [FoldoutGroup("Knockback")]
        [Tooltip("Performs a Rigidbody sweep test so the kinematic dummy stops against walls instead of clipping through them.")]
        [SerializeField, EnableIf(nameof(_enableKnockback))]
        private bool _stopOnWallCollision = true;

        [FoldoutGroup("Knockback")]
        [Tooltip("Returns the dummy to its initial spawn position and rotation when respawning.")]
        [SerializeField]
        private bool _resetTransformOnRespawn = true;

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public bool IsRespawning => _respawnTimer > 0f;

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public bool IsKnockedBack => _isKnockedBack;

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        private float _respawnTimer;

        [FoldoutGroup("Debug")]
        [Tooltip("Logs incoming damage amount, strength, knockback, and death/respawn events to the Console.")]
        [SerializeField]
        private bool _logEvents = true;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Health _health;
        private Rigidbody _rigidbody;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private MaterialPropertyBlock _propertyBlock;

        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private float _flashTimer;
        private bool _isFlashing;

        private bool _isKnockedBack;
        private Vector3 _knockbackDirection;
        private float _knockbackInitialSpeed;
        private float _knockbackElapsed;

        private void Reset()
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                ConfigureKinematicRigidbody(rb);
            }
        }

        private void Awake()
        {
            _health = GetComponent<Health>();
            _rigidbody = GetComponent<Rigidbody>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            _propertyBlock = new MaterialPropertyBlock();

            ConfigureKinematicRigidbody(_rigidbody);

            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;

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

            if (_respawnTimer > 0f)
            {
                _respawnTimer -= dt;
                if (_respawnTimer <= 0f)
                {
                    Respawn();
                }
            }
        }

        private void FixedUpdate()
        {
            if (_isKnockedBack)
            {
                TickKnockback(Time.fixedDeltaTime);
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
                ApplyKnockback(info.HitDirection, info.Knockback);
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

            if (_resetTransformOnRespawn)
            {
                transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
                _rigidbody.position = _spawnPosition;
                _rigidbody.rotation = _spawnRotation;
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

        private void TickKnockback(float fixedDt)
        {
            _knockbackElapsed += fixedDt;
            float normalizedTime = Mathf.Clamp01(_knockbackElapsed / _knockbackDuration);
            float currentSpeed = _knockbackInitialSpeed * _knockbackDecay.Evaluate(normalizedTime);
            float stepDistance = currentSpeed * fixedDt;

            if (_stopOnWallCollision && stepDistance > 0f)
            {
                if (_rigidbody.SweepTest(_knockbackDirection, out RaycastHit hit, stepDistance + WallSkinWidth, QueryTriggerInteraction.Ignore)
                    && hit.normal.y < MaxWalkableWallNormalY
                    && Vector3.Dot(hit.normal, _knockbackDirection) < 0f)
                {
                    float safeDistance = Mathf.Max(0f, hit.distance - WallSkinWidth);
                    _rigidbody.MovePosition(_rigidbody.position + _knockbackDirection * safeDistance);
                    _isKnockedBack = false;
                    return;
                }
            }

            _rigidbody.MovePosition(_rigidbody.position + _knockbackDirection * stepDistance);

            if (normalizedTime >= 1f)
            {
                _isKnockedBack = false;
            }
        }

        private static void ConfigureKinematicRigidbody(Rigidbody rb)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        private void SetFlash(bool active)
        {
            _isFlashing = active;

            if (_renderers == null) return;

            if (active)
            {
                Color flashColor = _flashColor * _flashIntensity;
                _propertyBlock.SetColor(BaseColorId, flashColor);
                _propertyBlock.SetColor(ColorId, flashColor);

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
        [ButtonGroup("Debug/Actions"), Button("Test Damage (10)")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void DebugTestDamage()
        {
            var info = new DamageInfo(10f, _health.Team == Team.Player ? Team.Enemy : Team.Player,
                HitStrength.Light, gameObject, transform.position, -transform.forward, 0f);
            _health.TakeDamage(in info);
        }

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/Actions"), Button("Heal (10)")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void DebugHeal()
        {
            _health.Heal(10f);
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