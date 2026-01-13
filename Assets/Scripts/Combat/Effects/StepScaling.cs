using System;
using UnityEngine;

public enum StepScalingMode
{
    Add, // base + steps * amountPerStep
    Multiply, // base * (1 + steps * amountPerStepPercent/100)
}

[Serializable]
public class StepScaling
{
    public StepScalingMode mode = StepScalingMode.Add;

    [Min(1)]
    public int startAtLevel = 1;

    [Min(1)]
    public int stepLevels = 5;

    // Add: direct value added each step
    // Multiply: percent per step (e.g. 10 means +10% per step)
    public int amountPerStep = 0;

    public int Apply(int baseValue, int spellLevel)
    {
        int lvl = Mathf.Max(1, spellLevel);
        int steps = (lvl - Mathf.Max(1, startAtLevel)) / Mathf.Max(1, stepLevels);
        steps = Mathf.Max(0, steps);

        return mode switch
        {
            StepScalingMode.Add => baseValue + steps * amountPerStep,

            StepScalingMode.Multiply => Mathf.RoundToInt(
                baseValue * (1f + steps * (amountPerStep / 100f))
            ),

            _ => baseValue,
        };
    }
}
