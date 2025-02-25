using System.Collections;
using UnityEngine;

public class AxisMotion : MonoBehaviour
{
    [Header("Rotation Ranges (in degrees)")]
    public float rotationXMin = -10f;
    public float rotationXMax = 10f;
    public float rotationYMin = -10f;
    public float rotationYMax = 10f;

    [Header("Rotation Timing")]
    [Tooltip("Minimum and maximum rotation durations (seconds) for the swivel.")]
    public float minRotationDuration = 0.5f;
    public float maxRotationDuration = 2f;

    [Header("Wait Timing")]
    [Tooltip("Minimum and maximum wait time (seconds) before rotating again.")]
    public float minWaitTime = 2f;
    public float maxWaitTime = 5f;

    private void Start()
    {
        // Start the organic motion routine
        StartCoroutine(MotionRoutine());
    }

    private IEnumerator MotionRoutine()
    {
        while (true)
        {
            // 1) Wait a random time before the next swivel
            float waitTime = Random.Range(minWaitTime, maxWaitTime);
            yield return new WaitForSeconds(waitTime);

            // 2) Pick a new random rotation around local X & Y (Z=0)
            float randomX = Random.Range(rotationXMin, rotationXMax);
            float randomY = Random.Range(rotationYMin, rotationYMax);

            Quaternion oldRotation = transform.localRotation;
            Quaternion newRotation = Quaternion.Euler(randomX, randomY, 0f);

            // 3) Choose a random rotation duration
            float rotationDuration = Random.Range(minRotationDuration, maxRotationDuration);

            // 4) Interpolate from oldRotation to newRotation with S-shaped easing
            float elapsed = 0f;
            while (elapsed < rotationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / rotationDuration);

                // S-shaped interpolation: ease-in, ease-out
                // SmoothStep(0,1,t) produces an S-curve from 0 to 1
                float s = Mathf.SmoothStep(0f, 1f, t);

                // Use Quaternion.Slerp for smooth rotation, applying the smooth-step value
                transform.localRotation = Quaternion.Slerp(oldRotation, newRotation, s);

                yield return null;
            }

            // Ensure final rotation is precisely newRotation
            transform.localRotation = newRotation;
        }
    }
}
