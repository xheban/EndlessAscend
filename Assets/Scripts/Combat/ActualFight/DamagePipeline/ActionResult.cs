using System.Collections.Generic;

namespace MyGame.Combat
{
    /// <summary>
    /// Final outcome of resolving one action.
    /// </summary>
    public sealed class ActionResult
    {
        public bool hit;

        public int damage;

        public List<EffectResult> effects = new();
    }

    public sealed class EffectResult
    {
        public string effectId;

        public bool attempted;
        public bool applied;
        public bool resisted;

        public float magnitude;
    }
}
