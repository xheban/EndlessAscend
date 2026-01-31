using System;
using System.Collections.Generic;
using MyGame.Common;
using UnityEngine;

[CreateAssetMenu(menuName = "MyGame/Effects/Effect Definition")]
public class EffectDefinition : ScriptableObject
{
    [Header("Identity")]
    public string effectId;
    public string displayName;
    public Sprite icon;

    [TextArea(2, 4)]
    public string description;

    // -------------------------
    // What the effect IS (stable meaning)
    // -------------------------

    [Tooltip("Used for UI/cleanse rules.")]
    public EffectPolarity polarity = EffectPolarity.Debuff;

    // -------------------------
    // Composite components (preferred)
    // -------------------------
    [Header("Components (required)")]
    public List<EffectComponentDefinition> components = new List<EffectComponentDefinition>();

    public int GetComponentCount()
    {
        return components != null ? components.Count : 0;
    }

    public EffectComponentDefinition GetComponent(int index)
    {
        if (components == null || index < 0 || index >= components.Count)
            return null;

        return components[index];
    }
}
