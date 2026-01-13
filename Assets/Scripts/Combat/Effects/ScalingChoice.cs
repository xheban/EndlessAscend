using System;
using MyGame.Common;
using UnityEngine;

[Serializable]
public class IntScalingChoice
{
    public ScalingType type = ScalingType.None;

    public StepScaling step;
    public MilestoneScaling milestone;

    public bool useMinMax = false;
    public int minValue = 0;
    public int maxValue = 999999;

    public int Evaluate(int spellLevel, int baseValue)
    {
        int v = type switch
        {
            ScalingType.Step => step != null ? step.Apply(baseValue, spellLevel) : baseValue,
            ScalingType.Milestone => milestone != null
                ? milestone.Evaluate(spellLevel, baseValue)
                : baseValue,
            _ => baseValue,
        };

        if (useMinMax)
            v = Mathf.Clamp(v, minValue, maxValue);

        return v;
    }
}
