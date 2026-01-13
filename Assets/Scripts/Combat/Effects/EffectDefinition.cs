using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Effects/Effect Definition")]
public class EffectDefinition : ScriptableObject
{
    [Header("Identity")]
    public string effectId;
    public string displayName;
    public Sprite icon;

    // Optional later:
    // public string description;
    // public EffectCategory category;
}
