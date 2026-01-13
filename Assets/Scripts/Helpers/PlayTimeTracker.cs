using MyGame.Save;
using UnityEngine;

public sealed class PlayTimeTracker : MonoBehaviour
{
    private double _sessionStartTime;

    private void OnEnable()
    {
        SaveSession.SessionStarted += OnSessionStarted;
    }

    private void OnDisable()
    {
        SaveSession.SessionStarted -= OnSessionStarted;
    }

    private void OnSessionStarted(SaveData data)
    {
        _sessionStartTime = Time.realtimeSinceStartupAsDouble;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            Flush();
    }

    private void OnApplicationQuit()
    {
        Flush();
    }

    public void Flush()
    {
        if (!SaveSession.HasSave || _sessionStartTime <= 0)
            return;

        double now = Time.realtimeSinceStartupAsDouble;
        int seconds = Mathf.FloorToInt((float)(now - _sessionStartTime));
        if (seconds <= 0)
            return;

        SaveSession.Current.totalPlayTimeSeconds += seconds;
        // Reset start time so multiple flushes don't double-count
        _sessionStartTime = now;
    }
}
