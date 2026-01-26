using System;
using System.Collections.Generic;
using MyGame.Common;
using UnityEngine;

namespace MyGame.Combat
{
    public static class DerivedStatMap
    {
        public static int Get(in DerivedCombatStats s, DerivedStatType stat) =>
            stat switch
            {
                DerivedStatType.MaxHp => s.maxHp,
                DerivedStatType.MaxMana => s.maxMana,

                DerivedStatType.AttackPower => s.attackPower,
                DerivedStatType.MagicPower => s.magicPower,

                DerivedStatType.PhysicalDefense => s.physicalDefense,
                DerivedStatType.MagicalDefense => s.magicalDefense,

                DerivedStatType.Evasion => s.evasion,
                DerivedStatType.Accuracy => s.accuracy,

                DerivedStatType.AttackSpeed => s.attackSpeed,
                DerivedStatType.CastSpeed => s.castSpeed,

                _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null),
            };

        public static void Set(ref DerivedCombatStats s, DerivedStatType stat, int value)
        {
            switch (stat)
            {
                case DerivedStatType.MaxHp:
                    s.maxHp = value;
                    break;
                case DerivedStatType.MaxMana:
                    s.maxMana = value;
                    break;

                case DerivedStatType.AttackPower:
                    s.attackPower = value;
                    break;
                case DerivedStatType.MagicPower:
                    s.magicPower = value;
                    break;

                case DerivedStatType.PhysicalDefense:
                    s.physicalDefense = value;
                    break;
                case DerivedStatType.MagicalDefense:
                    s.magicalDefense = value;
                    break;

                case DerivedStatType.Evasion:
                    s.evasion = value;
                    break;
                case DerivedStatType.Accuracy:
                    s.accuracy = value;
                    break;

                case DerivedStatType.AttackSpeed:
                    s.attackSpeed = value;
                    break;
                case DerivedStatType.CastSpeed:
                    s.castSpeed = value;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
            }
        }
    }

    public static class DerivedModifierApplier
    {
        public static void ApplyAll(
            ref DerivedCombatStats stats,
            IEnumerable<DerivedStatModifier> mods
        )
        {
            foreach (var m in mods)
            {
                int current = DerivedStatMap.Get(in stats, m.stat);

                int updated = m.op switch
                {
                    ModOp.Flat => current + m.value,
                    ModOp.Percent => ApplyPercent(current, m.value),
                    _ => current,
                };

                // optional: clamp if you want to guarantee non-negative
                if (updated < 0)
                    updated = 0;

                DerivedStatMap.Set(ref stats, m.stat, updated);
            }
        }

        private static int ApplyPercent(int baseValue, int percent)
        {
            // percent = 3 => +3%
            int delta = (baseValue * percent + (percent >= 0 ? 50 : -50)) / 100;
            return baseValue + delta;
        }
    }
}
