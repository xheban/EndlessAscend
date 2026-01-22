using System;

namespace MyGame.Combat
{
    public static class VitalsChangedBus
    {
        // (currentHp, currentMana)
        public static event Action<int, int> Changed;

        public static void Raise(int hp, int mana)
        {
            Changed?.Invoke(hp, mana);
        }
    }
}
