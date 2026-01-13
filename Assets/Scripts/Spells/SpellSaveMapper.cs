using MyGame.Save;

namespace MyGame.Spells
{
    public static class SpellSaveMapper
    {
        /// <summary>
        /// Builds a runtime spellbook from SaveData.
        /// </summary>
        public static PlayerSpellbook LoadFromSave(
            SaveData save,
            SpellDatabase db,
            SpellProgressionConfig progression
        )
        {
            var book = new PlayerSpellbook(db, progression);

            if (save?.spells == null)
                return book;

            foreach (var s in save.spells)
            {
                if (s == null || string.IsNullOrWhiteSpace(s.spellId))
                    continue;

                var entry = new PlayerSpellEntry(s.spellId, s.level)
                {
                    experience = s.experience,
                    cooldownRemainingTurns = s.cooldownRemainingTurns,
                    activeSlotIndex = s.activeSlotIndex,
                };

                book.AddOrReplace(entry);
            }

            return book;
        }

        /// <summary>
        /// Writes runtime spellbook state into SaveData.
        /// </summary>
        public static void WriteToSave(PlayerSpellbook book, SaveData save)
        {
            if (save == null)
                return;

            if (save.spells == null)
                save.spells = new System.Collections.Generic.List<SavedSpellEntry>();
            else
                save.spells.Clear();

            if (book == null)
                return;

            foreach (var kvp in book.Entries)
            {
                var e = kvp.Value;

                save.spells.Add(
                    new SavedSpellEntry
                    {
                        spellId = e.spellId,
                        level = e.level,
                        experience = e.experience,
                        cooldownRemainingTurns = e.cooldownRemainingTurns,
                        activeSlotIndex = e.activeSlotIndex,
                    }
                );
            }
        }
    }
}
