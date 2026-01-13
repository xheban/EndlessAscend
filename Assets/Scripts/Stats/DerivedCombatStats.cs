using System;

namespace MyGame.Combat
{
    [Serializable]
    public struct DerivedCombatStats
    {
        public int maxHp;
        public int maxMana;

        public int attackPower;
        public int magicPower;

        public int physicalDefense;
        public int magicalDefense;
        public int evasion;

        public int attackSpeed;
        public int castSpeed;
    }
}
