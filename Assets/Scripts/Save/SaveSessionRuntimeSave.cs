using MyGame.Run;
using MyGame.Spells;
using UnityEngine;

namespace MyGame.Save
{
    /// <summary>
    /// Writes runtime state (RunSession) back into the currently loaded SaveSession
    /// and persists it to disk.
    /// </summary>
    public static class SaveSessionRuntimeSave
    {
        public static void SaveNowWithRuntime()
        {
            if (!SaveSession.HasSave)
            {
                Debug.LogError("SaveSessionRuntimeSave: No active save session.");
                return;
            }

            if (!RunSession.IsInitialized)
            {
                Debug.LogError("SaveSessionRuntimeSave: RunSession is not initialized.");
                return;
            }

            // --- Runtime -> Save mapping ---
            RunSession.ApplyRuntimeToSave(SaveSession.Current);

            // --- Persist ---
            SaveSession.SaveNow();
        }
    }
}
