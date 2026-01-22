using System;

public sealed class UiEventHub
{
    public event Action CharacterChanged;
    public event Action SpellsChanged;
    public event Action InventoryChanged;
    public event Action TalentsChanged;

    public void RaiseCharacterChanged() => CharacterChanged?.Invoke();

    public void RaiseSpellsChanged() => SpellsChanged?.Invoke();

    public void RaiseInventoryChanged() => InventoryChanged?.Invoke();

    public void RaiseTalentsChanged() => TalentsChanged?.Invoke();
}
