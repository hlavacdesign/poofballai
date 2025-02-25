using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// This component creates an X-by-X grid of "pixel quads" that sample
/// their color from a video playing on a RenderTexture. The user can
/// adjust resolution, particle size (via a slider), and gap.
/// </summary>
public class PixelParticleVideoPlayer : MonoBehaviour
{
    [Header("Video & Grid Settings")]
    public VideoPlayer videoPlayer;
    [Tooltip("Number of pixels (quads) along each dimension.")]
    public int resolution = 64;

    [Header("Particle Size Controls")]
    [Tooltip("Minimum size for each quad.")]
    public float minParticleSize = 0.01f;
    [Tooltip("Maximum size for each quad.")]
    public float maxParticleSize = 0.05f;
    [Range(0f, 1f), Tooltip("Blends between min and max particle size.")]
    public float sizeSlider = 0.5f;

    [Header("Layout Controls")]
    [Tooltip("Gap to place between pixel quads.")]
    public float gap = 0.05f;

    // Internal references
    private GameObject[,] pixelGrid;
    private Texture2D videoTexture2D; // For CPU pixel reading

    private void Start()
    {
        // Safety check
        if (videoPlayer == null)
        {
            Debug.LogError("VideoPlayer is not assigned!");
            return;
        }

        // The VideoPlayer must be set to render into a RenderTexture 
        // for this CPU-based approach to work
        if (videoPlayer.targetTexture == null)
        {
            Debug.LogError("VideoPlayer needs a RenderTexture as 'Target Texture' to read pixel data!");
            return;
        }

        // Create a 2D array to store the quads
        pixelGrid = new GameObject[resolution, resolution];

        // Create a parent transform to hold all quads
        GameObject parentObject = new GameObject("PixelParticlesParent");
        parentObject.transform.SetParent(transform);

        // Initialize the pixel quads in a grid
        float initialSize = Mathf.Lerp(minParticleSize, maxParticleSize, sizeSlider);
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                // Create a quad
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.transform.SetParent(parentObject.transform);

                // Remove the collider (not needed for this effect)
                Destroy(quad.GetComponent<MeshCollider>());

                // Position in a grid around (0,0)
                // We'll update positions again in Update() if you want them to animate with size,
                // but you can also just set them once here if the layout can remain static.
                quad.transform.localPosition = new Vector3(
                    (x - resolution / 2f) * (initialSize + gap),
                    (y - resolution / 2f) * (initialSize + gap),
                    0f
                );

                // Set initial scale
                quad.transform.localScale = Vector3.one * initialSize;

                pixelGrid[x, y] = quad;
            }
        }

        // Prepare a Texture2D to store copied pixels from RenderTexture
        RenderTexture rt = videoPlayer.targetTexture;
        videoTexture2D = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);

        // Start the video
        videoPlayer.Play();
    }

    private void Update()
    {
        // If the video is playing and we have a valid texture to sample
        if (videoPlayer.isPlaying && videoPlayer.texture != null)
        {
            // Copy the RenderTexture to our Texture2D to allow CPU reads
            RenderTexture rt = videoPlayer.targetTexture;
            RenderTexture.active = rt;
            videoTexture2D.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            videoTexture2D.Apply();
            RenderTexture.active = null;

            // Calculate the current size from the slider
            float currentSize = Mathf.Lerp(minParticleSize, maxParticleSize, sizeSlider);

            // Loop over all quads in the resolution grid
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    // Sample the color at normalized UV = (x/res, y/res)
                    Color color = videoTexture2D.GetPixelBilinear(
                        (float)x / resolution,
                        (float)y / resolution
                    );

                    // Update each quad
                    GameObject quad = pixelGrid[x, y];

                    // Optionally reposition if you want the spacing to grow with size
                    quad.transform.localPosition = new Vector3(
                        (x - resolution / 2f) * (currentSize + gap),
                        (y - resolution / 2f) * (currentSize + gap),
                        0f
                    );

                    // Scale based on slider
                    quad.transform.localScale = Vector3.one * currentSize;

                    // Set material color
                    Renderer r = quad.GetComponent<Renderer>();
                    r.material.color = color;
                }
            }
        }
    }
}
