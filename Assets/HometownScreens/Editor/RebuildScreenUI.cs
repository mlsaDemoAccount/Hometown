using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

namespace Hometown.Screens.Editor
{
    // Rebuilds only the six preview prefabs. Gameplay assets are read-only references.
    public static class RebuildScreenUI
    {
        const string Folder = "Assets/HometownScreens/";
        const string Elements = "Assets/UiImages/Elements/";
        static readonly Color PanelColor = new Color(.035f, .07f, .065f, .96f);
        static readonly Color Blue = new Color(.035f, .16f, .78f, 1);
        static TMP_FontAsset font;
        static ReferenceScreen screen;
        static RectTransform content;
        static GameObject gameplayHUD;

        [MenuItem("Tools/Hometown/Rebuild Six Screen UI")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before rebuilding prefabs.");
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Inter/Inter-VariableFont_opsz,wght SDF.asset");
            var gameplayScene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Gameplay.unity");
            try
            {
                gameplayHUD = gameplayScene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<RectTransform>(true))
                    .First(rt => rt.name == "Gameplay" && rt.Find("TopPanel")).gameObject;
                foreach (ScreenKind kind in Enum.GetValues(typeof(ScreenKind))) Build(kind);
            }
            finally { gameplayHUD = null; EditorSceneManager.ClosePreviewScene(gameplayScene); }
            AssetDatabase.SaveAssets();
            ValidateAndRender();
        }

        static Sprite Sprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!sprite) throw new InvalidOperationException("Missing sprite: " + path);
            return sprite;
        }

        static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.gameObject.layer = 5;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static Image Art(string name, Transform parent, string path, float x, float y, float w, float h)
        {
            var im = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
            im.sprite = Sprite(path);
            im.type = Image.Type.Simple;
            im.preserveAspect = true; // Same setup as gameplay icons: never distort the source sprite.
            im.raycastTarget = false;
            return im;
        }

        static Image Panel(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var im = Art(name, parent, Folder + "Art/RoundedPanel.png", x, y, w, h);
            im.type = Image.Type.Sliced; // This sprite has 16px borders, unlike the decorative icons.
            im.preserveAspect = false;
            im.color = color;
            return im;
        }

        static TMP_Text Label(string name, Transform parent, string value, float x, float y, float w, float h, int size = 24, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var t = Rect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font; t.text = value; t.fontSize = size; t.alignment = align;
            t.color = Color.white; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate;
            return t;
        }

        static Button Button(string name, Transform parent, string label, float x, float y, float w, float h, UnityAction action, string sprite = null)
        {
            var im = sprite == null ? Panel(name, parent, x, y, w, h, Blue) : Art(name, parent, sprite, x, y, w, h);
            im.raycastTarget = true;
            var b = im.gameObject.AddComponent<Button>();
            b.targetGraphic = im;
            var colors = b.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(.85f, .94f, 1, 1);
            colors.pressedColor = new Color(.65f, .78f, .94f, 1); colors.selectedColor = Color.white;
            b.colors = colors;
            if (action != null) UnityEventTools.AddPersistentListener(b.onClick, action);
            im.gameObject.AddComponent<ButtonMotion>();
            if (!string.IsNullOrEmpty(label))
            {
                float labelWidth = sprite == null ? w - 22 : Mathf.Min(w, h * im.sprite.rect.width / im.sprite.rect.height) - 36;
                var text = Label("Label", im.transform, label, 4, 0, labelWidth, h - 12, h > 55 ? 28 : 22);
                text.enableAutoSizing = true; text.fontSizeMin = 18; text.fontSizeMax = h > 55 ? 28 : 22;
                text.textWrappingMode = TextWrappingModes.NoWrap;
            }
            return b;
        }

        static RectTransform Window(string title, string subtitle, float x, float y, float w, float h)
        {
            var border = Panel("Window Border", content, x, y, w + 4, h + 4, new Color(.53f, .64f, .61f, 1));
            var panel = Panel("Window", border.transform, 0, 0, w, h, PanelColor);
            Panel("Header", panel.transform, 0, h / 2 - 49, w - 22, 78, new Color(.03f, .12f, .38f, 1));
            screen.title = Label("Title", panel.transform, title, 0, h / 2 - 36, w - 140, 48, 38);
            screen.title.fontStyle = FontStyles.Bold;
            screen.subtitle = Label("Subtitle", panel.transform, subtitle, 0, h / 2 - 69, w - 140, 25, 16);
            Button("Close", panel.transform, "", w / 2 - 37, h / 2 - 40, 28, 28, screen.Close,
                "Assets/UiImages/Notification Popup/Symbol.png");
            return panel.rectTransform;
        }

        static void Hud()
        {
            // Use the gameplay scene as the authority for the shared HUD, including TMP text,
            // sprite assignments, image types, button transition colours and child transforms.
            var gameplay = gameplayHUD;
            var hud = Rect("Gameplay HUD", content, 0, 0, 1080, 607.5f);
            hud.localScale = Vector3.one * (1600f / 1080f);
            foreach (string name in new[] { "TopPanel", "RightPanel" })
            {
                var source = gameplay.transform.Find(name);
                if (!source) throw new InvalidOperationException("Gameplay HUD missing " + name);
                var clone = Object.Instantiate(source.gameObject, hud, false);
                clone.name = name;
                foreach (var image in clone.GetComponentsInChildren<Graphic>(true)) image.raycastTarget = false;
                foreach (var button in clone.GetComponentsInChildren<Button>(true))
                {
                    button.onClick = new Button.ButtonClickedEvent();
                    UnityAction action = button.name.Contains("Home") ? screen.Home :
                        button.name.Contains("Settings") ? screen.Audio :
                        button.name.Contains("Categories") ? screen.Categories :
                        button.name.Contains("Notification") ? screen.Memories : screen.PlayClick;
                    UnityEventTools.AddPersistentListener(button.onClick, action);
                    if (button.targetGraphic) button.targetGraphic.raycastTarget = true;
                    if (!button.GetComponent<ButtonMotion>()) button.gameObject.AddComponent<ButtonMotion>();
                }
            }
        }

        static void Build(ScreenKind kind)
        {
            string path = Folder + "Prefabs/" + kind + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                screen = root.GetComponent<ReferenceScreen>();
                // Keep the root/controller fileIDs so suite and preview scene links survive.
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                root.layer = 5;
                root.transform.localScale = Vector3.one;
                screen.title = screen.subtitle = screen.status = screen.levelText = null;
                screen.description = null; screen.photoPreview = null;
                screen.generalVolume = screen.musicVolume = screen.sfxVolume = null;
                screen.memoryLabels = Array.Empty<TMP_Text>();
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920); // Gameplay.unity CanvasScaler.
                scaler.matchWidthOrHeight = 0;
                var scrim = Rect("Screen Dimmer", root.transform, 0, 0, 0, 0);
                scrim.anchorMin = Vector2.zero; scrim.anchorMax = Vector2.one;
                var dimmer = scrim.gameObject.AddComponent<Image>();
                dimmer.color = new Color(.015f, .035f, .03f, kind == ScreenKind.AudioSettings ? .92f : .5f);
                dimmer.raycastTarget = true;
                var safe = Rect("Safe Area", root.transform, 0, 0, 0, 0);
                safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
                var design = Rect("Design 1600x900", safe, 0, 0, 1600, 900);
                content = Rect("Animated Screen", design, 0, 0, 1600, 900);
                root.GetComponent<SafeScreen>().safeArea = safe;
                root.GetComponent<SafeScreen>().design = design;
                root.GetComponent<ScreenMotion>().content = content;
                if (kind != ScreenKind.AudioSettings) Hud();
                if (kind == ScreenKind.NewMemory || kind == ScreenKind.OceanParkMemories || kind == ScreenKind.LevelUp)
                    Art("Character", content, Elements + "untitled 2.png", -590, -165, 300, 565);
                switch (kind)
                {
                    case ScreenKind.AudioSettings: Audio(); break;
                    case ScreenKind.DemolishConfirmation: Demolish(); break;
                    case ScreenKind.NewMemory: NewMemory(); break;
                    case ScreenKind.OceanParkMemories: Memories(); break;
                    case ScreenKind.MemoryDiary: Diary(); break;
                    case ScreenKind.LevelUp: LevelUp(); break;
                }
                EditorUtility.SetDirty(screen);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void Audio()
        {
            var p = Window("Audio Settings", "Adjust your game audio", 0, -10, 1230, 650);
            screen.generalVolume = Volume(p, "General Volume", 155, .8f);
            screen.musicVolume = Volume(p, "Music", -15, .9f);
            screen.sfxVolume = Volume(p, "SFX", -185, .9f);
        }

        static Slider Volume(Transform parent, string name, float y, float value)
        {
            var row = Rect(name, parent, 0, y, 1080, 105);
            Label("Label", row, name, -310, 33, 460, 40, 26, TextAlignmentOptions.MidlineLeft);
            var track = Panel("Track", row, 0, -16, 1080, 30, new Color(.84f, .89f, .91f));
            track.raycastTarget = true;
            var fill = Panel("Fill", track.transform, 0, 0, 0, 30, new Color(.08f, .15f, 1));
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.sizeDelta = Vector2.zero;
            var slider = row.gameObject.AddComponent<Slider>();
            slider.targetGraphic = track; slider.fillRect = fill.rectTransform;
            slider.minValue = 0; slider.maxValue = 1; slider.value = value;
            Label("Value", row, Mathf.RoundToInt(value * 100) + "%", 465, 33, 150, 40, 24, TextAlignmentOptions.MidlineRight);
            return slider;
        }

        static void Demolish()
        {
            var p = Window("Demolish building?", "This will remove the selected building.", 0, -10, 800, 400);
            Label("Question", p, "Would you like to demolish this building?", 0, 20, 690, 85, 30);
            Button("Accept", p, "Accept", -185, -112, 310, 78, screen.Accept, Elements + "Group (1) 1.png");
            Button("Decline", p, "Decline", 185, -112, 310, 78, screen.Close, Elements + "Group (2) 1.png");
        }

        static void NewMemory()
        {
            var p = Window("New Memory", "Ocean Park", 35, -50, 630, 640);
            var capture = Button("Capture Photo", p, "", 0, 137, 550, 120, screen.Capture);
            capture.GetComponent<Image>().color = new Color(.07f, .11f, .12f);
            screen.photoPreview = Art("Photo Preview", capture.transform, Elements + "ci_camera.png", 0, 12, 520, 70);
            Label("Capture Label", capture.transform, "Tap to capture photo", 0, -39, 500, 30, 18);
            Button("Upload Photo", p, "Upload from device", 0, 35, 550, 54, screen.Upload);
            Label("Description Label", p, "Description", -175, -17, 200, 30, 18, TextAlignmentOptions.MidlineLeft);
            var box = Panel("Description Input", p, 0, -110, 550, 148, new Color(.08f, .12f, .13f));
            box.raycastTarget = true;
            var viewport = Rect("Text Area", box.transform, 0, 0, 514, 120);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Label("Text", viewport, "", 0, 0, 514, 120, 20, TextAlignmentOptions.TopLeft);
            var placeholder = Label("Placeholder", viewport, "What makes this moment special?", 0, 0, 514, 120, 20, TextAlignmentOptions.TopLeft);
            placeholder.color = new Color(.7f, .77f, .78f);
            screen.description = box.gameObject.AddComponent<TMP_InputField>();
            screen.description.textViewport = viewport;
            screen.description.textComponent = text; screen.description.placeholder = placeholder;
            screen.description.targetGraphic = box;
            screen.description.lineType = TMP_InputField.LineType.MultiLineNewline;
            screen.description.characterLimit = 240;
            Button("Save Memory", p, "Save memory", 0, -239, 360, 94, screen.SaveMemory, Elements + "Group (2) 1.png");
            screen.status = Label("Status", p, "", 0, -303, 560, 28, 16);
        }

        static void Memories()
        {
            var p = Window("Ocean Park", "2 memories uploaded", 15, -30, 810, 650);
            screen.memoryLabels = new TMP_Text[2];
            for (int i = 0; i < 2; i++)
            {
                var row = Panel("Memory " + (i + 1), p, 0, 90 - 170 * i, 730, 145, new Color(.065f, .11f, .12f));
                Label("Date", row.transform, "1/2/2026", -185, 43, 300, 26, 16, TextAlignmentOptions.MidlineLeft);
                screen.memoryLabels[i] = Label("Description", row.transform, "Ocean park with family on weekends\nStay in resort, have dinner", -95, -12, 480, 78, 21, TextAlignmentOptions.MidlineLeft);
                Art("Photo", row.transform, Elements + "Group 1171275370 (1).png", 265, 0, 148, 122);
            }
            Button("Upload", p, "Upload", -190, -235, 335, 94, screen.NewMemory, Elements + "Group (2) 1.png");
            Button("View Diary", p, "View Diary", 190, -235, 335, 94, screen.Diary, Elements + "Group (2) 1.png");
        }

        static void Diary()
        {
            var p = Window("Memory Diary", "Ocean Park", 0, -15, 960, 580);
            Art("Family Photo", p, Elements + "Group 1171275370 (1).png", -225, 5, 390, 320);
            Label("Story", p, "A day together at Ocean Park.\n\nTime with family, new adventures, and memories worth keeping.", 220, 10, 380, 250, 25, TextAlignmentOptions.TopLeft);
            screen.subtitle = Label("Memory Caption", p, "I go with your mom in 2015\nWe are very happy", 0, -218, 810, 83, 27);
            Button("Previous Memory", content, "<", -565, -15, 60, 80, screen.NextPage);
            Button("Next Memory", content, ">", 565, -15, 60, 80, screen.NextPage);
        }

        static void LevelUp()
        {
            var p = Window("LEVEL UP!", "Great job! You've leveled up!", 40, -45, 850, 575);
            var medal = Art("Level Medal", p, Folder + "Art/Medal.png", 0, 5, 230, 230);
            medal.color = new Color(1, .78f, .11f);
            screen.levelText = Label("Level", medal.transform, screen.level.ToString(), 0, 0, 180, 160, 88);
            screen.levelText.fontStyle = FontStyles.Bold; screen.levelText.color = new Color(.14f, .09f, 0);
            medal.gameObject.AddComponent<IdleMotion>().pulse = true;
            Label("Reward Caption", p, "NEW LEVEL REACHED!", 0, -142, 650, 55, 32).fontStyle = FontStyles.Bold;
            Button("Collect", p, "COLLECT", 0, -221, 320, 70, screen.Collect, Elements + "Group (3).png");
        }

        [MenuItem("Tools/Hometown/Validate and Render Six Screen UI")]
        public static void ValidateAndRender()
        {
            string output = "Temp/HometownUIReview";
            Directory.CreateDirectory(output);
            var report = new StringBuilder("Six screen UI validation\n");
            foreach (ScreenKind kind in Enum.GetValues(typeof(ScreenKind)))
            {
                var root = PrefabUtility.LoadPrefabContents(Folder + "Prefabs/" + kind + ".prefab");
                try
                {
                    foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject) != 0) throw new Exception(kind + " missing script");
                    if (root.GetComponentsInChildren<RawImage>(true).Length != 0) throw new Exception(kind + " still contains screenshot artwork");
                    foreach (var im in root.GetComponentsInChildren<Image>(true))
                    {
                        if (im.sprite && AssetDatabase.GetAssetPath(im.sprite).Contains("ReferenceScreens")) throw new Exception("Reference screenshot still in use");
                        if (im.type == Image.Type.Sliced && im.sprite && im.sprite.border == Vector4.zero) throw new Exception("Sliced image without borders: " + im.name);
                    }
                    foreach (var b in root.GetComponentsInChildren<Button>(true))
                        if (!b.targetGraphic || b.onClick.GetPersistentEventCount() == 0 || !b.onClick.GetPersistentTarget(0)) throw new Exception("Unbound button: " + b.name);
                    var controller = root.GetComponent<ReferenceScreen>();
                    if (kind == ScreenKind.NewMemory && (!controller.description || !controller.photoPreview || !controller.status)) throw new Exception("Memory fields missing");
                    if (kind == ScreenKind.AudioSettings)
                        foreach (var slider in new[] { controller.generalVolume, controller.musicVolume, controller.sfxVolume })
                            if (!slider || !slider.fillRect || !slider.transform.Find("Value")) throw new Exception("Slider visuals missing");
                    report.AppendLine(kind + ": OK; " + root.GetComponentsInChildren<Image>(true).Length + " Images, " + root.GetComponentsInChildren<Button>(true).Length + " bound Buttons");
                    Render(root, output + "/" + kind + ".png", 1600, 900);
                    Render(root, output + "/" + kind + "-4x3.png", 1200, 900);
                    Render(root, output + "/" + kind + "-ultrawide.png", 2100, 900);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            File.WriteAllText(Folder + "Validation.txt", report.ToString());
            File.WriteAllText(output + "/complete.txt", report.ToString());
            Debug.Log(report.ToString());
        }

        static void Render(GameObject root, string path, int w, int h)
        {
            // Isolated prefab preview scene: does not open/save the user's current scene.
            var cameraObject = new GameObject("UI Review Camera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, root.scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = root.scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(root.scene);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.075f, .12f, .14f);
            camera.orthographic = true; camera.orthographicSize = h / 2f;
            camera.transform.position = new Vector3(0, 0, -100);
            var texture = new RenderTexture(w, h, 24);
            camera.targetTexture = texture;
            var canvas = root.GetComponent<Canvas>();
            root.GetComponent<CanvasScaler>().enabled = false;
            root.GetComponent<SafeScreen>().enabled = false;
            canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            canvas.scaleFactor = 1;
            var canvasRect = (RectTransform)root.transform;
            canvasRect.pivot = new Vector2(.5f, .5f);
            canvasRect.sizeDelta = new Vector2(w, h);
            canvasRect.position = Vector3.zero;
            canvasRect.localScale = Vector3.one;
            root.SetActive(true);
            Canvas.ForceUpdateCanvases();
            root.GetComponent<SafeScreen>().design.localScale = Vector3.one * Mathf.Min(w / 1600f, h / 900f);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = texture;
            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, w, h), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null;
            Object.DestroyImmediate(image); Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraObject);
        }
    }
}
