using System;
using System.Collections.Generic;
using MyGame.Common;
using MyGame.Helpers;
using MyGame.Inventory;
using MyGame.Run;
using MyGame.Save;
using MyGame.Spells;
using UnityEngine;

namespace MyGame.Combat
{
    public sealed class CombatEngine : ICombatEffectCallbacks
    {
        public event Action<CombatEvent> OnEvent;

        private readonly ICombatSpellResolver _spellResolver;

        private readonly IRng _rng;
        private readonly HitPhase _hitPhase;
        private readonly DamagePhase _damagePhase;
        private readonly EffectPhase _effectPhase;

        public CombatState State { get; private set; }
        private readonly CombatEffectSystem _effects;

        private PlayerEquipment _playerEquipment;

        public CombatEngine(ICombatSpellResolver spellResolver, EffectDatabase effectDb)
        {
            _spellResolver = spellResolver;
            _effects = new CombatEffectSystem(effectDb, this);
            // RNG (centralized)
            _rng = new UnityRng();

            // HIT PHASE: mount hit rules
            _hitPhase = new HitPhase(
                new IHitRule[]
                {
                    new LevelTierSuppressionHitRule(
                        levelPenaltyPerLevel: 3f,
                        tierPenaltyPerTier: 20f
                    ),
                }
            );

            // DAMAGE PHASE: mount damage rules
            _damagePhase = new DamagePhase(
                new IDamageRule[]
                {
                    new SpellBaseDamageBonusRule(), // správne nastaví baseDamage
                    new PowerScalingDamageRule(percentOfPower: 0.50f), // správne pridá flat damage bonus z AP alebo MP
                    new AttackerTypeBonusDamageRule(), //správne nastaví falt a mult bonus podľa typu spellu
                    new AttackerRangeBonusDamageRule(), // range-based bonuses (melee/ranged)
                    new DefenderVulnerabilityDamageRule(), //správne pridí do flatDamageBonus a damageMult
                    new AttackerWeakenMitigationDamageRule(), // odoberie z flat a mult final damage
                    new DefenderResistanceMitigationDamageRule(), // odoberie z flat a mult final damage
                    new DefenseMitigationDamageRule(), // vypočíta effective Defense
                    new LevelTierSuppressionDamageRule(
                        levelFactor: 0.03f,
                        tierFactor: 0.20f,
                        minMult: 0.05f
                    ), // korektne zdihne mult bonus
                    new DamageBonusRule(), // korektne zvysi mult aj flat bonus
                    new RandomVarianceDamageRule(pct: 0.20f), // dobre zysi damage mult
                }
            );

            _effectPhase = new EffectPhase(new IEffectRule[] { new ApplyEffectRule(_effects) });
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

            // Load equipment first so base-stat rolls can affect derived calculations.
            var equipment = RunSession.Equipment ?? InventorySaveMapper.LoadEquipmentFromSave(save);
            _playerEquipment = equipment;

            // Base stats used for derived calculations (flats first, then summed %).
            var effectiveBaseStats = PlayerBaseStatsResolver.BuildEffectiveBaseStats(
                save,
                equipment
            );

            var playerDerived = CombatStatCalculator.CalculateAll(
                effectiveBaseStats,
                save.level,
                save.tier
            );

            // Apply class/spec derived modifiers (maxHp, power, speed, etc.)
            var baseDerivedMods = new List<DerivedStatModifier>(64);
            var classDb =
                GameConfigProvider.Instance != null
                    ? GameConfigProvider.Instance.PlayerClassDatabase
                    : null;
            if (classDb != null)
            {
                var classSo = classDb.GetClass(save.classId);
                if (classSo != null)
                {
                    DerivedModifierApplier.ApplyAll(ref playerDerived, classSo.derivedStatMods);
                    if (classSo.derivedStatMods != null && classSo.derivedStatMods.Count > 0)
                        baseDerivedMods.AddRange(classSo.derivedStatMods);
                }

                var specSo = classDb.GetSpec(save.specId);
                if (specSo != null)
                {
                    DerivedModifierApplier.ApplyAll(ref playerDerived, specSo.derivedStatMods);
                    if (specSo.derivedStatMods != null && specSo.derivedStatMods.Count > 0)
                        baseDerivedMods.AddRange(specSo.derivedStatMods);
                }
            }

            // Apply equipped rolled derived modifiers.
            if (equipment != null)
            {
                foreach (var inst in equipment.GetEquippedInstances())
                {
                    if (
                        inst?.rolledDerivedStatMods == null
                        || inst.rolledDerivedStatMods.Count == 0
                    )
                        continue;
                    DerivedModifierApplier.ApplyAll(ref playerDerived, inst.rolledDerivedStatMods);
                    baseDerivedMods.AddRange(inst.rolledDerivedStatMods);
                }
            }
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
                    baseStats: effectiveBaseStats,
                    derived: playerDerived,
                    startHp: playerHp,
                    startMana: playerMana,
                    baseDerivedMods: baseDerivedMods
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
                playerItemCooldowns = new PlayerItemCooldownsRuntime(),
                waitingForPlayerInput = false,
                isFinished = false,
            };

            LoadPersistentItemCooldownsIntoRuntime(save, State.playerItemCooldowns);

            // Apply equipped rolled combat stat modifiers onto the player's modifier bucket.
            if (equipment != null)
            {
                foreach (var inst in equipment.GetEquippedInstances())
                {
                    if (inst?.rolledCombatStatMods == null || inst.rolledCombatStatMods.Count == 0)
                        continue;

                    for (int i = 0; i < inst.rolledCombatStatMods.Count; i++)
                    {
                        var mod = inst.rolledCombatStatMods[i];
                        CombatEffectSystem.ApplyCombatStatModifier(
                            State.player.modifiers,
                            mod.stat,
                            mod.op,
                            mod.value,
                            mod.damageType
                        );
                    }
                }
            }

            // Build monster spellbook into CombatState
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

            SetTurnMeter(CombatActorType.Player, State.player.turnMeter, DEFAULT_TURN_THRESHOLD);
            SetTurnMeter(CombatActorType.Enemy, State.enemy.turnMeter, DEFAULT_TURN_THRESHOLD);

            State.waitingForPlayerInput = true;
        }

        private static void LoadPersistentItemCooldownsIntoRuntime(
            SaveData save,
            PlayerItemCooldownsRuntime runtime
        )
        {
            if (save == null || runtime == null)
                return;

            if (save.persistentItemCooldowns == null || save.persistentItemCooldowns.Count == 0)
                return;

            var itemDb = GameConfigProvider.Instance?.ItemDatabase;
            var items = RunSession.Items;

            for (int i = 0; i < save.persistentItemCooldowns.Count; i++)
            {
                var e = save.persistentItemCooldowns[i];
                if (e == null)
                    continue;

                if (string.IsNullOrWhiteSpace(e.itemId) || e.remainingTurns <= 0)
                    continue;

                var def = itemDb != null ? itemDb.GetById(e.itemId) : null;
                if (def == null || !def.carryCooldownBetweenFights)
                    continue;

                // If the player no longer has any stack, ignore the persisted cooldown.
                if (items == null || items.GetCount(e.itemId) <= 0)
                    continue;

                runtime.StartCooldown(e.itemId, e.remainingTurns);
            }
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
                // If player has no queued action, stop and wait
                if (!State.player.HasQueuedAction)
                {
                    State.waitingForPlayerInput = true;
                    return;
                }

                if (State.waitingForEnemyDecision)
                    return;

                // If enemy has no queued action, request one (controller decides timing)
                if (!State.enemy.HasQueuedAction)
                {
                    State.waitingForEnemyDecision = true;
                    Emit(new EnemyDecisionRequestedEvent(State.enemy.displayName));

                    return;
                }

                // Resolve queued actions to determine speed/threshold.
                var playerQueued = ResolveQueuedAction(State.player);
                var enemyQueued = ResolveQueuedAction(State.enemy);

                float pSpeed = GetActionSpeed(State.player, playerQueued);
                float eSpeed = GetActionSpeed(State.enemy, enemyQueued);

                float pThreshold = GetThreshold(playerQueued);
                float eThreshold = GetThreshold(enemyQueued);

                float pMissing = pThreshold - State.player.turnMeter;
                float eMissing = eThreshold - State.enemy.turnMeter;

                // If someone already ready, fire immediately
                if (pMissing <= 0.001f)
                {
                    FireQueuedAction(CombatActorType.Player, playerQueued);
                    AfterActorFired(CombatActorType.Player);
                    continue;
                }

                if (eMissing <= 0.001f)
                {
                    FireQueuedAction(CombatActorType.Enemy, enemyQueued);
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

                // Snap to threshold (avoid 99.9999 issues)
                if (Mathf.Abs(newPMeter - pThreshold) < 0.001f)
                    newPMeter = pThreshold;
                if (Mathf.Abs(newEMeter - eThreshold) < 0.001f)
                    newEMeter = eThreshold;

                SetTurnMeter(CombatActorType.Player, newPMeter, pThreshold);
                SetTurnMeter(CombatActorType.Enemy, newEMeter, eThreshold);

                bool pReady = State.player.turnMeter >= pThreshold - 0.001f;
                bool eReady = State.enemy.turnMeter >= eThreshold - 0.001f;

                CombatActorType next;
                if (pReady && eReady)
                    next = CombatActorType.Player;
                else if (pReady)
                    next = CombatActorType.Player;
                else
                    next = CombatActorType.Enemy;

                if (next == CombatActorType.Player)
                {
                    FireQueuedAction(CombatActorType.Player, playerQueued);
                    AfterActorFired(CombatActorType.Player);
                }
                else
                {
                    FireQueuedAction(CombatActorType.Enemy, enemyQueued);
                    AfterActorFired(CombatActorType.Enemy);
                }
            }
        }

        private void EnemyQueueNextSpell()
        {
            string chosen = DecideEnemySpellId();

            if (string.IsNullOrWhiteSpace(chosen))
                chosen = "enemy_attack"; // fallback if no candidates

            if (State.isFinished)
                return;
            //TickEffectsForActor(CombatActorType.Enemy);
            State.enemy.queuedActionId = chosen;
            State.enemy.queuedActionType = QueuedActionType.Spell;
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
                        s.level,
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

            if (State.isFinished)
                return false;
            //TickEffectsForActor(CombatActorType.Player);
            State.player.queuedActionId = spellId;
            State.player.queuedActionType = QueuedActionType.Spell;
            Emit(
                new SpellQueuedEvent(
                    CombatActorType.Player,
                    State.player.displayName,
                    resolved.displayName
                )
            );

            // End player decision; simulation continues
            State.waitingForPlayerInput = false;

            if (!State.isFinished)
                AdvanceUntilPlayerInputOrEnd();

            return true;
        }

        // -------------------------
        // ✅ The important part: phases used here
        // -------------------------

        private void ResolveAction(
            CombatActorState attacker,
            CombatActorState defender,
            CombatActorType source,
            CombatActorType target,
            ResolvedSpell spell,
            StatModifiers modifiers,
            bool grantSpellXp = true
        )
        {
            var ctx = new ActionContext
            {
                attacker = attacker,
                defender = defender,
                spell = spell,
                spellLevel = spell.level,
                rng = _rng,
                hitChance = spell.hitChance,
            };

            // 0) ON-CAST EFFECTS
            if (spell.onCastEffects != null && spell.onCastEffects.Length > 0)
            {
                ctx.effectInstancesToApply = spell.onCastEffects;
                _effectPhase.Resolve(ctx, modifiers);
                ctx.effectInstancesToApply = null;
            }

            bool requiresHitCheck = HasType(spell, SpellType.Damage)
                || HasType(spell, SpellType.Heal);

            // 1) HIT
            if (requiresHitCheck)
            {
                _hitPhase.Resolve(ctx, modifiers);

                if (!ctx.hit)
                {
                    Emit(
                        new CombatLogEvent(
                            $"{attacker.displayName} casts {spell.displayName}, but it misses!"
                        )
                    );
                    return;
                }
            }
            else
            {
                ctx.hit = true;
            }

            Emit(new CombatLogEvent($"{attacker.displayName} casts {spell.displayName}!"));

            // 2) DAMAGE / HEAL / SKIP
            if (HasType(spell, SpellType.Damage))
            {
                _damagePhase.Resolve(ctx, modifiers);
                DealDamage(
                    source,
                    target,
                    ctx.finalDamage,
                    fromEffect: false,
                    fromSpell: true,
                    spell.displayName,
                    spell.icon
                );
            }
            else if (HasType(spell, SpellType.Heal))
            {
                ApplyHeal(
                    source,
                    target,
                    Mathf.Max(0, spell.damage),
                    fromEffect: false,
                    fromSpell: true,
                    spell.displayName,
                    spell.icon
                );
                ctx.finalDamage = Mathf.Max(0, spell.damage); // treat as "amount" if needed
            }
            else
            {
                ctx.finalDamage = 0;
            }

            // This is the value that DamageDealt-basis effects need
            ctx.lastDamageDealt = Mathf.Max(0, ctx.finalDamage);

            // 3) ON-HIT EFFECTS (after successful hit and after damage/heal)
            if (ctx.hit && spell.onHitEffects != null && spell.onHitEffects.Length > 0)
            {
                ctx.effectInstancesToApply = spell.onHitEffects;
                _effectPhase.Resolve(ctx, modifiers);
                ctx.effectInstancesToApply = null;
            }

            // XP for player spell
            if (grantSpellXp && source == CombatActorType.Player)
            {
                int xpPerHit =
                    1
                    + ctx.defender.level
                        * HelperFunctions.TierToFlatBonusMultiplier(ctx.defender.tier);

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

        public void ApplyDirectDamage(
            CombatActorType source,
            CombatActorType target,
            int amount,
            string effectName,
            Sprite icon
        )
        {
            DealDamage(
                source,
                target,
                amount,
                fromEffect: true,
                fromSpell: false,
                damageSourceName: effectName,
                icon
            );
        }

        public void ApplyDirectHeal(
            CombatActorType source,
            CombatActorType target,
            int amount,
            string effectName,
            Sprite icon
        )
        {
            ApplyHeal(
                source,
                target,
                amount,
                fromEffect: true,
                fromSpell: false,
                healSourceName: effectName,
                icon
            );
        }

        public void NotifyDerivedStatsChanged(
            CombatActorState actor,
            int oldHp,
            int oldMaxHp,
            int oldMana,
            int oldMaxMana
        )
        {
            if (actor == null)
                return;

            if (oldHp != actor.hp || oldMaxHp != actor.derived.maxHp)
            {
                Emit(
                    new HpChangedEvent(
                        actor.actorType,
                        actor.hp,
                        actor.derived.maxHp,
                        actor.hp - oldHp
                    )
                );
            }

            if (oldMana != actor.mana || oldMaxMana != actor.derived.maxMana)
            {
                Emit(
                    new ManaChangedEvent(
                        actor.actorType,
                        actor.mana,
                        actor.derived.maxMana,
                        actor.mana - oldMana
                    )
                );
            }
        }

        private void DealDamage(
            CombatActorType source,
            CombatActorType target,
            int amount,
            bool fromEffect,
            bool fromSpell,
            string damageSourceName,
            Sprite icon
        )
        {
            amount = Math.Max(0, amount);

            var t = State.Get(target);
            int before = t.hp;

            t.hp = Math.Max(0, t.hp - amount);

            int delta = t.hp - before; // negative on damage
            int applied = -delta;
            t.lastDamageTaken = applied;

            if (fromEffect)
            {
                Emit(
                    new CombatAdvancedLogEvent(
                        $"{damageSourceName} deals",
                        amount,
                        $"damage to {t.displayName}",
                        CombatLogType.Damage
                    )
                );
            }
            if (fromSpell)
            {
                Emit(
                    new CombatAdvancedLogEvent(
                        $"{State.Get(source).displayName}'s {damageSourceName} deals",
                        amount,
                        $"damage to {t.displayName}",
                        CombatLogType.Damage
                    )
                );
            }

            Emit(new HpChangedEvent(target, t.hp, t.derived.maxHp, delta));

            if (applied >= 0)
            {
                Emit(
                    new FloatingNumberEvent(
                        source: source,
                        target: target,
                        amount: applied,
                        kind: FloatingNumberKind.Damage,
                        icon: icon, // next step: wire real icon
                        label: damageSourceName
                    )
                );
            }

            if (t.hp <= 0)
                FinishCombat(source);
        }

        private void ApplyHeal(
            CombatActorType source,
            CombatActorType target,
            int amount,
            bool fromEffect,
            bool fromSpell,
            string healSourceName,
            Sprite icon
        )
        {
            amount = Math.Max(0, amount);

            var t = State.Get(target);
            int before = t.hp;

            t.hp = Clamp(t.hp + amount, 0, t.derived.maxHp);

            int delta = t.hp - before; // positive on heal
            if (delta <= 0)
                return;
            if (fromEffect)
            {
                Emit(
                    new CombatAdvancedLogEvent(
                        $"{healSourceName} heals",
                        delta,
                        $"HP for {t.displayName}",
                        CombatLogType.Heal
                    )
                );
            }
            if (fromSpell)
            {
                Emit(
                    new CombatAdvancedLogEvent(
                        $"{State.Get(source).displayName}'s {healSourceName} heals",
                        delta,
                        $"HP for {t.displayName}",
                        CombatLogType.Heal
                    )
                );
            }

            Emit(
                new FloatingNumberEvent(
                    source: source,
                    target: target,
                    amount: delta,
                    kind: FloatingNumberKind.Heal,
                    icon: icon, // next step: wire real icon
                    label: healSourceName
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

        private void TickEffectsForActor(CombatActorType actorType)
        {
            //LogAllStatModifiers(actorType, "Before ticking effects");
            DebugLogDerivedStats();
            DebugLogAllStatModifiers(actorType, "Before ticking effects");
            if (_effects == null || State == null || State.isFinished)
                return;

            var ticks = _effects.TickOnActionChosen(State, actorType);
            if (ticks == null || ticks.Count == 0)
                return;

            for (int i = 0; i < ticks.Count; i++)
            {
                var t = ticks[i];
                int amount = Math.Max(0, t.amount);
                if (amount <= 0)
                    continue;

                if (t.kind == EffectKind.DamageOverTime)
                {
                    // damage target
                    DealDamage(
                        source: t.source,
                        target: t.target,
                        amount: amount,
                        fromEffect: true,
                        fromSpell: false,
                        damageSourceName: t.effectName,
                        t.icon
                    );
                }
                else if (t.kind == EffectKind.HealOverTime)
                {
                    // heal target
                    ApplyHeal(
                        source: t.target,
                        target: t.target,
                        amount: amount,
                        fromEffect: true,
                        fromSpell: false,
                        healSourceName: t.effectName,
                        t.icon
                    );
                }
            }
        }

        private static int Clamp(int v, int min, int max)
        {
            if (v < min)
                return min;
            if (v > max)
                return max;
            return v;
        }

        private void SetTurnMeter(CombatActorType actor, float value, float threshold)
        {
            var a = State.Get(actor);

            float t = Mathf.Max(1f, threshold);
            float clamped = Mathf.Clamp(value, 0f, t);
            a.turnMeter = clamped;

            Emit(
                new TurnMeterChangedEvent(actor, Mathf.RoundToInt(a.turnMeter), Mathf.RoundToInt(t))
            );
        }

        private const int DEFAULT_TURN_THRESHOLD = 100;

        private const float DefaultItemUsageSpeed = 25f;

        private static float GetThreshold(ResolvedAction action)
        {
            if (action.type == QueuedActionType.Item && action.item != null)
                return Mathf.Max(1, UsageTimeToCastTimeValue(action.item.usageTime));

            var s = action.spell;
            if (s == null)
                return DEFAULT_TURN_THRESHOLD;
            return Mathf.Max(1, s.castTimeValue);
        }

        private static int UsageTimeToCastTimeValue(float usageTime)
        {
            // Items use the same concept as spells: usageTime == castTimeValue threshold.
            // If unset, default to 25.
            if (usageTime <= 0f)
                return Mathf.Max(1, Mathf.RoundToInt(DefaultItemUsageSpeed));
            return Mathf.Max(1, Mathf.RoundToInt(usageTime));
        }

        private float GetActionSpeed(CombatActorState actor, ResolvedAction action)
        {
            if (action.type == QueuedActionType.Item)
                return Mathf.Max(1f, DefaultItemUsageSpeed);

            var queuedSpell = action.spell;
            if (queuedSpell == null)
                return Mathf.Max(1f, DefaultItemUsageSpeed);

            float speed =
                queuedSpell.damageKind == DamageKind.Magical
                    ? actor.derived.castSpeed
                    : actor.derived.attackSpeed;

            if (speed < 1f)
                speed = 1f;

            return speed;
        }

        private void Emit(CombatEvent e) => OnEvent?.Invoke(e);

        private static bool HasType(ResolvedSpell spell, SpellType type)
        {
            if (spell == null)
                return false;

            if (spell.spellTypes == null || spell.spellTypes.Length == 0)
                return false;

            return Array.IndexOf(spell.spellTypes, type) >= 0;
        }

        private sealed class ResolvedItem
        {
            public string itemId;
            public ItemDefinitionSO def;
            public string displayName;
            public Sprite icon;
            public float usageTime;
        }

        private struct ResolvedAction
        {
            public QueuedActionType type;
            public ResolvedSpell spell;
            public ResolvedItem item;
        }

        private ResolvedAction ResolveQueuedAction(CombatActorState actor)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));

            if (State == null)
                throw new InvalidOperationException("CombatEngine.State is null.");

            if (actor.queuedActionType == QueuedActionType.Item)
            {
                return new ResolvedAction
                {
                    type = QueuedActionType.Item,
                    item = ResolveQueuedItem(actor),
                };
            }

            return new ResolvedAction
            {
                type = QueuedActionType.Spell,
                spell = ResolveQueuedSpell(actor),
            };
        }

        private ResolvedItem ResolveQueuedItem(CombatActorState actor)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));

            if (string.IsNullOrWhiteSpace(actor.queuedActionId))
                throw new InvalidOperationException($"{actor.actorType} has no queued item.");

            string itemId = actor.queuedActionId;
            var itemDb = GameConfigProvider.Instance?.ItemDatabase;
            var def = itemDb != null ? itemDb.GetById(itemId) : null;

            string displayName =
                def != null && !string.IsNullOrWhiteSpace(def.displayName)
                    ? def.displayName
                    : itemId;

            return new ResolvedItem
            {
                itemId = itemId,
                def = def,
                displayName = displayName,
                icon = def != null ? def.icon : null,
                usageTime = def != null ? Mathf.Max(0f, def.usageTime) : 0f,
            };
        }

        private ResolvedSpell ResolveQueuedSpell(CombatActorState actor)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));

            if (State == null)
                throw new InvalidOperationException("CombatEngine.State is null.");

            if (string.IsNullOrWhiteSpace(actor.queuedActionId))
                throw new InvalidOperationException($"{actor.actorType} has no queued spell.");

            // PLAYER
            if (actor.actorType == CombatActorType.Player)
            {
                var entry = State.playerSpellbook?.Get(actor.queuedActionId);
                if (entry == null)
                    throw new InvalidOperationException(
                        $"Player queued unknown spell '{actor.queuedActionId}'."
                    );

                if (
                    !_spellResolver.TryResolve(
                        actor.queuedActionId,
                        entry.level,
                        State.player.derived,
                        out var resolvedPlayer
                    )
                )
                    throw new InvalidOperationException(
                        $"Could not resolve player spell '{actor.queuedActionId}'."
                    );

                return resolvedPlayer;
            }

            // ENEMY
            var enemyBook = State.enemySpellbook;
            EnemySpellState enemySpellState = null;

            if (enemyBook != null && enemyBook.spells != null)
                enemySpellState = enemyBook.spells.Find(x => x.spellId == actor.queuedActionId);

            if (enemySpellState != null)
            {
                if (
                    !_spellResolver.TryResolve(
                        enemySpellState.spellId,
                        enemySpellState.level,
                        State.enemy.derived,
                        out var resolvedEnemy
                    )
                )
                    throw new InvalidOperationException(
                        $"Could not resolve enemy spell '{enemySpellState.spellId}' (Lv {enemySpellState.level})."
                    );

                return resolvedEnemy;
            }

            // FALLBACK
            return new ResolvedSpell(
                spellId: "enemy_attack",
                displayName: $"{State.enemy.displayName} Attack",
                manaCost: 0,
                cooldownTurns: 0,
                damage: 5,
                damageKind: DamageKind.Physical,
                damageRangeType: DamageRangeType.Melee,
                ignoreDefenseFlat: 0,
                ignoreDefensePercent: 0,
                hitChance: 90,
                baseUseSpeed: 50,
                castTimeValue: 100,
                damageTypes: new[] { DamageType.Slashing },
                onHitEffects: Array.Empty<EffectInstance>(),
                onCastEffects: Array.Empty<EffectInstance>(),
                spellTypes: new[] { SpellType.Damage },
                level: 1,
                icon: null
            );
        }

        private void FireQueuedAction(
            CombatActorType actorType,
            ResolvedAction resolvedQueuedAction
        )
        {
            var attacker = State.Get(actorType);
            var defender = State.GetOpponent(actorType);
            attacker.actionIndex++;
            var modsBeforeTick = attacker.modifiers.Clone();
            TickEffectsForActor(actorType);
            if (State == null || State.isFinished)
                return;

            // extra safety: if attacker is dead, don't cast
            if (attacker.hp <= 0)
                return;

            // (Optional extra safety) if defender died from tick, don't cast
            if (defender.hp <= 0)
                return;

            if (actorType == CombatActorType.Player)
            {
                State.playerSpellbook.TickCooldowns();
                State.playerItemCooldowns?.TickCooldowns();
                if (resolvedQueuedAction.type != QueuedActionType.Item)
                {
                    var resolvedQueuedSpell = resolvedQueuedAction.spell;
                    ChangeMana(CombatActorType.Player, -resolvedQueuedSpell.manaCost);
                    State.playerSpellbook.StartCooldown(
                        resolvedQueuedSpell.spellId,
                        resolvedQueuedSpell.cooldownTurns
                    );
                }
            }
            else
            {
                var resolvedQueuedSpell = resolvedQueuedAction.spell;
                State.enemySpellbook?.TickCooldowns();
                ChangeMana(CombatActorType.Enemy, -resolvedQueuedSpell.manaCost);
                State.enemySpellbook?.StartCooldown(
                    resolvedQueuedSpell.spellId,
                    resolvedQueuedSpell.cooldownTurns
                );
            }

            if (resolvedQueuedAction.type == QueuedActionType.Item)
            {
                var resolvedItem = resolvedQueuedAction.item;
                string itemId = resolvedItem != null ? resolvedItem.itemId : null;
                var itemDef = resolvedItem != null ? resolvedItem.def : null;

                // Spell scrolls: cast the referenced spell (no spellbook cooldown) using item cooldown.
                bool isScroll =
                    itemDef != null
                    && (
                        itemDef.itemType == ItemDefinitionType.SpellScroll
                        || (
                            itemDef.scrollData != null
                            && !string.IsNullOrWhiteSpace(itemDef.scrollData.spellId)
                        )
                    );
                if (isScroll)
                {
                    var sd = itemDef.scrollData;
                    if (sd == null || string.IsNullOrWhiteSpace(sd.spellId))
                    {
                        Emit(new CombatLogEvent($"Scroll '{itemId}' is missing scrollData."));
                        Emit(new SpellFiredEvent(actorType));
                        return;
                    }

                    int scrollLevel = Mathf.Max(1, sd.spellLevel);

                    if (
                        !_spellResolver.TryResolve(
                            sd.spellId,
                            scrollLevel,
                            State.player.derived,
                            out var resolvedScroll
                        )
                    )
                    {
                        Emit(
                            new CombatLogEvent(
                                $"Scroll '{itemDef.displayName}' failed to resolve spell '{sd.spellId}'."
                            )
                        );
                        Emit(new SpellFiredEvent(actorType));
                        return;
                    }

                    if (sd.usesPlayerMana && State.player.mana < resolvedScroll.manaCost)
                    {
                        Emit(
                            new CombatLogEvent(
                                $"Not enough mana for {resolvedScroll.displayName} (needs {resolvedScroll.manaCost})."
                            )
                        );
                        Emit(new SpellFiredEvent(actorType));
                        return;
                    }

                    // Consume 1 scroll stack.
                    bool removedScrollStack =
                        RunSession.Items != null && RunSession.Items.Remove(itemId, 1);
                    if (!removedScrollStack)
                    {
                        Emit(
                            new CombatLogEvent(
                                $"Tried to use '{itemId}' but no stack was available."
                            )
                        );
                        Emit(new SpellFiredEvent(actorType));
                        return;
                    }

                    // If this was the last stack, remove it from all active combat slots.
                    if (RunSession.Items == null || RunSession.Items.GetCount(itemId) <= 0)
                    {
                        var save = SaveSession.Current;
                        if (save != null)
                            ActiveCombatSlotsCleanup.RemoveItemFromAllActiveCombatSlots(
                                save,
                                itemId
                            );
                    }

                    if (SaveSession.HasSave && SaveSession.Current != null)
                        SaveSessionRuntimeSave.SaveNowWithRuntime();

                    int cdTurns = Mathf.Max(0, itemDef.cooldownTurns);
                    if (cdTurns > 0)
                        State.playerItemCooldowns?.StartCooldown(itemId, cdTurns);

                    // Pay mana and cast the scroll spell.
                    if (sd.usesPlayerMana)
                        ChangeMana(CombatActorType.Player, -resolvedScroll.manaCost);

                    ResolveAction(
                        attacker: attacker,
                        defender: defender,
                        source: actorType,
                        target: defender.actorType,
                        spell: resolvedScroll,
                        modifiers: modsBeforeTick,
                        grantSpellXp: false
                    );

                    Emit(new SpellFiredEvent(actorType));
                    return;
                }

                bool removed = RunSession.Items != null && RunSession.Items.Remove(itemId, 1);
                if (removed)
                {
                    // If this was the last stack, remove it from all active combat slots.
                    if (RunSession.Items == null || RunSession.Items.GetCount(itemId) <= 0)
                    {
                        var save = SaveSession.Current;
                        if (save != null)
                        {
                            ActiveCombatSlotsCleanup.RemoveItemFromAllActiveCombatSlots(
                                save,
                                itemId
                            );
                        }
                    }

                    if (SaveSession.HasSave && SaveSession.Current != null)
                        SaveSessionRuntimeSave.SaveNowWithRuntime();

                    int cdTurns = itemDef != null ? Mathf.Max(0, itemDef.cooldownTurns) : 0;
                    if (cdTurns > 0)
                        State.playerItemCooldowns?.StartCooldown(itemId, cdTurns);
                }
                else
                {
                    Emit(
                        new CombatLogEvent($"Tried to use '{itemId}' but no stack was available.")
                    );
                }

                Emit(
                    new CombatLogEvent(
                        $"{attacker.displayName} uses {resolvedQueuedAction.item?.displayName ?? itemId}."
                    )
                );
                Emit(new SpellFiredEvent(actorType));
                return;
            }

            ResolveAction(
                attacker: attacker,
                defender: defender,
                source: actorType,
                target: defender.actorType,
                spell: resolvedQueuedAction.spell,
                modifiers: modsBeforeTick
            );

            Emit(new SpellFiredEvent(actorType));
        }

        public bool TryUseActiveCombatItemSlot(int slotIndex)
        {
            if (State == null || State.isFinished)
                return false;
            if (!State.waitingForPlayerInput)
                return false;
            if (!SaveSession.HasSave || SaveSession.Current == null)
                return false;

            var save = SaveSession.Current;
            save.activeCombatSlots ??= new List<SavedCombatActiveSlotEntry>();
            while (save.activeCombatSlots.Count < 4)
                save.activeCombatSlots.Add(new SavedCombatActiveSlotEntry());

            if (slotIndex < 0 || slotIndex >= 4)
                return false;

            var entry = save.activeCombatSlots[slotIndex];
            if (entry == null)
                return false;

            if (!string.IsNullOrWhiteSpace(entry.itemId))
            {
                var items = RunSession.Items;
                if (items == null || items.GetCount(entry.itemId) <= 0)
                {
                    Emit(new CombatLogEvent($"You don't have '{entry.itemId}'."));
                    return false;
                }

                int remainingCd =
                    State.playerItemCooldowns != null
                        ? State.playerItemCooldowns.GetRemaining(entry.itemId)
                        : 0;
                if (remainingCd > 0)
                {
                    Emit(new CombatLogEvent($"Item is on cooldown ({remainingCd} turns)."));
                    return false;
                }

                var itemDb = GameConfigProvider.Instance?.ItemDatabase;
                var def = itemDb != null ? itemDb.GetById(entry.itemId) : null;
                if (def != null && !def.usableInCombat)
                {
                    Emit(new CombatLogEvent($"'{def.displayName}' cannot be used in combat."));
                    return false;
                }

                // Scrolls use mana and must be queueable like spells.
                bool isScroll =
                    def != null
                    && (
                        def.itemType == ItemDefinitionType.SpellScroll
                        || (
                            def.scrollData != null
                            && !string.IsNullOrWhiteSpace(def.scrollData.spellId)
                        )
                    );
                if (isScroll)
                {
                    var sd = def.scrollData;
                    if (sd == null || string.IsNullOrWhiteSpace(sd.spellId))
                    {
                        Emit(
                            new CombatLogEvent($"Scroll '{def.displayName}' is missing scrollData.")
                        );
                        return false;
                    }

                    if (sd.usesPlayerMana)
                    {
                        int scrollLevel = Mathf.Max(1, sd.spellLevel);

                        if (
                            !_spellResolver.TryResolve(
                                sd.spellId,
                                scrollLevel,
                                State.player.derived,
                                out var resolvedScroll
                            )
                        )
                        {
                            Emit(
                                new CombatLogEvent(
                                    $"Scroll '{def.displayName}' failed to resolve spell '{sd.spellId}'."
                                )
                            );
                            return false;
                        }

                        if (State.player.mana < resolvedScroll.manaCost)
                        {
                            Emit(
                                new CombatLogEvent(
                                    $"Not enough mana for {resolvedScroll.displayName} (needs {resolvedScroll.manaCost})."
                                )
                            );
                            return false;
                        }
                    }
                }

                State.player.queuedActionId = entry.itemId;
                State.player.queuedActionType = QueuedActionType.Item;

                string name =
                    def != null && !string.IsNullOrWhiteSpace(def.displayName)
                        ? def.displayName
                        : entry.itemId;

                Emit(
                    new ItemQueuedEvent(
                        CombatActorType.Player,
                        State.player.displayName,
                        entry.itemId
                    )
                );
                State.waitingForPlayerInput = false;

                if (!State.isFinished)
                    AdvanceUntilPlayerInputOrEnd();

                return true;
            }

            if (!string.IsNullOrWhiteSpace(entry.equipmentInstanceId))
            {
                Emit(new CombatLogEvent("Equipment use is not supported yet."));
                return false;
            }

            Emit(new CombatLogEvent($"Combat item slot {slotIndex + 1} is empty."));
            return false;
        }

        public bool TryGetPlayerItemCooldown(
            string itemId,
            out int remainingTurns,
            out int maxTurns
        )
        {
            remainingTurns = 0;
            maxTurns = 0;

            if (State == null || State.isFinished)
                return false;

            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            remainingTurns =
                State.playerItemCooldowns != null
                    ? Mathf.Max(0, State.playerItemCooldowns.GetRemaining(itemId))
                    : 0;

            var itemDb = GameConfigProvider.Instance?.ItemDatabase;
            var def = itemDb != null ? itemDb.GetById(itemId) : null;
            maxTurns = def != null ? Mathf.Max(1, def.cooldownTurns) : Mathf.Max(1, remainingTurns);

            if (remainingTurns > maxTurns)
                maxTurns = remainingTurns;

            return true;
        }

        private void AfterActorFired(CombatActorType actorType)
        {
            var a = State.Get(actorType);

            // Consume 100 meter
            SetTurnMeter(actorType, 0, DEFAULT_TURN_THRESHOLD);

            // Clear queued action (it was used)
            a.queuedActionId = null;
            a.queuedActionType = QueuedActionType.None;

            if (State.isFinished)
                return;

            if (actorType == CombatActorType.Enemy)
            {
                State.waitingForEnemyDecision = true;
                Emit(new EnemyDecisionRequestedEvent(State.enemy.displayName));
            }
            else
            {
                State.waitingForPlayerInput = true;
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

            if (
                !_spellResolver.TryResolve(
                    spellId,
                    entry.level,
                    State.player.derived,
                    out var resolved
                )
            )
            {
                maxTurns = Mathf.Max(1, remainingTurns);
                return true;
            }

            maxTurns = Mathf.Max(1, resolved.cooldownTurns);

            if (remainingTurns > maxTurns)
                maxTurns = remainingTurns;

            return true;
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

        private void LogAllStatModifiers(CombatActorType actorType, string header = null)
        {
            if (State == null)
                return;

            var a = State.Get(actorType);
            if (a == null)
                return;

            var m = a.modifiers;
            if (m == null)
            {
                Emit(new CombatLogEvent($"{a.displayName} has no StatModifiers object."));
                return;
            }

            // Helper for consistent formatting
            static string F(string name, int v) => $"{name}: {v}";
            static string P(string name, float v) => $"{name}: {v:0.###}";
            // static string JoinLines(List<string> lines)
            // {
            //     if (lines == null || lines.Count == 0)
            //         return "";
            //     return string.Join("\n", lines);
            // }

            var lines = new List<string>(128);

            // Title
            lines.Add("==============================================================");
            lines.Add($"STAT MODIFIERS DUMP: {a.displayName} ({actorType})");
            if (!string.IsNullOrWhiteSpace(header))
                lines.Add(header);
            lines.Add("--------------------------------------------------------------");

            // ---------
            // Flat fields (based on what your mapping uses)
            // ---------
            lines.Add("[FLATS]");

            lines.Add(F("damageFlat", m.damageFlat));
            lines.Add(F("attackDamageFlat", m.attackDamageFlat));
            lines.Add(F("magicDamageFlat", m.magicDamageFlat));

            // ---------
            // Final multiplier properties (you referenced these in old system)
            // ---------
            lines.Add("--------------------------------------------------------------");
            lines.Add("[FINAL MULTIPLIERS]");

            // ---------
            // If you have “mult containers” (spellBaseMult etc.), dump them
            // (This assumes your mult container has readable ToString or fields.
            // If not, we’ll adjust to your exact class.)
            // ---------
            lines.Add("--------------------------------------------------------------");
            lines.Add("[MULT CONTAINERS]");
            lines.Add($"spellBaseMult: {m.spellBaseMult}");
            lines.Add($"physicalSpellBaseMult: {m.physicalSpellBaseMult}");
            lines.Add($"magicSpellBaseMult: {m.magicSpellBaseMult}");

            // ---------
            // Type-based tables (AttackerBonus / DefenderVuln / Resist / Weaken)
            // We can only dump these if you expose them from StatModifiers.
            // Common patterns:
            //  - Dictionary<DamageType, int> attackerBonusFlat
            //  - Dictionary<DamageType, MultBucket> attackerBonusMore
            //
            // If your StatModifiers has getters like GetAttackerBonusFlat(type) etc.,
            // we can iterate over all DamageType enum values.
            // ---------
            lines.Add("--------------------------------------------------------------");
            lines.Add("[TYPE-BASED MODIFIERS]");
            try
            {
                var allTypes = (DamageType[])Enum.GetValues(typeof(DamageType));
                for (int i = 0; i < allTypes.Length; i++)
                {
                    var t = allTypes[i];

                    // These methods may or may not exist in your StatModifiers.
                    // If they don't, tell me your StatModifiers code and I’ll adapt.
                    int atkBonusFlat = m.GetAttackerBonusFlat(t);
                    float atkBonusMore = m.GetAttackerBonusMult(t);

                    int defVulnFlat = m.GetDefenderVulnFlat(t);
                    float defVulnMore = m.GetDefenderVulnMult(t);

                    int defResistFlat = m.GetDefenderResistFlat(t);
                    float defResistLess = m.GetDefenderResistMult(t);

                    int atkWeakenFlat = m.GetAttackerWeakenFlat(t);
                    float atkWeakenLess = m.GetAttackerWeakenMult(t);

                    bool any =
                        atkBonusFlat != 0
                        || Math.Abs(atkBonusMore) > 0.0001f
                        || defVulnFlat != 0
                        || Math.Abs(defVulnMore) > 0.0001f
                        || defResistFlat != 0
                        || Math.Abs(defResistLess) > 0.0001f
                        || atkWeakenFlat != 0
                        || Math.Abs(atkWeakenLess) > 0.0001f;

                    if (!any)
                        continue;

                    lines.Add($"- {t}:");
                    lines.Add($"    AttackerBonus: flat {atkBonusFlat}, more {atkBonusMore:0.###}");
                    lines.Add($"    DefenderVuln:  flat {defVulnFlat}, more {defVulnMore:0.###}");
                    lines.Add(
                        $"    DefenderResist:flat {defResistFlat}, less {defResistLess:0.###}"
                    );
                    lines.Add(
                        $"    AttackerWeaken:flat {atkWeakenFlat}, less {atkWeakenLess:0.###}"
                    );
                }
            }
            catch (Exception ex)
            {
                // If your StatModifiers doesn't expose these getters yet, we still want the dump.
                lines.Add($"(Type-based dump skipped: {ex.GetType().Name} - {ex.Message})");
                lines.Add(
                    "If you paste StatModifiers, I will wire this dump to your exact fields."
                );
            }

            lines.Add("==============================================================");

            // Emit as multiple log lines (so your UI log doesn't get one giant label)
            for (int i = 0; i < lines.Count; i++)
                Emit(new CombatLogEvent(lines[i]));
        }

        private void DebugLogAllStatModifiers(CombatActorType actorType, string header = null)
        {
            if (State == null)
            {
                Debug.Log("[StatDump] CombatEngine.State is null.");
                return;
            }

            var a = State.Get(actorType);
            if (a == null)
            {
                Debug.Log($"[StatDump] Actor not found for {actorType}.");
                return;
            }

            var m = a.modifiers;
            if (m == null)
            {
                Debug.Log($"[StatDump] {a.displayName} has no StatModifiers object.");
                return;
            }

            // Local helpers
            static string F(string name, int v) => $"{name, -24}: {v}";
            static string PF(string name, float v) => $"{name, -24}: {v:0.###}";

            var sb = new System.Text.StringBuilder(2048);

            sb.AppendLine("==============================================================");
            sb.AppendLine($"STAT MODIFIERS DUMP: {a.displayName} ({actorType})");
            if (!string.IsNullOrWhiteSpace(header))
                sb.AppendLine(header);
            sb.AppendLine("--------------------------------------------------------------");

            // -------------------------
            // FLATS (based on your ApplyModifierDelta mappings)
            // -------------------------
            sb.AppendLine("[FLATS]");

            sb.AppendLine(F("damageFlat", m.damageFlat));
            sb.AppendLine(F("attackDamageFlat", m.attackDamageFlat));
            sb.AppendLine(F("magicDamageFlat", m.magicDamageFlat));

            sb.AppendLine("--------------------------------------------------------------");

            // -------------------------
            // FINAL MULTIPLIERS (you referenced these earlier)
            // -------------------------
            sb.AppendLine("[FINAL MULTIPLIERS]");

            sb.AppendLine("--------------------------------------------------------------");

            // -------------------------
            // MULT CONTAINERS (ToString() dump; if ugly, we’ll adapt to your type)
            // -------------------------
            sb.AppendLine("[MULT CONTAINERS]");
            sb.AppendLine($"spellBaseMult            : {m.spellBaseMult}");
            sb.AppendLine($"physicalSpellBaseMult    : {m.physicalSpellBaseMult}");
            sb.AppendLine($"magicSpellBaseMult       : {m.magicSpellBaseMult}");

            sb.AppendLine("--------------------------------------------------------------");

            // -------------------------
            // TYPE-BASED MODIFIERS
            // This requires getter methods on StatModifiers.
            // If you don't have them, this section will say "skipped".
            // -------------------------
            sb.AppendLine("[TYPE-BASED MODIFIERS]");
            try
            {
                var allTypes = (DamageType[])Enum.GetValues(typeof(DamageType));
                bool printedAny = false;

                for (int i = 0; i < allTypes.Length; i++)
                {
                    var t = allTypes[i];

                    // These methods must exist on StatModifiers for this to work:
                    int atkBonusFlat = m.GetAttackerBonusFlat(t);
                    float atkBonusMore = m.GetAttackerBonusMult(t);

                    int defVulnFlat = m.GetDefenderVulnFlat(t);
                    float defVulnMore = m.GetDefenderVulnMult(t);

                    int defResistFlat = m.GetDefenderResistFlat(t);
                    float defResistLess = m.GetDefenderResistMult(t);

                    int atkWeakenFlat = m.GetAttackerWeakenFlat(t);
                    float atkWeakenLess = m.GetAttackerWeakenMult(t);

                    bool any =
                        atkBonusFlat != 0
                        || Math.Abs(atkBonusMore) > 0.0001f
                        || defVulnFlat != 0
                        || Math.Abs(defVulnMore) > 0.0001f
                        || defResistFlat != 0
                        || Math.Abs(defResistLess) > 0.0001f
                        || atkWeakenFlat != 0
                        || Math.Abs(atkWeakenLess) > 0.0001f;

                    if (!any)
                        continue;

                    printedAny = true;
                    sb.AppendLine($"- {t}:");
                    sb.AppendLine(
                        $"    AttackerBonus : flat {atkBonusFlat}, more {atkBonusMore:0.###}"
                    );
                    sb.AppendLine(
                        $"    DefenderVuln  : flat {defVulnFlat}, more {defVulnMore:0.###}"
                    );
                    sb.AppendLine(
                        $"    DefenderResist: flat {defResistFlat}, less {defResistLess:0.###}"
                    );
                    sb.AppendLine(
                        $"    AttackerWeaken: flat {atkWeakenFlat}, less {atkWeakenLess:0.###}"
                    );
                }

                if (!printedAny)
                    sb.AppendLine("(none)");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"(Type-based dump skipped: {ex.GetType().Name} - {ex.Message})");
                sb.AppendLine("Paste StatModifiers and I’ll wire this to your exact structure.");
            }

            sb.AppendLine("==============================================================");

            Debug.Log(sb.ToString());
        }
    }
}
