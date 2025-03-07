using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Camera))]
public class CameraFOVAnimator : MonoBehaviour
{
    [Header("FOV Range")]
    public float minFOV = 0f;
    public float maxFOV = 60f;

    [Header("Animation Settings")]
    [Tooltip("How many seconds the animation should take.")]
    public float duration = 1f;

    [Tooltip("An ease-in/out curve used to interpolate between minFOV and maxFOV.")]
    public AnimationCurve sCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Black Screen Fade-Out")]
    [Tooltip("A black quad in front of the camera that we will fade out (alpha=1 to alpha=0). " +
             "Must use a material/shader that supports alpha transparency (e.g. Unlit/Transparent).")]
    public GameObject blackQuad;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    /// <summary>
    /// Animate camera FOV from minFOV -> maxFOV, optionally after a delay,
    /// and simultaneously fade out the blackQuad if assigned.
    /// </summary>
    /// <param name="delay">Seconds to wait before starting the animation.</param>
    public void FlyIn(float delay)
    {
        StartCoroutine(DelayedFlyIn(delay, minFOV, maxFOV));
    }

    /// <summary>
    /// Overload that starts FlyIn() immediately (no delay).
    /// </summary>
    public void FlyIn()
    {
        FlyIn(0f);
    }

    /// <summary>
    /// Animate camera FOV from maxFOV -> minFOV immediately (no black quad effect).
    /// </summary>
    public void FlyOut()
    {
        StartCoroutine(FOVAnimCoroutine(maxFOV, minFOV));
    }

    /// <summary>
    /// Wait 'delay' seconds, then animate FOV from 'startFOV' to 'endFOV'
    /// while also fading blackQuad from alpha=1 to alpha=0, if present.
    /// </summary>
    private IEnumerator DelayedFlyIn(float delay, float startFOV, float endFOV)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        yield return StartCoroutine(FadeAndFOVCoroutine(startFOV, endFOV));
    }

    /// <summary>
    /// Animates the camera FOV and (if blackQuad is assigned) fades its alpha from 1 -> 0
    /// over 'duration' seconds using the sCurve. Destroys blackQuad once done.
    /// </summary>
    private IEnumerator FadeAndFOVCoroutine(float startFOV, float endFOV)
    {
        // We'll do the same timing for FOV + fade
        float elapsed = 0f;

        Renderer quadRenderer = null;
        Color initialColor = Color.black; // fallback if no material
        if (blackQuad != null)
        {
            quadRenderer = blackQuad.GetComponent<Renderer>();
            if (quadRenderer != null && quadRenderer.material != null)
            {
                // We'll read the initial color from the material to handle tints other than pure black
                initialColor = quadRenderer.material.color;
                // Ensure alpha is at 1 at start (if you want to forcibly set it)
                // initialColor.a = 1f;
                // quadRenderer.material.color = initialColor;
            }
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curveValue = sCurve.Evaluate(t);

            // Animate the field of view
            cam.fieldOfView = Mathf.Lerp(startFOV, endFOV, curveValue);

            // Animate the black quad alpha from 1 -> 0
            if (quadRenderer != null && quadRenderer.material != null)
            {
                // alpha is reversed: alpha = 1 - curveValue
                Color c = initialColor;
                c.a = Mathf.Lerp(1f, 0f, curveValue);
                quadRenderer.material.color = c;
            }

            yield return null;
        }

        // Snap to final FOV
        cam.fieldOfView = endFOV;

        // If we have a quad, fade to fully transparent
        if (quadRenderer != null && quadRenderer.material != null)
        {
            Color c = quadRenderer.material.color;
            c.a = 0f;
            quadRenderer.material.color = c;
        }

        // Destroy the quad so it doesn't slow down rendering
        if (blackQuad != null)
        {
            Destroy(blackQuad);
        }
    }

    /// <summary>
    /// A simpler FOV-only coroutine, used by FlyOut() if you don't want the black screen effect.
    /// </summary>
    private IEnumerator FOVAnimCoroutine(float startFOV, float endFOV)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curveValue = sCurve.Evaluate(t);
            cam.fieldOfView = Mathf.Lerp(startFOV, endFOV, curveValue);
            yield return null;
        }

        cam.fieldOfView = endFOV;
    }
}
