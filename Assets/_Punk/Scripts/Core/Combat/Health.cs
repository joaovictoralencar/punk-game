using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace PunkGame.Core.Combat
{
    [DisallowMultipleComponent]
    public class Health : MonoBehaviour, IDamageable
    {
        [BoxGroup("Configuration")]
        [Tooltip("Maximum health capacity of this character.")]
        [SerializeField, MinValue(1f)]
        private float _maxHealth = 100f;

        [BoxGroup("Configuration")]
        [Tooltip("Team affiliation used to ignore friendly fire.")]
        [SerializeField]
        private Team _team = Team.Enemy;

        [BoxGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        [ProgressBar(0f, nameof(Max), ColorGetter = nameof(GetHealthBarColor), Height = 22)]
        public float Current { get; private set; }

        public float Max => _maxHealth;
        public float Normalized => _maxHealth > 0f ? Current / _maxHealth : 0f;
        public Team Team => _team;

        [BoxGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public bool IsAlive => Current > 0f;

        [BoxGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public bool CanTakeDamage { get; private set; } = true;

        [FoldoutGroup("Debug")]
        [Tooltip("Logs damage, healing, death, and state changes to the Console.")]
        [SerializeField]
        private bool _debugLogs;

        public event Action<DamageInfo> Damaged;
        /// <summary>Invoked when health changes: (currentHealth, delta).</summary>
        public event Action<float, float> HealthChanged;
        public event Action Died;

        private void Awake()
        {
            Current = _maxHealth;
            CanTakeDamage = true;
        }

        public DamageResult TakeDamage(in DamageInfo info)
        {
            if (!IsAlive || !CanTakeDamage || info.Team == _team || info.Amount <= 0f)
            {
                if (_debugLogs)
                {
                    Debug.Log($"[Health] {name} ignored {info.Amount} ({info.Strength}) damage from {info.Team}.", this);
                }

                return DamageResult.Ignored;
            }

            SetHealthInternal(Current - info.Amount);

            if (_debugLogs)
            {
                Debug.Log($"[Health] {name} took {info.Amount} ({info.Strength}) damage. Current: {Current}/{_maxHealth}.", this);
            }

            Damaged?.Invoke(info);

            if (!IsAlive)
            {
                if (_debugLogs)
                {
                    Debug.Log($"[Health] {name} died.", this);
                }

                Died?.Invoke();
                return DamageResult.Killed;
            }

            return DamageResult.Hit;
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;

            SetHealthInternal(Current + amount);

            if (_debugLogs)
            {
                Debug.Log($"[Health] {name} healed {amount}. Current: {Current}/{_maxHealth}.", this);
            }
        }

        public void Kill(bool callDamageEvents = false)
        {
            if (!IsAlive) return;

            if (callDamageEvents)
            {
                var info = new DamageInfo(
                    Current,
                    GetOpposingTeam(),
                    HitStrength.Light,
                    null,
                    transform.position,
                    Vector3.zero,
                    0f);

                TakeDamage(in info);
            }
            else
            {
                SetHealthInternal(0f);

                if (_debugLogs)
                {
                    Debug.Log($"[Health] {name} killed directly.", this);
                }

                Died?.Invoke();
            }
        }

        public void ResetHealth(bool notify = true)
        {
            CanTakeDamage = true;
            float oldValue = Current;
            Current = _maxHealth;

            if (_debugLogs)
            {
                Debug.Log($"[Health] {name} health reset to {Current}/{_maxHealth}.", this);
            }

            if (notify)
            {
                HealthChanged?.Invoke(Current, Current - oldValue);
            }
        }

        public void SetCanTakeDamage(bool newValue)
        {
            if (CanTakeDamage == newValue) return;

            CanTakeDamage = newValue;

            if (_debugLogs)
            {
                Debug.Log($"[Health] {name} CanTakeDamage set to {CanTakeDamage}.", this);
            }
        }

        private void SetHealthInternal(float newAmount, bool notify = true)
        {
            float clamped = Mathf.Clamp(newAmount, 0f, _maxHealth);
            if (Mathf.Approximately(clamped, Current)) return;

            float oldValue = Current;
            Current = clamped;

            if (notify)
            {
                HealthChanged?.Invoke(Current, Current - oldValue);
            }
        }

        private Team GetOpposingTeam()
        {
            return _team == Team.Player ? Team.Enemy : Team.Player;
        }

        private Color GetHealthBarColor(float value)
        {
            float ratio = Normalized;
            if (ratio > 0.5f) return new Color(0.2f, 0.8f, 0.3f);
            if (ratio > 0.25f) return new Color(0.95f, 0.7f, 0.15f);
            return new Color(0.9f, 0.2f, 0.2f);
        }

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/Damage"), Button("Light Damage (10)")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void DebugLightDamage()
        {
            var info = new DamageInfo(10f, GetOpposingTeam(), HitStrength.Light, gameObject, transform.position, -transform.forward, 0f);
            TakeDamage(in info);
        }

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/Damage"), Button("Heavy Damage (10)")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void DebugHeavyDamage()
        {
            var info = new DamageInfo(10f, GetOpposingTeam(), HitStrength.Heavy, gameObject, transform.position, -transform.forward, 0f);
            TakeDamage(in info);
        }

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/State"), Button("Heal (10)")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void DebugHeal()
        {
            Heal(10f);
        }

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/State"), Button("Kill")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void DebugKill()
        {
            Kill();
        }

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/State"), Button("Reset")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void DebugReset()
        {
            ResetHealth();
        }

        [FoldoutGroup("Debug")]
        [Button("Toggle CanTakeDamage")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void DebugToggleCanTakeDamage()
        {
            SetCanTakeDamage(!CanTakeDamage);
        }
    }
}