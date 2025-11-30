using UnityEngine;
using PCP.LibLime;

/// <summary>
/// StreamPointer: Handles VR controller-based mouse input for stream displays.
/// 
/// WHAT IT DOES:
/// - Uses the RIGHT HAND CONTROLLER (RightHandAnchor) to raycast at quads
/// - Converts hit UV coordinates to desktop mouse position
/// - Sends mouse clicks and movement to StreamManager
/// - ONLY works with controller pointing - NOT headset tracking
/// 
/// SETUP:
/// - controllerTransform: MUST be RightHandAnchor (NOT headset/camera)
/// - screenCollider: Collider on the quad GameObject (BoxCollider or MeshCollider)
/// - streamManager: Reference to StreamManager component
/// </summary>
public class StreamPointer : MonoBehaviour
{
    [Header("References")]
    public Transform controllerTransform; // MUST be assigned (RightHandAnchor)
    public StreamManager streamManager;   // Drag StreamManager here
    public Collider screenCollider;       // Collider (BoxCollider or MeshCollider) on the Quad GameObject
    
    [Header("Visual Feedback")]
    public GameObject cursorVisual;       // Drag a small Sphere GameObject here (optional)
    
    [Header("Monitor Configuration")]
    [Tooltip("Total width of the ENTIRE desktop being streamed (e.g., 7680 for 4x 1920x1200)")]
    public float totalStreamWidth = 7680f;
    [Tooltip("Total height of the ENTIRE desktop being streamed (e.g., 1200)")]
    public float totalStreamHeight = 1200f;
    [Tooltip("Width of THIS specific monitor (e.g., 1920)")]
    public float thisMonitorWidth = 1920f;
    [Tooltip("Height of THIS specific monitor (e.g., 1200)")]
    public float thisMonitorHeight = 1200f;
    [Tooltip("X Position where this monitor starts (e.g., 0 for Left, 1920 for Right)")]
    public float thisMonitorXOffset = 0f;
    [Tooltip("Y Position where this monitor starts (e.g., 0 for Bottom, 1080 for Top)")]
    public float thisMonitorYOffset = 0f;
    
    [Header("Settings")]
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;
    public OVRInput.Button clickButton = OVRInput.Button.PrimaryIndexTrigger;
    [Tooltip("If true, invert the local Y UV before mapping to desktop space (for flipped Android textures).")]
    public bool isTextureFlippedY = false;
    
    private bool wasPressed = false;
    private static StreamPointer activePointer = null;

    void Update()
    {
        // FAILSAFE: If these are missing, the script stops silently.
        if (streamManager == null || screenCollider == null || controllerTransform == null) 
        {
            Debug.LogWarning("StreamPointer: Missing References! Check Inspector.");
            return;
        }

        // Ensure controllerTransform is actually a controller, not headset
        // Validate that it's the right controller transform by checking if it's active and valid
        if (!controllerTransform.gameObject.activeInHierarchy)
        {
            return; // Controller not active, don't do anything
        }
        
        // Ensure we're only using the specified controller for input
        // Check if the controller button is available before proceeding
        bool controllerAvailable = false;
        if (controller == OVRInput.Controller.RTouch)
        {
            controllerAvailable = OVRInput.IsControllerConnected(OVRInput.Controller.RTouch);
        }
        else if (controller == OVRInput.Controller.LTouch)
        {
            controllerAvailable = OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);
        }
        
        if (!controllerAvailable)
        {
            // Controller not connected, hide cursor and don't proceed
            if (cursorVisual != null) cursorVisual.SetActive(false);
            if (activePointer == this) activePointer = null;
            return;
        }

        Ray ray = new Ray(controllerTransform.position, controllerTransform.forward);
        RaycastHit hit;

        // Check if we hit the RawImage collider
        if (screenCollider.Raycast(ray, out hit, 100f))
        {
            // 1. Show the Cursor Visual at the hit point
            if (cursorVisual != null)
            {
                cursorVisual.SetActive(true);
                cursorVisual.transform.position = hit.point;
                // Optional: Rotate flat against screen
                cursorVisual.transform.rotation = Quaternion.LookRotation(hit.normal); 
            }

            // 2. Handle Mouse Movement Logic
            if (activePointer == null || activePointer == this || activePointer.gameObject.activeInHierarchy == false)
            {
                activePointer = this;

                // --- THE MATH MAGIC ---
                // 1. Get local UV (0.0 to 1.0) on this specific Quad
                float localUvX = hit.textureCoord.x;
                float localUvY = hit.textureCoord.y;
                if (isTextureFlippedY)
                {
                    // If the underlying texture is flipped in Y, adjust the sampled UV
                    localUvY = 1.0f - localUvY;
                }

                // 2. Calculate how much of the TOTAL stream this monitor takes up
                float widthRatio = thisMonitorWidth / totalStreamWidth;
                float heightRatio = thisMonitorHeight / totalStreamHeight;

                // 3. Calculate the 'Start' percentage for this monitor
                float startOffsetRatioX = thisMonitorXOffset / totalStreamWidth;
                float startOffsetRatioY = thisMonitorYOffset / totalStreamHeight;

                // 4. Combine to get the Global UV (0.0 to 1.0 across the WHOLE desktop)
                float globalUvX = startOffsetRatioX + (localUvX * widthRatio);
                
                // For Y, Unity UV (0,0) is Bottom-Left, but we need to account for monitor position
                // Invert Y if needed (Unity UV starts at bottom, screens start at top)
                float globalUvY = startOffsetRatioY + (localUvY * heightRatio);

                // 5. Send Global UVs to StreamManager
                streamManager.SendMousePosition(globalUvX, globalUvY);

                // 3. Handle Clicking
                bool isPressed = OVRInput.Get(clickButton, controller);

                if (isPressed && !wasPressed)
                {
                    streamManager.SendMouseButton(1, true); // Left Click Down
                }
                else if (!isPressed && wasPressed)
                {
                    streamManager.SendMouseButton(1, false); // Left Click Up
                }

                wasPressed = isPressed;
            }
        }
        else
        {
            // Hide cursor if looking away
            if (cursorVisual != null) cursorVisual.SetActive(false);
            if (activePointer == this) activePointer = null;
        }
    }
}
