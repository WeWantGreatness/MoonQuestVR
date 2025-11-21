using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class CurvedScreen : MonoBehaviour
{
    [Header("Dimensions")]
    [Min(0.1f)] public float width = 1.6f;
    [Min(0.1f)] public float height = 0.9f;
    
    [Header("Curvature")]
    [Tooltip("Radius of the cylinder. Smaller = More curved. Large (100+) = Flat.")]
    [Min(1.0f)] public float radius = 10f; 
    [Range(1, 64)] public int segments = 32; 

    private Mesh mesh;

    void Start() 
    {
        // CRITICAL: Always sync CurvedScreen with Transform scale on startup
        // This ensures the mesh dimensions match the visual scale set in Unity
        // avoiding any "jump" to default values
        
        // Get the actual scale of the GameObject (lossyScale accounts for parent scaling)
        Vector3 scale = transform.lossyScale;
        
        // Use Transform scale as CurvedScreen's width/height
        // But ensure we don't set zero dimensions
        if (Mathf.Abs(scale.x) > 0.01f && Mathf.Abs(scale.y) > 0.01f)
        {
            width = Mathf.Abs(scale.x);
            height = Mathf.Abs(scale.y);
            
            // Reset Transform scale to 1,1,1 so the mesh dimensions handle the sizing
            // This prevents double-scaling (Transform * Mesh)
            transform.localScale = Vector3.one;
            
            Debug.Log($"CurvedScreen: Synced dimensions from Transform scale: width={width}, height={height}");
        }
        
        GenerateMesh();
    }

    public void GenerateMesh()
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "CurvedMonitorMesh" };
            GetComponent<MeshFilter>().mesh = mesh;
        }

        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        
        float thetaStart = -0.5f * width / radius;
        float deltaTheta = (0.5f * width / radius - thetaStart) / segments;

        for (int i = 0; i <= segments; i++)
        {
            float theta = thetaStart + (i * deltaTheta);
            float x = radius * Mathf.Sin(theta);
            // Change Z direction: use +radius to face forward, -radius faces backward
            float z = radius * Mathf.Cos(theta) - radius; // Faces viewer when radius is positive

            int top = i * 2;
            int bottom = i * 2 + 1;

            vertices[top] = new Vector3(x, height / 2, z);
            vertices[bottom] = new Vector3(x, -height / 2, z);

            // UV mapping - original (left to right) direction
            // If texture is still flipped, you can change this to: float t = 1.0f - ((float)i / segments);
            float t = (float)i / segments;
            uvs[top] = new Vector2(t, 1);
            uvs[bottom] = new Vector2(t, 0);
        }

        // Generate triangles - reversed winding to face forward
        // This makes the mesh face towards the viewer (front-facing)
        int[] triangles = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            int v = i * 2;
            int t = i * 6;
            // Reverse triangle winding: v+2 comes before v+1 to flip face direction
            triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider != null)
        {
            collider.sharedMesh = mesh;
        }
        
        // NOTE: We do NOT touch the material/texture here
        // StreamManager will assign the texture separately and manage it
        // This script ONLY generates the curved mesh geometry
    }

    void OnValidate() 
    { 
        if (Application.isPlaying && mesh != null) 
        {
            GenerateMesh();
        }
}
}
