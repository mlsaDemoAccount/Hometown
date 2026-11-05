#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// Adds the iOS privacy text the QR scanner needs. Without the camera text, scanning crashes the app.
public static class IOSNetworkPermissions
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        string plistPath = pathToBuiltProject + "/Info.plist";
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        var root = plist.root;

        if (!root.values.ContainsKey("NSCameraUsageDescription"))
            root.SetString("NSCameraUsageDescription",
                "The camera is used to scan a QR code to join a friend's city, and to take memory photos.");

        plist.WriteToFile(plistPath);
    }
}
#endif
