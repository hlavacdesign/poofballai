using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;

/// <summary>
/// A single image item that loads a remote texture via URL, 
/// sets it in a RawImage, and fades in.
/// 
/// This version does not rely on a LayoutElement. Instead, it manually
/// sets the RectTransform's sizeDelta to (scaledWidth, desiredHeight).
/// 
/// Usage:
///  1) Ensure the parent doesn't override the child's size (e.g., uncheck
///     "Child Control Width/Height" in any HorizontalLayoutGroup).
///  2) In your parent "GalleryBox", you might just place these children
///     in a horizontal layout or a simple container. 
///  3) If you want a consistent "max height," specify it in code or read
///     from the parent's RectTransform.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ImageFrame : MonoBehaviour
{
    [Tooltip("RawImage used to display the downloaded texture. Anchored to fill this entire RectTransform.")]
    public RawImage rawImage;

    [Tooltip("The maximum height (in UI pixels) you want for this image.")]
    public float maxHeight = 400f;

    [Tooltip("How long to fade in once the texture has loaded.")]
    public float fadeDuration = 0.5f;

    private CanvasGroup canvasGroup;
    private Coroutine loadCoroutine;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f; // start invisible
        }

        if (rawImage == null)
        {
            rawImage = GetComponentInChildren<RawImage>();
        }

        // Ensure the RawImage stretches to fill our own RectTransform
        if (rawImage != null)
        {
            RectTransform imageRT = rawImage.rectTransform;
            imageRT.anchorMin = Vector2.zero;
            imageRT.anchorMax = Vector2.one;
            imageRT.offsetMin = Vector2.zero;
            imageRT.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// Begins loading the image from the given URL, then sets our RectTransform
    /// to the correct aspect ratio while capping the height to maxHeight.
    /// </summary>
    public void Initialize(string imageUrl)
    {
        CancelLoad(); 
        loadCoroutine = StartCoroutine(LoadImageRoutine(imageUrl));
    }

    public void CancelLoad()
    {
        if (loadCoroutine != null)
        {
            StopCoroutine(loadCoroutine);
            loadCoroutine = null;
        }
    }

    private IEnumerator LoadImageRoutine(string url)
    {
        Debug.Log($"ImageFrame: Loading image from {url}");

        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"ImageFrame: Failed to download {url}\nError: {www.error}");
                yield break;
            }

            // Get the downloaded texture
            Texture2D tex = DownloadHandlerTexture.GetContent(www);
            if (tex == null)
            {
                Debug.LogWarning($"ImageFrame: No texture returned from {url}");
                yield break;
            }

            // Assign the texture to the RawImage
            rawImage.texture = tex;

            // 1) Determine final displayed height.
            //    For example, if your parent has some known maxHeight, we use that.
            float usedHeight = maxHeight;

            // Alternatively, you could read the parent's current height:
            // RectTransform parentRT = transform.parent as RectTransform;
            // if (parentRT != null && parentRT.rect.height > 0f)
            // {
            //     usedHeight = parentRT.rect.height; // or min(parentRT.rect.height, maxHeight)
            // }

            // 2) Calculate width from aspect ratio
            float aspect = (float)tex.width / tex.height;
            float newWidth = usedHeight * aspect;

            // 3) Set our own RectTransform
            RectTransform rt = transform as RectTransform;
            rt.sizeDelta = new Vector2(newWidth, usedHeight);

            // 4) Force the UI to refresh the geometry (in case it doesn't automatically)
            rawImage.SetAllDirty();

            // 5) Fade in
            yield return StartCoroutine(FadeInRoutine());

            Debug.Log($"ImageFrame: {tex.width}x{tex.height},  displayed as {newWidth}x{usedHeight}");
        }

        loadCoroutine = null;
    }

    private IEnumerator FadeInRoutine()
    {
        float time = 0f;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float alpha = Mathf.Clamp01(time / fadeDuration);
            if (canvasGroup != null) canvasGroup.alpha = alpha;
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }
}
