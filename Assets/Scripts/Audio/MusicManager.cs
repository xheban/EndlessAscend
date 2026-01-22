using UnityEngine;

public sealed class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Assign in Inspector")]
    [SerializeField]
    private AudioSource musicSource;

    [Header("Defaults")]
    [Range(0f, 1f)]
    [SerializeField]
    private float defaultVolume = 0.7f;

    private const string VolumeKey = "MusicVolume";
    private const string MutedKey = "MusicMuted";

    public float Volume => musicSource != null ? musicSource.volume : 0f;
    public bool IsMuted => musicSource != null && musicSource.mute;
    public bool IsPlaying => musicSource != null && musicSource.isPlaying;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (musicSource == null)
            musicSource = GetComponent<AudioSource>();

        // Ensure it loops
        musicSource.loop = true;

        // Load settings (or defaults)
        float vol = PlayerPrefs.GetFloat(VolumeKey, defaultVolume);
        bool muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;

        ApplyVolume(vol);
        SetMuted(muted);

        // Auto-play if a clip is assigned
        if (musicSource.clip != null && !musicSource.isPlaying)
            musicSource.Play();
    }

    public void SetClip(AudioClip clip, bool restart = true)
    {
        if (musicSource == null || clip == null)
            return;

        if (musicSource.clip == clip && !restart)
            return;

        musicSource.clip = clip;
        musicSource.loop = true;

        if (restart)
        {
            musicSource.time = 0f;
            musicSource.Play();
        }
        else if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    public void Play()
    {
        if (musicSource != null && musicSource.clip != null && !musicSource.isPlaying)
            musicSource.Play();
    }

    public void Pause()
    {
        if (musicSource != null && musicSource.isPlaying)
            musicSource.Pause();
    }

    public void TogglePlayPause()
    {
        if (musicSource == null)
            return;
        if (musicSource.isPlaying)
            Pause();
        else
            Play();
    }

    public void ApplyVolume(float value01)
    {
        if (musicSource == null)
            return;

        musicSource.volume = Mathf.Clamp01(value01);
        PlayerPrefs.SetFloat(VolumeKey, musicSource.volume);
        PlayerPrefs.Save();
    }

    public void SetMuted(bool muted)
    {
        if (musicSource == null)
            return;

        musicSource.mute = muted;
        PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void ToggleMute()
    {
        if (musicSource == null)
            return;
        SetMuted(!musicSource.mute);
    }
}
