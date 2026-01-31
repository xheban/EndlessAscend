using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ActionModalListener : MonoBehaviour
{
    [SerializeField]
    private ScreenSwapper swapper;

    private void OnEnable()
    {
        MyGame.Progression.PlayerLevelUp.PlayerLeveledUp += OnPlayerLeveledUp;
    }

    private void OnDisable()
    {
        MyGame.Progression.PlayerLevelUp.PlayerLeveledUp -= OnPlayerLeveledUp;
    }

    private void OnPlayerLeveledUp(int gained, System.Collections.Generic.List<string> unlockedIds)
    {
        if (swapper == null)
            return;

        string unlockLine = null;
        if (unlockedIds != null && unlockedIds.Count > 0)
        {
            var db = MyGame.Run.GameConfigProvider.Instance?.UnlockDatabase;
            if (db != null)
            {
                var names = new System.Collections.Generic.List<string>(unlockedIds.Count);
                for (int i = 0; i < unlockedIds.Count; i++)
                {
                    var id = unlockedIds[i];
                    if (string.IsNullOrWhiteSpace(id))
                        continue;
                    names.Add(db.GetDisplayName(id));
                }
                if (names.Count > 0)
                    unlockLine = "Unlocked: " + JoinNames(names);
            }
            else
            {
                unlockLine = "Unlocked: " + JoinNames(unlockedIds);
            }
        }

        string message = $"You gained {gained} level{(gained == 1 ? "" : "s")}!";

        var modal = swapper.ShowGlobalModal(
            title: "Level Up!",
            message: message,
            primaryText: "OK",
            onPrimary: () => { },
            closeOnOutsideClick: true,
            centerMessage: true
        );

        if (!string.IsNullOrWhiteSpace(unlockLine) && modal != null)
        {
            var unlockLabel = new Label(unlockLine);
            unlockLabel.AddToClassList("label-md");
            unlockLabel.style.marginTop = 16;
            unlockLabel.style.whiteSpace = WhiteSpace.Normal;
            unlockLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            modal.SetCustomContent(unlockLabel, replace: true);
        }
    }

    private static string JoinNames(System.Collections.Generic.List<string> items)
    {
        if (items == null || items.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < items.Count; i++)
        {
            var s = items[i];
            if (string.IsNullOrWhiteSpace(s))
                continue;

            if (sb.Length > 0)
                sb.Append(", ");
            sb.Append(s);
        }
        return sb.ToString();
    }
}
