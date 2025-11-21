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