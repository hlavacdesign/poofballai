using System.Collections;
using UnityEngine;

public class DepthMotion : MonoBehaviour
{
    [Header("Local Z Position Range")]
    public float minZ = -0.5f;
    public float maxZ = 0.5f;

    [Header("Motion Timing")]
    [Tooltip("Minimum and maximum duration (seconds) for each move.")]
    public float minMoveTime = 1f;
    public float maxMoveTime = 3f;

    [Tooltip("Minimum and maximum wait time (seconds) before moving again.")]
    public float minWaitTime = 1f;
    public float maxWaitTime = 3f;

    private void Start()
    {
        // Start the coroutine that loops infinitely, creating an organic movement along Z.
        StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        while (true)
        {
            // 1) Wait a random time before the next move
            float waitTime = Random.Range(minWaitTime, maxWaitTime);
            yield return new WaitForSeconds(waitTime);

            // 2) Pick a random Z position in [minZ, maxZ]
            float targetZ = Random.Range(minZ, maxZ);

            // 3) Store current local position, and define the new local position
            Vector3 oldLocalPos = transform.localPosition;
            Vector3 newLocalPos = oldLocalPos;
            newLocalPos.z = targetZ;

            // 4) Randomly choose how long the move should take
            float moveDuration = Random.Range(minMoveTime, maxMoveTime);

            // 5) Smoothly interpolate from oldLocalPos to newLocalPos using an S-shaped curve
            float elapsed = 0f;
            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / moveDuration);

                // Use SmoothStep for ease-in and ease-out
                float s = Mathf.SmoothStep(0f, 1f, t);

                // Lerp between the old and new positions
                transform.localPosition = Vector3.Lerp(oldLocalPos, newLocalPos, s);

                yield return null;
            }

            // Ensure we end exactly at the new position
            transform.localPosition = newLocalPos;
        }
    }
}
