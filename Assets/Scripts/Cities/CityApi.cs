using UnityEngine;

/// The game's single door to the cities backend. Right now it hands out the on-device mock; when
/// the real backend exists, a REST implementation of ICityBackend goes here and nothing else changes.
/// Created on first use and kept for the whole app. Also sends the presence heartbeat ("I'm online")
/// every 30 s while the app is open and the player is signed in.
public class CityApi : MonoBehaviour
{
    private const float HeartbeatSeconds = 30f;

    private static CityApi instance;
    private ICityBackend backend;
    private float nextBeat;

    public static ICityBackend Backend
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("CityApi");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<CityApi>();
                instance.backend = new MockCityBackend();
            }
            return instance.backend;
        }
    }

    void Update()
    {
        if (Time.unscaledTime < nextBeat) return;
        if (UserDetails.Instance == null || !UserDetails.Instance.isLoggedIn) return; // nobody signed in = nobody online
        nextBeat = Time.unscaledTime + HeartbeatSeconds;
        backend.Heartbeat();
    }
}
