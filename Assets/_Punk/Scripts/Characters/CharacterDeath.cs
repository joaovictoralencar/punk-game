using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using PunkGame.Core.Combat;
using PunkGame.Player;

namespace PunkGame.Characters
{
    public enum DeathCleanup
    {
        /// <summary>Leaves the object active (player).</summary>
        None,

        /// <summary>Deactivates the object after the cleanup delay. Reactivating it revives the character.</summary>
        Deactivate,

        /// <summary>Destroys the object after the cleanup delay.</summary>
        Destroy
    }

    // Only does what Health.Died listeners do not already do: CharacterAttack, WeaponHitbox and CharacterAnimations
    // already cancel the swing, close the hitbox and play the Die animation on their own.
    [DisallowMultipleComponent]
    public class CharacterDeath : MonoBehaviour
    {
        #region Inspector - References

        [FoldoutGroup("References")]
        [Tooltip("Health component whose Died event triggers this component.")]
        [Required, SerializeField]
        private Health _health;

        [FoldoutGroup("References")]
        [Tooltip("Optional CharacterMovement. Move, rotate and jump locks are added on death so the body stops walking.")]
        [SerializeField]
        private CharacterMovement _movement;

        [FoldoutGroup("References")]
        [Tooltip("Optional CharacterDodge. An active dodge is canceled on death (player only).")]
        [SerializeField]
        private CharacterDodge _dodge;

        [FoldoutGroup("References")]
        [Tooltip("Optional CharacterController disabled after the controller delay so the corpse stops blocking others.")]
        [SerializeField]
        private CharacterController _controller;

        [FoldoutGroup("References")]
        [Tooltip("Scripts disabled on death and re-enabled on revive (PlayerController, PlayerInputHandler, EnemyBrain...).")]
        [SerializeField]
        private List<Behaviour> _behavioursToDisable = new List<Behaviour>();

        #endregion

        #region Inspector - Cleanup

        [FoldoutGroup("Cleanup")]
        [Tooltip("What happens to the object once the cleanup delay has passed.")]
        [SerializeField]
        private DeathCleanup _cleanup = DeathCleanup.Deactivate;

        [FoldoutGroup("Cleanup")]
        [Tooltip("Seconds after death before the cleanup runs. Should be at least as long as the Die animation.")]
        [SerializeField, MinValue(0f)]
        private float _cleanupDelay = 2f;

        [FoldoutGroup("Cleanup")]
        [Tooltip("Seconds after death before the CharacterController is disabled. Keeps the controller alive long enough for the death hit knockback to slide the body.")]
        [SerializeField, MinValue(0f)]
        private float _disableControllerDelay = 0.25f;

        #endregion

        #region Inspector - Debug

        [FoldoutGroup("Debug")]
        [Tooltip("Logs death, cleanup and revive events to the Console.")]
        [SerializeField]
        private bool _debugLogs;

        #endregion

        #region Runtime State

        private bool _isDead;
        private bool _locksHeld;
        private Coroutine _routine;

        #endregion

        #region Properties

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public bool IsDead => _isDead;

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            _health = GetComponent<Health>();
            _movement = GetComponent<CharacterMovement>();
            _dodge = GetComponent<CharacterDodge>();
            _controller = GetComponent<CharacterController>();

            // Player scripts are picked up automatically. Add EnemyBrain by hand on enemies.
            AddIfPresent(GetComponent<PlayerController>());
            AddIfPresent(GetComponent<PlayerInputHandler>());
        }

        private void Awake()
        {
            if (_health == null) _health = GetComponent<Health>();
            if (_movement == null) _movement = GetComponent<CharacterMovement>();
            if (_dodge == null) _dodge = GetComponent<CharacterDodge>();
            if (_controller == null) _controller = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            if (_health != null) _health.Died += OnDied;

            // Re-enabled after a death (deactivated enemy being reused): start clean.
            if (_isDead) Revive();
        }

        private void OnDisable()
        {
            if (_health != null) _health.Died -= OnDied;

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        #endregion

        #region Death

        private void OnDied()
        {
            if (_isDead) return;
            _isDead = true;

            if (_debugLogs)
            {
                Debug.Log($"[CharacterDeath] {name} died. Cleanup: {_cleanup} in {_cleanupDelay:0.##}s.", this);
            }

            // Without locks the last SetMoveDirection would keep the corpse walking.
            if (_movement != null && !_locksHeld)
            {
                _movement.AddMoveLock();
                _movement.AddRotateLock();
                _movement.AddJumpLock();
                _locksHeld = true;
            }

            if (_dodge != null) _dodge.CancelDodge();

            SetBehavioursEnabled(false);
            _routine = StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            if (_disableControllerDelay > 0f) yield return new WaitForSeconds(_disableControllerDelay);

            if (_controller != null) _controller.enabled = false;

            float remaining = Mathf.Max(0f, _cleanupDelay - _disableControllerDelay);
            if (remaining > 0f) yield return new WaitForSeconds(remaining);

            _routine = null;
            RunCleanup();
        }

        private void RunCleanup()
        {
            switch (_cleanup)
            {
                case DeathCleanup.Deactivate:
                    gameObject.SetActive(false);
                    break;
                case DeathCleanup.Destroy:
                    Destroy(gameObject);
                    break;
            }
        }

        #endregion

        #region Revive

        private void Revive()
        {
            _isDead = false;

            if (_debugLogs)
            {
                Debug.Log($"[CharacterDeath] {name} revived.", this);
            }

            // CharacterMovement.OnDisable already zeroed its lock counters when the object was deactivated.
            // Removing here is harmless (counters are clamped at zero) and covers the case where only this component was toggled.
            if (_movement != null && _locksHeld)
            {
                _movement.RemoveMoveLock();
                _movement.RemoveRotateLock();
                _movement.RemoveJumpLock();
            }

            _locksHeld = false;

            if (_controller != null) _controller.enabled = true;
            SetBehavioursEnabled(true);
            if (_health != null) _health.ResetHealth();
        }

        #endregion

        #region Helpers

        private void SetBehavioursEnabled(bool value)
        {
            for (int i = 0; i < _behavioursToDisable.Count; i++)
            {
                Behaviour behaviour = _behavioursToDisable[i];
                if (behaviour == null || ReferenceEquals(behaviour, this)) continue;

                behaviour.enabled = value;
            }
        }

        private void AddIfPresent(Behaviour behaviour)
        {
            if (behaviour != null && !_behavioursToDisable.Contains(behaviour))
            {
                _behavioursToDisable.Add(behaviour);
            }
        }

        #endregion
    }
}
