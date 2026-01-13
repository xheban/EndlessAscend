using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LevelMilestone
{
    [Min(1)]
    public int atLevel = 1;
    public int value = 0;
}

[Serializable]
public class MilestoneScaling
{
    public List<LevelMilestone> milestones = new();

    public int Evaluate(int spellLevel, int defaultValue)
    {
        if (milestones == null || milestones.Count == 0)
            return defaultValue;

        int lvl = Mathf.Max(1, spellLevel);

        int best = defaultValue;
        int bestAt = int.MinValue;

        for (int i = 0; i < milestones.Count; i++)
        {
            var m = milestones[i];
            if (m.atLevel <= lvl && m.atLevel >= bestAt)
            {
                bestAt = m.atLevel;
                best = m.value;
            }
        }

        return best;
    }
}
