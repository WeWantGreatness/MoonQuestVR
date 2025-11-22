using UnityEngine;
using System.Collections;
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

    // State tracking to prevent rapid-fire clicks
    private bool r3WasPressed = false;
    
    // State tracking for horizontal scroll coroutine
    private bool isSendingHorizontalScroll = false;

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

        // 4. Scroll Wheel (Right Stick Movement) - Vertical and Horizontal
        Vector2 scroll = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
        
        // Vertical Scroll (Up/Down)
        if (Mathf.Abs(scroll.y) > 0.01f) // Removed deadzone - minimal threshold just to avoid drift
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
        
        // Horizontal Scroll (Left/Right) - Use Shift+Scroll with proper timing
        // Many Linux apps interpret Shift+Scroll as horizontal scroll
        if (Mathf.Abs(scroll.x) > 0.01f && !isSendingHorizontalScroll) // Removed deadzone, prevent overlapping coroutines
        {
            // INVERTED: Right (+X) = Scroll Left, Left (-X) = Scroll Right
            // Negate scroll.x to fix direction
            int horizontalScroll = Mathf.RoundToInt(-scroll.x * scrollSpeed);
            if (horizontalScroll != 0)
            {
                StartCoroutine(SendHorizontalScroll(horizontalScroll));
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
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch))
        {
            // Scancode 0x5B (91 decimal) = Super/Windows key (from showkey: 0xe0 0x5b)
            // O key = 0x4F (79 decimal)
            const int VK_LWIN = 0x5B;  // Super key - matches scancode 0x5b
            const int VK_O = 0x4F;     // O key
            const int SS_KBE_FLAG_NON_NORMALIZED = 0x01;  // Send raw scancode to Linux
            
            Debug.Log("InputManager: Sending Super+O shortcut");
            
            // Simple sequence: Super down -> O down -> O up -> Super up
            // No delays, no modifiers - just press Super, then press O
            streamManager.SendKeyboardInputWithModifierAndFlags(VK_LWIN, 0, 0, SS_KBE_FLAG_NON_NORMALIZED);  // Super down
            streamManager.SendKeyboardInput(VK_O, 0);  // O down
            streamManager.SendKeyboardInput(VK_O, 1);  // O up
            streamManager.SendKeyboardInputWithModifierAndFlags(VK_LWIN, 1, 0, SS_KBE_FLAG_NON_NORMALIZED);  // Super up
            
            Debug.Log("InputManager: Super+O sent");
        }
        
        // 7. Menu Button - Spawn/Remove Monitors
        HandleMenuButton();
    }
    
    [Header("Menu Button Settings")]
    public Transform rightHandAnchor; // Drag RightControllerAnchor here
    
    private bool wasMenuPressed = false;
    
    void HandleMenuButton()
    {
        bool menuPressed = OVRInput.Get(OVRInput.Button.Start);
        bool rightGripHeld = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);
        
        // On menu button press
        if (menuPressed && !wasMenuPressed)
        {
            // If right grip is held, remove monitor under pointer
            if (rightGripHeld)
            {
                // Raycast from controller to find monitor
                if (rightHandAnchor != null)
                {
                    Ray ray = new Ray(rightHandAnchor.position, rightHandAnchor.forward);
                    GameObject monitorToRemove = streamManager.GetMonitorUnderPointer(ray);
                    
                    if (monitorToRemove != null)
                    {
                        streamManager.RemoveMonitor(monitorToRemove);
                    }
                }
            }
            else
            {
                // Otherwise, spawn a monitor
                streamManager.SpawnNextMonitor();
            }
        }
        
        wasMenuPressed = menuPressed;
    }
    
    
    // Coroutine to send horizontal scroll via Shift+Scroll with proper timing
    IEnumerator SendHorizontalScroll(int scrollAmount)
    {
        isSendingHorizontalScroll = true; // Prevent overlapping coroutines
        
        const int VK_LSHIFT = 0xA0;  // Left Shift key
        const int MODIFIER_SHIFT = 0x01;
        
        Debug.Log($"InputManager: Sending horizontal scroll via Shift+Scroll: {scrollAmount}");
        
        // 1. Press Shift down
        streamManager.SendKeyboardInputWithModifier(VK_LSHIFT, 0, MODIFIER_SHIFT);
        yield return new WaitForSeconds(0.02f); // Small delay for OS to register Shift
        
        // 2. Send scroll while Shift is held
        streamManager.SendMouseScroll(scrollAmount);
        yield return new WaitForSeconds(0.02f);
        
        // 3. Release Shift
        streamManager.SendKeyboardInputWithModifier(VK_LSHIFT, 1, MODIFIER_SHIFT);
        
        yield return new WaitForSeconds(0.05f); // Wait before allowing another scroll
        
        isSendingHorizontalScroll = false; // Allow next scroll
        Debug.Log("InputManager: Horizontal scroll sent");
    }
}