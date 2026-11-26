using Sirenix.OdinInspector;
using UnityEngine;
using PunkGame.Characters;
using PunkGame.Core.Combat;

namespace PunkGame.Combat
{
    // Runs at order 5 (same as CharacterAttack) so the override is set before CharacterMovement (order 10) applies it.
    // Enemies only: the player never gets this component.
    [DefaultExecutionOrder(5)]
    [DisallowMultipleComponent]
    public class CharacterKnockback : MonoBehaviour
    {
        private const float MinDirectionSqrMagnitude = 0.0001f;

        #region Inspector - References

        [FoldoutGroup("References")]
        [Tooltip("Health component whose Damaged event triggers the knockback.")]
        [Required, SerializeField]
        private Health _health;

        [FoldoutGroup("References")]
        [Tooltip("CharacterMovement that applies the slide through its horizontal velocity override.")]
        [Required, SerializeField]
        private CharacterMovement _movement;

        [FoldoutGroup("References")]
        [Tooltip("Optional CharacterAttack. A swing that is not interrupted by the hit keeps its position (no slide).")]
        [SerializeField]
        private CharacterAttack _attack;

        #endregion

        #region Inspector - Knockback

        [FoldoutGroup("Knockback")]
        [Tooltip("Scales the incoming DamageInfo.Knockback value, which is used as the initial slide speed.")]
        [SerializeField, MinValue(0f)]
        private float _multiplier = 1f;

        [FoldoutGroup("Knockback")]
        [Tooltip("Duration of the slide in seconds.")]
        [SerializeField, MinValue(0.01f)]
        private float _duration = 0.2f;

        [FoldoutGroup("Knockback")]
        [Tooltip("Speed falloff over the normalized slide duration (X = time 0 to 1, Y = speed multiplier).")]
        [SerializeField]
        private AnimationCurve _decay = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        #endregion

        #region Inspector - Debug

        [FoldoutGroup("Debug")]
        [Tooltip("Logs started and ignored knockbacks to the Console.")]
        [SerializeField]
        private bool _debugLogs;

        #endregion

        #region Runtime State

        private Vector3 _direction;
        private float _initialSpeed;
        private float _elapsed;

        #endregion

        #region Properties

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public bool IsKnockedBack { get; private set; }

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            _health = GetComponent<Health>();
            _movement = GetComponent<CharacterMovement>();
            _attack = GetComponent<CharacterAttack>();
        }

        private void Awake()
        {
            if (_health == null) _health = GetComponent<Health>();
            if (_movement == null) _movement = GetComponent<CharacterMovement>();
            if (_attack == null) _attack = GetComponent<CharacterAttack>();
        }

        private void OnEnable()
        {
            if (_health != null) _health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
            EndKnockback();
        }

        private void Update()
        {
            if (!IsKnockedBack) return;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            _movement.SetHorizontalVelocityOverride(_direction * (_initialSpeed * _decay.Evaluate(t)));

            if (t >= 1f) EndKnockback();
        }

        #endregion

        #region Handlers

        private void OnDamaged(DamageInfo info)
        {
            if (info.Knockback <= 0f || info.HitDirection.sqrMagnitude < MinDirectionSqrMagnitude) return;

            // A swing that was not interrupted keeps its position (for example a Heavy swing hit by a Light blow).
            // The result is the same whether CharacterAttack.OnDamaged runs before or after this handler:
            // if the hit cancels the swing, IsAttacking is already false or CanBeInterruptedBy is true.
            if (_attack != null && _attack.IsAttacking && !_attack.CanBeInterruptedBy(info.Strength))
            {
                if (_debugLogs)
                {
                    Debug.Log($"[CharacterKnockback] {name} ignored knockback from a {info.Strength} hit (swing not interrupted).", this);
                }

                return;
            }

            Vector3 flat = Vector3.ProjectOnPlane(info.HitDirection, Vector3.up);
            if (flat.sqrMagnitude < MinDirectionSqrMagnitude) return;

            _direction = flat.normalized;
            _initialSpeed = info.Knockback * _multiplier;
            _elapsed = 0f;
            IsKnockedBack = true;

            if (_debugLogs)
            {
                Debug.Log($"[CharacterKnockback] {name} knocked back at {_initialSpeed:0.##} for {_duration:0.##}s.", this);
            }
        }

        private void EndKnockback()
        {
            if (!IsKnockedBack) return;

            IsKnockedBack = false;
            if (_movement != null) _movement.ClearHorizontalVelocityOverride();
        }

        #endregion

        #region Debug

        [FoldoutGroup("Debug")]
        [Button("Test Knockback (8)")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void DebugKnockback()
        {
            var info = new DamageInfo(0f, Team.Neutral, HitStrength.Light, gameObject, transform.position, -transform.forward, 8f);
            OnDamaged(info);
        }

        #endregion
    }
}
