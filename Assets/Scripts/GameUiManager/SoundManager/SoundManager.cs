using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    // =========================================================
    // AUDIO SOURCES
    // =========================================================

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;


    // =========================================================
    // BACKGROUND MUSIC
    // =========================================================

    [Header("Background Music")]
    [SerializeField] private AudioClip mainMusic;
    [SerializeField] private AudioClip lobbyMusic;


    // =========================================================
    // SOUND EFFECTS
    // =========================================================

    [Header("Sound Effects")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip typingSound;
    [SerializeField] private AudioClip wrongSound;
    [SerializeField] private AudioClip panelOpenSound;
    [SerializeField] private AudioClip buttonArrivalSound;

    // =========================================================
    // VOLUME
    // =========================================================

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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }

        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume;
        }
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

        if (musicSource.clip == mainMusic &&
            musicSource.isPlaying)
        {
            return;
        }

        musicSource.clip = mainMusic;
        musicSource.loop = true;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }


    // =========================================================
    // LOBBY MUSIC
    // =========================================================

    public void PlayLobbyMusic()
    {
        if (musicSource == null || lobbyMusic == null)
            return;

        if (musicSource.clip == lobbyMusic &&
            musicSource.isPlaying)
        {
            return;
        }

        musicSource.clip = lobbyMusic;
        musicSource.loop = true;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }


    // =========================================================
    // BUTTON CLICK SOUND
    // =========================================================

    public void PlayButtonSound()
    {
        if (sfxSource == null ||
            buttonClickSound == null)
        {
            return;
        }

        sfxSource.PlayOneShot(
            buttonClickSound,
            sfxVolume
        );
    }


    // =========================================================
    // TYPING SOUND
    // =========================================================

    public void PlayTypingSound()
    {
        if (sfxSource == null ||
            typingSound == null)
        {
            return;
        }

        sfxSource.PlayOneShot(
            typingSound,
            sfxVolume
        );
    }


    // =========================================================
    // WRONG / ERROR SOUND
    // =========================================================

    public void PlayWrongSound()
    {
        if (sfxSource == null ||
            wrongSound == null)
        {
            return;
        }

        sfxSource.PlayOneShot(
            wrongSound,
            sfxVolume
        );
    }


    // =========================================================
    // PANEL OPEN SOUND
    // =========================================================

    public void PlayPanelOpenSound()
    {
        if (sfxSource == null ||
            panelOpenSound == null)
        {
            return;
        }

        sfxSource.PlayOneShot(
            panelOpenSound,
            sfxVolume
        );
    }


    // =========================================================
    // CUSTOM SOUND
    // =========================================================

    public void PlaySound(AudioClip clip)
    {
        if (sfxSource == null ||
            clip == null)
        {
            return;
        }

        sfxSource.PlayOneShot(
            clip,
            sfxVolume
        );
    }


    // =========================================================
    // STOP MUSIC
    // =========================================================

    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }


    // =========================================================
    // MUSIC VOLUME
    // =========================================================

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);

        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }
    }


    // =========================================================
    // SFX VOLUME
    // =========================================================

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);

        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume;
        }
    }


    // =========================================================
    // MUTE / UNMUTE
    // =========================================================

    public void ToggleMute()
    {
        AudioListener.pause = !AudioListener.pause;
    }

}