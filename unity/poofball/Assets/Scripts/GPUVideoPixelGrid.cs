using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GPUVideoPixelGrid : MonoBehaviour
{
    [Header("Pixel Grid Settings")]
    // This is how many "pixels" along each axis the shader will simulate.
    // The shader will do floor() logic in the fragment to produce a pixel look.
    [Range(1, 512)] 
    public int resolution = 64;

    // Gap between “pixel squares” (if > 0, they become partially transparent).
    [Range(0f, 1.0f)]
    public float gap = 0.5f;

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
        meshFilter   = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        // Create a simple quad mesh. 
        // The new shader does all the pixelation in the fragment shader, 
        // so we only need one quad to fill the screen (or the local area).
        meshFilter.mesh = CreateQuad();

        // Create a *new* material instance from our "Custom/PixelVideo_WebGLFriendly" shader.
        // Make sure the name here matches exactly the name in the shader's first line.
        Shader shader = Shader.Find("Custom/PixelVideo_WebGLFriendly");
        if (shader == null)
        {
            Debug.LogError("Could not find 'Custom/PixelVideo_WebGLFriendly' shader. " +
                           "Make sure the shader file is in the project.");
            return;
        }
        materialInstance = new Material(shader);

        // Assign the material to the MeshRenderer
        meshRenderer.sharedMaterial = materialInstance;

        // Optionally: set a default blend of 0.0 (fully using _VideoTexA)
        materialInstance.SetFloat("_Blend", 0f);
    }

    private void Update()
    {
        if (materialInstance == null) return;

        // The new shader only needs _Resolution and _Gap
        materialInstance.SetFloat("_Resolution", resolution);
        materialInstance.SetFloat("_Gap", gap);
    }

    /// <summary>
    /// Creates a simple centered quad that covers -0.5..+0.5 in X and Y. 
    /// The shader will handle pixelation in the fragment stage.
    /// </summary>
    private Mesh CreateQuad()
    {
        Mesh mesh = new Mesh();

        Vector3[] vertices = new Vector3[4];
        Vector2[] uv       = new Vector2[4];
        int[]     triangles = new int[6];

        // A simple square from (-0.5, -0.5) to (+0.5, +0.5)
        vertices[0] = new Vector3(-0.5f, -0.5f, 0f); 
        vertices[1] = new Vector3(+0.5f, -0.5f, 0f);
        vertices[2] = new Vector3(+0.5f, +0.5f, 0f);
        vertices[3] = new Vector3(-0.5f, +0.5f, 0f);

        uv[0] = new Vector2(0f, 0f);
        uv[1] = new Vector2(1f, 0f);
        uv[2] = new Vector2(1f, 1f);
        uv[3] = new Vector2(0f, 1f);

        // Two triangles:  (0,1,2), (0,2,3)
        triangles[0] = 0;
        triangles[1] = 1;
        triangles[2] = 2;
        triangles[3] = 0;
        triangles[4] = 2;
        triangles[5] = 3;

        mesh.vertices  = vertices;
        mesh.uv        = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}
