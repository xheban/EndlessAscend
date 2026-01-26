using MyGame.Inventory;
using MyGame.Progression;
using MyGame.Run;
using MyGame.Save;

namespace MyGame.Rewards
{
    public sealed class CombatRewardsPipeline
    {
        private readonly ICombatRewardCalculator _calculator;

        public CombatRewardsPipeline(ICombatRewardCalculator calculator)
        {
            _calculator = calculator;
        }

        public CombatRewardResult GrantVictoryRewards(
            SaveData save,
            MonsterDefinition monster,
            int monsterLevel
        )
        {
            var result = _calculator.Calculate(monster, monsterLevel);

            // Apply to save (adjust field names if yours differ)
            save.exp += result.exp;
            save.gold.Add(result.gold);

            GrantLoot(save, result.loot);
            PlayerLevelUp.ApplyLevelUps(save);
            return result;
        }

        private static void GrantLoot(SaveData save, CombatRewardResult.LootItem[] loot)
        {
            if (loot == null || loot.Length == 0)
                return;

            // Prefer runtime grants so the player sees new drops immediately in-session.
            if (RunSession.IsInitialized)
            {
                for (int i = 0; i < loot.Length; i++)
                {
                    var li = loot[i];
                    if (li == null || string.IsNullOrWhiteSpace(li.lootId))
                        continue;

                    if (li.kind == LootDropKind.Item)
                        RunSession.Items.Add(li.lootId, li.stackCount);
                    else if (li.kind == LootDropKind.Equipment)
                    {
                        // Generates a rolled instance and adds to equipment inventory.
                        li.equipmentInstanceId = InventoryGrantService.GrantEquipmentToInventory(
                            li.lootId,
                            saveNow: false
                        );
                    }
                }

                // Persist runtime (items/equipment) once.
                SaveSessionRuntimeSave.SaveNowWithRuntime();
                return;
            }

            // Fallback: write directly to save if runtime isn't available.
            for (int i = 0; i < loot.Length; i++)
            {
                var li = loot[i];
                if (li == null || string.IsNullOrWhiteSpace(li.lootId))
                    continue;

                if (li.kind == LootDropKind.Item)
                    AddOrIncrementItem(save, li.lootId, li.stackCount);
                else if (li.kind == LootDropKind.Equipment)
                    li.equipmentInstanceId = InventoryGrantService.GrantEquipmentToSave(
                        save,
                        li.lootId
                    );
            }
        }

        private static void AddOrIncrementItem(SaveData data, string itemId, int quantity)
        {
            if (data == null || string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
                return;

            data.items ??= new System.Collections.Generic.List<SavedItemStackEntry>();

            for (int i = 0; i < data.items.Count; i++)
            {
                var e = data.items[i];
                if (e == null)
                    continue;

                if (string.Equals(e.itemId, itemId, System.StringComparison.OrdinalIgnoreCase))
                {
                    e.quantity += quantity;
                    return;
                }
            }

            data.items.Add(new SavedItemStackEntry { itemId = itemId, quantity = quantity });
        }
    }
}
