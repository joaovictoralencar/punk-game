# PunkGame — Project Improvement Roadmap (v4)

*Build order: verify the existing combat slice, add health bars and damage numbers, simplify the enemy state-machine boundary, finish enemy perception, add repeatable encounters, then expand enemy types and progression. The game already has a working player/enemy foundation, so this roadmap starts from the current code instead of repeating the original setup phases.*

## Ground rules

- **Keep the current component boundaries:** `CharacterMovement` owns body movement; `CharacterAttack` owns melee combo timing; `EnemyBrain` makes AI decisions; `EnemyAIMovement` owns NavMesh paths; `Health` and `WeaponHitbox` own damage rules.
- **Prefer small extensions over rewrites.** Keep the enemy state enum and switch while the state set remains small. Introduce state classes, attack strategies, or shared registries only when a second real use case needs them.
- **Keep combat typed and team-aware.** New attacks should use `DamageInfo`, `DamageResult`, `IDamageable`, and the existing team checks rather than creating a parallel damage path.
- **Use the existing events for feedback.** `Health.HealthChanged`, `Health.Damaged`, and `WeaponHitbox.Hit` already cover the first HUD and hit-feedback needs; do not add static broadcast events for them.
- **No global registry by default.** Prefer assigning targets directly or having the spawner call `EnemyBrain.Engage`; do not add `Health.Active` or static event buses just to find one player.
- **No pooling or crowd-steering framework yet.** Start with a modest active-enemy cap and profile/playtest before adding either.
- **Each phase ends in a playable check.** Do not add a menu, progression system, or new enemy framework before the basic encounter loop is reliable.

## Where we are

Already implemented:

- Player input, locomotion, dodge with buffering and invulnerability, melee combos, animation-event forwarding, hitbox filtering, team-aware damage, health and death events.
- Character knockback and configurable death cleanup, including deactivation/reuse, plus the training dummy.
- Enemy patrol, chase, and attack decisions; distance-based target acquisition with separate sight/lose ranges; attack facing, cooldown and leash behavior.
- `EnemyBrain`, plain-C# `EnemyStateMachine`, replaceable `IEnemyTargetSense`, and a separate `EnemyAIMovement` adapter.
- Enemy prefabs (`pfb_EnemyBase`, `pfb_Enemy_Robot_Chomp`), player prefab, test scene, and a baked NavMesh asset. AI Navigation is already installed.
- NavMesh local avoidance is enabled in code at high quality, priorities vary per enemy, and path movement follows `NavMeshAgent.desiredVelocity` so avoidance steering reaches the `CharacterController`.
- `Scene_Test_Environment` and `Scene_Player` are enabled in Build Settings. `Scene_Islands` exists but is not currently enabled there.

Still incomplete or not established:

- Target sensing is distance-based; there is no field of view, obstacle line-of-sight, last-seen search, or hit-triggered aggro.
- The avoidance change needs a deliberate in-Editor playtest at several enemy counts and around stationary/engaged enemies.
- No project-owned health-bar, damage-number, game-over, enemy-spawner, or encounter/wave director component was found. The test scene has a `MainCanvas`, which should be inspected and extended rather than replaced. DamageNumbersPro assets are already present.
- `EnemyStateMachine` works for its three current states, but combines target-command precedence, target validation, state decisions, and attack cooldown handling. Its positional constructor also takes many tuning values, and attack completion can transition state both in the event callback and in the next state tick.
- No final game loop has been selected: the project could be fixed encounters, timed survival, or a progression-based run.

## Roadmap at a glance

| Phase | Goal | New files (candidate) | Done when |
| :-- | :-- | :-- | :-- |
| 0 | Stabilize the existing combat and AI slice | None | The test scene has a reliable, repeatable player-versus-enemy encounter |
| 1 | Add health bars and damage numbers | `HealthBar`, `DamageNumberFeedback` | Player and enemy health and accepted hits are immediately readable |
| 2 | Simplify the enemy state-machine boundary | `EnemyAISettings`; possibly `EnemyTargetResolver` | Existing patrol/chase/attack behavior is unchanged and state ownership is clearer |
| 3 | Finish enemy awareness and response | Extend `EnemyVisionSense` and `EnemyStateMachine` | Enemies see, lose, search for, and reacquire the player predictably |
| 4 | Build a repeatable encounter/spawn path | `EnemySpawner` or `EncounterController` | Enemies spawn on the NavMesh, receive the intended target, and are tracked until death |
| 5 | Add a clear defeat/restart loop | `GameOverPanel` | The player can restart a complete encounter cleanly |
| 6 | Add one distinct enemy archetype | One focused attack/behavior component if needed | The new enemy reuses health, damage, death, and movement without duplicating core systems |
| 7 | Add waves and progression if the chosen game mode needs them | `WaveDirector`, wave data asset | A complete run has a beginning, escalation, and ending |
| 8 | Prepare the selected scene for a shareable build | Menus/settings components only as needed | The intended scene launches and the core loop works in a standalone build |

## Phase 0: Stabilize the existing slice

Goal: establish a dependable baseline before adding more systems.

- [ ] Open `Scene_Test_Environment` and verify the `pfb_Player` and `pfb_Enemy_Robot_Chomp` setup, references, attack animation events, NavMesh bounds, and baked data.
- [ ] Check the enemy's `CharacterController` and `NavMeshAgent` dimensions together. Adjust the agent radius only if in-Editor playtesting shows visual overlap or excessive spacing.
- [ ] Verify `EnemyAIMovement` remains the only NavMesh/body adapter: the agent does not write the transform, and the `CharacterController` remains the movement authority.
- [ ] Playtest avoidance with 1, 5, and 10 enemies: head-on crossings, several enemies pursuing one player, enemies reaching attack range, and enemies stopped beside one another.
- [ ] Confirm an enemy that is attacking or stationary does not cause persistent pushing, oscillation, or path jitter. Tune avoidance settings only against a reproduced issue.
- [ ] Run through player dodge, attack interruption, hit deduplication, enemy knockback, death cleanup, and deactivate/reactivate reuse.
- [ ] Decide which scene is the actual game entry point. Keep test scenes available for development, but avoid shipping a test scene as the default by accident.
- [ ] Record any repeatable failures as focused fixes; do not respond to a single bad setup by adding a general-purpose steering or pooling layer.

Done when: a developer can enter the test scene, understand the player/enemy controls, complete several fights, and repeat the run without errors, stale state, or obvious movement conflicts.

## Phase 1: Health bars and damage numbers

Goal: make the existing fight easier to read before adding more AI behavior.

- [ ] Inspect the existing `MainCanvas` in `Scene_Test_Environment`; reuse its hierarchy and styling rather than making a parallel HUD.
- [ ] Add one reusable `HealthBar` that binds to a `Health`, listens to `Health.HealthChanged`, displays current/max health, and unsubscribes when disabled.
- [ ] Put a health bar on the player first, then add an enemy bar only if the playtest needs it. Keep the two instances on the same component and configure visibility per prefab.
- [ ] Add a `DamageNumberFeedback` adapter that listens to the hit target's `Health.Damaged` event and uses `DamageInfo.HitPoint`, `Amount`, and `Strength` with the already-installed DamageNumbersPro assets.
- [ ] Decide whether numbers represent the attack's requested damage or actual health removed. `Health.Damaged` includes the incoming `DamageInfo`; `Health.HealthChanged` includes the actual delta. If exact damage is required, add a small applied-damage value to the damage notification rather than a static global event.
- [ ] Ensure duplicate colliders do not display duplicate numbers; retain `WeaponHitbox`'s per-target hit deduplication and exercise the real enemy/player prefabs.
- [ ] Keep this feature local to each `Health`/feedback component. Do not add `Health.AnyDamaged` or a global UI manager for the first implementation.

Playtest checklist:

- [ ] Player health updates on damage, healing, death, and reset.
- [ ] Enemy health updates on damage and death without a missing-reference error when the bar is hidden or absent.
- [ ] A normal hit and a killing blow each show one damage number at the impact point.
- [ ] Overkill behavior matches the chosen damage-number semantics.
- [ ] Disabling/re-enabling the character does not leave duplicate event subscriptions or stale bars.

Done when: the current test encounter clearly communicates player/enemy health and accepted hits without introducing a global event or feedback framework.

## Phase 2: Simplify the enemy state-machine boundary

Goal: reduce coupling and make the existing three-state AI easier to extend without changing its behavior.

- [ ] Capture current behavior in a transition table and playtest it before editing: Patrol → Chase, Chase → Attack, Attack → Chase/Patrol, target loss, target command, and death.
- [ ] Make attack completion have one transition owner. Currently `OnAttackEnded` changes state and `TickAttack` also checks `IsAttacking`; retain one authoritative transition path while keeping cooldown timing tied to `AttackEnded`.
- [ ] Group the AI tuning values currently passed positionally into a named `EnemyAISettings` object. Keep it as serialized data on `EnemyBrain` at first; use a ScriptableObject only if multiple enemy prefabs need shared editable tuning.
- [ ] Separate command-target precedence and target validity from state ticking only if doing so gives a genuinely clearer boundary. If extracted, keep it a small `EnemyTargetResolver` with tests; do not create a service locator or registry.
- [ ] Keep `EnemyBrain` as the Unity lifecycle/composition root and `EnemyStateMachine` as the decision logic. Avoid moving Animator, NavMeshAgent, or scene lookup responsibilities into the state machine.
- [ ] Keep Patrol, Chase, and Attack as the current enum/switch. Do not create one file/object per state solely to reduce line count; reconsider that pattern after Search or a second behavior set makes state logic branch-heavy.
- [ ] Add focused transition tests where practical, plus the same test-scene playthrough. If automated tests require new test infrastructure, add only the Unity Test Framework needed for these cases.
- [ ] Verify `OnDisable` unsubscribes `AttackEnded`, `Dispose` stops movement, and re-enabling the brain creates exactly one live state machine.

Done when: Patrol/Chase/Attack behavior is unchanged, settings are named rather than positional, attack completion transitions once, and the state machine has one clear owner for each responsibility.

## Phase 3: Finish enemy awareness and reactions

Goal: make current enemies react intentionally instead of chasing solely by distance.

- [ ] Extend `EnemyVisionSense` with a configurable view angle and obstacle line-of-sight check. Keep its current acquire/lose-range hysteresis.
- [ ] Add a small close-awareness range so an enemy can notice a nearby player outside its view cone without making it omniscient.
- [ ] Add a short sense interval only if profiling or tests show the current per-frame distance check is costly; do not add a scheduler pre-emptively.
- [ ] Preserve the last visible player position in `EnemyStateMachine`. When sight is lost, move to that position, wait for a short search duration, then return to patrol; reacquiring the player returns to chase.
- [ ] Listen to the enemy's existing `Health.Damaged` event only if hits should provoke aggro. Use `DamageInfo.Source` to identify the attacker and pass the target through the existing `Engage` path.
- [ ] Test attack recovery and interruption using existing `CharacterAttack` rules. Add a dedicated `Stagger` state only if the desired behavior cannot be expressed cleanly by attack cancellation plus a short recovery timer.
- [ ] Keep the enum/switch for Patrol, Chase, Attack, and the first Search implementation. Revisit per-state classes only if states become substantially independent or another enemy needs a different state set.

Playtest checklist:

- [ ] A player outside sight range is ignored.
- [ ] A player in range but behind an obstacle is not acquired.
- [ ] Entering the view cone starts a chase; stepping behind cover sends the enemy to the last seen position before it gives up.
- [ ] Re-entering sight during search resumes the chase.
- [ ] A nearby player can be noticed at the configured close-awareness range.
- [ ] If damage-triggered aggro is enabled, the enemy responds to the actual attacker and not an unrelated object.
- [ ] A hit during an attack follows the configured interruption rules; no attack, hitbox, or movement lock remains stuck.

Done when: awareness rules and transitions are predictable in the test scene, and all attack/hit/death behavior from Phase 0 still works.

## Phase 4: Add repeatable encounters and spawning

Goal: place or spawn enemies reliably without introducing a global enemy manager.

- [ ] First choose the encounter style: fixed groups placed in the level, a timer-based spawner, or both. Implement only the first mode the game needs.
- [ ] If using a spawner, add one small `EnemySpawner` with spawn-point transforms, an enemy prefab list, a spawn interval, a minimum distance from the player, and a configurable alive cap.
- [ ] Sample a valid NavMesh position before spawning and call `NavMeshAgent.Warp` when needed. Log a clear warning when a configured spawn point cannot produce a valid position.
- [ ] Assign the player target explicitly after spawning with `EnemyBrain.Engage`; retain the existing scene lookup only as a convenient fallback for hand-placed test enemies.
- [ ] Track spawned enemies through their `Health.Died` event or the spawned instance reference. Unsubscribe when the instance is removed; do not add a static `Health.Active` list.
- [ ] Use 10 active enemies as an initial crowding/performance test target, not a permanent design limit. Make any shipped alive cap an Inspector setting; raise it only after the scene behaves well and profiling supports it.
- [ ] Use ordinary instantiate/destroy or the existing death deactivation behavior initially. Add pooling only if measured allocation or spawn spikes justify its lifecycle complexity.
- [ ] Keep test-scene controls for placing a few fixed enemies even after a spawner exists; they make AI debugging quicker.

Done when: configured encounters start consistently, all enemies begin on the NavMesh with the correct target, alive counts recover after death, and the configured cap cannot be exceeded.

## Phase 5: Add a clear defeat/restart loop

Goal: let the player restart a complete encounter after defeat.

- [ ] Add a `GameOverPanel` that listens to the player's `Health.Died`, disables further player input, and offers a restart action.
- [ ] Confirm the restart action reloads the chosen gameplay scene and restores `Time.timeScale` if the game-over panel pauses it.
- [ ] Test restart after a hand-placed enemy encounter first, then after a spawned encounter exists.
- [ ] Ensure deactivated enemies, hitboxes, health bars, and event subscriptions do not survive into the restarted scene.

Done when: player death is clear, the current encounter stops, and restart restores a clean playable scene.

## Phase 6: Add one new enemy archetype

Goal: prove that the existing systems can support a meaningful behavior difference before generalizing them.

- [ ] Choose one archetype based on the desired gameplay: a faster melee variant is mostly data; a ranged enemy is a larger behavior change.
- [ ] For a data-only melee variant, create a prefab variant and tune existing health, movement, attack steps, and cooldowns. Do not create a subclass just to hold different numbers.
- [ ] For a ranged enemy, keep its projectile and firing logic in a focused component and send projectile hits through `DamageInfo`/`IDamageable`.
- [ ] Verify how `EnemyBrain` starts attacks and how `CharacterAttack`'s required hitbox is configured before choosing the ranged integration. If melee and ranged attack lifecycles truly differ, introduce the smallest attack boundary needed then; do not add a generic ability framework in advance.
- [ ] Add a dedicated movement behavior only if the ranged enemy must keep distance or kite. The current chase movement intentionally approaches the target.
- [ ] Keep enemy-specific tuning in prefab values initially. Add an `EnemyAIConfig` ScriptableObject only when multiple prefabs need shared, centrally editable AI tuning.
- [ ] Add a focused test scene layout or prefab variant for the new archetype and compare it against the baseline enemy.

Done when: the new enemy creates a distinct decision for the player, still respects team/damage/death rules, and does not require copying the basic enemy's systems.

## Phase 7: Add waves and progression

Goal: support the selected game mode, not a progression framework without a game objective.

- [ ] Decide whether the core experience is an arena survival run, a sequence of fixed encounters, or exploration with occasional fights.
- [ ] If waves fit the chosen mode, add a small `WaveDirector` that owns wave timing and delegates actual instantiation to the spawner.
- [ ] Store wave composition as simple data (prefab, count, delay). Use ScriptableObject assets when designers need reusable wave definitions; otherwise start with serialized data on the director.
- [ ] Add a pause between waves and a clear completion/defeat condition before adding player XP or upgrades.
- [ ] Add kill rewards or player upgrades only after there is a clear reason to earn them. Keep rewards driven by the existing death events rather than adding global registries.
- [ ] Tune difficulty with a few authored waves before adding automatic scaling.

Done when: the selected mode has a clear start, progression through encounters, and a win or loss condition that can be playtested end-to-end.

## Phase 8: Prepare a shareable build

Goal: make the chosen game loop easy to launch and verify outside the editor.

- [ ] Confirm the intended entry scene and reorder Build Settings so it launches first. Keep development/test scenes available without making them the default release scene.
- [ ] Add only the needed front-end flow: start, pause/resume, restart, and return-to-menu.
- [ ] Ensure pausing and game over restore time scale and input state correctly when resuming or restarting.
- [ ] Add basic audio feedback for attack, hit, dodge, enemy death, and UI actions; route through existing component events.
- [ ] Do a standalone build smoke test for player input, NavMesh movement, scene transitions, UI, and restart.
- [ ] Profile the target hardware with the planned enemy cap. Add pooling or lower avoidance quality only if measurements show a real bottleneck.

Done when: the build launches into the intended scene and the complete chosen gameplay loop works without relying on editor-only actions.

## Architecture notes for future work

### Keep the current ownership boundaries

`CharacterMovement` is the single body-movement authority. Player and enemy controllers provide intent; `CharacterMovement` applies locks, velocity, gravity, root motion, and the `CharacterController` move. The NavMeshAgent is a path/avoidance solver that is synchronized to the body, not a second transform writer.

`CharacterAttack` owns combo and animation-event timing. `WeaponHitbox` owns contact filtering and one-hit-per-target behavior. `Health` applies team rules and emits typed results. New weapon or projectile behavior should call the same damage API rather than duplicate combat rules.

### Grow enemy AI only at its pressure points

The current separation (`EnemyBrain`, `EnemyStateMachine`, `EnemyAIMovement`, `IEnemyTargetSense`) is already more extensible than one large enemy base class. Preserve it. Add line-of-sight to the existing sense implementation; add state logic only when the behavior is real; introduce attack polymorphism only when a second attack type needs it.

### Configuration, folders, and assemblies

Enemy settings currently live across the brain and movement component, which is fine for the current number of prefabs. If tuning becomes duplicated, introduce one read-only enemy configuration asset and pass it as a settings object instead of adding many positional constructor parameters. Avoid storing runtime state in shared ScriptableObjects.

Gameplay scripts are grouped under `Scripts/Core/Combat`, `Characters`, `Combat`, `Enemies`, `Player`, `Presentation`, and `Development`. Their original Unity `.meta` files travel with the scripts so prefab and scene GUID references stay stable.

`PunkGame.Core` contains shared damage contracts and health; `PunkGame.Gameplay` contains character, player, enemy, and presentation behavior. Player and enemy code remain together in Gameplay because they share character components and currently refer to each other; separate assemblies per feature would add dependency management without a useful boundary yet. Project-owned Editor and test assemblies can be added when those scripts or tests exist.

### Avoidance validation

The body is moved by `CharacterController`; the NavMeshAgent must remain `updatePosition = false` and `updateRotation = false`. Its `desiredVelocity` is the local-avoidance-aware steering direction, and its `nextPosition` must stay synchronized to the body. Test actual gameplay before changing radius, priorities, prediction settings, obstacle carving, or adding a separate steering layer.

## Open decisions

1. Which scene is the primary playable level: `Scene_Player`, `Scene_Islands`, or a new encounter scene?
2. Should the first repeatable game mode use fixed encounters or a timer-based spawner?
3. Should enemies use a view cone and last-seen search, or is distance-based awareness the intended arcade behavior?
4. Should damage numbers show incoming attack damage or actual health removed on overkill?
5. Which archetype should be the first meaningful expansion: a ranged enemy or a second melee style?
6. What ends a run: clearing a room, surviving waves, defeating a boss, or something else?
