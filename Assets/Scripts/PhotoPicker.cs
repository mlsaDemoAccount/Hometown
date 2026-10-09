using System.Collections;
using UnityEngine;

/// Result of a camera / gallery request. Wait on isDone from your own coroutine.
/// texture is a readable, already-downscaled image, or null if cancelled / denied / failed.
public class PhotoPickHandle
{
    public bool isDone;
    public Texture2D texture;
}

/// Takes a photo with the phone camera or picks one from the photo library (Android + iOS via
/// the NativeCamera / NativeGallery plugins - they handle the runtime permission prompts).
/// In the Unity Editor there is no camera, so both calls open a file picker instead.
/// Those plugins only offer callbacks, so the callback lives inside this class and just fills
/// the handle - the rest of the game keeps the wait-on-handle pattern.
public static class PhotoPicker
{
    public const int MaxSize = 1280; // longest side in pixels - keeps uploads small

    public static PhotoPickHandle TakePhoto()
    {
        var handle = new PhotoPickHandle();
#if UNITY_EDITOR
        PickFileInEditor(handle);
#elif UNITY_ANDROID || UNITY_IOS
        NativeCamera.TakePicture(path => Finish(handle, path), MaxSize);
#else
        handle.isDone = true;
#endif
        return handle;
    }

    public static PhotoPickHandle PickFromGallery()
    {
        var handle = new PhotoPickHandle();
#if UNITY_EDITOR
        PickFileInEditor(handle);
#elif UNITY_ANDROID || UNITY_IOS
        NativeGallery.GetImageFromGallery(path => Finish(handle, path), "Select a photo", "image/*");
#else
        handle.isDone = true;
#endif
        return handle;
    }

#if UNITY_EDITOR
    private static void PickFileInEditor(PhotoPickHandle handle)
    {
        string path = UnityEditor.EditorUtility.OpenFilePanel("Pick a photo", "", "png,jpg,jpeg");
        if (!string.IsNullOrEmpty(path))
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(System.IO.File.ReadAllBytes(path))) handle.texture = Downscale(tex);
            else Object.Destroy(tex);
        }
        handle.isDone = true;
    }

    private static Texture2D Downscale(Texture2D source)
    {
        int longest = Mathf.Max(source.width, source.height);
        if (longest <= MaxSize) return source;

        float scale = (float)MaxSize / longest;
        int w = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
        int h = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
        var rt = RenderTexture.GetTemporary(w, h, 0);
        Graphics.Blit(source, rt);
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var result = new Texture2D(w, h, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        result.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        Object.Destroy(source);
        return result;
    }
#endif

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
    private static void Finish(PhotoPickHandle handle, string path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            // Loads with the correct EXIF rotation and downscales to MaxSize; keep it readable
            // because the screen JPG-encodes it for upload.
            handle.texture = NativeGallery.LoadImageAtPath(path, MaxSize, false);
        }
        handle.isDone = true;
    }
#endif
}
