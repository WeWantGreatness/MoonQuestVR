using UnityEngine;

/// <summary>
/// ScreenManipulator: Handles grabbing, moving, resizing, and curving screens in VR.
/// 
/// WHAT IT DOES:
/// - GRAB/MOVE: Grip button to grab screen by pointing at it (like StreamPointer)
/// - RESIZE: While grabbed, analog stick UP/DOWN resizes the screen
/// - CURVE: While grabbed, analog stick LEFT/RIGHT adjusts curvature (radius)
/// 
/// CONTROLS:
/// - Grip Button = Grab/Release screen (only grabs the quad you're pointing at)
/// - Move Controller = Move screen position
/// - Analog Stick Up/Down = Resize screen
/// - Analog Stick Left/Right = Adjust curvature (curveSpeed setting)
/// </summary>
public class ScreenManipulator : MonoBehaviour
{
    [Header("Settings")]
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;
    public float moveSpeed = 2.0f;
    public float resizeSpeed = 0.5f;
    [Tooltip("Speed of curvature adjustment when moving analog stick left/right while grabbed")]
    public float curveSpeed = 5.0f;
    [Tooltip("Max distance for raycast grab detection")]
    public float maxGrabDistance = 100f;

    private CurvedScreen screen;
    private bool isGrabbing = false;
    private Vector3 lastControllerPos;
    private Quaternion lastControllerRot;
    
    // Static lock to prevent moving multiple objects at once
    private static GameObject activeGrabber = null;
    private Collider screenCollider; // Collider for raycast detection
    
    /// <summary>
    /// Public method to clear the grab state. Called when a monitor is removed.
    /// </summary>
    public static void ClearGrabState()
    {
        if (activeGrabber != null)
        {
            ScreenManipulator manipulator = activeGrabber.GetComponent<ScreenManipulator>();
            if (manipulator != null)
            {
                manipulator.isGrabbing = false;
            }
            activeGrabber = null;
            Debug.Log("ScreenManipulator: Cleared grab state");
        }
    }

    void Start() 
    { 
        screen = GetComponent<CurvedScreen>();
        screenCollider = GetComponent<Collider>(); // Get BoxCollider or MeshCollider
        // Only enable manipulation if CurvedScreen is present (prevents issues with normal quads)
        if (screen == null)
        {
            enabled = false; // Disable this script if no CurvedScreen component
            Debug.LogWarning("ScreenManipulator: No CurvedScreen component found. Disabling ScreenManipulator.");
        }
        if (screenCollider == null)
        {
            Debug.LogWarning("ScreenManipulator: No Collider found on GameObject. Grab detection may not work.");
        }
    }

    /// <summary>
    /// Check if the controller is pointing at this screen using raycast (like StreamPointer does)
    /// Uses the same raycast logic as StreamPointer to detect which quad you're pointing at
    /// </summary>
    private bool IsControllerPointingAtScreen()
    {
        if (screenCollider == null) return false;
        
        // Get controller transform - try to find it like StreamPointer does
        Transform controllerTransform = null;
        
        // Try multiple ways to find the controller anchor
        if (controller == OVRInput.Controller.RTouch)
        {
            // Method 1: Find by name
            GameObject rightHand = GameObject.Find("RightHandAnchor");
            if (rightHand != null)
            {
                controllerTransform = rightHand.transform;
            }
            
            // Method 2: Find via OVRCameraRig
            if (controllerTransform == null)
            {
                OVRCameraRig cameraRig = FindObjectOfType<OVRCameraRig>();
                if (cameraRig != null)
                {
                    // Check if cameraRig has rightHandAnchor property
                    var rightHandProperty = cameraRig.GetType().GetField("rightHandAnchor");
                    if (rightHandProperty != null)
                    {
                        controllerTransform = rightHandProperty.GetValue(cameraRig) as Transform;
                    }
                }
            }
        }
        else if (controller == OVRInput.Controller.LTouch)
        {
            GameObject leftHand = GameObject.Find("LeftHandAnchor");
            if (leftHand != null)
            {
                controllerTransform = leftHand.transform;
            }
            
            if (controllerTransform == null)
            {
                OVRCameraRig cameraRig = FindObjectOfType<OVRCameraRig>();
                if (cameraRig != null)
                {
                    var leftHandProperty = cameraRig.GetType().GetField("leftHandAnchor");
                    if (leftHandProperty != null)
                    {
                        controllerTransform = leftHandProperty.GetValue(cameraRig) as Transform;
                    }
                }
            }
        }
        
        // Create ray from controller
        Ray ray;
        if (controllerTransform != null)
        {
            // Use transform position and forward (like StreamPointer does)
            ray = new Ray(controllerTransform.position, controllerTransform.forward);
        }
        else
        {
            // Fallback: use OVRInput directly
            Vector3 controllerPos = OVRInput.GetLocalControllerPosition(controller);
            Quaternion controllerRot = OVRInput.GetLocalControllerRotation(controller);
            ray = new Ray(controllerPos, controllerRot * Vector3.forward);
        }
        
        // Raycast to see if we hit this screen's collider
        RaycastHit hit;
        if (screenCollider.Raycast(ray, out hit, maxGrabDistance))
        {
            // Make sure we hit THIS specific screen (not another one)
            return hit.collider == screenCollider;
        }
        
        return false;
    }

    void Update()
    {
        // Check if activeGrabber still exists (in case it was destroyed/disabled)
        if (activeGrabber != null && !activeGrabber.activeInHierarchy)
        {
            activeGrabber = null;
            isGrabbing = false;
        }
        
        // Only process grab logic if this GameObject is active
        if (!gameObject.activeInHierarchy)
        {
            if (isGrabbing && activeGrabber == gameObject)
            {
                isGrabbing = false;
                activeGrabber = null;
            }
            return;
        }
        
        // 1. Grab Start - Only grab if controller is pointing at THIS screen
        if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, controller))
        {
            if (activeGrabber == null)
            {
                // Check if controller is pointing at this specific screen (like StreamPointer detection)
                if (IsControllerPointingAtScreen())
                {
                    isGrabbing = true;
                    activeGrabber = gameObject;
                    lastControllerPos = OVRInput.GetLocalControllerPosition(controller);
                    lastControllerRot = OVRInput.GetLocalControllerRotation(controller);
                    Debug.Log("ScreenManipulator: Grabbed " + gameObject.name + " (pointing detection)");
                }
            }
        }
        // 2. Grab End
        else if (OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, controller))
        {
            if (isGrabbing && activeGrabber == gameObject)
            {
                isGrabbing = false;
                activeGrabber = null;
                Debug.Log("ScreenManipulator: Released " + gameObject.name);
            }
        }

        // 3. Manipulation
        if (isGrabbing && activeGrabber == gameObject)
        {
            // Get controller position and stick input
            Vector3 currentPos = OVRInput.GetLocalControllerPosition(controller);
            Quaternion currentRot = OVRInput.GetLocalControllerRotation(controller);
            Vector2 rawStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, controller);
            
            // Apply deadzone to prevent accidental triggers when stick is slightly off-center
            // Only process stick input if individual axis is beyond the deadzone threshold
            Vector2 stick = rawStick;
            float threshold = 0.1f; // Hardcoded small threshold since we removed the variable
            
            if (Mathf.Abs(stick.x) < threshold)
            {
                stick.x = 0f; 
            }
            if (Mathf.Abs(stick.y) < threshold)
            {
                stick.y = 0f; 
            }
            
            // Check if we're adjusting curvature or resizing (with deadzone applied)
            // Use magnitude comparison to prioritize which action to take
            // If X input is dominant, adjust curve. If Y input is dominant, resize.
            float absX = Mathf.Abs(stick.x);
            float absY = Mathf.Abs(stick.y);
            bool hasXInput = absX >= threshold;
            bool hasYInput = absY >= threshold;
            
            // Determine which action to take based on which input is stronger
            bool isAdjustingCurve = screen != null && hasXInput && absX >= absY;
            bool isResizing = screen != null && hasYInput && absY > absX;

            // Joystick Adjustments (Resize/Curve) - Handle FIRST
            // IMPORTANT: These are mutually exclusive - resizing should NOT happen when adjusting curvature
            bool didAdjustCurveOrResize = false;
            if (screen != null)
            {
                // Adjust curvature with left/right stick (ONLY affects curvature, NOT scale or position)
                if (isAdjustingCurve)
                {
                    // Invert stick X so Right (+X) decreases radius (More Curved), Left (-X) increases radius (Flatter)
                    float oldRadius = screen.radius;
                    screen.radius -= stick.x * curveSpeed * Time.deltaTime;
                    screen.radius = Mathf.Clamp(screen.radius, 1.0f, 500.0f);
                    
                    // Only regenerate mesh if radius actually changed
                    if (Mathf.Abs(screen.radius - oldRadius) > 0.001f)
                    {
                        screen.GenerateMesh();
                        Debug.Log($"ScreenManipulator: Adjusted curvature - radius={screen.radius:F2}, stick.x={stick.x:F2}");
                    }
                    didAdjustCurveOrResize = true;
                }
                // Resize with up/down stick (ONLY when NOT adjusting curvature)
                else if (isResizing)
                {
                    float scale = 1.0f + (stick.y * resizeSpeed * Time.deltaTime);
                    screen.width *= scale;
                    screen.height *= scale;
                    screen.GenerateMesh();
                    Debug.Log($"ScreenManipulator: Resized screen - width={screen.width:F2}, height={screen.height:F2}, stick.y={stick.y:F2}");
                    didAdjustCurveOrResize = true;
                }
            }
            
            // Move & Rotate (ONLY if NOT adjusting curvature or resize)
            // CRITICAL: Update lastControllerPos/Rot when adjusting curve/resize to prevent position drift
            // This prevents the screen from moving forward/backward when adjusting curvature
            if (didAdjustCurveOrResize)
            {
                // Update lastControllerPos to CURRENT position to prevent position drift
                // This "freezes" the position reference while adjusting curve/resize
                lastControllerPos = currentPos;
                lastControllerRot = currentRot;
            }
            else
            {
                // Move & Rotate only when NOT adjusting curve/resize
                // This applies position/rotation changes only when physically moving the controller
                transform.position += (currentPos - lastControllerPos) * moveSpeed;
                transform.rotation = (currentRot * Quaternion.Inverse(lastControllerRot)) * transform.rotation;
                
                lastControllerPos = currentPos;
                lastControllerRot = currentRot;
    }
}
    }
}
