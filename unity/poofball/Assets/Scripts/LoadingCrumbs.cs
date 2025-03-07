using UnityEngine;
using TMPro;
using System.Collections;

public class LoadingCrumbs : MonoBehaviour
{
    [Tooltip("Reference to the TMP_Text that will display loading dots.")]
    public TMP_Text crumbText;

    [Tooltip("Number of seconds between each dot.")]
    public float speed = 1f;

    // Keep track of the active coroutine so we can stop it later
    private Coroutine crumbCoroutine;

    public void Start() {

        StartCrumbs();
    }

    /// <summary>
    /// Immediately starts the dot-adding coroutine (if not already running).
    /// </summary>
    public void StartCrumbs()
    {
        // If there's already a coroutine running, stop it first
        if (crumbCoroutine != null)
        {
            StopCrumbs();
        }
        crumbCoroutine = StartCoroutine(AppendDots());
    }

    /// <summary>
    /// Stops generating dots (if it's running).
    /// </summary>
    public void StopCrumbs()
    {
        if (crumbCoroutine != null)
        {
            StopCoroutine(crumbCoroutine);
            crumbCoroutine = null;
            crumbText.text += "please ckick anywhere";
        }
    }

    private IEnumerator AppendDots()
    {
        while (true)
        {
            yield return new WaitForSeconds(speed);
            crumbText.text += ".";
        }
    }
}
