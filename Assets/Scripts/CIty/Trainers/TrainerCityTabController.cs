using System;
using System.Collections.Generic;
using MyGame.Common;
using MyGame.Run;
using MyGame.Spells;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class TrainerCityTabController : ICityTabController
{
    private const string SelectedClass = "picked-button";

    private const string WarriorId = "warrior_trainer";
    private const string MageId = "mage_trainer";
    private const string RangerId = "ranger_trainer";

    private VisualElement _root;

    // Databases (from context)
    private TrainerDatabaseSO _trainerDb;
    private SpellDatabase _spellDb;

    // Trainer buttons
    private Button _warriorBtn;
    private Button _mageBtn;
    private Button _rangerBtn;
    private Button _generalBtn; // does nothing

    // Refresh
    private Button _refreshBtn;

    // Trainer portrait + dialog
    private VisualElement _trainerArt;
    private Label _trainerDialogText;
    private VisualElement _trainerPart;

    // Offer slots (4)
    private VisualElement[] _offerSlots;
    private VisualElement[] _offerIcons;
    private Label[] _offerPriceLabels;

    // Backing data for current offers
    private string[] _offerSpellIds = new string[4];

    // Stored callbacks so we can unregister cleanly
    private EventCallback<ClickEvent>[] _offerSlotClickCallbacks;

    private string _activeTrainerId;

    public void Bind(VisualElement tabRoot, object context)
    {
        _root = tabRoot;
        var cfg = GameConfigProvider.Instance;
        var ctx = context as CityTabContext;
        _trainerDb = ctx?.TrainerDatabase;
        _spellDb = cfg?.SpellDatabase;

        // Buttons
        _warriorBtn = _root?.Q<Button>("WarriorTrainer");
        _mageBtn = _root?.Q<Button>("MageTrainer");
        _rangerBtn = _root?.Q<Button>("RangerTrainer");
        _generalBtn = _root?.Q<Button>("GeneralTrainer"); // no callback

        _refreshBtn = _root?.Q<Button>("Refresh");

        // Trainer portrait + dialog
        _trainerArt = _root?.Q<VisualElement>("TrainerArt");
        _trainerPart = _root?.Q<VisualElement>("TrainerPart");
        _trainerDialogText = _root?.Q<VisualElement>("TrainerDialog")?.Q<Label>("DialogText");

        if (_warriorBtn != null)
            _warriorBtn.clicked += OnWarrior;
        if (_mageBtn != null)
            _mageBtn.clicked += OnMage;
        if (_rangerBtn != null)
            _rangerBtn.clicked += OnRanger;

        if (_refreshBtn != null)
            _refreshBtn.clicked += OnRefresh;

        BindOfferSlots();

        ClearSelection();
    }

    public void OnShow() { }

    public void OnHide() { }

    public void Unbind()
    {
        if (_warriorBtn != null)
            _warriorBtn.clicked -= OnWarrior;
        if (_mageBtn != null)
            _mageBtn.clicked -= OnMage;
        if (_rangerBtn != null)
            _rangerBtn.clicked -= OnRanger;
        if (_refreshBtn != null)
            _refreshBtn.clicked -= OnRefresh;

        UnbindOfferSlots();

        _warriorBtn?.RemoveFromClassList(SelectedClass);
        _mageBtn?.RemoveFromClassList(SelectedClass);
        _rangerBtn?.RemoveFromClassList(SelectedClass);

        _warriorBtn = null;
        _mageBtn = null;
        _rangerBtn = null;
        _generalBtn = null;
        _refreshBtn = null;

        _trainerArt = null;
        _trainerDialogText = null;
        _trainerPart = null;

        _trainerDb = null;
        _spellDb = null;

        _root = null;
        _activeTrainerId = null;
    }

    public void MarkDirty()
    {
        if (!string.IsNullOrWhiteSpace(_activeTrainerId))
            SelectTrainer(_activeTrainerId, refreshOffers: false);
    }

    private void OnWarrior() => SelectTrainer(WarriorId, refreshOffers: true);

    private void OnMage() => SelectTrainer(MageId, refreshOffers: true);

    private void OnRanger() => SelectTrainer(RangerId, refreshOffers: true);

    private void OnRefresh()
    {
        if (string.IsNullOrWhiteSpace(_activeTrainerId))
            return;

        SelectTrainer(_activeTrainerId, refreshOffers: true);
    }

    private void SelectTrainer(string trainerId, bool refreshOffers)
    {
        _activeTrainerId = trainerId;

        // Update button highlight (only these three)
        SetSelected(_warriorBtn, trainerId == WarriorId);
        SetSelected(_mageBtn, trainerId == MageId);
        SetSelected(_rangerBtn, trainerId == RangerId);

        if (_trainerDb == null)
            return;

        var def = _trainerDb.GetById(trainerId);
        if (def == null)
            return;

        ShowTrainerUI();

        SetDialog(
            string.IsNullOrWhiteSpace(def.greetingDialogue)
                ? "Greetings, adventurer."
                : def.greetingDialogue
        );

        SetPortrait(def.portrait);

        if (refreshOffers)
            PopulateOffers(def);
    }

    private void PopulateOffers(TrainerDefinitionSO trainerDef)
    {
        // Clear UI first
        for (int i = 0; i < 4; i++)
        {
            _offerSpellIds[i] = null;
            SetOfferSlot(i, null, 0);
        }

        if (trainerDef == null || _spellDb == null)
            return;

        // Build rarity pools from trainer "teaches"
        var commons = new List<TrainerDefinitionSO.TeachEntry>();
        var uncommons = new List<TrainerDefinitionSO.TeachEntry>();
        var rares = new List<TrainerDefinitionSO.TeachEntry>();

        foreach (var teach in trainerDef.teaches)
        {
            if (teach == null || string.IsNullOrWhiteSpace(teach.spellId))
                continue;

            var spellDef = GetSpellById(teach.spellId);
            if (spellDef == null)
                continue;

            switch (spellDef.rarity)
            {
                case Rarity.Common:
                    commons.Add(teach);
                    break;
                case Rarity.Uncommon:
                    uncommons.Add(teach);
                    break;
                case Rarity.Rare:
                    rares.Add(teach);
                    break;
                default:
                    commons.Add(teach);
                    break;
            }
        }

        // ✅ Separate rolls
        bool addRare = UnityEngine.Random.value < 0.05f; // 5%

        int uncommonCount = 0;
        float uRoll = UnityEngine.Random.value;
        if (uRoll < 0.10f)
            uncommonCount = 2; // 10%
        else if (uRoll < 0.10f + 0.25f)
            uncommonCount = 1; // 25%

        // Pick unique
        var picked = new List<TrainerDefinitionSO.TeachEntry>(4);
        var usedIds = new HashSet<string>();

        TrainerDefinitionSO.TeachEntry TakeUnique(List<TrainerDefinitionSO.TeachEntry> pool)
        {
            if (pool == null || pool.Count == 0)
                return null;

            // Try random a few times
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var c = pool[UnityEngine.Random.Range(0, pool.Count)];
                if (c == null)
                    continue;
                if (string.IsNullOrWhiteSpace(c.spellId))
                    continue;
                if (usedIds.Contains(c.spellId))
                    continue;
                return c;
            }

            // Fallback scan
            for (int i = 0; i < pool.Count; i++)
            {
                var c = pool[i];
                if (c == null)
                    continue;
                if (string.IsNullOrWhiteSpace(c.spellId))
                    continue;
                if (usedIds.Contains(c.spellId))
                    continue;
                return c;
            }

            return null;
        }

        void AddIfPossible(List<TrainerDefinitionSO.TeachEntry> pool)
        {
            if (picked.Count >= 4)
                return;

            var e = TakeUnique(pool);
            if (e == null)
                return;

            picked.Add(e);
            usedIds.Add(e.spellId);
        }

        // 1) Rare (max 1)
        if (addRare)
            AddIfPossible(rares);

        // 2) Uncommons (0/1/2)
        for (int i = 0; i < uncommonCount; i++)
            AddIfPossible(uncommons);

        // 3) Fill rest with Commons ONLY
        while (picked.Count < 4)
        {
            var e = TakeUnique(commons);
            if (e == null)
                break; // not enough commons to fill
            picked.Add(e);
            usedIds.Add(e.spellId);
        }

        // Push to UI
        for (int i = 0; i < 4; i++)
        {
            if (i >= picked.Count)
            {
                _offerSpellIds[i] = null;
                SetOfferSlot(i, null, 0);
                continue;
            }

            var teach = picked[i];
            var spellDef = GetSpellById(teach.spellId);

            _offerSpellIds[i] = teach.spellId;
            SetOfferSlot(i, spellDef?.icon, teach.goldCost);
        }
    }

    private void BindOfferSlots()
    {
        _offerSlots = new VisualElement[4];
        _offerIcons = new VisualElement[4];
        _offerPriceLabels = new Label[4];
        _offerSlotClickCallbacks = new EventCallback<ClickEvent>[4];

        _offerSlots[0] = _root?.Q<VisualElement>("Spell1");
        _offerSlots[1] = _root?.Q<VisualElement>("Spell2");
        _offerSlots[2] = _root?.Q<VisualElement>("Spell3");
        _offerSlots[3] = _root?.Q<VisualElement>("Spell4");

        for (int i = 0; i < 4; i++)
        {
            var slot = _offerSlots[i];
            if (slot == null)
                continue;

            _offerIcons[i] = slot.Q<VisualElement>("SpellFrame")?.Q<VisualElement>("Icon");
            _offerPriceLabels[i] = slot.Q<VisualElement>("Price")?.Q<Label>("Value");

            int index = i;
            _offerSlotClickCallbacks[i] = _ => OnOfferClicked(index);
            slot.RegisterCallback(_offerSlotClickCallbacks[i]);
        }
    }

    private void UnbindOfferSlots()
    {
        if (_offerSlots != null && _offerSlotClickCallbacks != null)
        {
            for (int i = 0; i < 4; i++)
            {
                if (_offerSlots[i] != null && _offerSlotClickCallbacks[i] != null)
                    _offerSlots[i].UnregisterCallback(_offerSlotClickCallbacks[i]);
            }
        }

        _offerSlots = null;
        _offerIcons = null;
        _offerPriceLabels = null;
        _offerSlotClickCallbacks = null;

        for (int i = 0; i < _offerSpellIds.Length; i++)
            _offerSpellIds[i] = null;
    }

    private void OnOfferClicked(int offerIndex)
    {
        var spellId = _offerSpellIds[offerIndex];
        if (string.IsNullOrWhiteSpace(spellId))
            return;

        // This is where you continue:
        // - open "choose one of 4 equip slots" UI
        // - check gold, requirements, etc.
        Debug.Log($"Offer clicked: slot={offerIndex} spellId={spellId}");
    }

    private void SetOfferSlot(int index, Sprite icon, int goldCost)
    {
        if (index < 0 || index >= 4)
            return;

        if (_offerIcons != null && _offerIcons[index] != null)
        {
            _offerIcons[index].style.backgroundImage =
                (icon == null) ? StyleKeyword.None : new StyleBackground(icon);
        }

        if (_offerPriceLabels != null && _offerPriceLabels[index] != null)
            _offerPriceLabels[index].text = goldCost > 0 ? goldCost.ToString() : string.Empty;
    }

    private SpellDefinition GetSpellById(string spellId)
    {
        if (_spellDb == null || string.IsNullOrWhiteSpace(spellId))
            return null;

        // Assumes your SpellDatabaseSO has GetById(string)
        return _spellDb.GetById(spellId);
    }

    private void SetSelected(Button btn, bool selected)
    {
        if (btn == null)
            return;

        if (selected)
            btn.AddToClassList(SelectedClass);
        else
            btn.RemoveFromClassList(SelectedClass);
    }

    private void ClearSelection()
    {
        _activeTrainerId = null;

        _warriorBtn?.RemoveFromClassList(SelectedClass);
        _mageBtn?.RemoveFromClassList(SelectedClass);
        _rangerBtn?.RemoveFromClassList(SelectedClass);

        HideTrainerUI();
        SetDialog(string.Empty);
        SetPortrait(null);

        // clear offer UI
        for (int i = 0; i < 4; i++)
        {
            _offerSpellIds[i] = null;
            SetOfferSlot(i, null, 0);
        }
    }

    private void ShowTrainerUI()
    {
        if (_trainerPart != null)
            _trainerPart.style.display = DisplayStyle.Flex;
    }

    private void HideTrainerUI()
    {
        if (_trainerPart != null)
            _trainerPart.style.display = DisplayStyle.None;
    }

    private void SetDialog(string text)
    {
        if (_trainerDialogText != null)
            _trainerDialogText.text = text ?? string.Empty;
    }

    private void SetPortrait(Sprite sprite)
    {
        if (_trainerArt == null)
            return;

        _trainerArt.style.backgroundImage =
            (sprite == null) ? StyleKeyword.None : new StyleBackground(sprite);
    }
}
