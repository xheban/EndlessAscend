using System;

public enum ArrayBonusType
{
    PerMasteryLevel = 0,
    General = 1,
    MatchingTags = 2,
}

[Serializable]
public sealed class ArrayBonusEntry
{
    public ArrayBonusType bonusType = ArrayBonusType.General;
    public float value = 0f;
}
