using UnityEngine;

namespace PCP.LibLime
{
	/// <summary>
	/// Adds a sky blue border outline around a quad for visibility.
	/// Creates a slightly larger quad behind this one with a colored border material.
	/// </summary>
	[RequireComponent(typeof(MeshRenderer))]
	public class QuadBorder : MonoBehaviour
	{
		[Header("Border Settings")]
		[Tooltip("Color of the border outline (sky blue by default)")]
		public Color borderColor = new Color(0.5f, 0.8f, 1.0f, 1.0f); // Sky blue: RGB(128, 204, 255)
		
		[Tooltip("Width/thickness of the border edge in world units")]
		[Range(0.005f, 0.1f)]
		public float borderWidth = 0.01f;
		
		[Tooltip("Depth of the border frame (how far it extends from the quad surface)")]
		[Range(0.001f, 0.05f)]
		public float borderDepth = 0.01f;
		
		[Tooltip("Offset the border slightly behind the quad to prevent z-fighting")]
		[Range(-0.01f, 0.01f)]
		public float borderDepthOffset = -0.001f;
		
		private GameObject borderContainer;
		private GameObject[] borderEdges = new GameObject[4]; // Top, Bottom, Left, Right
		
		private void Awake()
		{
			// Create border immediately - Awake runs even if GameObject is disabled
			CreateBorder();
		}
		
		private void Start()
		{
			// Ensure border is created (in case Awake didn't run)
			if (borderContainer == null)
			{
				CreateBorder();
			}
		}
		
		private void LateUpdate()
		{
			// Keep border transform synced with this quad's transform
			// This ensures the border moves/resizes with the quad even when it's disabled
			if (borderContainer != null && borderContainer.activeSelf)
			{
				UpdateBorderTransform();
			}
		}
		
		private void UpdateBorderTransform()
		{
			if (borderContainer == null) return;
			
			// Match world position and rotation exactly
			borderContainer.transform.position = transform.position;
			borderContainer.transform.rotation = transform.rotation;
			borderContainer.transform.localScale = Vector3.one;
			
			// Recalculate edge positions and sizes based on actual quad dimensions
			// Use the same method as CreateBorder() to get accurate dimensions
			float width = 0f;
			float height = 0f;
			
			CurvedScreen curvedScreen = GetComponent<CurvedScreen>();
			if (curvedScreen != null)
			{
				// Use CurvedScreen's width/height properties (these are the actual dimensions)
				width = curvedScreen.width;
				height = curvedScreen.height;
			}
			else
			{
				// Fall back to MeshRenderer bounds to get actual visual size
				MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
				if (meshRenderer != null && meshRenderer.bounds.size.magnitude > 0.001f)
				{
					// Get world-space bounds size
					Bounds bounds = meshRenderer.bounds;
					width = bounds.size.x;
					height = bounds.size.y;
				}
				else
				{
					// Last resort: use transform lossyScale
					Vector3 scale = transform.lossyScale;
					width = Mathf.Abs(scale.x);
					height = Mathf.Abs(scale.y);
				}
			}
			
			float halfWidth = width / 2f;
			float halfHeight = height / 2f;
			float edgeThickness = borderWidth;
			float edgeDepth = borderDepth;
			
			// Update each edge's position and scale to match current quad dimensions
			int childCount = borderContainer.transform.childCount;
			for (int i = 0; i < childCount; i++)
			{
				Transform edge = borderContainer.transform.GetChild(i);
				Vector3 newPos = Vector3.zero;
				Vector3 newScale = Vector3.one;
				
				if (edge.name == "Top")
				{
					newPos = new Vector3(0, halfHeight + edgeThickness / 2f, 0);
					newScale = new Vector3(width + edgeThickness * 2, edgeThickness, edgeDepth);
				}
				else if (edge.name == "Bottom")
				{
					newPos = new Vector3(0, -halfHeight - edgeThickness / 2f, 0);
					newScale = new Vector3(width + edgeThickness * 2, edgeThickness, edgeDepth);
				}
				else if (edge.name == "Left")
				{
					newPos = new Vector3(-halfWidth - edgeThickness / 2f, 0, 0);
					newScale = new Vector3(edgeThickness, height + edgeThickness * 2, edgeDepth);
				}
				else if (edge.name == "Right")
				{
					newPos = new Vector3(halfWidth + edgeThickness / 2f, 0, 0);
					newScale = new Vector3(edgeThickness, height + edgeThickness * 2, edgeDepth);
				}
				
				if (newPos != Vector3.zero) // Only update if we found a matching edge
				{
					edge.localPosition = newPos;
					edge.localScale = newScale;
				}
			}
		}
		
		private void CreateBorder()
		{
			// Destroy existing border if it exists
			if (borderContainer != null)
			{
				DestroyImmediate(borderContainer);
			}
			
			// Create a container for all border edges
			borderContainer = new GameObject(gameObject.name + "_Border");
			
			// Set as sibling (same parent) so it can stay active independently
			if (transform.parent != null)
			{
				borderContainer.transform.SetParent(transform.parent, false);
			}
			else
			{
				borderContainer.transform.SetParent(null, false);
			}
			
			// Get the quad's actual visual dimensions from MeshRenderer bounds
			// This gives us the true visual size regardless of transform scale
			float width = 0f;
			float height = 0f;
			
			// Try to get from CurvedScreen component first (if it exists)
			CurvedScreen curvedScreen = GetComponent<CurvedScreen>();
			if (curvedScreen != null)
			{
				// Use CurvedScreen's width/height properties (these are the actual dimensions)
				width = curvedScreen.width;
				height = curvedScreen.height;
				Debug.Log($"QuadBorder: {gameObject.name} - Using CurvedScreen dimensions: width={width:F3}, height={height:F3}");
			}
			else
			{
				// Get actual visual size from MeshRenderer bounds
				MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
				if (meshRenderer != null)
				{
					Bounds bounds = meshRenderer.bounds;
					// Get world-space size from bounds
					width = bounds.size.x;
					height = bounds.size.y;
					Debug.Log($"QuadBorder: {gameObject.name} - Using MeshRenderer bounds: width={width:F3}, height={height:F3}");
				}
				else
				{
					// Fall back to transform lossyScale
					Vector3 scale = transform.lossyScale;
					width = Mathf.Abs(scale.x);
					height = Mathf.Abs(scale.y);
					Debug.Log($"QuadBorder: {gameObject.name} - Using transform scale: width={width:F3}, height={height:F3}");
				}
			}
			
			// Create border material
			Material borderMaterial = CreateBorderMaterial();
			if (borderMaterial == null) return; // Exit if material creation failed
			
			// Position border container to match this quad's world transform exactly
			borderContainer.transform.position = transform.position;
			borderContainer.transform.rotation = transform.rotation;
			borderContainer.transform.localScale = Vector3.one; // Use world scale, not local
			
			// Calculate edge positions based on quad dimensions
			float halfWidth = width / 2f;
			float halfHeight = height / 2f;
			float edgeThickness = borderWidth;
			float edgeDepth = borderDepth;
			
			// Create 4 3D edge boxes that form a frame visible from all angles
			// Each edge is a thin 3D box (cube primitive) positioned at the perimeter
			// Top edge: horizontal box at top
			Create3DEdge(borderContainer.transform, "Top", new Vector3(0, halfHeight + edgeThickness / 2f, 0), new Vector3(width + edgeThickness * 2, edgeThickness, edgeDepth), borderMaterial);
			// Bottom edge: horizontal box at bottom
			Create3DEdge(borderContainer.transform, "Bottom", new Vector3(0, -halfHeight - edgeThickness / 2f, 0), new Vector3(width + edgeThickness * 2, edgeThickness, edgeDepth), borderMaterial);
			// Left edge: vertical box on left
			Create3DEdge(borderContainer.transform, "Left", new Vector3(-halfWidth - edgeThickness / 2f, 0, 0), new Vector3(edgeThickness, height + edgeThickness * 2, edgeDepth), borderMaterial);
			// Right edge: vertical box on right
			Create3DEdge(borderContainer.transform, "Right", new Vector3(halfWidth + edgeThickness / 2f, 0, 0), new Vector3(edgeThickness, height + edgeThickness * 2, edgeDepth), borderMaterial);
			
			// Keep border active even if parent is disabled
			borderContainer.SetActive(true);
		}
		
		private void Create3DEdge(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
		{
			// Create a 3D box (cube) primitive - this is visible from all angles
			GameObject edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
			edge.name = name;
			edge.transform.SetParent(parent, false);
			edge.transform.localPosition = localPosition;
			edge.transform.localScale = localScale;
			edge.transform.localRotation = Quaternion.identity;
			
			// Apply material - cube primitives are automatically visible from all sides
			MeshRenderer renderer = edge.GetComponent<MeshRenderer>();
			renderer.material = material;
			
			// Remove collider - we don't want it to interfere with interaction
			Collider collider = edge.GetComponent<Collider>();
			if (collider != null)
			{
				DestroyImmediate(collider);
			}
		}
		
		private Material CreateBorderMaterial()
		{
			// Try to find a suitable shader
			Shader borderShader = Shader.Find("Universal Render Pipeline/Unlit");
			if (borderShader == null)
			{
				borderShader = Shader.Find("Unlit/Color");
			}
			if (borderShader == null)
			{
				borderShader = Shader.Find("Standard");
			}
			
			if (borderShader != null)
			{
				Material borderMaterial = new Material(borderShader);
				borderMaterial.color = borderColor;
				
				// Set URP-specific properties if using URP shader
				if (borderShader.name.Contains("Universal Render Pipeline"))
				{
					borderMaterial.SetFloat("_Surface", 0); // Opaque
					borderMaterial.SetFloat("_Blend", 0);
				}
				
				// Enable double-sided rendering
				borderMaterial.SetInt("_Cull", 0); // Cull Off = double-sided
				
				return borderMaterial;
			}
			
			Debug.LogError("QuadBorder: Could not find suitable shader for border material!");
			return null;
		}
		
		private void OnEnable()
		{
			// Enable border when quad is enabled
			if (borderContainer != null)
			{
				borderContainer.SetActive(true);
			}
			else if (Application.isPlaying)
			{
				// Create border if it doesn't exist yet (e.g., when quad is enabled after being disabled)
				CreateBorder();
			}
		}
		
		private void OnDisable()
		{
			// Hide border when quad is disabled (e.g., when monitor is removed)
			// This ensures the border disappears when you remove a monitor
			if (borderContainer != null)
			{
				borderContainer.SetActive(false);
			}
		}
		
		private void OnDestroy()
		{
			// Clean up border when quad is destroyed
			if (borderContainer != null)
			{
				DestroyImmediate(borderContainer);
			}
		}
		
#if UNITY_EDITOR
		/// <summary>
		/// Editor helper: Update border when values change in inspector
		/// </summary>
		private void OnValidate()
		{
			// Only recreate border if we're in play mode or if border already exists
			if (Application.isPlaying && borderContainer != null)
			{
				CreateBorder();
			}
		}
#endif
	}
}

