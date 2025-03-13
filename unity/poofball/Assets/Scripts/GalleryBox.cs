using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A container that lines up ImageFrame objects horizontally.
/// 
/// Prerequisites:
///  - Attach this to a prefab that has a HorizontalLayoutGroup
///  - Optionally a LayoutElement to fix the height, e.g. 200
///  - The ImageFrame prefab should be assigned to imageFramePrefab in the Inspector.
/// </summary>
public class GalleryBox : MonoBehaviour
{
    [Tooltip("Prefab of a single image frame.")]
    public GameObject imageFramePrefab;

    /// <summary>
    /// Called after you instantiate the GalleryBox to create ImageFrames
    /// for each given URL.
    /// </summary>
    public void Initialize(List<string> imageUrls)
    {
        if (imageFramePrefab == null)
        {
            Debug.LogWarning("GalleryBox: No ImageFrame prefab assigned.");
            return;
        }

        foreach (string url in imageUrls)
        {
            // Spawn one ImageFrame for each URL
            GameObject frameObj = Instantiate(imageFramePrefab, transform);
            ImageFrame frame = frameObj.GetComponent<ImageFrame>();
            if (frame != null)
            {
                frame.Initialize(url);
            }
            else
            {
                Debug.LogWarning("GalleryBox: No ImageFrame component found on prefab!");
            }
        }

        // Optional: Force a layout rebuild to ensure immediate correct sizing
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
    }
}
