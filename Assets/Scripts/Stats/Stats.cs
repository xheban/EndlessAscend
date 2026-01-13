using UnityEngine;

[System.Serializable]
public struct Stats
{
    public int agility;
    public int intelligence;
    public int strength;
    public int spirit;
    public int endurance;

    public static Stats operator +(Stats a, Stats b)
    {
        return new Stats
        {
            agility = a.agility + b.agility,
            intelligence = a.intelligence + b.intelligence,
            strength = a.strength + b.strength,
            spirit = a.spirit + b.spirit,
            endurance = a.endurance + b.endurance,
        };
    }
}
