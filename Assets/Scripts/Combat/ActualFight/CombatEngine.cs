using System;
using System.Collections.Generic;
using MyGame.Common;
using MyGame.Helpers;
using MyGame.Save;
using MyGame.Spells;
using UnityEngine;

namespace MyGame.Combat
{
    public sealed class CombatEngine
    {
        public event Action<CombatEvent> OnEvent;

        private readonly ICombatSpellResolver _spellResolver;

        private readonly IRng _rng;
        private readonly HitPhase _hitPhase;
        private readonly DamagePhase _damagePhase;
        private readonly EffectPhase _effectPhase;
        private readonly EffectDatabase _effectDb;
        private readonly CombatEffectSystem _effects;
        public CombatState State { get; private set; }

        public CombatEngine(ICombatSpellResolver spellResolver, EffectDatabase effectDb)
        {
            _spellResolver = spellResolver;
            _effectDb = effectDb;

            _effects = new CombatEffectSystem(effectDb);
            // -------------------------
            // RNG (centralized)
            // -------------------------
            // Later you can pass a seeded RNG here for deterministic runs/replays.
            _rng = new UnityRng();

            // -------------------------
            // HIT PHASE: mount hit rules
            // -------------------------
            _hitPhase = new HitPhase(
                new IHitRule[]
                {
                    // 1) Level/tier suppression affects hit chance (weaker attacker -> lower hit chance)
                    new LevelTierSuppressionHitRule(
                        levelPenaltyPerLevel: 3f,
                        tierPenaltyPerTier: 20f
                    ),
                    // 2) Accuracy vs Evasion (diminishing returns)
                    // Later:
                    // new BuffDebuffHitRule(),
                    // new GuaranteedHitRule(),
                    // new MinimumHitChanceRule(0.10f),
                    // new MaximumHitChanceRule(0.95f),
                }
            );

            // -------------------------
            // DAMAGE PHASE: mount damage rules
            // -------------------------
            _damagePhase = new DamagePhase(
                new IDamageRule[]
                {
                    // 1) Update spell base Damage
                    new SpellBaseDamageBonusRule(),
                    // 1) Spell scaling from attacker stats (attackPower/magicPower -> flat bonus)
                    new PowerScalingDamageRule(percentOfPower: 0.50f),
                    // 2) Damage Type bonuses / mitigations from Types [Fire, Piercing etc]
                    new AttackerTypeBonusDamageRule(),
                    new DefenderVulnerabilityDamageRule(),
                    new AttackerWeakenMitigationDamageRule(),
                    new DefenderResistanceMitigationDamageRule(),
                    // 4) Defence Mitigation
                    new DefenseMitigationDamageRule(),
                    // 5) Level/tier suppression affects damage (bidirectional, weaker -> less, stronger -> more)
                    new LevelTierSuppressionDamageRule(
                        levelFactor: 0.03f,
                        tierFactor: 0.20f,
                        minMult: 0.05f
                    ),
                    // 5) BonusDamage simple bonus to final damage and final damage mult
                    new DamageBonusRule(),
                    // 6) Random variance at the end (±20%)
                    new RandomVarianceDamageRule(pct: 0.20f),

                    // Later:
                    // new CritDamageRule(),
                }
            );
            _effectPhase = new EffectPhase(new IEffectRule[] { new ApplyEffectsRule(_effects) });
        }

        // -------------------------
        // Setup
        // -------------------------

        public void StartEncounter(
            SaveData save,
            MonsterDefinition monsterDef,
            int monsterLevel,
            PlayerSpellbook spellbook
        )
        {
            if (save == null)
                throw new ArgumentNullException(nameof(save));
            if (monsterDef == null)
                throw new ArgumentNullException(nameof(monsterDef));
            if (spellbook == null)
                throw new ArgumentNullException(nameof(spellbook));

            var playerDerived = CombatStatCalculator.CalculateAll(
                save.finalStats,
                save.level,
                save.tier
            );
            var enemyDerived = CombatStatCalculator.CalculateAll(
                monsterDef.BaseStats,
                monsterLevel,
                monsterDef.Tier
            );

            int playerHp = Clamp(save.currentHp, 0, playerDerived.maxHp);
            int playerMana = Clamp(save.currentMana, 0, playerDerived.maxMana);

            int enemyHp = enemyDerived.maxHp;
            int enemyMana = enemyDerived.maxMana;

            State = new CombatState
            {
                player = new CombatActorState(
                    type: CombatActorType.Player,
                    name: string.IsNullOrWhiteSpace(save.characterName)
                        ? "Player"
                        : save.characterName,
                    level: save.level,
                    tier: save.tier,
                    baseStats: save.finalStats,
                    derived: playerDerived,
                    startHp: playerHp,
                    startMana: playerMana
                ),
                enemy = new CombatActorState(
                    type: CombatActorType.Enemy,
                    name: string.IsNullOrWhiteSpace(monsterDef.DisplayName)
                        ? "Enemy"
                        : monsterDef.DisplayName,
                    level: monsterLevel,
                    tier: monsterDef.Tier,
                    baseStats: monsterDef.BaseStats,
                    derived: enemyDerived,
                    startHp: enemyHp,
                    startMana: enemyMana
                ),
                playerSpellbook = spellbook,
                waitingForPlayerInput = false,
                isFinished = false,
            };
            //Build monster spellbbok into ombatState
            State.enemySpellbook = new EnemySpellbookRuntime();

            var monsterSpells = monsterDef.Spells;
            if (monsterSpells != null)
            {
                for (int i = 0; i < monsterSpells.Count; i++)
                {
                    var s = monsterSpells[i];
                    if (s == null || string.IsNullOrWhiteSpace(s.SpellId))
                        continue;

                    State.enemySpellbook.spells.Add(
                        new EnemySpellState(s.SpellId, s.Level, s.Weight)
                    );
                }
            }

            DebugLogDerivedStats();
            Emit(
                new CombatLogEvent(
                    $"Encounter starts: {State.player.displayName} vs {State.enemy.displayName}"
                )
            );
            Emit(
                new HpChangedEvent(
                    CombatActorType.Player,
                    State.player.hp,
                    State.player.derived.maxHp,
                    0
                )
            );
            Emit(
                new ManaChangedEvent(
                    CombatActorType.Player,
                    State.player.mana,
                    State.player.derived.maxMana,
                    0
                )
            );
            Emit(
                new HpChangedEvent(
                    CombatActorType.Enemy,
                    State.enemy.hp,
                    State.enemy.derived.maxHp,
                    0
                )
            );
            Emit(
                new ManaChangedEvent(
                    CombatActorType.Enemy,
                    State.enemy.mana,
                    State.enemy.derived.maxMana,
                    0
                )
            );

            EnemyQueueNextSpell();

            SetTurnMeter(CombatActorType.Player, State.player.turnMeter);
            SetTurnMeter(CombatActorType.Enemy, State.enemy.turnMeter);

            State.waitingForPlayerInput = true;
        }

        // -------------------------
        // Main loop control
        // -------------------------

        public void AdvanceUntilPlayerInputOrEnd()
        {
            if (State == null || State.isFinished)
                return;

            while (!State.isFinished && !State.waitingForPlayerInput)
            {
                // If player has no queued spell, stop and wait
                if (!State.player.HasQueuedSpell)
                {
                    State.waitingForPlayerInput = true;
                    return;
                }

                // Ensure enemy always has a queued spell
                //if (!State.enemy.HasQueuedSpell)
                //    EnemyQueueNextSpell();

                if (State.waitingForEnemyDecision)
                    return;

                // If enemy has no queued spell, request one (but don't auto-queue here).
                // This makes enemy decision fully controlled by controller timing.
                if (!State.enemy.HasQueuedSpell)
                {
                    State.waitingForEnemyDecision = true;
                    Emit(new EnemyDecisionRequestedEvent(State.enemy.displayName));
                    return;
                }

                // Resolve queued spells to determine DamageKind (attackSpeed vs castSpeed)
                ResolvedSpell playerQueued = ResolveQueuedSpell(State.player);
                ResolvedSpell enemyQueued = ResolveQueuedSpell(State.enemy);

                float pSpeed = GetActionSpeed(State.player, playerQueued);
                float eSpeed = GetActionSpeed(State.enemy, enemyQueued);

                float pMissing = TURN_THRESHOLD - State.player.turnMeter;
                float eMissing = TURN_THRESHOLD - State.enemy.turnMeter;

                // If someone already ready (should be rare), fire immediately
                if (pMissing <= 0.001f)
                {
                    FireQueuedSpell(CombatActorType.Player, playerQueued);
                    AfterActorFired(CombatActorType.Player);
                    continue;
                }

                if (eMissing <= 0.001f)
                {
                    FireQueuedSpell(CombatActorType.Enemy, enemyQueued);
                    AfterActorFired(CombatActorType.Enemy);
                    continue;
                }

                // Compute "fraction of cast interval" needed for each to reach 100
                float tPlayer = pMissing / pSpeed;
                float tEnemy = eMissing / eSpeed;

                float t = Mathf.Min(tPlayer, tEnemy);

                // Advance both meters by the same fraction
                float newPMeter = State.player.turnMeter + (pSpeed * t);
                float newEMeter = State.enemy.turnMeter + (eSpeed * t);

                // Snap winner to threshold (avoid 99.9999 issues)
                if (Mathf.Abs(newPMeter - TURN_THRESHOLD) < 0.001f)
                    newPMeter = TURN_THRESHOLD;
                if (Mathf.Abs(newEMeter - TURN_THRESHOLD) < 0.001f)
                    newEMeter = TURN_THRESHOLD;

                SetTurnMeter(CombatActorType.Player, newPMeter);
                SetTurnMeter(CombatActorType.Enemy, newEMeter);

                // Decide who fires (tie -> player, deterministic)
                bool pReady = State.player.turnMeter >= TURN_THRESHOLD - 0.001f;
                bool eReady = State.enemy.turnMeter >= TURN_THRESHOLD - 0.001f;

                CombatActorType next;
                if (pReady && eReady)
                    next = CombatActorType.Player; // tie-break
                else if (pReady)
                    next = CombatActorType.Player;
                else
                    next = CombatActorType.Enemy;

                // Fire and loop again (enemy may fire multiple times before player)
                if (next == CombatActorType.Player)
                {
                    FireQueuedSpell(CombatActorType.Player, playerQueued);
                    AfterActorFired(CombatActorType.Player);
                }
                else
                {
                    FireQueuedSpell(CombatActorType.Enemy, enemyQueued);
                    AfterActorFired(CombatActorType.Enemy);
                }
            }
        }

        private void EnemyQueueNextSpell()
        {
            string chosen = DecideEnemySpellId();

            if (string.IsNullOrWhiteSpace(chosen))
                chosen = "enemy_attack"; // fallback if no candidates

            var ticks = _effects.OnActionChosen(State, CombatActorType.Enemy);
            ApplyPeriodicTicks(ticks);
            if (State.isFinished)
                return;
            State.enemy.queuedSpellId = chosen;
            Emit(new SpellQueuedEvent(CombatActorType.Enemy, State.enemy.displayName, chosen));
        }

        private string DecideEnemySpellId()
        {
            var book = State.enemySpellbook;
            if (book == null || book.spells == null || book.spells.Count == 0)
                return null;

            // candidates: off cooldown + resolvable + enough mana
            var candidates = new List<EnemySpellState>();

            for (int i = 0; i < book.spells.Count; i++)
            {
                var s = book.spells[i];
                if (s.cooldownRemainingTurns > 0)
                    continue;

                if (
                    !_spellResolver.TryResolve(
                        s.spellId,
                        s.level, // ✅ monster spell level
                        State.enemy.derived,
                        out var resolved
                    )
                )
                    continue;

                if (State.enemy.mana < resolved.manaCost)
                    continue;

                candidates.Add(s);
            }

            if (candidates.Count == 0)
                return null;

            float total = 0f;
            for (int i = 0; i < candidates.Count; i++)
                total += candidates[i].weight;

            float r = _rng.Range(0f, total);

            float acc = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                acc += candidates[i].weight;
                if (r <= acc)
                    return candidates[i].spellId;
            }

            return candidates[^1].spellId;
        }

        private void FinishCombat(CombatActorType winner)
        {
            State.isFinished = true;
            State.waitingForPlayerInput = false;

            Emit(new CombatLogEvent(winner == CombatActorType.Player ? "You win!" : "You lose!"));
            Emit(new CombatEndedEvent(winner));
        }

        // -------------------------
        // Player actions
        // -------------------------

        public bool TryUseSpell(string spellId)
        {
            if (State == null || State.isFinished)
                return false;
            if (!State.waitingForPlayerInput)
                return false;
            if (string.IsNullOrWhiteSpace(spellId))
                return false;

            var entry = State.playerSpellbook.Get(spellId);
            if (entry == null)
            {
                Emit(new CombatLogEvent($"You don't know spell '{spellId}'."));
                return false;
            }

            if (entry.cooldownRemainingTurns > 0)
            {
                Emit(
                    new CombatLogEvent(
                        $"Spell is on cooldown ({entry.cooldownRemainingTurns} turns)."
                    )
                );
                return false;
            }

            if (
                !_spellResolver.TryResolve(
                    spellId,
                    entry.level,
                    State.player.derived,
                    out var resolved
                )
            )
            {
                Emit(new CombatLogEvent($"Spell '{spellId}' could not be resolved."));
                return false;
            }

            if (State.player.mana < resolved.manaCost)
            {
                Emit(
                    new CombatLogEvent(
                        $"Not enough mana ({State.player.mana}/{resolved.manaCost})."
                    )
                );
                return false;
            }

            _effects.OnActionChosen(State, CombatActorType.Player);
            var ticks = _effects.OnActionChosen(State, CombatActorType.Player);
            ApplyPeriodicTicks(ticks);

            if (State.isFinished)
                return false;

            State.player.queuedSpellId = spellId;
            Emit(
                new SpellQueuedEvent(
                    CombatActorType.Player,
                    State.player.displayName,
                    resolved.displayName
                )
            );
            // End player turn and continue
            State.waitingForPlayerInput = false;
            //EndTurn(CombatActorType.Player);

            if (!State.isFinished)
                AdvanceUntilPlayerInputOrEnd();

            return true;
        }

        // -------------------------
        // Enemy actions
        // -------------------------

        // -------------------------
        // ✅ THE IMPORTANT PART: Phases mounted above are used here
        // -------------------------

        private void ResolveAction(
            CombatActorState attacker,
            CombatActorState defender,
            CombatActorType source,
            CombatActorType target,
            ResolvedSpell spell
        )
        {
            // 1) Create action context (shared scratchpad for all phases)
            var ctx = new ActionContext
            {
                attacker = attacker,
                defender = defender,
                spell = spell,
                spellLevel = spell.level,
                rng = _rng,

                // Start from spell base hit chance (your question earlier)
                hitChance = spell.hitChance,
            };
            // -------------------------
            // 0) ON-CAST EFFECTS
            // -------------------------
            if (spell.onCastEffects != null && spell.onCastEffects.Length > 0)
            {
                ctx.effectInstancesToApply = spell.onCastEffects;
                _effectPhase.Resolve(ctx);
                ctx.effectInstancesToApply = null;
            }

            bool requiresHitCheck = (
                spell.intent == SpellIntent.Damage || spell.intent == SpellIntent.Heal
            );
            // 2) HIT PHASE
            if (requiresHitCheck)
            {
                _hitPhase.Resolve(ctx);

                if (!ctx.hit)
                {
                    Emit(
                        new CombatLogEvent(
                            $"{attacker.displayName} uses {spell.displayName}, but it misses!"
                        )
                    );
                    return;
                }
            }
            else
            {
                // Buff/Debuff/Utility: treat as successful
                ctx.hit = true;
            }

            Emit(new CombatLogEvent($"{attacker.displayName} uses {spell.displayName}!"));

            // -------------------------
            // 2) DAMAGE / HEAL / SKIP
            // -------------------------
            if (spell.intent == SpellIntent.Damage)
            {
                _damagePhase.Resolve(ctx);
                DealDamage(source, target, ctx.finalDamage);
            }
            else if (spell.intent == SpellIntent.Heal)
            {
                // Minimal: treat resolved damage as "healing amount" OR add dedicated heal amount later
                // For now: you can reuse ctx.finalDamage as "final heal amount" if you build a HealPhase later.
                // We'll keep it simple and heal by spell.damage.
                ApplyHeal(source, target, Mathf.Max(0, spell.damage));
            }
            else
            {
                ctx.finalDamage = 0;
            }
            ctx.lastDamageDealt = ctx.finalDamage;

            // -------------------------
            // 3) ON-HIT EFFECTS (after successful hit and after damage/heal)
            // -------------------------
            if (ctx.hit && spell.onHitEffects != null && spell.onHitEffects.Length > 0)
            {
                ctx.effectInstancesToApply = spell.onHitEffects;
                _effectPhase.Resolve(ctx);
                ctx.effectInstancesToApply = null;
            }

            if (source == CombatActorType.Player)
            {
                int xpPerHit =
                    1
                    + ctx.defender.level
                        * HelperFunctions.TierToFlatBonusMultiplier(ctx.defender.tier); // your rule
                int levelsGained = State.playerSpellbook.GrantExperience(spell.spellId, xpPerHit);

                if (levelsGained > 0)
                {
                    var entry = State.playerSpellbook.Get(spell.spellId);
                    Emit(
                        new CombatLogEvent(
                            $"{spell.displayName} leveled up! Now Lv {entry.level} (+{levelsGained})."
                        )
                    );
                }
            }
        }

        // -------------------------
        // HP / Mana helpers
        // -------------------------

        private void DealDamage(CombatActorType source, CombatActorType target, int amount)
        {
            amount = Math.Max(0, amount);

            var t = State.Get(target);
            int before = t.hp;

            t.hp = Math.Max(0, t.hp - amount);

            int delta = t.hp - before; // negative on damage

            Emit(
                new CombatAdvancedLogEvent(
                    $"{State.Get(source).displayName} deals",
                    amount,
                    $"damage to {t.displayName}",
                    CombatLogType.Damage
                )
            );
            Emit(new HpChangedEvent(target, t.hp, t.derived.maxHp, delta));

            if (t.hp <= 0)
            {
                FinishCombat(source);
            }
        }

        private void ApplyHeal(CombatActorType source, CombatActorType target, int amount)
        {
            amount = Math.Max(0, amount);

            var t = State.Get(target);
            int before = t.hp;

            t.hp = Clamp(t.hp + amount, 0, t.derived.maxHp);

            int delta = t.hp - before; // positive on heal

            if (delta <= 0)
                return;

            Emit(
                new CombatAdvancedLogEvent(
                    $"{State.Get(source).displayName} heals",
                    delta,
                    $"HP for {t.displayName}",
                    CombatLogType.Heal
                )
            );

            Emit(new HpChangedEvent(target, t.hp, t.derived.maxHp, delta));
        }

        private void ChangeMana(CombatActorType actor, int delta)
        {
            var a = State.Get(actor);

            int before = a.mana;
            a.mana = Clamp(a.mana + delta, 0, a.derived.maxMana);

            int realDelta = a.mana - before;

            Emit(new ManaChangedEvent(actor, a.mana, a.derived.maxMana, realDelta));
        }

        private static int Clamp(int v, int min, int max)
        {
            if (v < min)
                return min;
            if (v > max)
                return max;
            return v;
        }

        private const int TURN_THRESHOLD = 100;

        private void SetTurnMeter(CombatActorType actor, float value)
        {
            var a = State.Get(actor);

            float clamped = Mathf.Clamp(value, 0f, TURN_THRESHOLD);
            a.turnMeter = clamped;
            Emit(
                new TurnMeterChangedEvent(
                    actor,
                    Mathf.RoundToInt(a.turnMeter),
                    Mathf.RoundToInt(TURN_THRESHOLD)
                )
            );
        }

        private float GetActionSpeed(CombatActorState actor, ResolvedSpell queuedSpell)
        {
            float bonus =
                queuedSpell.damageKind == DamageKind.Magical
                    ? actor.derived.castSpeed
                    : actor.derived.attackSpeed;
            // TODO pridať sem modifiery na cast/attackSpeed

            float speed = queuedSpell.baseUseSpeed + bonus;

            if (speed < 1f)
                speed = 1f;

            return speed;
        }

        private void Emit(CombatEvent e) => OnEvent?.Invoke(e);

        private ResolvedSpell ResolveQueuedSpell(CombatActorState actor)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));

            if (State == null)
                throw new InvalidOperationException("CombatEngine.State is null.");

            if (string.IsNullOrWhiteSpace(actor.queuedSpellId))
                throw new InvalidOperationException($"{actor.actorType} has no queued spell.");

            // -------------------------
            // PLAYER
            // -------------------------
            if (actor.actorType == CombatActorType.Player)
            {
                var entry = State.playerSpellbook?.Get(actor.queuedSpellId);
                if (entry == null)
                    throw new InvalidOperationException(
                        $"Player queued unknown spell '{actor.queuedSpellId}'."
                    );

                if (
                    !_spellResolver.TryResolve(
                        actor.queuedSpellId,
                        entry.level,
                        State.player.derived,
                        out var resolvedPlayer
                    )
                )
                    throw new InvalidOperationException(
                        $"Could not resolve player spell '{actor.queuedSpellId}'."
                    );

                return resolvedPlayer;
            }

            // -------------------------
            // ENEMY
            // -------------------------
            // If enemy spellbook exists, try resolve using monster-defined spell level.
            var enemyBook = State.enemySpellbook;
            EnemySpellState enemySpellState = null;

            if (enemyBook != null && enemyBook.spells != null)
                enemySpellState = enemyBook.spells.Find(x => x.spellId == actor.queuedSpellId);

            if (enemySpellState != null)
            {
                if (
                    _spellResolver.TryResolve(
                        enemySpellState.spellId,
                        enemySpellState.level,
                        State.enemy.derived,
                        out var resolvedEnemy
                    )
                )
                {
                    return resolvedEnemy;
                }

                throw new InvalidOperationException(
                    $"Could not resolve enemy spell '{enemySpellState.spellId}' (Lv {enemySpellState.level})."
                );
            }

            // -------------------------
            // FALLBACK (placeholder)
            // -------------------------
            // If enemy spellbook is missing OR queued id is not in it, fallback to basic attack.
            // This prevents combat hard-crashing while you're still wiring content.
            return new ResolvedSpell(
                spellId: "enemy_attack",
                displayName: $"{State.enemy.displayName} Attack",
                manaCost: 0,
                cooldownTurns: 0,
                damage: 5,
                damageKind: DamageKind.Physical,
                ignoreDefenseFlat: 0,
                ignoreDefensePercent: 0,
                hitChance: 90,
                baseUseSpeed: 50,
                damageTypes: new[] { DamageType.Slashing },
                onHitEffects: Array.Empty<EffectInstance>(),
                onCastEffects: Array.Empty<EffectInstance>(),
                intent: SpellIntent.Damage,
                level: 1
            );
        }

        private void FireQueuedSpell(CombatActorType actorType, ResolvedSpell resolvedQueuedSpell)
        {
            var attacker = State.Get(actorType);
            var defender = State.GetOpponent(actorType);

            if (actorType == CombatActorType.Player)
            {
                State.playerSpellbook.TickCooldowns();
                ChangeMana(CombatActorType.Player, -resolvedQueuedSpell.manaCost);
                State.playerSpellbook.StartCooldown(
                    resolvedQueuedSpell.spellId,
                    resolvedQueuedSpell.cooldownTurns
                );
            }
            else
            {
                State.enemySpellbook?.TickCooldowns();
                // Spend enemy mana
                ChangeMana(CombatActorType.Enemy, -resolvedQueuedSpell.manaCost);

                // Apply cooldown to the spell just used
                State.enemySpellbook?.StartCooldown(
                    resolvedQueuedSpell.spellId,
                    resolvedQueuedSpell.cooldownTurns
                );
            }
            Emit(new SpellFiredEvent(actorType));

            ResolveAction(
                attacker: attacker,
                defender: defender,
                source: actorType,
                target: defender.actorType,
                spell: resolvedQueuedSpell
            );
        }

        private void AfterActorFired(CombatActorType actorType)
        {
            var a = State.Get(actorType);

            // Consume 100 meter
            SetTurnMeter(actorType, a.turnMeter - TURN_THRESHOLD);

            // Clear queued spell (it was cast)
            a.queuedSpellId = null;

            if (State.isFinished)
                return;

            if (actorType == CombatActorType.Enemy)
            {
                // Enemy immediately queues next spell and we continue looping
                State.waitingForEnemyDecision = true;
                Emit(new EnemyDecisionRequestedEvent(State.enemy.displayName));
            }
            else
            {
                // Player must choose next spell -> stop simulation here
                State.waitingForPlayerInput = true;
                //Emit(new CombatLogEvent("Choose your next spell."));
            }
        }

        public void QueueEnemySpellAfterDecision()
        {
            if (State == null || State.isFinished)
                return;

            EnemyQueueNextSpell();
            State.waitingForEnemyDecision = false;
        }

        public bool TryGetPlayerSpellCooldown(
            string spellId,
            out int remainingTurns,
            out int maxTurns
        )
        {
            remainingTurns = 0;
            maxTurns = 0;

            if (State == null || State.isFinished)
                return false;

            if (string.IsNullOrWhiteSpace(spellId))
                return false;

            var entry = State.playerSpellbook?.Get(spellId);
            if (entry == null)
                return false;

            remainingTurns = Mathf.Max(0, entry.cooldownRemainingTurns);

            // Resolve to get the design-time cooldownTurns (max)
            if (
                !_spellResolver.TryResolve(
                    spellId,
                    entry.level,
                    State.player.derived,
                    out var resolved
                )
            )
            {
                // If we can't resolve, we still can show remaining as "some cooldown",
                // but we don't know max. Use remaining as max so bar doesn't break.
                maxTurns = Mathf.Max(1, remainingTurns);
                return true;
            }

            maxTurns = Mathf.Max(1, resolved.cooldownTurns);

            // Safety: if remaining is bigger than max for any reason, clamp max upward
            // so visuals still make sense.
            if (remainingTurns > maxTurns)
                maxTurns = remainingTurns;

            return true;
        }

        private void ApplyPeriodicTicks(List<CombatEffectSystem.PeriodicTickResult> ticks)
        {
            if (ticks == null || ticks.Count == 0)
                return;

            for (int i = 0; i < ticks.Count; i++)
            {
                var t = ticks[i];
                int amount = Mathf.Max(0, t.amount);
                if (amount <= 0)
                    continue;

                if (t.kind == EffectKind.DamageOverTime)
                {
                    DealDamage(source: t.source, target: t.target, amount: amount);
                }
                else if (t.kind == EffectKind.HealOverTime)
                {
                    ApplyHeal(source: t.source, target: t.target, amount: amount);
                }

                // If combat ended from DOT damage, stop processing remaining ticks
                if (State.isFinished)
                    return;
            }
        }

        private void DebugLogDerivedStats()
        {
            Debug.Log(BuildDerivedStatsBlock("PLAYER", State.player));
            Debug.Log(BuildDerivedStatsBlock("ENEMY", State.enemy));
        }

        private string BuildDerivedStatsBlock(string label, CombatActorState actor)
        {
            var d = actor.derived;

            return $@"==================== {label} DERIVED STATS ====================
Name: {actor.displayName}
Level: {actor.level}
Tier: {actor.tier}

-- Core --
Max HP:          {d.maxHp}
Max Mana:        {d.maxMana}
Attack Power:    {d.attackPower}
Magic Power:     {d.magicPower}
PhysicalDefense: {d.physicalDefense}
MagicalDefense:  {d.magicalDefense}

-- Speed --
Attack Speed:    {d.attackSpeed}
Cast Speed:      {d.castSpeed}

-- Accuracy --
Evasion:         {d.evasion}

==============================================================";
        }
    }
}
