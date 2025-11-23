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
- **Startup Behavior**: All quads disabled in `Awake()`, only first monitor enabled in `OnCreate()` when stream starts
- **Code Location**: `InputManager.cs` lines 152-192, `StreamManager.cs` lines 25-99, 390-450

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
- Pointer stabilization: Adjust `smoothingSpeed` and `movementThreshold` in Inspector if needed
- Quad borders: Toggle `StreamManager.enableQuadBorders` to enable/disable
- Audio: Monitor for clipping during high-load scenarios (should be eliminated)
- Texture filtering: Verify no shimmering/blurriness on stream texture