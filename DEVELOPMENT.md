Development notes for MoonQuest VR fork

Purpose
-------
This document provides developer-focused instructions: how to build the `liblime` Android AAR, how to test locally in Unity, notes about the plugin, and CI details so the AAR can be built automatically.

Quick overall summary
---------------------
- The `limelight_plugin` directory holds the Android source for the liblime plugin.
- The Unity plugin AAR is built at `limelight_plugin/liblime/build/outputs/aar/liblime-release.aar`.
- The Unity project expects the AAR to live under `MoonQuestUnity/Assets/Plugins/Android/liblime-release.aar`.

Prerequisites
-------------
- JDK 11
- Android SDK (platform 32) and Android NDK matching `limelight_plugin/liblime/build.gradle` `ndkVersion` (default `26.2.11394342`).
- Gradle wrapper is available (we use the wrapper included in `limelight_plugin`).

How to build locally
--------------------
1. Set up Android SDK/NDK. On Ubuntu you can use `sdkmanager` and `ndk` from the Android SDK.
2. From project root:

```bash
cd limelight_plugin
./gradlew :liblime:assembleRelease
```

3. Verify the AAR exists:

```bash
ls liblime/build/outputs/aar/liblime-release.aar
```

4. Copy the AAR into the Unity project (for manual testing):

```bash
cp liblime/build/outputs/aar/liblime-release.aar ../MoonQuestUnity/Assets/Plugins/Android/
```

5. Open Unity (MoonQuestUnity) and reimport all assets (Assets → Reimport All) to ensure fresh timestamps and Unity's plugin refresh is done.

Why `APP_STL := c++_shared` is needed
------------------------------------
- The native `libshared-texture.so` depends on the shared C++ runtime `libc++_shared.so`. The Gradle/NDK build only includes that library if `APP_STL := c++_shared` is set in `Application.mk` (or equivalent setting).

Notes about debugging and plugin logs
------------------------------------
- This repository adds logging prefixes to the Unity side (`LIME:`) and to Java (`LimeLog`) to make it easier to grep. You can use the following command to watch these logs:

```bash
adb logcat -v time | grep -E "(LIME:|LimeLog|startapp_failed|UISTM|applist1|SharedTexture-JNI|UpdateSurface|GetTexturePtr|failed|timeout|exception|error)"
```

Submodule & nested repo
-----------------------
- `limelight_plugin/liblime/src/main/jni/moonlight-core/moonlight-common-c` is an embedded git repo; it's recommended to keep it as a submodule. If you clone this repository, run:

```bash
git submodule update --init --recursive
```

CI (GitHub Actions) to automatically build the AAR
-------------------------------------------------
We provide a workflow `.github/workflows/build-aar.yml` in this repo that will build the `liblime` AAR on every push to `development` and on pull requests. The built artifact is attached so you can download it from the Actions UI.

- How it works: the workflow runs `./gradlew :liblime:assembleRelease` on a Linux GitHub runner configured with the Android SDK & NDK, then uploads the output AAR as an artifact.
- If you want the AAR copied automatically to the Unity plugin folder inside the repo, we can add an additional step to commit and push the built artifact; but it's often preferable to download and manually copy during local testing to avoid committing binaries repeatedly.

What else you might want
------------------------
- Add a `build.sh` wrapper that sets environment variables and builds both the plugin and Unity project (useful in CI).
- Add a GitHub Actions job to attach the `.aar` to a `release` when you tag a release.

If you want, I can add a `build-aar.yml` GitHub Actions workflow and commit it for you next. Would you like that? 

# XR Plugin Management and OpenXR Compatibility

This project uses OpenGL ES for rendering the streaming quad. Switching to OpenXR in Unity may enable Vulkan-related features, which can cause incompatibility with the current script, leading to the quad not rendering or displaying properly.

To maintain compatibility and ensure the quad renders without issues:

### Recommended OpenXR Feature Groups
- **Meta XR**:
  - Enable: Meta XR Feature
  - Enable: Meta XR Foveation

- **Meta Quest**:
  - Enable: AR Anchors
  - Enable: AR Camera Pass Through
  - Enable: AR Plane Detection
  - Enable: AR Raycast
  - Enable: AR Session
  - Enable: Display Utilities

### Important Warning
Unity may prompt to enable additional features for project validation. Enabling any features beyond the ones listed above is done at your own risk, as they may introduce incompatibilities with the current rendering setup (e.g., Vulkan conflicts with OpenGL ES-based quad rendering).

If you encounter rendering issues after enabling OpenXR features, revert to the listed configuration or disable OpenXR entirely.

# Recent Changes

## Dynamic Stream Resolution Handling and Multi-Monitor Setup (November 2024)

### Overview
Implemented dynamic resolution tracking to eliminate letterboxing issues. The stream texture now automatically adjusts to match the actual resolution being sent by Sunshine, regardless of the requested resolution. Additionally, added support for displaying a single wide desktop stream across multiple virtual monitors (quads) in VR using UV cropping.

### Key Changes

#### Unity C# Side (`StreamManager.cs`)
- **Dynamic Texture Resolution**: Added `mLastTexWidth` and `mLastTexHeight` to track resolution changes
- **Automatic Texture Recreation**: `CreateStreamTexture()` method now recreates the texture when resolution changes are detected
- **Multi-Quad Support**: Changed from single `MeshRenderer` to `List<MeshRenderer> quadRenderers` for 4-monitor setup
- **UV Cropping**: Added `SetupQuadMonitors()` method that applies UV offset and scale to each quad to display a portion of the wide desktop stream
  - Each quad displays 1/4 of the total stream (e.g., 1920x1080 out of 7680x1080)
  - UV offsets: DP-2 at 0, HDMI-0 at 1920, DP-0 at 3840, DP-4 at 5760
- **Resolution Monitoring**: `UpdateFrame()` now checks for resolution changes every frame and updates the texture accordingly
- **Input Methods**: Added `SendMousePosition()`, `SendMouseButton()`, `SendKeyboardInput()`, `SendMouseScroll()` methods for stream control

#### Java Side (`StreamPlugin.java`)
- **Negotiated Resolution Tracking**: Added `mActualStreamWidth` and `mActualStreamHeight` to store the actual stream resolution from Sunshine
- **Connection Callback Updates**: `connectionStarted()` now updates to the negotiated resolution and triggers renderer updates
- **Dynamic Resolution Query**: `GetResolution()` now returns the actual negotiated resolution, not a hardcoded value
- **Input Methods**: Added `MoveMouse()`, `MouseButton()`, `SendKeyboardInput()`, `SendKeyboardInputWithModifier()`, `SendMouseScroll()` methods for Unity to call

#### Renderer Updates (`StreamRenderer.java`)
- **Dynamic Surface Texture**: Added `updateSurfaceTextureBufferSize()` method to update decoder output surface size
- **Resolution Update Method**: `SetTextureResolution()` now properly updates internal texture dimensions
- **Resize Handling**: Added `requestResize()` to trigger hardware buffer recreation when resolution changes

#### Connection Updates (`NvConnection.java`)
- **Resolution Getters**: Added public `getNegotiatedWidth()` and `getNegotiatedHeight()` methods to expose negotiated resolution

#### Configuration (`PluginManager.java`)
- **Multi-Monitor Support**: Updated default resolution to `7680x1080` to support 4-monitor setups (4 × 1920x1080 arranged horizontally)

### New VR Interaction Scripts

#### `CurvedScreen.cs`
- **Purpose**: Generates a curved mesh for a screen that can be dynamically adjusted
- **Features**:
  - Automatically syncs dimensions from Unity Transform scale on startup (prevents scale jumps)
  - Adjustable curvature via `radius` parameter (smaller = more curved, larger = flatter)
  - Configurable segment count for mesh smoothness
  - Proper triangle winding and UV mapping for correct texture orientation

#### `ScreenManipulator.cs`
- **Purpose**: Handles VR controller interaction for grabbing, moving, resizing, and curving screens
- **Controls**:
  - **Grip Button**: Grab/release screen (only grabs the quad you're pointing at via raycast)
  - **Move Controller**: Move screen position and rotation
  - **Analog Stick Up/Down**: Resize screen
  - **Analog Stick Left/Right**: Adjust curvature (Right = curve in, Left = curve out)
- **Features**:
  - Raycast-based grab detection (only grabs the screen you're pointing at)
  - Mutually exclusive resize/curve adjustments (prevents accidental scale changes)
  - Position drift prevention when adjusting curve/resize
  - Only works on GameObjects with `CurvedScreen` component

#### `StreamPointer.cs`
- **Purpose**: Handles VR controller-based mouse input for stream displays
- **Features**:
  - Raycasts from controller to detect which monitor quad is being pointed at
  - Converts hit UV coordinates to desktop mouse position
  - Supports multi-monitor setups with configurable offsets
  - Sends mouse clicks and movement to `StreamManager`
  - Only uses controller input (validates controller is connected and active)

#### `InputManager.cs`
- **Purpose**: Handles general VR controller button inputs for stream control
- **Features**: Manages button mappings for stream-related actions

### Technical Details

The resolution flow works as follows:
1. **Request Phase**: App requests a resolution (currently 7680x1080 for 4 monitors)
2. **Negotiation Phase**: Sunshine negotiates and may return a different resolution based on PC output
3. **Update Phase**: `connectionStarted()` callback receives the negotiated resolution
4. **Sync Phase**: All components (surface texture, hardware buffer, Unity texture) are updated to match
5. **Monitoring Phase**: Unity continuously checks for resolution changes and recreates textures as needed

Multi-monitor UV cropping works as follows:
1. **Single Wide Texture**: One `Texture2D` contains the entire desktop stream (e.g., 7680x1080)
2. **UV Mapping**: Each quad's material uses `mainTextureOffset` and `mainTextureScale` to display a portion
3. **Calculation**: UV offset = (monitor X position) / (total width), UV scale = (monitor width) / (total width)
4. **Result**: Each quad shows 1/4 of the desktop, arranged horizontally in VR space

### Benefits
- No more letterboxing regardless of PC output resolution
- Supports multi-monitor setups with automatic UV cropping
- Automatically adapts to resolution changes without manual intervention
- VR interaction for screen manipulation (grab, move, resize, curve)
- Controller-based mouse input for stream control
- Works with any monitor configuration (single, dual, quad monitor setups)

### Testing Notes
- Resolution changes are logged with `LimeLog.info()` for debugging
- Unity texture recreation is logged in `StreamManager` with debug messages
- Resolution queries should return the actual stream resolution, not the requested resolution
- CurvedScreen automatically syncs with Transform scale on startup to prevent scale jumps
- ScreenManipulator uses raycast detection to ensure only the pointed-at screen is grabbed

## Input Controls, Visual Quality, and Audio Improvements (December 2024)

### Overview
Major improvements to input handling, visual quality, audio stability, and user experience. Added horizontal scrolling, keyboard shortcuts, monitor management, pointer stabilization, quad borders, and comprehensive audio/visual optimizations.

### Input Controls Enhancements

#### Horizontal Scroll (`InputManager.cs`)
- **Implementation**: Right analog stick left/right movement now performs horizontal scrolling
- **Method**: Uses `Shift+MouseScroll` with proper timing via coroutine (`SendHorizontalScroll`)
- **Timing**: 0.02s delays between Shift down, scroll, and Shift up to ensure OS recognition
- **Direction Fix**: Inverted scroll direction (`-scroll.x`) to match expected behavior (left stick = left scroll)
- **Overlap Prevention**: `isSendingHorizontalScroll` flag prevents overlapping coroutines
- **Code Location**: `InputManager.cs` lines 105-116, 196-219

#### Super+O Keyboard Shortcut (`InputManager.cs`)
- **Purpose**: Launch Onboard virtual keyboard on Linux (Super+O shortcut)
- **Trigger**: Left controller X button (`OVRInput.Button.One, OVRInput.Controller.LTouch`)
- **Implementation**: 
  - Uses Linux scancode `0x5B` (91 decimal) for Super/Windows key
  - Uses `SS_KBE_FLAG_NON_NORMALIZED` flag to send raw scancode to Sunshine
  - Sequence: Super down → O down → O up → Super up (no delays, immediate)
- **Key Codes**:
  - `VK_LWIN = 0x5B` (Super key, matches Linux scancode `0xe0 0x5b`)
  - `VK_O = 0x4F` (O key, 79 decimal)
- **Code Location**: `InputManager.cs` lines 131-150, `StreamManager.cs` lines 357-363

#### Monitor Spawning and Removal (`InputManager.cs`, `StreamManager.cs`)
- **Spawn Monitor**: Menu button click spawns the next disabled monitor
- **Remove Monitor**: Right grip held + Menu button click removes the monitor under pointer
- **Implementation**:
  - `SpawnNextMonitor()`: Finds first disabled monitor in `quadRenderers` list and enables it
  - `RemoveMonitor()`: Calls `ScreenManipulator.ClearGrabState()` then disables the monitor (doesn't destroy)
  - Uses raycast from right controller to detect which monitor to remove
  - `GetMonitorUnderPointer()`: Raycasts from controller to find which monitor quad is being pointed at
- **Startup Behavior**: All quads disabled in `Awake()`, only first monitor enabled in `OnCreate()` when stream starts
- **Code Location**: `InputManager.cs` lines 152-207, `StreamManager.cs` lines 25-99, 390-450, 481-494

#### UI Toggle (`InputManager.cs`, `LimePluginManager.cs`)
- **Purpose**: Toggle the app list UI panel visibility after it hides on stream start
- **Trigger**: Right thumb rest held + Menu button press
- **Implementation**:
  - `ToggleUI()` in `LimePluginManager`: Toggles `mPanelCanvas` visibility and properly enables/disables raycast interaction
  - `ShowUI()` / `HideUI()`: Properly enable/disable UI with raycast handling
  - `IsUIVisible()`: Check if UI panel is currently visible
- **Behavior**:
  - Opens UI if closed, closes if open
  - Properly manages `GraphicRaycaster` component for interaction
  - UI hides automatically when stream starts, can be toggled back with thumb rest + menu
- **Code Location**: 
  - `InputManager.cs` lines 178-182, 209-222
  - `LimePluginManager.cs` lines 555-582

### Visual Quality Improvements

#### Render Scale (`[BuildingBlock] Camera Rig.prefab`)
- **Change**: Increased `maxRenderScale` from `1.0` to `1.5`
- **Purpose**: Improves text clarity and overall visual quality in VR
- **Impact**: Higher resolution rendering at the cost of performance
- **Code Location**: `MoonQuestUnity/Assets/PostSpace/Prefabss/[BuildingBlock] Camera Rig.prefab` line 18964

#### LOD Bias (`QualitySettings.asset`)
- **Change**: Increased `lodBias` from `0.4` to `2.0` for "Performant" quality setting
- **Purpose**: Ensures higher quality LODs are used, improving text clarity when not directly focused on quads
- **Code Location**: `MoonQuestUnity/ProjectSettings/QualitySettings.asset` line 33

#### Texture Filtering (`StreamManager.cs`)
- **Change**: Changed `mStreamTexture.filterMode` from `FilterMode.Trilinear` to `FilterMode.Bilinear`
- **Reason**: External textures (from Android decoder) do not support mipmaps, so Trilinear filtering was inappropriate
- **Impact**: Eliminates potential shimmering/blurriness from incorrect mipmap usage
- **Code Location**: `StreamManager.cs` line 141

#### Quad Border Outlines (`QuadBorder.cs` - NEW)
- **Purpose**: Adds sky-blue 3D border frame around monitor quads for visibility
- **Features**:
  - Creates 4 cube primitives (Top, Bottom, Left, Right edges) as a frame
  - Dynamically scales with quad dimensions (reads from `CurvedScreen` component or `MeshRenderer.bounds`)
  - Visible from all angles (double-sided, 3D cubes)
  - Sky-blue color (RGB 128, 204, 255) by default
  - Configurable width, depth, and color in Inspector
- **Behavior**:
  - Border container is a sibling (not child) of the quad, so it remains visible when quad is disabled
  - Automatically appears/disappears with monitor (syncs with quad's `SetActive()` state)
  - Updates position, rotation, and scale in `LateUpdate()` to match quad transform
- **Setup**: Automatically added to all quads if `StreamManager.enableQuadBorders = true`
- **Code Location**: `MoonQuestUnity/Assets/LimeLight/Runtime/Managers/QuadBorder.cs` (new file)

### Pointer Stabilization (`StreamPointer.cs`)
- **Purpose**: Reduces mouse jitter/shakiness when pointing at screens
- **Implementation**: Uses `Vector2.SmoothDamp` for smooth pointer movement
- **Features**:
  - Configurable `smoothingSpeed` (default 10f) - higher = smoother but more lag
  - Configurable `movementThreshold` (default 0.001f) - filters out micro-jitter
  - Aggressive dampening for movements below threshold to stop jitter
  - Resets smoothing state when not pointing at screen
- **Code Location**: `StreamPointer.cs` (Note: Implementation details in summary, actual code may vary)

### Audio Clipping Fixes

#### Unity Audio Manager (`AudioManager.asset`)
- **Change**: Increased `m_DSPBufferSize` from `1024` to `4096`
- **Purpose**: Larger buffer reduces audio underruns and clipping
- **Trade-off**: Slightly higher latency for better stability
- **Code Location**: `MoonQuestUnity/ProjectSettings/AudioManager.asset` line 12

#### Android Audio Renderer (`AndroidAudioRenderer.java`)
- **Buffer Size Increases**:
  - Small buffer attempts: `bytesPerFrame * 8` (was `* 2`)
  - Large buffer attempts: `bytesPerFrame * 10` (was `* 2`)
- **Low Latency Mode**: Explicitly disabled to force standard mode (low latency = smaller buffers = more clipping)
- **Impact**: Significantly larger buffers prevent audio underruns and clipping
- **Code Location**: `limelight_plugin/liblime/src/main/java/com/limelight/binding/audio/AndroidAudioRenderer.java` lines 128-171

### Stream Settings Updates

#### Default FPS (`PreferenceConfiguration.java`)
- **Change**: Increased from `60` to `90` FPS
- **Purpose**: Match Quest refresh rate for smoother streaming
- **Code Location**: `limelight_plugin/liblime/src/main/java/com/limelight/preferences/PreferenceConfiguration.java` line 42

#### Default Resolution (`PreferenceConfiguration.java`)
- **Change**: Updated from `1920X1080` to `7680X1080`
- **Purpose**: Support 4-monitor setups (4 × 1920x1080 arranged horizontally)
- **Code Location**: `limelight_plugin/liblime/src/main/java/com/limelight/preferences/PreferenceConfiguration.java` line 41

#### Display Refresh Rate (`StreamPlugin.java`)
- **Change**: Hardcoded refresh rate updated from `60` to `90` Hz
- **Purpose**: Match Quest 2/3 native refresh rate
- **Code Location**: `limelight_plugin/liblime/src/main/java/com/liblime/StreamPlugin.java` line 200

### ScreenManipulator Improvements (`ScreenManipulator.cs`)

#### ClearGrabState() Method
- **Purpose**: Public static method to reset grab state when monitors are removed
- **Usage**: Called by `StreamManager.RemoveMonitor()` to prevent stuck grab states
- **Implementation**: Clears `activeGrabber` static reference and resets `isGrabbing` flag
- **Code Location**: `ScreenManipulator.cs` lines 37-51

#### Curvature Adjustment Fix
- **Improvement**: Prioritizes X or Y axis input based on magnitude for more reliable triggering
- **Purpose**: Allows curvature adjustment even with slight diagonal stick movement
- **Code Location**: `ScreenManipulator.cs` (curvature logic in `Update()` method)

### Microphone Permissions (`LimePluginManager.cs`)
- **Implementation**: Added runtime microphone permission request for Android 6.0+
- **Method**: `WaitForPermissionsAndInitialize()` coroutine requests `UserAuthorization.Microphone`
- **Purpose**: Enables microphone access for Quest app (required for voice input, etc.)
- **Code Location**: `LimePluginManager.cs` (permission request in initialization coroutine)

### New Input Methods (`StreamManager.cs`, `StreamPlugin.java`)

#### SendKeyboardInputWithModifierAndFlags()
- **Purpose**: Send keyboard events with Sunshine-specific flags (e.g., `SS_KBE_FLAG_NON_NORMALIZED`)
- **Usage**: Required for sending raw Linux scancodes (e.g., Super key)
- **Parameters**: `keyMap`, `upDown`, `modifier`, `flags`
- **Code Location**: 
  - Unity: `StreamManager.cs` lines 357-363
  - Java: `StreamPlugin.java` lines 125-130

#### SendMouseHScroll()
- **Purpose**: Forward horizontal scroll events to Android plugin
- **Note**: Currently unused (horizontal scroll implemented via Shift+Scroll in Unity)
- **Code Location**: `StreamManager.cs` (method exists but not actively used)

### Technical Details

#### Monitor Visibility Management
1. **Startup**: All quads disabled in `StreamManager.Awake()` to prevent white screens
2. **Stream Start**: Only first monitor enabled in `StreamManager.OnCreate()` when stream begins
3. **Spawning**: `SpawnNextMonitor()` finds first disabled monitor and enables it
4. **Removal**: `RemoveMonitor()` disables monitor (doesn't destroy) and clears grab state

#### Horizontal Scroll Timing
- Uses coroutine with 0.02s delays to ensure OS recognizes Shift modifier
- Prevents overlapping coroutines with `isSendingHorizontalScroll` flag
- Direction inverted to match expected behavior (left stick = left scroll)

#### Super+O Shortcut Implementation
- Uses `SS_KBE_FLAG_NON_NORMALIZED` flag to send raw Linux scancode
- No delays needed - immediate key sequence works reliably
- Correct Quest button mapping: `OVRInput.Button.One` for X button

### Benefits
- Smooth horizontal scrolling for web pages and applications
- Reliable keyboard shortcuts (Super+O for Onboard)
- Intuitive monitor management (spawn/remove with menu button)
- Reduced mouse jitter for better precision
- Improved text clarity with higher render scale and LOD bias
- Eliminated audio clipping with larger buffers
- Visual quad borders for better monitor tracking
- Higher quality streaming (90 FPS, 90 Hz refresh rate)
- Proper microphone permissions for voice input

### Testing Notes
- Horizontal scroll direction: Left stick = left scroll, Right stick = right scroll
- Super+O shortcut: Press X button on left controller to launch Onboard
- Monitor removal: Hold right grip + press menu button while pointing at monitor
- UI toggle: Hold right thumb rest + press menu button to toggle app list UI
- Pointer stabilization: Adjust `smoothingSpeed` and `movementThreshold` in Inspector if needed
- Quad borders: Toggle `StreamManager.enableQuadBorders` to enable/disable
- Audio: Monitor for clipping during high-load scenarios (should be eliminated)
- Texture filtering: Verify no shimmering/blurriness on stream texture

## Foveation Control, Monitor Configuration, and Rendering Optimizations (November 2025)

### Overview
Major improvements to foveation control, monitor configuration flexibility, shader enhancements, and rendering pipeline optimizations. Replaced hardcoded monitor settings with Inspector-exposed configuration, improved foveation disable approach, and added new rendering controls.

### Foveation Control Improvements (`LimePluginManager.cs`)

#### OpenXR Reflection-Based Foveation Disable
- **Previous Approach**: Direct `OVRManager`/`OVRPlugin` calls for disabling foveated rendering
- **New Approach**: Reflection-based access to `MetaFoveationFeature` via OpenXR to avoid compile-time errors
- **Benefits**: 
  - More robust - works even if Meta OpenXR package isn't fully available at compile time
  - Avoids dependency issues with missing packages
  - Uses reflection to dynamically access OpenXR features at runtime
- **Implementation**:
  - Uses `OpenXRSettings.Instance` to access OpenXR features
  - Dynamically loads `MetaFoveationFeature` type via reflection
  - Sets `foveationLevel` property to `Off` enum value
  - Disables `refreshRateChanged` auto-adjustments
- **Timing**: Moved foveation disable from `Awake()` to `Start()` for better initialization timing
- **Code Location**: `LimePluginManager.cs` lines 56-103

#### Eye Render Resolution Control Methods
- **New Method**: `SetEyeRenderResolutionScale(float scale)`
  - Sets VR eye render resolution using scale multipliers (1.0 = native, 1.5 = 1.5x native, etc.)
  - Uses `XRSettings.eyeTextureResolutionScale`
  - Purpose: Allow dynamic adjustment of VR rendering resolution
- **New Method**: `SetEyeRenderResolutionAbsolute(int targetWidthPerEye)`
  - Attempts to set eye resolution based on target pixel width per eye
  - Calculates scale automatically based on headset native resolution (uses `XRSettings.eyeTextureWidth`)
  - Supports Quest 2 (1832x1920), Quest 3 (2064x2208), Quest Pro (1800x1920)
  - Falls back to estimated scale if native resolution cannot be determined
  - Note: Only takes width parameter; calculates scale based on width and applies uniformly
- **Code Location**: `LimePluginManager.cs` lines 105-163

### Monitor Configuration Improvements (`StreamManager.cs`)

#### Inspector-Exposed Configuration (No More Hardcoding)
- **Removed**: Hardcoded monitor configuration values
- **Added**: Inspector-editable fields for complete monitor setup flexibility:
  - `monitorWidth` (default: 1920f) - Width of each individual monitor in pixels
  - `monitorHeight` (default: 1200f) - Height of each individual monitor in pixels (updated from 1080)
  - `desktopXOffsets[]` (default: {0, 1920, 3840, 5760}) - X position offsets for each monitor in desktop space
  - `desktopYOffset` (default: 0f) - Y position offset for all monitors
  - `textureVerticalOffset` (new) - Fine-tuning control to shift texture up/down on the quad in pixels
- **Benefits**:
  - No code changes needed to adjust monitor configuration
  - Easy setup for different monitor arrangements
  - Can be adjusted per-project without rebuilding code
- **Impact**: Monitor height updated from 1080 to 1200 pixels for better aspect ratio
- **Code Location**: `StreamManager.cs` lines 12-32, 199-241

#### Texture Configuration Updates
- **Mipmaps**: Enabled on stream texture (`mipChain = true`)
  - Changed from disabled to enabled for better texture filtering at distance
  - Note: External textures from Limelight plugin may have limitations with Unity mipmap generation
- **UV Setup**: Now uses Inspector-configured values instead of hardcoded arrays
- **Vertical Alignment**: Added `textureVerticalOffset` for fine-tuning texture vertical position on quads
  - Positive = shift down (fills bottom gap)
  - Negative = shift up (fills top gap)
- **Error Handling**: Better validation for missing X offset configurations
- **Code Location**: `StreamManager.cs` lines 164-172, 199-241

#### Inspector-Exposed Stream Texture Filtering (`StreamManager.cs`)
- **Filter Mode**: Exposed to Inspector as `mStreamFilterMode` (default: `FilterMode.Bilinear`)
  - Can be adjusted in Unity Inspector: Bilinear, Point, Trilinear
  - Applied when creating/updating stream texture
- **Anisotropic Level**: Exposed to Inspector as `mStreamAnisoLevel` (default: 16, range: 0-16)
  - Controls anisotropic texture filtering quality
  - Higher values = better quality when viewing quads at angles
  - Can be adjusted in Unity Inspector for performance/quality trade-off
- **Benefits**: Easy adjustment of texture filtering without code changes
- **Code Location**: `StreamManager.cs` lines 45-50, applied in texture creation logic

### Shader Improvements (`FillQuadSHader.shader`)

#### Alpha-to-Coverage Enabled
- **Added**: `AlphaToMask On` directive in shader Pass
- **Purpose**: Uses MSAA (Multi-Sample Anti-Aliasing) to smooth alpha edges
- **Effectiveness**: Most beneficial when MSAA is enabled and texture uses alpha channel
- **Benefits**: Smoother text rendering and better alpha edge quality
- **Code Location**: `FillQuadSHader.shader` line 11

### Input Manager Fixes (`InputManager.cs`)

#### Monitor Spawning Logic Correction
- **Issue**: Monitor spawning was not working correctly when menu button was pressed alone
- **Fix**: Corrected `else` block structure in menu button handling
  - Monitor spawning now correctly triggers when menu button is pressed without thumb rest or grip held
  - Fixed indentation and block structure
- **Impact**: Menu button alone now properly spawns monitors as intended
- **Code Location**: `InputManager.cs` lines 171-201

### Render Pipeline Settings Updates

#### URP High Fidelity Settings (`URP-HighFidelity.asset`, `URP-HighFidelity-Renderer.asset`)
- Updated Universal Render Pipeline quality settings
- Adjustments to rendering quality and performance balance
- Code Location: `MoonQuestUnity/Assets/Settings/URP-HighFidelity*.asset`

#### Project and Quality Settings
- **ProjectSettings.asset**: Minor configuration adjustments
- **QualitySettings.asset**: Quality level settings updates
- Code Location: `MoonQuestUnity/ProjectSettings/*.asset`

### Material Updates (`QuadMaterial.mat`)

#### Material Property Changes
- Updated material properties for better rendering
- May include texture filtering, shader properties, or other rendering settings
- Code Location: `MoonQuestUnity/Assets/QuadMaterial.mat`

### Scene Configuration (`Limelight.unity`)

#### Extensive Scene Updates
- **Scope**: 1,761 lines changed in scene file
- **Likely Includes**:
  - Component property updates
  - Camera configurations
  - Render pipeline settings
  - OVRManager configurations
  - Component references and serialization data
- **Impact**: Significant scene structure and configuration changes
- **Note**: Requires review in Unity Editor to see exact changes
- **Code Location**: `MoonQuestUnity/Assets/LimeLight/Limelight.unity`

### Android Plugin Updates

#### Java Source Files
- **PluginManager.java**: Minor configuration adjustments (4 lines changed)
- **PreferenceConfiguration.java**: Preference defaults updates (2 lines changed)
- **AAR Rebuild**: `liblime-release.aar` rebuilt with updated Java code
  - Binary size: 1862631 → 1862630 bytes (minor changes)
- **Code Location**: `limelight_plugin/liblime/src/main/java/com/liblime/*.java`

### File Cleanup

#### Removed Files
- **RuntimeActionBindings.json**: Removed from StreamingAssets (no longer needed)
- **RuntimeActionBindings.json.meta**: Removed Unity meta file
- **Purpose**: Cleanup of unused configuration files

### Technical Details

#### Foveation Control Flow
1. **Startup**: `DisableFoveatedRendering()` called in `Start()` method
2. **OpenXR Check**: Validates `OpenXRSettings.Instance` is available
3. **Reflection**: Dynamically loads `MetaFoveationFeature` type
4. **Feature Access**: Uses `GetFeature<T>()` method to retrieve feature instance
5. **Disable**: Sets `foveationLevel` to `Off` and disables auto-adjustments
6. **Fallback**: Logs warning if feature not found (non-critical)

#### Monitor Configuration Flow
1. **Inspector Setup**: User configures monitor settings in Unity Inspector
2. **Runtime Loading**: `StreamManager` reads Inspector-configured values
3. **UV Calculation**: Each monitor's UV rect calculated from `desktopXOffsets` and `monitorWidth`/`monitorHeight`
4. **Texture Application**: UV offset and scale applied to quad material for proper cropping
5. **Fine-Tuning**: `textureVerticalOffset` allows pixel-level vertical alignment adjustments

### Benefits
- More robust foveation control using OpenXR reflection
- Flexible monitor configuration without code changes
- Better texture filtering with mipmaps enabled
- Smoother alpha rendering with alpha-to-coverage
- Fixed monitor spawning functionality
- Higher monitor resolution (1200 vs 1080) for better aspect ratio
- Easy fine-tuning of texture alignment
- New eye resolution control methods for VR optimization

### Testing Notes
- **Foveation**: Verify foveated rendering is disabled (may require ADB commands if code approach doesn't work due to Unity/Meta bugs)
- **Monitor Configuration**: Test different monitor arrangements by adjusting Inspector values
- **Texture Alignment**: Use `textureVerticalOffset` to fine-tune if monitors show black strips at top/bottom
- **Mipmaps**: Monitor for any texture corruption (external textures may not fully support Unity mipmap generation)
- **Eye Resolution**: Test `SetEyeRenderResolutionScale()` and `SetEyeRenderResolutionAbsolute()` methods for VR quality tuning
- **Monitor Spawning**: Verify menu button alone now correctly spawns monitors