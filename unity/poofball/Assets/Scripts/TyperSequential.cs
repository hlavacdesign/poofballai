using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// A small helper that types text in random-sized chunks, 
/// and waits a random delay between chunks, simulating a streaming LLM response.
/// </summary>
public class TyperSequential : MonoBehaviour
{
    [Header("Chunk Size Settings")]
    [Tooltip("Minimum number of characters to reveal in one chunk.")]
    public int minChunkSize = 1;

    [Tooltip("Maximum number of characters to reveal in one chunk (inclusive).")]
    public int maxChunkSize = 4;

    [Header("Chunk Delay Settings")]
    [Tooltip("Minimum seconds to wait after appending each chunk.")]
    public float minChunkDelay = 0.01f;

    [Tooltip("Maximum seconds to wait after appending each chunk (inclusive).")]
    public float maxChunkDelay = 0.05f;

    /// <summary>
    /// Type 'fullText' into 'textComponent' in random chunk sizes 
    /// with random delays between them.
    /// Optionally call 'onFinished' when done.
    /// </summary>
    public IEnumerator TypeTextCoroutine(TMP_Text textComponent, string fullText, System.Action onFinished = null)
    {
        textComponent.text = "";  // start empty

        int index = 0;
        while (index < fullText.Length)
        {
            // Pick a random chunk size (inclusive range)
            int chunkSize = Random.Range(minChunkSize, maxChunkSize + 1);

            // Check how many characters remain
            int remaining = fullText.Length - index;
            // If fewer remain than chunkSize, take just what's left
            int currentChunk = Mathf.Min(chunkSize, remaining);

            // Extract this chunk from the full text
            string nextBlock = fullText.Substring(index, currentChunk);

            // Append to the visible text
            textComponent.text += nextBlock;

            // Advance our pointer
            index += currentChunk;

            // Pick a random delay in [minChunkDelay, maxChunkDelay]
            float chunkDelay = Random.Range(minChunkDelay, maxChunkDelay);

            // Wait that random amount of time before next chunk
            yield return new WaitForSeconds(chunkDelay);
        }

        // Optional callback after finishing
        onFinished?.Invoke();
    }
}
