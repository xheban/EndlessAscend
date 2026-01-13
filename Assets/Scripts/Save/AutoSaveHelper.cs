using System;
using MyGame.Save;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Coordinates periodic autosaves for the currently loaded SaveSession:
/// - Toggles the UI Toolkit autosave icon animation (USS class)
/// - Flushes play time into SaveSession.Current.totalPlayTimeSeconds
/// - Writes SaveSession.Current to the active slot via SaveSession.SaveNow()
///
/// Desktop-friendly defaults:
/// - Autosave every N seconds (set 0 to disable)
/// - Always save on quit
/// - Optionally save on pause (can trigger on focus changes on desktop)
/// </summary>
public sealed class AutoSaveHelper : MonoBehaviour
{
    [Header("Saving")]
    [SerializeField]
    private PlayTimeTracker playTimeTracker;

    [Tooltip("Set to 0 to disable periodic autosave and only save on quit/pause.")]
    [SerializeField]
    private float autosaveIntervalSeconds = 120f;

    [Header("Autosave Icon UX")]
    [Tooltip(
        "Minimum time the autosave icon stays animating (prevents flicker when saves are instant)."
    )]
    [SerializeField]
    private float minIconVisibleSeconds = 1.0f;

    private VisualElement _autosaveIcon;
    private VisualElement _quillIcon;

    private float _timer;

    private bool _isSavingUI;
    private float _iconShownAt = -1f;

    private const string AutosaveIconName = "autosave";
    private const string AppRootName = "app-root";
    private const string AutosaveActiveClass = "autosave--active";
    private const string VisibleClass = "autosave-visible";
    private const string HiddenClass = "autosave-hidden";
    private const string StopSavingInvokeName = nameof(StopSavingUI);
    private IVisualElementScheduledItem _quillLoop;
    private bool _quillRight;

    private void Awake()
    {
        // Try immediately.
        TryBindAutosaveIcon();
    }

    private void Start()
    {
        // If UI wasn't ready in Awake (rare), try again once in Start.
        if (_autosaveIcon == null)
            TryBindAutosaveIcon();
    }

    private void OnDisable()
    {
        CancelInvoke(StopSavingInvokeName);
        StopQuillLoop();
    }

    private void Update()
    {
        // If the UI document got recreated (scene change) and we lost refs, try rebinding.
        if (_autosaveIcon == null)
            TryBindAutosaveIcon();

        if (!SaveSession.HasSave)
            return;

        if (autosaveIntervalSeconds <= 0f)
            return;

        _timer += Time.unscaledDeltaTime;

        if (_timer >= autosaveIntervalSeconds)
        {
            AutoSave();
            _timer = 0f;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            AutoSave();
    }

    private void OnApplicationQuit()
    {
        AutoSave();
    }

    /// <summary>
    /// Finds UIDocument -> #app-root -> #autosave (by UXML "name="...""), and caches the icon.
    /// </summary>
    private void TryBindAutosaveIcon()
    {
        // Unity 6+ correct API
        var doc = FindUIDocumentByGameObjectName("Root");
        if (doc == null)
        {
            Debug.LogWarning("AppRoot UIDocument not found.");
            return;
        }

        var root = doc.rootVisualElement;
        if (root == null)
            return;

        // Find the container, then the icon inside it
        var appRoot = root.Q<VisualElement>(AppRootName);
        _autosaveIcon =
            (appRoot != null)
                ? appRoot.Q<VisualElement>(AutosaveIconName)
                : root.Q<VisualElement>(AutosaveIconName); // fallback if autosave is directly under root
        if (_autosaveIcon == null)
        {
            Debug.LogWarning("[AutoSaveHelper] autosave element not found.");
            return;
        }

        _quillIcon = _autosaveIcon.Q<VisualElement>("quill");
        if (_quillIcon == null)
            Debug.LogWarning("[AutoSaveHelper] quill element not found under autosave.");
        _autosaveIcon.AddToClassList(HiddenClass);
        _autosaveIcon.RemoveFromClassList(VisibleClass);
    }

    private void AutoSave()
    {
        if (!SaveSession.HasSave)
            return;

        StartSavingUI();

        playTimeTracker?.Flush();
        SaveSessionRuntimeSave.SaveNowWithRuntime();

        StopSavingUIWithMinimumDuration();
    }

    private void StartSavingUI()
    {
        if (_autosaveIcon == null)
            return;

        _isSavingUI = true;
        StartQuillLoop();
        _iconShownAt = Time.unscaledTime;

        CancelInvoke(StopSavingInvokeName);

        _autosaveIcon.RemoveFromClassList(HiddenClass);
        _autosaveIcon.AddToClassList(VisibleClass);
    }

    private void StopSavingUIWithMinimumDuration()
    {
        if (_autosaveIcon == null || !_isSavingUI)
            return;

        float elapsed = Time.unscaledTime - _iconShownAt;

        if (elapsed >= minIconVisibleSeconds)
        {
            StopSavingUI();
        }
        else
        {
            float remaining = minIconVisibleSeconds - elapsed;
            Invoke(StopSavingInvokeName, remaining);
        }
    }

    private void StopSavingUI()
    {
        if (_autosaveIcon == null)
            return;

        _autosaveIcon.RemoveFromClassList(VisibleClass);
        _autosaveIcon.AddToClassList(HiddenClass);
        _isSavingUI = false;
        StopQuillLoop();
    }

    private void StartQuillLoop()
    {
        if (_autosaveIcon == null || _quillLoop != null)
            return;

        _quillRight = false;

        _quillLoop = _autosaveIcon
            .schedule.Execute(() =>
            {
                _quillRight = !_quillRight;
                _autosaveIcon.EnableInClassList("quill-right", _quillRight);
            })
            .Every(1000); // ms — tweak 350–500 for feel
    }

    private void StopQuillLoop()
    {
        if (_quillLoop == null)
            return;

        _quillLoop.Pause();
        _quillLoop = null;

        // Reset to resting position (left)
        _autosaveIcon.EnableInClassList("quill-right", false);
    }

    private static UIDocument FindUIDocumentByGameObjectName(string goName)
    {
        var docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);

        foreach (var doc in docs)
        {
            if (doc.gameObject.name == goName)
                return doc;
        }

        return null;
    }
}
