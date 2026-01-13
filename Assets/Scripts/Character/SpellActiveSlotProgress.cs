using System;
using UnityEngine;

namespace MyGame.Save
{
    [Serializable]
    public sealed class SpellActiveSlotProgress
    {
        // One-off unlocks (cannot be derived)
        public bool specialQuestChainCompleted;

        // Optional: achievements, events, etc.
        public bool alchemyTinctureDrank;
    }
}
