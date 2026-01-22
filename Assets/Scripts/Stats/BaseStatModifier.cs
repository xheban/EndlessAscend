using System;
using UnityEngine;

namespace MyGame.Common
{
    public enum BaseStatType
    {
        Strength,
        Agility,
        Intelligence,
        Spirit,
        Endurance,
    }

    public enum ModOp
    {
        Flat, // +5 INT
        Percent, // +10% INT
    }

    [Serializable]
    public struct BaseStatModifier
    {
        public BaseStatType stat;
        public ModOp op;

        [Tooltip("Flat = whole number (ex: 5). Percent = percent (ex: 10 means +10%).")]
        public float value;
    }

    public enum DerivedStatType
    {
        MaxHp,
        MaxMana,
        AttackPower,
        MagicPower,
        PhysicalDefense,
        MagicalDefense,
        Evasion,
        Accuracy,
        AttackSpeed,
        CastSpeed,
    }

    [Serializable]
    public struct DerivedStatModifier
    {
        public DerivedStatType stat;
        public ModOp op;

        [Tooltip("Flat = whole number. Percent = percent (ex: 3 means +3%).")]
        public float value;
    }
}
