using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Background Music")]
    [SerializeField] private AudioClip mainMusic;
    [SerializeField] private AudioClip lobbyMusic;

    [Header("Button Sound")]
    [SerializeField] private AudioClip buttonClickSound;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;


    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Keep SoundManager between scenes
        DontDestroyOnLoad(gameObject);

        // Set volumes
        musicSource.volume = musicVolume;
        sfxSource.volume = sfxVolume;
    }


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        PlayMainMusic();
    }


    // =========================================================
    // MAIN MUSIC
    // =========================================================

    public void PlayMainMusic()
    {
        if (musicSource == null || mainMusic == null)
            return;

        if (musicSource.clip == mainMusic && musicSource.isPlaying)
            return;

        musicSource.clip = mainMusic;
        musicSource.loop = true;
        musicSource.Play();
    }


    // =========================================================
    // LOBBY MUSIC
    // =========================================================

    public void PlayLobbyMusic()
    {
        if (musicSource == null || lobbyMusic == null)
            return;

        if (musicSource.clip == lobbyMusic && musicSource.isPlaying)
            return;

        musicSource.clip = lobbyMusic;
        musicSource.loop = true;
        musicSource.Play();
    }


    // =========================================================
    // BUTTON SOUND
    // =========================================================

    public void PlayButtonSound()
    {
        if (sfxSource == null || buttonClickSound == null)
            return;

        sfxSource.PlayOneShot(buttonClickSound, sfxVolume);
    }


    // =========================================================
    // PLAY CUSTOM SOUND
    // =========================================================

    public void PlaySound(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
            return;

        sfxSource.PlayOneShot(clip, sfxVolume);
    }


    // =========================================================
    // STOP MUSIC
    // =========================================================

    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }


    // =========================================================
    // MUSIC VOLUME
    // =========================================================

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);

        if (musicSource != null)
            musicSource.volume = musicVolume;
    }


    // =========================================================
    // SFX VOLUME
    // =========================================================

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);

        if (sfxSource != null)
            sfxSource.volume = sfxVolume;
    }


    // =========================================================
    // MUTE / UNMUTE
    // =========================================================

    public void ToggleMute()
    {
        AudioListener.pause = !AudioListener.pause;
    }
}