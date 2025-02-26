using UnityEngine;
using UnityEngine.Video;
using System.Collections;
using System.Collections.Generic;

public class VideoSourceController : MonoBehaviour
{
    [Header("Video URLs (Cloudinary or other)")]
    [Tooltip("A list of direct URLs to .mp4 (H.264) or .webm (VP8/VP9) files.")]
    public List<string> videoURLs = new List<string>() {
        "https://res.cloudinary.com/hlavacdesign/video/upload/v1740510178/349456376320421895_dessns.mp4",
        "https://res.cloudinary.com/hlavacdesign/video/upload/v1740510176/349454633436110849_yaw86g.mp4",
        "https://res.cloudinary.com/hlavacdesign/video/upload/v1740509836/349454558903336963_rfiz6o.mp4",
        "https://res.cloudinary.com/hlavacdesign/video/upload/v1740519601/349456382641225736_djytvn.mp4",
        "https://res.cloudinary.com/hlavacdesign/video/upload/v1740519601/349457032326352899_dopwhx.mp4",
        "https://res.cloudinary.com/hlavacdesign/video/upload/v1740519601/349458449933975560_vi14s5.mp4",
        "https://res.cloudinary.com/hlavacdesign/video/upload/v1740519601/349458481710026755_uexiif.mp4",
        "https://res.cloudinary.com/hlavacdesign/video/upload/v1740519602/349456798409986056_rn71by.mp4",
        "https://res.cloudinary.com/hlavacdesign/video/upload/v1740519603/349458481710026756_bqppt9.mp4",
        "https://res.cloudinary.com/hlavacdesign/video/upload/v1740519603/349458481710026757_zxfngj.mp4",
        "https://res.cloudinary.com/hlavacdesign/video/upload/v1740519603/349460539519451143_ca4ona.mp4",
        "https://res.cloudinary.com/hlavacdesign/video/upload/v1740519603/349458481710026758_zm2ga5.mp4",
        "https://res.cloudinary.com/hlavacdesign/video/upload/v1740519603/349460591994392581_dmhe6i.mp4",
        "https://res.cloudinary.com/hlavacdesign/video/upload/v1740519605/349460622650560519_awcvaq.mp4"
    };

    [Header("Cross-Fade Settings")]
    [Tooltip("Seconds it takes to cross-fade from one clip to the next.")]
    public float crossFadeDuration = 2f;

    [Header("Video Players")]
    public VideoPlayer videoPlayerA;
    public VideoPlayer videoPlayerB;

    [Header("RenderTextures")]
    public RenderTexture videoTextureA;
    public RenderTexture videoTextureB;

    [Header("Dependencies")]
    [Tooltip("Reference to the GPUVideoPixelGrid so we can grab its material.")]
    public GPUVideoPixelGrid pixelGrid;

    // The material that does the cross-fade
    private Material crossFadeMaterial;

    // If true, A is currently considered the "active" (foreground) player.
    private bool isPlayerAActive = false;

    private void Start()
    {
        if (videoURLs == null || videoURLs.Count == 0)
        {
            Debug.LogWarning("No video URLs assigned in VideoSourceController!");
            return;
        }

        // Configure both VideoPlayers for RenderTexture
        if (videoPlayerA != null)
        {
            videoPlayerA.renderMode = VideoRenderMode.RenderTexture;
            videoPlayerA.targetTexture = videoTextureA;
            videoPlayerA.isLooping = false;
            videoPlayerA.audioOutputMode = VideoAudioOutputMode.None; // Mute to avoid autoplay block
        }
        if (videoPlayerB != null)
        {
            videoPlayerB.renderMode = VideoRenderMode.RenderTexture;
            videoPlayerB.targetTexture = videoTextureB;
            videoPlayerB.isLooping = false;
            videoPlayerB.audioOutputMode = VideoAudioOutputMode.None; // Mute to avoid autoplay block
        }

        // Grab the pixel-grid shader material
        if (pixelGrid != null)
        {
            crossFadeMaterial = pixelGrid.PixelMaterial;
            if (crossFadeMaterial != null)
            {
                // Assign RenderTextures to the cross-fade shader
                crossFadeMaterial.SetTexture("_VideoTexA", videoTextureA);
                crossFadeMaterial.SetTexture("_VideoTexB", videoTextureB);
                crossFadeMaterial.SetFloat("_Blend", 0f); // Start fully on A
            }
        }
        else
        {
            Debug.LogWarning("No GPUVideoPixelGrid assigned. Can't set up cross-fade material!");
        }

        // Start the first clip on Player A
        PlayFirstVideoOnA();
    }

    /// <summary>
    /// Picks a random URL, plays it on VideoPlayerA, then schedules a cross-fade to the second clip.
    /// </summary>
    private void PlayFirstVideoOnA()
    {
        if (!videoPlayerA) return;

        string url = GetRandomURL();
        StartCoroutine(PlayVideoRoutine(videoPlayerA, url, isFirst: true));
    }

    /// <summary>
    /// Plays one clip on the given VideoPlayer, waits (clipLength - crossFadeDuration),
    /// then triggers the cross-fade sequence to the other player.
    /// 
    /// isFirst = true means we also set isPlayerAActive for the first time.
    /// </summary>
    private IEnumerator PlayVideoRoutine(VideoPlayer vp, string url, bool isFirst = false)
    {
        Debug.Log($"[PlayVideoRoutine] Starting clip on {vp.name}: {url}");
        vp.url = url;
        vp.Prepare();

        // Wait until it's prepared
        while (!vp.isPrepared)
            yield return null;

        vp.Play();
        // Debug.Log($"[PlayVideoRoutine] {vp.name} is now playing. length={vp.length}");

        if (isFirst)
        {
            // If vp is A => isPlayerAActive = true; if vp is B => false
            isPlayerAActive = (vp == videoPlayerA);
        }

        double clipLength = vp.length; 
        if (clipLength < 0.1)
        {
            // Some hosting solutions do not report length properly
            clipLength = 10.0; 
            Debug.LogWarning($"[PlayVideoRoutine] Using fallback length of 10s for {url}");
        }

        // Wait until near end of this clip
        float waitTime = (float)clipLength - crossFadeDuration;
        if (waitTime < 0) waitTime = 0;
        yield return new WaitForSeconds(waitTime);

        // Now cross-fade to next video (on the other player)
        StartCoroutine(CrossFadeToNextVideo());
    }

    /// <summary>
    /// Cross-fades from the currently active player to the other player,
    /// picking a new random URL for the other player.
    /// 
    /// After the cross-fade completes, stops the old player,
    /// and then schedules the next cross-fade from the newly active player.
    /// </summary>
    private IEnumerator CrossFadeToNextVideo()
    {
        // Decide which player is "next"
        // If A is active, we cross-fade to B, otherwise to A
        VideoPlayer nextPlayer = isPlayerAActive ? videoPlayerB : videoPlayerA;
        if (!nextPlayer)
        {
            Debug.LogWarning("[CrossFadeToNextVideo] Next player is null!");
            yield break;
        }

        // Pick a random new URL
        string nextURL = GetRandomURL();

        // Prepare and play the next clip
        yield return StartCoroutine(PlayVideoOnNextPlayer(nextPlayer, nextURL));

        // Cross-fade
        yield return StartCoroutine(CrossFadeRoutine());

        // Swap active player
        isPlayerAActive = !isPlayerAActive;

        // Stop the old player
        if (isPlayerAActive && videoPlayerB) videoPlayerB.Stop(); 
        else if (!isPlayerAActive && videoPlayerA) videoPlayerA.Stop();

        // Now that the new player is active, let's schedule the NEXT cross-fade from it.
        double newClipLength = nextPlayer.length;
        if (newClipLength < 0.1)
        {
            newClipLength = 10.0;
            Debug.LogWarning($"[CrossFadeToNextVideo] Next clip length unknown. Using fallback 10s.");
        }

        float waitTime = (float)newClipLength - crossFadeDuration;
        if (waitTime < 0) waitTime = 0;
        yield return new WaitForSeconds(waitTime);

        // Cross-fade again (repeat forever)
        StartCoroutine(CrossFadeToNextVideo());
    }

    /// <summary>
    /// Prepares and plays the "next" player's clip (no scheduling here).
    /// </summary>
    private IEnumerator PlayVideoOnNextPlayer(VideoPlayer nextPlayer, string url)
    {
        // Debug.Log($"[PlayVideoOnNextPlayer] Setting {nextPlayer.name} to {url}");
        nextPlayer.url = url;
        nextPlayer.Prepare();

        // Wait until prepared
        while (!nextPlayer.isPrepared)
            yield return null;

        nextPlayer.Play();
        // Debug.Log($"[PlayVideoOnNextPlayer] {nextPlayer.name} is now playing. length={nextPlayer.length}");
    }

    /// <summary>
    /// Fades _Blend over crossFadeDuration.
    /// If isPlayerAActive == true, that means we are currently showing A,
    /// so we fade (0..1) to show B. If false, we fade (1..0) to show A.
    /// </summary>
    private IEnumerator CrossFadeRoutine()
    {
        if (!crossFadeMaterial)
        {
            Debug.LogWarning("[CrossFadeRoutine] No crossFadeMaterial!");
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < crossFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / crossFadeDuration);

            // If A is active now, we fade from A->B => blend goes 0..1
            // If B is active now, we fade from B->A => blend goes 1..0
            float blendValue = isPlayerAActive ? t : (1f - t);

            crossFadeMaterial.SetFloat("_Blend", blendValue);
            yield return null;
        }

        // Snap to final
        crossFadeMaterial.SetFloat("_Blend", isPlayerAActive ? 1f : 0f);
    }

    /// <summary>
    /// Returns a random URL from the list.
    /// </summary>
    private string GetRandomURL()
    {
        if (videoURLs == null || videoURLs.Count == 0)
            return null;

        int index = Random.Range(0, videoURLs.Count);
        return videoURLs[index];
    }
}
