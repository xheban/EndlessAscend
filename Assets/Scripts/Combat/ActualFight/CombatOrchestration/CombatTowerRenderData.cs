using UnityEngine;

public sealed class PlayerPanelRenderData
{
    public string Name;
    public string LevelText;
    public string TierText;
    public string ClassText;
    public string SpecText;

    public Sprite SmallIcon;
    public Sprite BigIcon;
}

public sealed class EnemyPanelRenderData
{
    public string Name;
    public string LevelText;
    public string TierText;
    public string TagsText;

    public Sprite SmallIcon;
    public Sprite BigIcon;
}

public sealed class ActiveSpellSlotData
{
    public string SpellId;
    public int Level;
    public string DisplayName; // already resolved (fallback to id)
    public Sprite Icon; // can be null
}
