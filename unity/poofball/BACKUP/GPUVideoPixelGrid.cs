using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GPUVideoPixelGrid : MonoBehaviour
{
    [Header("Video")]
    public VideoPlayer videoPlayer; // Must point to a VideoPlayer that outputs to a RenderTexture
    public RenderTexture videoTexture; // The same RenderTexture used by the VideoPlayer

    [Header("Grid Settings")]
    public int resolution = 64;
    public float minSize = 0.01f;
    public float maxSize = 0.05f;
    [Range(0f, 1f)]
    public float sizeSlider = 0.5f;
    public float gap = 0.01f;

    // We'll set these on the material
    private Material materialInstance;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        // Create a *new* material instance from the shader so we can tweak properties at runtime
        Shader shader = Shader.Find("Custom/PixelVideo");
        if (shader == null)
        {
            Debug.LogError("Could not find 'Custom/PixelVideo' shader. Make sure the shader file is in the project.");
            return;
        }
        materialInstance = new Material(shader);

        // Assign to MeshRenderer
        meshRenderer.sharedMaterial = materialInstance;

        // Create the mesh
        meshFilter.mesh = CreatePixelGridMesh(resolution);

        // Optional: Start the video
        if (videoPlayer != null)
        {
            videoPlayer.Play();
        }
    }

    private void Update()
    {
        if (materialInstance == null) return;

        // Update material properties each frame if user adjusts them
        materialInstance.SetFloat("_Resolution", resolution);
        materialInstance.SetFloat("_MinSize", minSize);
        materialInstance.SetFloat("_MaxSize", maxSize);
        materialInstance.SetFloat("_SizeSlider", sizeSlider);
        materialInstance.SetFloat("_Gap", gap);

        if (videoTexture != null)
        {
            materialInstance.SetTexture("_VideoTex", videoTexture);
        }
    }

    /// <summary>
    /// Creates a single mesh containing resolution*resolution quads.
    /// Each quad has 4 vertices, storing:
    ///   - position (in [-0.5,0.5])
    ///   - uv2 = (row, col)
    /// </summary>
    private Mesh CreatePixelGridMesh(int resolution)
    {
        // Total number of quads
        int quadCount = resolution * resolution;

        // 4 vertices per quad
        int vertCount = quadCount * 4;

        // 6 indices per quad (two triangles)
        int indexCount = quadCount * 6;

        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uv = new Vector2[vertCount];    // We might not strictly need this, but good to keep standard UV usage
        Vector2[] uv2 = new Vector2[vertCount];   // We'll store (row, col) here
        int[] triangles = new int[indexCount];

        // We define the corners of a "unit" quad in local space (centered at (0,0)):
        // Let's define corners from -0.5..+0.5 for convenience
        Vector3 topLeft     = new Vector3(-0.5f,  0.5f, 0f);
        Vector3 topRight    = new Vector3( 0.5f,  0.5f, 0f);
        Vector3 bottomLeft  = new Vector3(-0.5f, -0.5f, 0f);
        Vector3 bottomRight = new Vector3( 0.5f, -0.5f, 0f);

        int vertIndex = 0;
        int triIndex = 0;

        for (int row = 0; row < resolution; row++)
        {
            for (int col = 0; col < resolution; col++)
            {
                // Index for this quad
                int i = row * resolution + col;

                // The 4 vertices for the quad
                vertices[vertIndex + 0] = bottomLeft;
                vertices[vertIndex + 1] = bottomRight;
                vertices[vertIndex + 2] = topRight;
                vertices[vertIndex + 3] = topLeft;

                // Standard UV just set them to corners of 0..1
                uv[vertIndex + 0] = new Vector2(0f, 0f);
                uv[vertIndex + 1] = new Vector2(1f, 0f);
                uv[vertIndex + 2] = new Vector2(1f, 1f);
                uv[vertIndex + 3] = new Vector2(0f, 1f);

                // Store the row/col in uv2 for each vertex
                // Even though row,col are integers, we store them as floats
                uv2[vertIndex + 0] = new Vector2(row, col);
                uv2[vertIndex + 1] = new Vector2(row, col);
                uv2[vertIndex + 2] = new Vector2(row, col);
                uv2[vertIndex + 3] = new Vector2(row, col);

                // Now set up triangles (two per quad):
                // triangle 1: bottomLeft, bottomRight, topRight
                // triangle 2: bottomLeft, topRight, topLeft
                triangles[triIndex + 0] = vertIndex + 0;
                triangles[triIndex + 1] = vertIndex + 1;
                triangles[triIndex + 2] = vertIndex + 2;
                triangles[triIndex + 3] = vertIndex + 0;
                triangles[triIndex + 4] = vertIndex + 2;
                triangles[triIndex + 5] = vertIndex + 3;

                vertIndex += 4;
                triIndex += 6;
            }
        }

        // Build the mesh
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // allow >65535 indices if resolution is large
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.uv2 = uv2;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
