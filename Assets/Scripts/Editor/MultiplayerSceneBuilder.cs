using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// One-click builder for the multiplayer test scene:
///   Hometown > Multiplayer > Create Test Scene
/// It makes the networked cube prefab (Fusion needs the editor to register it), the test scene,
/// and adds the scene to Build Settings. Safe to run again - it rebuilds both.
public static class MultiplayerSceneBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Multiplayer/NetworkCube.prefab";
    private const string ScenePath = "Assets/Scenes/Multiplayer/MultiplayerTest.unity";

    [MenuItem("Hometown/Multiplayer/Create Test Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        System.IO.Directory.CreateDirectory("Assets/Prefabs/Multiplayer");
        System.IO.Directory.CreateDirectory("Assets/Scenes/Multiplayer");

        // ---- Networked cube prefab ----
        var cubeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeGo.name = "NetworkCube";
        cubeGo.GetComponent<Renderer>().sharedMaterial = LoadOrCreateMaterial();
        cubeGo.AddComponent<NetworkObject>();
        cubeGo.AddComponent<NetworkTransform>();
        cubeGo.AddComponent<SyncedCube>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(cubeGo, PrefabPath);
        Object.DestroyImmediate(cubeGo);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Fusion spawns prefabs from its own table - make sure the cube is in it.
        if (!EditorApplication.ExecuteMenuItem("Tools/Fusion/Rebuild Prefab Table"))
            Debug.LogWarning("[Multiplayer] Couldn't find Tools > Fusion > Rebuild Prefab Table. Run it by hand once.");

        // ---- Scene ----
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        camGo.transform.SetPositionAndRotation(new Vector3(0f, 14f, -9f), Quaternion.Euler(58f, 0f, 0f));
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.16f, 0.22f);

        var lightGo = new GameObject("Directional Light", typeof(Light));
        lightGo.GetComponent<Light>().type = LightType.Directional;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(1.4f, 1f, 1.4f); // ~14 m square (cube limit is 6 m)

        var net = new GameObject("Network");
        var session = net.AddComponent<NetworkSessionManager>();
        session.cubePrefab = prefab.GetComponent<NetworkObject>();
        net.AddComponent<CubeDragController>();
        CoopUiBuilder.Build(net.AddComponent<MultiplayerTestUI>());

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log("[Multiplayer] Test scene created at " + ScenePath + " and added to Build Settings.");
    }

    private const string SyncPrefabPath = "Assets/Prefabs/Multiplayer/CityNetworkSync.prefab";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";

    /// Adds co-op to the real game: the networked "city sync" prefab, plus a "Co-op" object in the
    /// Gameplay scene (session manager + the Co-op button / menu). Safe to run again.
    [MenuItem("Hometown/Multiplayer/Add Co-op To Gameplay Scene")]
    public static void AddToGameplay()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        System.IO.Directory.CreateDirectory("Assets/Prefabs/Multiplayer");

        var syncGo = new GameObject("CityNetworkSync");
        syncGo.AddComponent<NetworkObject>();
        syncGo.AddComponent<CityNetworkSync>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(syncGo, SyncPrefabPath);
        Object.DestroyImmediate(syncGo);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!EditorApplication.ExecuteMenuItem("Tools/Fusion/Rebuild Prefab Table"))
            Debug.LogWarning("[Multiplayer] Couldn't find Tools > Fusion > Rebuild Prefab Table. Run it by hand once.");

        var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

        SetUpCoopObjects(prefab.GetComponent<NetworkObject>(), overlay: true);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings(GameplayScenePath);
        Debug.Log("[Multiplayer] Co-op added to the Gameplay scene.");
    }

    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    /// Adds co-op to the main menu: the session manager (which then carries the connection into
    /// Gameplay) and the Host / Join screen that the Invite button opens. Safe to run again.
    [MenuItem("Hometown/Multiplayer/Add Co-op To Main Menu Scene")]
    public static void AddToMainMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SyncPrefabPath);
        if (prefab == null) { Debug.LogError("[Multiplayer] Run 'Add Co-op To Gameplay Scene' first (it creates the sync prefab)."); return; }

        var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        SetUpCoopObjects(prefab.GetComponent<NetworkObject>(), overlay: false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings(MainMenuScenePath);
        Debug.Log("[Multiplayer] Co-op added to the MainMenu scene.");
    }

    /// Two separate root objects: "Network Session" (survives scene loads) and "Co-op UI" (belongs
    /// to the scene). Any older single "Co-op" object from the first version is split up.
    private static void SetUpCoopObjects(NetworkObject syncPrefab, bool overlay)
    {
        var old = GameObject.Find("Co-op");
        if (old != null)
        {
            var oldSession = old.GetComponent<NetworkSessionManager>();
            if (oldSession != null) Object.DestroyImmediate(oldSession);
            var oldUi = old.GetComponent<MultiplayerTestUI>();
            if (oldUi != null) Object.DestroyImmediate(oldUi);
            Object.DestroyImmediate(old);
        }

        var existing = Object.FindFirstObjectByType<NetworkSessionManager>();
        GameObject sessionGo = existing != null ? existing.gameObject : new GameObject("Network Session");
        var session = sessionGo.GetComponent<NetworkSessionManager>() ?? sessionGo.AddComponent<NetworkSessionManager>();
        session.citySyncPrefab = syncPrefab;
        session.syncScene = false; // both phones load Gameplay themselves - Fusion must not do it
        EditorUtility.SetDirty(sessionGo);

        var uiExisting = Object.FindFirstObjectByType<MultiplayerTestUI>();
        GameObject uiGo = uiExisting != null ? uiExisting.gameObject : new GameObject("Co-op UI");
        var ui = uiGo.GetComponent<MultiplayerTestUI>() ?? uiGo.AddComponent<MultiplayerTestUI>();
        ui.overlayMode = overlay;
        ui.menuMode = !overlay;
        CoopUiBuilder.Build(ui);
        EditorUtility.SetDirty(uiGo);
    }

    private static Material LoadOrCreateMaterial()
    {
        const string path = "Assets/Prefabs/Multiplayer/NetworkCube.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        mat = new Material(shader) { color = new Color(0.95f, 0.45f, 0.15f) };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void AddToBuildSettings(string path)
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(x => x.path == path);
        var entry = new EditorBuildSettingsScene(path, true);

        // The game relies on MainMenu = index 0 and Gameplay = index 1 (LobbyScreen loads by index).
        if (path == MainMenuScenePath) list.Insert(0, entry);
        else if (path == GameplayScenePath)
        {
            int menuAt = list.FindIndex(x => x.path == MainMenuScenePath);
            list.Insert(menuAt >= 0 ? menuAt + 1 : 0, entry);
        }
        else list.Add(entry);
        EditorBuildSettings.scenes = list.ToArray();
    }
}
