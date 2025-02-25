using UnityEngine;
using UnityEngine.Video;
using System.Collections;
using System.Collections.Generic;

public class VideoSourceController : MonoBehaviour
{
    [Header("Video Clips")]
    public List<VideoClip> videoClips = new List<VideoClip>();

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

    // Internal references
    private Material crossFadeMaterial;
    private bool isPlayerAActive = false; // We'll set true once we start playing on A

    private void Start()
    {
        // If we have no clips, bail out
        if (videoClips == null || videoClips.Count == 0)
        {
            Debug.LogWarning("No VideoClips assigned in VideoSourceController!");
            return;
        }

        // 1) Make sure both players use RenderTexture mode
        //    so we can feed the result into the material.
        if (videoPlayerA != null)
        {
            videoPlayerA.renderMode = VideoRenderMode.RenderTexture;
            videoPlayerA.targetTexture = videoTextureA;
            videoPlayerA.isLooping = false; // We'll handle scheduling
        }
        if (videoPlayerB != null)
        {
            videoPlayerB.renderMode = VideoRenderMode.RenderTexture;
            videoPlayerB.targetTexture = videoTextureB;
            videoPlayerB.isLooping = false;
        }

        // 2) Get the material from the GPUVideoPixelGrid
        if (pixelGrid != null)
        {
            crossFadeMaterial = pixelGrid.PixelMaterial;
            if (crossFadeMaterial != null)
            {
                // Assign the RenderTextures to the material
                crossFadeMaterial.SetTexture("_VideoTexA", videoTextureA);
                crossFadeMaterial.SetTexture("_VideoTexB", videoTextureB);

                // Start fully showing A (blend=0 => show A, blend=1 => show B)
                crossFadeMaterial.SetFloat("_Blend", 0f);
            }
        }
        else
        {
            Debug.LogWarning("No GPUVideoPixelGrid assigned. Can't set up cross-fade material!");
        }

        // 3) Start playing the first clip on Player A
        PlayFirstClip();
    }

    /// <summary>
    /// Plays the first clip on VideoPlayerA, sets up the scheduling to cross-fade.
    /// </summary>
    private void PlayFirstClip()
    {
        if (!videoPlayerA) return;

        // Pick a random clip from the list
        VideoClip firstClip = GetRandomClip();
        if (!firstClip) return;

        videoPlayerA.clip = firstClip;
        videoPlayerA.Play();
        isPlayerAActive = true;

        // Schedule the next cross-fade
        StartCoroutine(ScheduleNextCrossFade(firstClip.length));
    }

    /// <summary>
    /// After we start a clip, we know its length in seconds. 
    /// We'll wait (clipLength - crossFadeDuration) seconds, then cross-fade.
    /// 
    /// If crossFadeDuration is bigger than the clip length, we cross-fade immediately.
    /// </summary>
    private IEnumerator ScheduleNextCrossFade(double clipLengthD)
    {
        float clipLength = (float)clipLengthD;
        float waitTime = clipLength - crossFadeDuration;
        if (waitTime < 0f) waitTime = 0f;

        yield return new WaitForSeconds(waitTime);

        // Start cross-fade to next clip
        StartCoroutine(CrossFadeToNextClip());
    }

    /// <summary>
    /// Picks a new random clip from the list, plays it on the "other" player, cross-fades,
    /// then schedules another cross-fade when *that* clip is near the end.
    /// </summary>
    private IEnumerator CrossFadeToNextClip()
    {
        // Decide which player is next
        VideoPlayer nextPlayer = isPlayerAActive ? videoPlayerB : videoPlayerA;
        if (nextPlayer == null)
        {
            Debug.LogWarning("Next player is null!");
            yield break;
        }

        // Pick a new random clip
        VideoClip nextClip = GetRandomClip();
        if (!nextClip)
        {
            Debug.LogWarning("No valid clip to cross-fade to!");
            yield break;
        }

        // Start playing the next clip on the other player
        nextPlayer.clip = nextClip;
        nextPlayer.Play();

        // Perform the cross fade over crossFadeDuration
        yield return StartCoroutine(CrossFadeRoutine());

        // Swap active player
        isPlayerAActive = !isPlayerAActive;

        // Stop the old player
        if (isPlayerAActive && videoPlayerB) videoPlayerB.Stop();
        else if (!isPlayerAActive && videoPlayerA) videoPlayerA.Stop();

        // Schedule next cross fade from the new clip
        StartCoroutine(ScheduleNextCrossFade(nextClip.length));
    }

    /// <summary>
    /// Animates the shader’s _Blend over crossFadeDuration.
    /// If isPlayerAActive == true, we fade from A->B (0..1).
    /// If false, we fade from B->A (1..0).
    /// </summary>
    private IEnumerator CrossFadeRoutine()
    {
        if (!crossFadeMaterial)
        {
            Debug.LogWarning("No crossFadeMaterial. Cannot cross-fade!");
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < crossFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / crossFadeDuration);

            // If A is active right now, we fade from A->B.  (0 -> 1)
            // If B is active right now, we fade from B->A.  (1 -> 0)
            float blendValue = isPlayerAActive ? t : (1f - t);

            crossFadeMaterial.SetFloat("_Blend", blendValue);

            yield return null;
        }

        // Snap to final value
        crossFadeMaterial.SetFloat("_Blend", isPlayerAActive ? 1f : 0f);
    }

    /// <summary>
    /// Returns a random clip from the videoClips list.
    /// </summary>
    private VideoClip GetRandomClip()
    {
        if (videoClips == null || videoClips.Count == 0)
            return null;

        int index = Random.Range(0, videoClips.Count);
        return videoClips[index];
    }
}
