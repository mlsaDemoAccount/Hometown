using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// "Ana is waiting for you - Join": a banner at the top of the main menu that appears when a friend
/// is hosting one of your cities right now. Needs no scene setup - it creates itself when the app
/// starts, polls the cities list every few seconds while you are signed in and in the main menu,
/// and builds its own small overlay UI. Only reaches players who have the app open.
public class FriendWaitingBanner : MonoBehaviour
{
    private const string MenuScene = "MainMenu";
    private const float PollSeconds = 8f;

    private GameObject panel;
    private TMP_Text message;
    private string shownCityId;     // the city the banner is showing
    private string dismissedCityId; // the city whose banner the player closed (until that host stops)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("FriendWaitingBanner");
        DontDestroyOnLoad(go);
        go.AddComponent<FriendWaitingBanner>();
    }

    void Awake() => BuildUi();

    void OnEnable() => StartCoroutine(PollLoop());

    private bool CanShow =>
        SceneManager.GetActiveScene().name == MenuScene
        && UserDetails.Instance != null && UserDetails.Instance.isLoggedIn
        && MultiplayerTestUI.Instance != null
        && !CityLauncher.IsCityMode && !CityLauncher.Instance.Busy;

    private IEnumerator PollLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(PollSeconds);
            if (!CanShow) { Hide(); continue; }

            var call = CityApi.Backend.GetMyCities();
            while (!call.isDone) yield return null;
            if (!call.ok || !CanShow) { if (!call.ok) Hide(); continue; }

            string me = CityApi.Backend.MyUserId;
            CityDto waiting = null;
            ParticipantDto host = null;
            foreach (var city in call.result.cities)
            {
                var partner = city.Partner(me);
                if (partner != null && city.hostingNow && city.hostUserId == partner.userId) { waiting = city; host = partner; break; }
            }

            if (waiting == null) { dismissedCityId = null; Hide(); continue; }
            if (waiting.cityId == dismissedCityId) { Hide(); continue; }

            shownCityId = waiting.cityId;
            message.text = host.firstName + " is waiting for you";
            panel.SetActive(true);
        }
    }

    private void Hide()
    {
        shownCityId = null;
        if (panel != null) panel.SetActive(false);
    }

    private void OnJoin()
    {
        string id = shownCityId;
        Hide();
        if (id == null || !CanShow) return;
        MultiplayerTestUI.Instance.Open();
        CityLauncher.Instance.BeginPlay(id);
    }

    private void OnClose()
    {
        dismissedCityId = shownCityId;
        Hide();
    }

    // ---------- UI (built here so no scene change is needed) ----------

    private void BuildUi()
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        panel = Box("Banner", canvasGo.transform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1000f, 110f), new Color(0.07f, 0.06f, 0.1f, 0.96f));
        panel.GetComponent<Image>().raycastTarget = true;

        message = Text("Message", panel.transform, new Vector2(-120f, 0f), new Vector2(560f, 90f), 38, FontStyles.Bold, TextAlignmentOptions.Left);

        var join = Box("Join", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(300f, 0f), new Vector2(200f, 70f), new Color(0.95f, 0.55f, 0.1f));
        join.AddComponent<Button>().targetGraphic = join.GetComponent<Image>();
        join.GetComponent<Button>().onClick.AddListener(OnJoin);
        Text("Label", join.transform, Vector2.zero, new Vector2(200f, 70f), 34, FontStyles.Bold, TextAlignmentOptions.Center).text = "Join";

        var close = Box("Close", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(445f, 0f), new Vector2(70f, 70f), new Color(0.25f, 0.25f, 0.3f));
        close.AddComponent<Button>().targetGraphic = close.GetComponent<Image>();
        close.GetComponent<Button>().onClick.AddListener(OnClose);
        Text("Label", close.transform, Vector2.zero, new Vector2(70f, 70f), 34, FontStyles.Bold, TextAlignmentOptions.Center).text = "X";

        panel.SetActive(false);
    }

    // anchor.y == 1 pins to the top of the screen; otherwise the box is centred in its parent
    private static GameObject Box(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return go;
    }

    private static TMP_Text Text(string name, Transform parent, Vector2 pos, Vector2 size, float fontSize, FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = "";
        return text;
    }
}
