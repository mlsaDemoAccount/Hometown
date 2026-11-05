using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Co-op screens (scene objects, see the fields below):
///   Main     -> Host / Join buttons
///   Host     -> QR code + session code, players connected
///   Join     -> live camera (scan the host's QR) + manual code entry
///   Session  -> connected: player count, Leave button
/// Polls NetworkSessionManager every frame (no events).
public class MultiplayerTestUI : MonoBehaviour
{
    [Tooltip("ON in the Gameplay scene: a small Co-op button opens the menu, and the connected bar is a slim pill so the game stays visible. OFF in the test scene.")]
    public bool overlayMode;

    [Tooltip("ON in the main menu: no Co-op button - the Invite button opens this (Open()). Once both players are connected the host starts the game and both phones show the lobby countdown, then load Gameplay.")]
    public bool menuMode;

    public static MultiplayerTestUI Instance { get; private set; }

    // The screens are real scene objects (built once by Hometown > UI > Build Co-op UI) - restyle,
    // re-sprite or move them freely, just keep these references assigned.
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject hostPanel, joinPanel, sessionPanel, awayPanel;

    [Header("Texts")]
    [SerializeField] private TMP_Text mainStatus;
    [SerializeField] private TMP_Text hostPlayers, hostPayload, joinStatus, sessionText, noticeText, awayText, awayTimer;

    [Header("Pictures and input")]
    [SerializeField] private RawImage qrImage;
    [SerializeField] private RawImage camImage;
    [SerializeField] private TMP_InputField manualInput;

    [Header("Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton, mainCloseButton, hostCancelButton, connectButton, joinCancelButton,
        sessionLeaveButton, awayLeaveButton, coopButton, returnButton;

    private QrScanner scanner;
    private Texture2D qrTexture;
    private bool joining;
    private bool menuOpen;
    private string shownCode;
    private bool startSent, lobbyShown, everLive;

    void Awake()
    {
        Instance = this;
        scanner = gameObject.AddComponent<QrScanner>();
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        Listen(hostButton, OnHostClicked);
        Listen(joinButton, OnJoinClicked);
        Listen(mainCloseButton, OnCloseMenu);
        Listen(hostCancelButton, OnLeave);
        Listen(connectButton, OnConnectTyped);
        Listen(joinCancelButton, OnCancelJoin);
        Listen(sessionLeaveButton, OnLeave);
        Listen(awayLeaveButton, OnLeave);
        Listen(coopButton, OnOpenMenu);
        Listen(returnButton, BackToMenu);
        if (manualInput != null) manualInput.characterLimit = menuMode ? 6 : 4;

        // start hidden: only the Co-op button (Gameplay) or the Main screen (test scene) can show
        Show(overlayMode || menuMode ? null : mainPanel);
        awayPanel.SetActive(false);
        noticeText.gameObject.SetActive(false);
        returnButton.gameObject.SetActive(false);
        coopButton.gameObject.SetActive(overlayMode);
    }

    private static void Listen(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (qrTexture != null) Destroy(qrTexture);
    }

    // ---------- Per-frame ----------

    /// Main menu: show the Host / Join screen (called by the Invite button).
    public void Open() => menuOpen = true;

    void Update()
    {
        var net = NetworkSessionManager.Instance;
        if (net == null) return;

        if (menuMode)
        {
            if (net.StartGameRequested && !lobbyShown)
            {
                // both players are in: show the lobby countdown, which then loads Gameplay
                lobbyShown = true;
                net.ConsumeStartGame();
                scanner.End();
                menuOpen = false;
                GameUIManager.Instance?.InvitePlayerScreenToggle(false);
                GameUIManager.Instance?.LobbyScreenToggle(true);
            }
            if (lobbyShown) { Show(null); return; }
            if (net.State == NetSessionState.Hosting && net.PlayerCount >= 2 && !startSent
                && (CityLauncher.Instance == null || !CityLauncher.Instance.AwaitingGuest))
            {
                startSent = true;
                net.HostStartGame();
            }
            if (net.State != NetSessionState.Hosting) startSent = false;
        }

        // A QR code was scanned: try to join.
        if (joining && scanner.Result != null)
        {
            string scanned = scanner.Result;
            scanner.End();
            TryJoin(scanned);
        }
        if (joining && scanner.Error != null) joinStatus.text = scanner.Error;
        if (net.State == NetSessionState.Connected) { scanner.End(); joining = false; }

        int players = net.PlayerCount;
        switch (net.State)
        {
            case NetSessionState.Hosting:
                string payload = menuMode ? MenuInvitePayload() : net.Session.ToPayload();
                if (shownCode != payload) ShowInvite(payload);
                qrImage.gameObject.SetActive(!string.IsNullOrEmpty(shownCode));
                if (players >= 2)
                {
                    Show(sessionPanel);
                    sessionText.text = SessionLine(players);
                }
                else
                {
                    Show(hostPanel);
                    hostPlayers.text = "Players connected: " + players + "\n" + net.StatusMessage;
                }
                break;

            case NetSessionState.Connected:
                Show(sessionPanel);
                sessionText.text = SessionLine(players);
                break;

            default: // Idle / Connecting / Failed
                if (joining)
                {
                    Show(joinPanel);
                    if (menuMode && CityLauncher.Instance != null && (CityLauncher.Instance.Busy || CityLauncher.Instance.Failed))
                        joinStatus.text = CityLauncher.Instance.Status;
                    else if (scanner.Error == null)
                        joinStatus.text = net.State == NetSessionState.Connecting ? net.StatusMessage
                                        : net.State == NetSessionState.Failed ? net.StatusMessage
                                        : "Point the camera at the host's QR code.";
                }
                else if (menuMode && menuOpen && CityLauncher.Instance != null && (CityLauncher.Instance.Busy || CityLauncher.Instance.Failed))
                {
                    // creating / opening a city, or it failed: a plain status screen with a Cancel button
                    Show(hostPanel);
                    shownCode = null;
                    qrImage.gameObject.SetActive(false);
                    hostPayload.text = "";
                    hostPlayers.text = CityLauncher.Instance.Status;
                }
                else if ((overlayMode || menuMode) && !menuOpen)
                {
                    Show(null); // game is fully visible; only the Co-op button shows
                }
                else
                {
                    Show(mainPanel);
                    mainStatus.text = net.State == NetSessionState.Failed || net.IsBusy ? net.StatusMessage
                        : "Both players need an internet connection.\nHost shows a QR code, the other player scans it.";
                }
                break;
        }
    }

    private string SessionLine(int players)
    {
        if (menuMode) return "Connected! Starting the game...";
        if (!overlayMode) return "Connected - Players: " + players + "\nDrag the cube: both phones see it move.";
        var sync = CityNetworkSync.Instance;
        if (sync != null && !sync.CityReady) return "Loading the host's city...";
        return "Co-op: building together (" + players + "/2)";
    }

    private void LateUpdate()
    {
        if (!overlayMode) return;

        // A short message from the host ("Not enough funds.").
        var sync = CityNetworkSync.Instance;
        bool notice = sync != null && !string.IsNullOrEmpty(sync.Notice) && Time.unscaledTime < sync.NoticeUntil;
        noticeText.gameObject.SetActive(notice);
        if (notice) noticeText.text = sync.Notice;

        // The Co-op button shows only when nothing else of ours is on screen.
        bool nothingOpen = !mainPanel.activeSelf && !hostPanel.activeSelf && !joinPanel.activeSelf && !sessionPanel.activeSelf;

        // The game only works while both players are connected: once a session we were in has
        // ended, the only way on is back to the menu.
        var net = NetworkSessionManager.Instance;
        bool live = net != null && (net.State == NetSessionState.Connected || net.State == NetSessionState.Hosting);
        if (live) everLive = true;
        bool sessionOver = everLive && !live;
        bool away = net != null && net.Away;
        returnButton.gameObject.SetActive(sessionOver && !away);
        coopButton.gameObject.SetActive(nothingOpen && !sessionOver && !away);

        // The other player (or our own connection) is gone: a full-screen pause with a countdown.
        awayPanel.SetActive(away);
        if (away)
        {
            string who = PartnerName();
            if (net.AwayTimedOut)
            {
                awayText.text = net.OwnConnectionLost
                    ? "Still can't reconnect."
                    : who + " hasn't come back.";
                awayTimer.text = "The city is locked until you are both online.";
                awayTimer.fontSize = 44;
            }
            else
            {
                awayText.text = net.OwnConnectionLost
                    ? "Connection lost.\nTrying to reconnect..."
                    : who + " disconnected.\nWaiting for them to come back...";
                int secs = Mathf.CeilToInt(net.AwayLeft);
                awayTimer.text = (secs / 60) + ":" + (secs % 60).ToString("00");
                awayTimer.fontSize = 90;
            }
        }
    }

    private static string PartnerName()
    {
        string name = CityLauncher.IsCityMode ? CityLauncher.Instance.WaitingFor : "";
        if (string.IsNullOrEmpty(name)) name = "your friend";
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    private void Show(GameObject panel)
    {
        mainPanel.SetActive(panel == mainPanel);
        hostPanel.SetActive(panel == hostPanel);
        joinPanel.SetActive(panel == joinPanel);
        sessionPanel.SetActive(panel == sessionPanel);
    }

    // ---------- Buttons ----------

    private void OnHostClicked()
    {
        if (menuMode) { CityLauncher.Instance.BeginNewCity(); return; }

        // The room is created online first; Update() draws the QR once the host is live
        // (only then is the room's Photon region known).
        NetworkSessionManager.Instance.StartHost();
    }

    /// Menu mode: what the host's QR carries - the backend invite link (empty for an existing city, nothing to scan).
    private static string MenuInvitePayload()
    {
        var invite = CityLauncher.Instance != null ? CityLauncher.Instance.Invite : null;
        return invite != null ? invite.url : "";
    }

    private void ShowInvite(string payload)
    {
        if (qrTexture != null) { Destroy(qrTexture); qrTexture = null; }
        shownCode = payload;
        if (string.IsNullOrEmpty(payload))
        {
            hostPayload.text = "Waiting for " + CityLauncher.Instance.WaitingFor + " to join...";
            return;
        }
        qrTexture = QrCodeUtil.Encode(payload);
        qrImage.texture = qrTexture;
        if (menuMode) hostPayload.text = "Invite code\n" + CityLauncher.Instance.Invite.inviteCode + "\nOr scan the QR code.";
        else
        {
            JoinCode.TryParse(payload, out var code);
            hostPayload.text = "Room code\n" + code.ToCode() + "\nOr scan the QR code.";
        }
    }

    private void OnJoinClicked()
    {
        joining = true;
        joinStatus.text = "Starting camera...";
        manualInput.text = "";
        scanner.Begin(camImage);
    }

    private void OnConnectTyped() => TryJoin(manualInput.text);

    private static readonly System.Text.RegularExpressions.Regex InviteCodePattern = new System.Text.RegularExpressions.Regex("^[0-9]{6}$");

    private void TryJoin(string text)
    {
        if (menuMode)
        {
            // an invite code: the 6 digits, or a link ending in them
            string value = (text ?? "").Trim().TrimEnd('/');
            int slash = value.LastIndexOf('/');
            if (slash >= 0) value = value.Substring(slash + 1);
            if (!InviteCodePattern.IsMatch(value))
            {
                joinStatus.text = "That isn't an invite code. Enter the 6 digits your friend shows.";
                if (joining && scanner.Error == null) scanner.Begin(camImage);
                return;
            }
            scanner.End();
            CityLauncher.Instance.BeginJoinInvite(value);
            return;
        }

        if (!JoinCode.TryParse(text, out var info))
        {
            joinStatus.text = "That isn't a Hometown code. Enter the 4-digit room code.";
            if (joining && scanner.Error == null) scanner.Begin(camImage); // keep scanning
            return;
        }
        scanner.End();
        NetworkSessionManager.Instance.StartClient(info);
    }

    private void OnCancelJoin()
    {
        if (menuMode) CityLauncher.Instance.Cancel();
        scanner.End();
        joining = false;
        menuOpen = false;
        NetworkSessionManager.Instance.Shutdown();
    }

    private void OnLeave()
    {
        if (menuMode) CityLauncher.Instance.Cancel();
        scanner.End();
        joining = false;
        menuOpen = false;
        NetworkSessionManager.Instance.Shutdown();
        if (overlayMode) BackToMenu();
    }

    private void OnCloseMenu() => menuOpen = false;
    private void OnOpenMenu() => menuOpen = true;

    /// Co-op is over (or the player left): back to the main menu, where cities are picked.
    private void BackToMenu()
    {
        NetworkSessionManager.Instance?.Shutdown();
        GameUIManager.ReturningFromGame = true;
        SceneManager.LoadScene(0);
    }

}
