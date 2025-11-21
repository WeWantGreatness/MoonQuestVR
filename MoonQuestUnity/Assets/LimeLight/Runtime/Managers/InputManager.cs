using UnityEngine;
using PCP.LibLime;

/// <summary>
/// InputManager: Handles VR controller button inputs for stream control.
/// 
/// WHAT IT DOES:
/// - Maps controller buttons to mouse clicks and keyboard shortcuts
/// - Handles scroll wheel via right stick
/// - Menu button controls for spawning/removing monitors
/// - Does NOT handle mouse position (that's StreamPointer's job)
/// 
/// CONTROLS:
/// - Right Trigger = Left Click
/// - Right Button B = Right Click  
/// - Right Stick Click = Middle Click
/// - Right Stick Movement = Scroll Wheel
/// - Left Trigger = Left Click (alternative)
/// - Left Button X = Launch Onboard (Super+O)
/// - Menu Button = Spawn/Remove monitors
/// </summary>
public class InputManager : MonoBehaviour
{
    [Header("Dependencies")]
    private StreamManager streamManager;

    [Header("Settings")]
    public float scrollSpeed = 1.0f;
    public float scrollDeadzone = 0.2f;

    // State tracking to prevent rapid-fire clicks
    private bool r3WasPressed = false;

    void Awake()
    {
        // Get StreamManager from the same GameObject (LimePluginManager)
        streamManager = GetComponent<StreamManager>();
        if (streamManager == null)
        {
            Debug.LogError("InputManager: StreamManager not found on same GameObject!");
        }
    }

    void Update()
    {
        if (streamManager == null) return;

        HandleRightController();
        HandleLeftController();
    }

    void HandleRightController()
    {
        // 1. Left Click (Right Trigger)
        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
        {
            streamManager.SendMouseButton(1, true); // 1 = Left Click Down
        }
        else if (OVRInput.GetUp(OVRInput.Button.SecondaryIndexTrigger))
        {
            streamManager.SendMouseButton(1, false); // Left Click Up
        }

        // 2. Right Click (Button B)
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            streamManager.SendMouseButton(3, true); // 3 = Right Click Down
        }
        else if (OVRInput.GetUp(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            streamManager.SendMouseButton(3, false); // Right Click Up
        }

        // 3. Middle Click (R3 - Right Stick Click)
        bool r3Pressed = OVRInput.Get(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch);
        if (r3Pressed && !r3WasPressed)
        {
            streamManager.SendMouseButton(2, true); // 2 = Middle Click Down
        }
        else if (!r3Pressed && r3WasPressed)
        {
            streamManager.SendMouseButton(2, false); // Middle Click Up
        }
        r3WasPressed = r3Pressed;

        // 4. Scroll Wheel (Right Stick Movement)
        Vector2 scroll = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
        
        // Vertical Scroll
        if (Mathf.Abs(scroll.y) > scrollDeadzone)
        {
            // Convert stick value (-1 to 1) to scroll "clicks"
            // Positive Y (Up) = Scroll Up. Negative Y (Down) = Scroll Down.
            // Multiply by scrollSpeed to control sensitivity (default 1.0 = 1 click per unit)
            int scrollAmount = Mathf.RoundToInt(scroll.y * scrollSpeed);
            if (scrollAmount != 0)
            {
                streamManager.SendMouseScroll(scrollAmount);
            }
        }
    }

    void HandleLeftController()
    {
        // 5. Left Click (Left Trigger) - Same as Right Trigger
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger))
        {
            streamManager.SendMouseButton(1, true); // 1 = Left Click Down
        }
        else if (OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger))
        {
            streamManager.SendMouseButton(1, false); // Left Click Up
        }

        // 6. Launch Onboard (Button X) - Sends Super+O keyboard shortcut
        if (OVRInput.GetDown(OVRInput.Button.Three, OVRInput.Controller.LTouch))
        {
            // Super key (Windows key) = 0x5B (VK_LWIN)
            // O key = 0x4F
            // MODIFIER_META = 0x08
            const int VK_LWIN = 0x5B;  // Left Windows/Super key
            const int VK_O = 0x4F;     // O key
            const int MODIFIER_META = 0x08;  // Super/Windows modifier
            
            // Press Super+O to launch onboard
            // Note: Super key is pressed without modifier flag (it gets handled specially)
            streamManager.SendKeyboardInput(VK_LWIN, 0);  // Super down
            streamManager.SendKeyboardInputWithModifier(VK_O, 0, MODIFIER_META);    // O down (with Super)
            streamManager.SendKeyboardInputWithModifier(VK_O, 1, MODIFIER_META);    // O up (with Super)
            streamManager.SendKeyboardInput(VK_LWIN, 1);  // Super up
        }
        
        // 7. Menu Button - Spawn/Remove Monitors
        HandleMenuButton();
    }
    
    [Header("Menu Button Settings")]
    public Transform rightHandAnchor; // Drag RightControllerAnchor here
    
    private float menuButtonHoldTime = 0f;
    private const float REMOVE_HOLD_TIME = 2.0f;
    private bool wasMenuPressed = false;
    private bool hasRemovedThisHold = false; // Prevent multiple removals per hold
    
    void HandleMenuButton()
    {
        bool menuPressed = OVRInput.Get(OVRInput.Button.Start);
        
        if (menuPressed)
        {
            menuButtonHoldTime += Time.deltaTime;
            
            // On initial press (click), spawn a monitor
            if (!wasMenuPressed)
            {
                streamManager.SpawnNextMonitor();
                Debug.Log("InputManager: Spawned next monitor");
                hasRemovedThisHold = false;
            }
            
            // If held for 2+ seconds, remove the monitor under pointer
            if (menuButtonHoldTime >= REMOVE_HOLD_TIME && !hasRemovedThisHold)
            {
                // Raycast from controller to find monitor
                if (rightHandAnchor != null)
                {
                    Ray ray = new Ray(rightHandAnchor.position, rightHandAnchor.forward);
                    GameObject monitorToRemove = streamManager.GetMonitorUnderPointer(ray);
                    
                    if (monitorToRemove != null)
                    {
                        streamManager.RemoveMonitor(monitorToRemove);
                        Debug.Log("InputManager: Removed monitor: " + monitorToRemove.name);
                        hasRemovedThisHold = true; // Prevent multiple removals per hold
                    }
                }
            }
        }
        else
        {
            menuButtonHoldTime = 0f;
            hasRemovedThisHold = false;
        }
        
        wasMenuPressed = menuPressed;
    }
}