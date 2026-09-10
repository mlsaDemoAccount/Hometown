using UnityEngine;
using UnityEngine.SceneManagement;

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
    [SerializeField] private AudioClip gameplayMusic;


    // =========================================================
    // SOUND EFFECTS
    // =========================================================

    [Header("Sound Effects")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip typingSound;
    [SerializeField] private AudioClip wrongSound;
    [SerializeField] private AudioClip panelOpenSound;


    // =========================================================
    // VOLUME
    // =========================================================

    // General Volume multiplies both channels without changing their own settings.
    [Range(0f, 1f)]
    [SerializeField] private float generalVolume = 1f;

    public float GeneralVolume => generalVolume;
    public float MusicVolume => musicVolume;
    public float SFXVolume => sfxVolume;

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
        // Destroy duplicate SoundManager
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Keep SoundManager alive from Scene 0 -> Scene 1
        DontDestroyOnLoad(gameObject);

        // Restore the player's settings when the game starts.
        generalVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Audio.GeneralVolume", generalVolume));
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Audio.MusicVolume", musicVolume));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Audio.SFXVolume", sfxVolume));
        ApplyVolumes();

        Debug.Log("SOUND MANAGER CREATED");
    }


    // =========================================================
    // ON ENABLE
    // =========================================================

    private void OnEnable()
    {
        SceneManager.sceneLoaded += SceneLoaded;
    }


    // =========================================================
    // ON DISABLE
    // =========================================================

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
    }


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        int currentScene =
            SceneManager.GetActiveScene().buildIndex;

        Debug.Log("START SCENE INDEX = " + currentScene);


        // Scene 0 = Main Menu
        if (currentScene == 0)
        {
            PlayMainMusic();
        }


        // Scene 1 = Gameplay
        else if (currentScene == 1)
        {
            PlayGameplayMusic();
        }
    }


    // =========================================================
    // WHEN NEW SCENE LOADS
    // =========================================================

    private void SceneLoaded(
        Scene scene,
        LoadSceneMode mode
    )
    {
        Debug.Log(
            "SCENE LOADED! INDEX = " +
            scene.buildIndex
        );


        // =============================================
        // SCENE 0 = MAIN MENU
        // =============================================

        if (scene.buildIndex == 0)
        {
            Debug.Log("MAIN MENU DETECTED");

            PlayMainMusic();
        }


        // =============================================
        // SCENE 1 = GAMEPLAY
        // =============================================

        else if (scene.buildIndex == 1)
        {
            Debug.Log("GAMEPLAY DETECTED");

            PlayGameplayMusic();
        }
    }


    // =========================================================
    // MAIN MUSIC
    // =========================================================

    public void PlayMainMusic()
    {
        if (musicSource == null)
        {
            Debug.LogError("MUSIC SOURCE NOT ASSIGNED!");
            return;
        }

        if (mainMusic == null)
        {
            Debug.LogError("MAIN MUSIC NOT ASSIGNED!");
            return;
        }


        // Already playing
        if (musicSource.clip == mainMusic &&
            musicSource.isPlaying)
        {
            return;
        }


        // Stop previous music
        musicSource.Stop();


        // Set main music
        musicSource.clip = mainMusic;

        musicSource.loop = true;

        musicSource.volume = musicVolume * generalVolume;

        musicSource.Play();


        Debug.Log("MAIN MUSIC PLAYING");
    }


    // =========================================================
    // LOBBY MUSIC
    // =========================================================

    public void PlayLobbyMusic()
    {
        if (musicSource == null)
        {
            Debug.LogError("MUSIC SOURCE NOT ASSIGNED!");
            return;
        }

        if (lobbyMusic == null)
        {
            Debug.LogError("LOBBY MUSIC NOT ASSIGNED!");
            return;
        }


        if (musicSource.clip == lobbyMusic &&
            musicSource.isPlaying)
        {
            return;
        }


        musicSource.Stop();

        musicSource.clip = lobbyMusic;

        musicSource.loop = true;

        musicSource.volume = musicVolume * generalVolume;

        musicSource.Play();


        Debug.Log("LOBBY MUSIC PLAYING");
    }


    // =========================================================
    // GAMEPLAY MUSIC
    // =========================================================

    public void PlayGameplayMusic()
    {
        Debug.Log("PlayGameplayMusic() CALLED");


        if (musicSource == null)
        {
            Debug.LogError(
                "GAMEPLAY FAILED: MUSIC SOURCE IS NULL!"
            );

            return;
        }


        if (gameplayMusic == null)
        {
            Debug.LogError(
                "GAMEPLAY FAILED: GAMEPLAY MUSIC IS NOT ASSIGNED!"
            );

            return;
        }


        // STOP MAIN MUSIC
        musicSource.Stop();


        Debug.Log("OLD MUSIC STOPPED");


        // Change AudioClip
        musicSource.clip = gameplayMusic;


        // Loop gameplay music
        musicSource.loop = true;


        // Set volume
        musicSource.volume = musicVolume * generalVolume;


        // Start gameplay music
        musicSource.Play();


        Debug.Log(
            "GAMEPLAY MUSIC PLAYING = " +
            gameplayMusic.name
        );
    }


    // =========================================================
    // BUTTON SOUND
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
            1f // Source volume already includes SFX and General Volume.
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
            1f // Source volume already includes SFX and General Volume.
        );
    }


    // =========================================================
    // WRONG SOUND
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
            1f // Source volume already includes SFX and General Volume.
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
            1f // Source volume already includes SFX and General Volume.
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
            1f // Source volume already includes SFX and General Volume.
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
        PlayerPrefs.SetFloat("Audio.MusicVolume", musicVolume);
        ApplyVolumes();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("Audio.SFXVolume", sfxVolume);
        ApplyVolumes();
    }

    public void SetGeneralVolume(float volume)
    {
        generalVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("Audio.GeneralVolume", generalVolume);
        ApplyVolumes();
    }

    private void ApplyVolumes()
    {
        // Example: General 50% and Music 80% gives music output of 40%.
        if (musicSource != null)
            musicSource.volume = generalVolume * musicVolume;

        if (sfxSource != null)
            sfxSource.volume = generalVolume * sfxVolume;
    }


    // =========================================================
    // MUTE
    // =========================================================

    public void ToggleMute()
    {
        AudioListener.pause =
            !AudioListener.pause;
    }
}