using UnityEngine;

namespace MyGame.Combat
{
    public sealed class FloatingNumberEvent : CombatEvent
    {
        public CombatActorType source;
        public CombatActorType target;

        public int amount; // positive number
        public FloatingNumberKind kind; // Damage / Heal
        public Sprite icon; // icon of spell/effect responsible
        public string label; // optional ("Fireball", "Bleed", etc.)

        public FloatingNumberEvent(
            CombatActorType source,
            CombatActorType target,
            int amount,
            FloatingNumberKind kind,
            Sprite icon,
            string label = null
        )
        {
            this.source = source;
            this.target = target;
            this.amount = amount;
            this.kind = kind;
            this.icon = icon;
            this.label = label;
        }
    }

    public enum FloatingNumberKind
    {
        Damage,
        Heal,
    }
}
