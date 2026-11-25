using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace PunkGame
{
    [Serializable]
    public class AttackStep
    {
        [Tooltip("Display name for this attack step in the Inspector and logs.")]
        public string Name = "Attack Step";

        [Tooltip("Determines interruption rules, impact feedback, and hitstop duration.")]
        public HitStrength Strength = HitStrength.Light;

        [Tooltip("Base damage dealt when this step hits a valid target.")]
        [MinValue(0f)]
        public float Damage = 10f;

        [Tooltip("Horizontal knockback force applied on hit.")]
        [MinValue(0f)]
        public float Knockback = 5f;

        [Tooltip("Attack animation clip. Its name must match the state inside the Animator's Attacks sub-state machine unless overridden below.")]
        [Required]
        public AnimationClip Clip;

        [Tooltip("Cross-fade duration in seconds when blending into this attack state.")]
        [MinValue(0f)]
        public float BlendDuration = 0.1f;

        [Tooltip("Optional Animator state name override. Leave empty to use Clip.name (useful with AnimatorOverrideController).")]
        public string StateNameOverride;

        [Tooltip("Fallback duration in seconds to force cleanup if AttackEnd animation event is missed.")]
        [MinValue(0.05f)]
        public float SafetyDuration = 1.5f;

        public string StateName => !string.IsNullOrEmpty(StateNameOverride)
            ? StateNameOverride
            : (Clip != null ? Clip.name : string.Empty);
    }

    // Runs at order 5 (same as CharacterDodge) so movement/rotation locks apply before CharacterMovement (order 10).
    [DefaultExecutionOrder(5)]
    [DisallowMultipleComponent]
    public class CharacterAttack : MonoBehaviour
    {
        private const float MinMoveCancelSqrSpeed = 0.0001f;

        #region Inspector - References

        [FoldoutGroup("References")]
        [Tooltip("WeaponHitbox enabled and disabled by animation events.")]
        [Required, SerializeField]
        private WeaponHitbox _hitbox;

        [FoldoutGroup("References")]
        [Tooltip("Optional CharacterMovement driven for locks and root motion when present.")]
        [SerializeField]
        private CharacterMovement _movement;

        [FoldoutGroup("References")]
        [Tooltip("Optional Health component used to cancel on death and evaluate heavy-hit interruption.")]
        [SerializeField]
        private Health _health;

        [FoldoutGroup("References")]
        [Tooltip("Optional CharacterDodge component used to cancel active attacks when dodging.")]
        [SerializeField]
        private CharacterDodge _dodge;

        #endregion

        #region Inspector - Combat

        [FoldoutGroup("Combat")]
        [Tooltip("Ordered list of combo steps. Step 0 is the opener.")]
        [SerializeField]
        private List<AttackStep> _steps = new List<AttackStep>();

        [FoldoutGroup("Combat")]
        [Tooltip("How long (in seconds) an early attack input is remembered before the combo window opens.")]
        [SerializeField, MinValue(0f)]
        private float _comboInputBufferTime = 0.15f;

        [FoldoutGroup("Combat")]
        [Tooltip("Releases movement locks on HitboxOff so movement input during recovery cancels the rest of the clip into locomotion. Disable on enemies.")]
        [SerializeField]
        private bool _allowMovementCancel = true;

        #endregion

        #region Inspector - Debug

        [FoldoutGroup("Debug")]
        [Tooltip("Logs attack steps, combo chains, buffering, cancellations, and interruptions.")]
        [SerializeField]
        private bool _debugLogs;

        [FoldoutGroup("Debug")]
        [Tooltip("Draws a runtime state overlay on screen during Play Mode.")]
        [SerializeField]
        private bool _debugOverlay;

        #endregion

        #region Runtime State

        private bool _locksHeld;
        private bool _hasBufferedInput;
        private float _bufferedUntil;
        private float _stepSafetyDeadline;
        private int _remainingAutoSteps;

        #endregion

        #region Properties

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public bool IsAttacking { get; private set; }

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public bool IsComboWindowOpen { get; private set; }

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public int StepIndex { get; private set; }

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        public int RemainingAutoSteps => _remainingAutoSteps;

        public AttackStep CurrentStep =>
            IsAttacking && _steps != null && StepIndex >= 0 && StepIndex < _steps.Count
                ? _steps[StepIndex]
                : null;

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        private string CurrentStepName => CurrentStep != null ? CurrentStep.Name : "None";

        [FoldoutGroup("Runtime")]
        [ShowInInspector, ReadOnly]
        private float BufferRemaining =>
            _hasBufferedInput ? Mathf.Max(0f, _bufferedUntil - Time.time) : 0f;

        private bool HasNextStep => _steps != null && StepIndex + 1 < _steps.Count;

        #endregion

        #region Events

        /// <summary>Raised when an attack step starts: (stepData, stepIndex).</summary>
        public event Action<AttackStep, int> AttackStarted;

        /// <summary>Raised when the combo chain window opens (at HitboxOff).</summary>
        public event Action ComboWindowOpened;

        /// <summary>Raised when an attack ends: true = finished naturally, false = canceled or interrupted.</summary>
        public event Action<bool> AttackEnded;

        /// <summary>Raised whenever the combo resets back to step 0.</summary>
        public event Action ComboReset;

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            _hitbox = GetComponentInChildren<WeaponHitbox>(true);
            _movement = GetComponent<CharacterMovement>();
            _health = GetComponent<Health>();
            _dodge = GetComponent<CharacterDodge>();
        }

        private void Awake()
        {
            if (_hitbox == null) _hitbox = GetComponentInChildren<WeaponHitbox>(true);
            if (_movement == null) _movement = GetComponent<CharacterMovement>();
            if (_health == null) _health = GetComponent<Health>();
            if (_dodge == null) _dodge = GetComponent<CharacterDodge>();
        }

        private void OnEnable()
        {
            if (_dodge != null) _dodge.Dodged += OnDodged;
            if (_movement != null) _movement.SpeedChanged += OnMovementSpeedChanged;
            if (_health != null)
            {
                _health.Died += OnDied;
                _health.Damaged += OnDamaged;
            }
        }

        private void OnDisable()
        {
            if (_dodge != null) _dodge.Dodged -= OnDodged;
            if (_movement != null) _movement.SpeedChanged -= OnMovementSpeedChanged;
            if (_health != null)
            {
                _health.Died -= OnDied;
                _health.Damaged -= OnDamaged;
            }

            Cleanup();
            StepIndex = 0;
        }

        private void Update()
        {
            if (!IsAttacking) return;

            if (_allowMovementCancel
                && !_locksHeld
                && _movement != null
                && _movement.DesiredMoveSpeed.sqrMagnitude > MinMoveCancelSqrSpeed)
            {
                CancelFromMovement();
                return;
            }

            if (_hasBufferedInput && Time.time > _bufferedUntil)
            {
                _hasBufferedInput = false;
            }

            if (_stepSafetyDeadline > 0f && Time.time >= _stepSafetyDeadline)
            {
                if (_debugLogs)
                {
                    Debug.LogWarning($"[CharacterAttack] {name} step '{CurrentStepName}' hit safety timeout ({CurrentStep?.SafetyDuration:0.##}s). Canceling.", this);
                }

                Cancel();
            }
        }

        #endregion

        #region Public API

        /// <summary>Starts step 0, chains into the next step if the combo window is open, or buffers the input.</summary>
        public bool TryAttack()
        {
            if (!CanInitiateAttack()) return false;

            _remainingAutoSteps = 0;

            if (!IsAttacking)
            {
                StartStep(0);
                return true;
            }

            if (IsComboWindowOpen && HasNextStep)
            {
                StartStep(StepIndex + 1);
                return true;
            }

            if (HasNextStep && _comboInputBufferTime > 0f)
            {
                _hasBufferedInput = true;
                _bufferedUntil = Time.time + _comboInputBufferTime;

                if (_debugLogs)
                {
                    Debug.Log($"[CharacterAttack] {name} buffered next step for {_comboInputBufferTime:0.##}s.", this);
                }
            }

            return false;
        }

        /// <summary>
        /// Starts a combo at step 0 and automatically chains up to <paramref name="maxSteps"/> total steps
        /// without requiring further inputs (pass -1 to run the full combo list). Intended for NPC AI.
        /// </summary>
        public bool StartCombo(int maxSteps = -1)
        {
            if (IsAttacking || maxSteps == 0 || !CanInitiateAttack()) return false;

            int totalSteps = maxSteps < 0
                ? _steps.Count
                : Mathf.Min(maxSteps, _steps.Count);

            _remainingAutoSteps = Mathf.Max(0, totalSteps - 1);
            StartStep(0);
            return true;
        }

        /// <summary>
        /// Stops any queued auto-combo or buffered follow-ups so the current swing finishes naturally
        /// without advancing to the next step (e.g. when an NPC target moves out of range).
        /// </summary>
        public void StopChaining()
        {
            _remainingAutoSteps = 0;
            _hasBufferedInput = false;
            _bufferedUntil = 0f;
        }

        /// <summary>Cancels any active attack immediately through the single cleanup path.</summary>
        public void Cancel()
        {
            if (!IsAttacking) return;

            if (_debugLogs)
            {
                Debug.Log($"[CharacterAttack] {name} canceled attack at step {StepIndex} ('{CurrentStepName}').", this);
            }

            Cleanup();
            StepIndex = 0;
            AttackEnded?.Invoke(false);
            ComboReset?.Invoke();
        }

        /// <summary>
        /// True when a hit of the given strength would cancel the current step (a Light step hit by a Heavy blow).
        /// Always false when not attacking. Used by CharacterKnockback to decide whether a hit pushes a swinging enemy.
        /// </summary>
        public bool CanBeInterruptedBy(HitStrength incoming)
        {
            AttackStep step = CurrentStep;
            return step != null
                   && step.Strength == HitStrength.Light
                   && incoming == HitStrength.Heavy;
        }

        #endregion

        #region Animation Event Handlers

        public void OnHitboxOn()
        {
            AttackStep step = CurrentStep;
            if (step == null || _hitbox == null) return;

            _hitbox.Begin(step.Damage, step.Knockback, step.Strength);
        }

        public void OnHitboxOff()
        {
            if (_hitbox != null) _hitbox.End();
            if (!IsAttacking) return;

            IsComboWindowOpen = true;
            ComboWindowOpened?.Invoke();

            if (_remainingAutoSteps > 0 && HasNextStep)
            {
                _remainingAutoSteps--;

                if (_debugLogs)
                {
                    Debug.Log($"[CharacterAttack] {name} auto-chaining to step {StepIndex + 1} ({_remainingAutoSteps} remaining).", this);
                }

                StartStep(StepIndex + 1);
                return;
            }

            if (_hasBufferedInput && Time.time <= _bufferedUntil && HasNextStep)
            {
                if (_debugLogs)
                {
                    Debug.Log($"[CharacterAttack] {name} consumed buffered input -> chaining to step {StepIndex + 1}.", this);
                }

                StartStep(StepIndex + 1);
                return;
            }

            if (_allowMovementCancel)
            {
                ReleaseMovementLocks();
            }
        }

        public void OnRootMotionOn()
        {
            if (!IsAttacking || _movement == null) return;

            _movement.SetRootMotion(true);
        }

        public void OnRootMotionOff()
        {
            if (_movement != null) _movement.SetRootMotion(false);
        }

        public void OnAttackEnd()
        {
            if (!IsAttacking) return;

            // Fallback chain if a clip finished without firing HitboxOff while auto-chaining is still active.
            if (_remainingAutoSteps > 0 && HasNextStep)
            {
                _remainingAutoSteps--;
                StartStep(StepIndex + 1);
                return;
            }

            if (_debugLogs)
            {
                Debug.Log($"[CharacterAttack] {name} finished attack at step {StepIndex} ('{CurrentStepName}').", this);
            }

            Cleanup();
            StepIndex = 0;
            AttackEnded?.Invoke(true);
            ComboReset?.Invoke();
        }

        #endregion

        #region Internal Execution

        private bool CanInitiateAttack()
        {
            if (_steps == null || _steps.Count == 0) return false;
            if (_health != null && !_health.IsAlive) return false;
            if (_dodge != null && _dodge.IsDodging) return false;
            return true;
        }

        private void StartStep(int index)
        {
            AttackStep step = _steps[index];

            if (_hitbox != null) _hitbox.End();
            if (_movement != null) _movement.SetRootMotion(false);

            IsAttacking = true;
            IsComboWindowOpen = false;
            StepIndex = index;
            _hasBufferedInput = false;
            _bufferedUntil = 0f;
            _stepSafetyDeadline = step.SafetyDuration > 0f ? Time.time + step.SafetyDuration : 0f;

            if (!_locksHeld && _movement != null)
            {
                _movement.AddMoveLock();
                _movement.AddRotateLock();
                _locksHeld = true;
            }

            if (_debugLogs)
            {
                Debug.Log($"[CharacterAttack] {name} started step {index}: '{step.Name}' ({step.Strength}, {step.Damage} dmg).", this);
            }

            AttackStarted?.Invoke(step, index);
        }

        private void ReleaseMovementLocks()
        {
            if (_movement != null)
            {
                _movement.SetRootMotion(false);
            }

            if (_locksHeld && _movement != null)
            {
                _movement.RemoveMoveLock();
                _movement.RemoveRotateLock();
                _locksHeld = false;
            }
        }

        private void Cleanup()
        {
            IsAttacking = false;
            IsComboWindowOpen = false;
            _hasBufferedInput = false;
            _bufferedUntil = 0f;
            _stepSafetyDeadline = 0f;
            _remainingAutoSteps = 0;

            if (_hitbox != null) _hitbox.End();
            ReleaseMovementLocks();
        }

        private void CancelFromMovement()
        {
            if (_debugLogs)
            {
                Debug.Log($"[CharacterAttack] {name} canceled attack recovery via movement at step {StepIndex} ('{CurrentStepName}').", this);
            }

            Cancel();
        }

        private void OnMovementSpeedChanged(Vector3 desiredSpeed)
        {
            if (!IsAttacking || !_allowMovementCancel || _locksHeld) return;

            if (desiredSpeed.sqrMagnitude > MinMoveCancelSqrSpeed)
            {
                CancelFromMovement();
            }
        }

        private void OnDodged(Vector3 _) => Cancel();
        private void OnDied() => Cancel();

        private void OnDamaged(DamageInfo info)
        {
            if (CanBeInterruptedBy(info.Strength))
            {
                if (_debugLogs)
                {
                    Debug.Log($"[CharacterAttack] {name} interrupted by {info.Strength} hit.", this);
                }

                Cancel();
            }
        }

        #endregion

        #region Debug

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/Actions"), Button("Try Attack")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void DebugTryAttack() => TryAttack();

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/Actions"), Button("Start Full Combo")]
        [EnableIf("@UnityEngine.Application.isPlaying && !IsAttacking")]
        private void DebugStartFullCombo() => StartCombo();

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/Actions"), Button("Stop Chaining")]
        [EnableIf("@UnityEngine.Application.isPlaying && IsAttacking")]
        private void DebugStopChaining() => StopChaining();

        [FoldoutGroup("Debug")]
        [ButtonGroup("Debug/Actions"), Button("Cancel")]
        [EnableIf("@UnityEngine.Application.isPlaying && IsAttacking")]
        private void DebugCancel() => Cancel();

        private void OnGUI()
        {
            if (!_debugOverlay) return;

            GUI.Label(
                new Rect(12f, 36f, 520f, 24f),
                $"Attack: {(IsAttacking ? $"{CurrentStepName} (#{StepIndex})" : "Idle")} | Window: {(IsComboWindowOpen ? "Open" : "Closed")} | Locks: {(_locksHeld ? "Held" : "Released")} | Buffer: {BufferRemaining:0.00}s");
        }

        #endregion
    }
}