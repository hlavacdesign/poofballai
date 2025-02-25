using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GPUVideoPixelGrid : MonoBehaviour
{
    [Header("Pixel Grid Settings")]
    public int resolution = 64;
    public float minSize = 0.01f;
    public float maxSize = 0.05f;
    [Range(0f, 1f)]
    public float sizeSlider = 0.5f;
    public float gap = 0.01f;

    // Internal references
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    // We'll store the actual runtime material instance
    private Material materialInstance;

    /// <summary>
    /// Expose the material so that another script can set its textures and blend at runtime.
    /// </summary>
    public Material PixelMaterial
    {
        get { return materialInstance; }
    }

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        // Create the pixel-grid mesh
        meshFilter.mesh = CreatePixelGridMesh(resolution);

        // Create a *new* material instance from your "Custom/PixelVideo" shader.
        // This shader should handle two textures, _Blend, etc.
        Shader shader = Shader.Find("Custom/PixelVideo");
        if (shader == null)
        {
            Debug.LogError("Could not find 'Custom/PixelVideo' shader. Make sure the shader file is in the project.");
            return;
        }
        materialInstance = new Material(shader);

        // Assign the material to the MeshRenderer
        meshRenderer.sharedMaterial = materialInstance;
    }

    private void Update()
    {
        if (materialInstance == null) return;

        // Update the pixel-grid properties (resolution, minSize, etc.) on the shader each frame
        materialInstance.SetFloat("_Resolution", resolution);
        materialInstance.SetFloat("_MinSize", minSize);
        materialInstance.SetFloat("_MaxSize", maxSize);
        materialInstance.SetFloat("_SizeSlider", sizeSlider);
        materialInstance.SetFloat("_Gap", gap);
    }

    /// <summary>
    /// Creates a single mesh containing resolution*resolution quads in a grid.
    /// Each quad has 4 vertices storing (position, uv, uv2=row/col).
    /// This is the same logic you had before.
    /// </summary>
    private Mesh CreatePixelGridMesh(int resolution)
    {
        int quadCount = resolution * resolution;
        int vertCount = quadCount * 4;
        int indexCount = quadCount * 6;

        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uv = new Vector2[vertCount];
        Vector2[] uv2 = new Vector2[vertCount];
        int[] triangles = new int[indexCount];

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
                // The 4 vertices for this quad
                vertices[vertIndex + 0] = bottomLeft;
                vertices[vertIndex + 1] = bottomRight;
                vertices[vertIndex + 2] = topRight;
                vertices[vertIndex + 3] = topLeft;

                // Standard UV
                uv[vertIndex + 0] = new Vector2(0f, 0f);
                uv[vertIndex + 1] = new Vector2(1f, 0f);
                uv[vertIndex + 2] = new Vector2(1f, 1f);
                uv[vertIndex + 3] = new Vector2(0f, 1f);

                // Store row,col in uv2
                uv2[vertIndex + 0] = new Vector2(row, col);
                uv2[vertIndex + 1] = new Vector2(row, col);
                uv2[vertIndex + 2] = new Vector2(row, col);
                uv2[vertIndex + 3] = new Vector2(row, col);

                // Two triangles per quad
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

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // allow big meshes
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.uv2 = uv2;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
