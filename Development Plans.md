# MoonQuest Development Plans

## Overview
MoonQuest is a VR adaptation of Moonlight Android, porting its streaming and UI features to Unity for Oculus Quest. The core streaming logic is reused via JNI, but input, rendering, and UI are adapted for VR.

## Current Status
- ✅ Basic streaming works (fixed libc++_shared.so bundling, added logging).
- ✅ UI (app list) displays on RawImage, stream on Quad.
- ✅ Added permission handling for microphone and other Android 6.0+ permissions.
- ✅ VR-specific input handling implemented (mouse, keyboard, controller buttons).
- ✅ Multi-monitor support (4 virtual displays with UV cropping).
- ✅ Screen manipulation (grab, move, resize, curve).
- ✅ Visual quality improvements (render scale, LOD bias, texture filtering).
- ✅ Pointer stabilization for smooth mouse input.
- ✅ Quad borders for monitor visibility.

## Planned Features

### Input Handling
- ✅ **Mouse Input via VR Controllers**:
  - ✅ Use VR controller raycasting (`StreamPointer.cs`) to simulate mouse movement on the remote desktop.
  - ✅ Convert VR ray hits to mouse coordinates and send via JNI as `MouseButtonPacket`.
  - ✅ Allow grabbing/clicking with controller triggers.
  - ✅ Pointer stabilization implemented (smoothing to reduce jitter).

- ✅ **Keyboard Input**:
  - ✅ JNI methods implemented (`SendKeyboardInput`, `SendKeyboardInputWithModifier`, `SendKeyboardInputWithModifierAndFlags`).
  - ✅ Send key presses via JNI as `KeyboardPacket`.
  - ✅ Super+O shortcut implemented (launch Onboard virtual keyboard on Linux).
  - ⚠️ **Virtual Keyboard UI**: Visual Keyboard assets exist in Unity (`Assets/Visual Keyboard/`) but not yet integrated with streaming input.

- ✅ **Controller Button Mapping**:
  - ✅ Assign VR controller buttons to mouse actions (`InputManager.cs`):
    - Right Trigger = Left Click
    - Right Button B = Right Click
    - Right Stick Click = Middle Click
    - Right Stick Movement = Vertical/Horizontal Scroll
    - Left Trigger = Left Click (alternative)
    - Left Button X = Launch Onboard (Super+O)
    - Menu Button = Spawn/Remove monitors
  - ✅ Button mappings finalized and tested.

- ❌ **Gamepad Support**:
  - ❌ Port Moonlight Android's `ControllerHandler.java` via JNI for standard gamepad input.
  - Status: Not yet implemented.

### VR Interaction Features
- ✅ **Screen Manipulation**:
  - ✅ Grab and move the Quad (stream display) in 3D space using VR controllers (`ScreenManipulator.cs`).
  - ✅ Resize the Quad for better viewing (analog stick up/down while grabbed).
  - ✅ Curve the Quad using adjustable curvature (`CurvedScreen.cs`, analog stick left/right while grabbed).
  - ✅ Rotate via controller movement while grabbed.
  - ✅ Quad borders for visibility (`QuadBorder.cs` - sky-blue 3D frame around quads).

- ❌ **Headlock Mode**:
  - ❌ Toggle mode where the screen follows head movement (stays in view).
  - ❌ Adjustable offset within mode.
  - Status: Not yet implemented.

- ❌ **UI Toggle**:
  - ❌ Button/key to bring back the UI (app list) after hiding on stream start.
  - Status: Not yet implemented (UI hides on stream start but no toggle button).

- ❌ **Blue Light Filter**:
  - ❌ Option to apply a blue light filter shader to the Quad for eye comfort.
  - Status: Not yet implemented.

- ✅ **Multiple Virtual Displays**:
  - ✅ Support 4 virtual screens in VR environment (multiple Quads for multi-monitor setups).
  - ✅ UV cropping implemented to display portions of wide desktop stream (7680x1080 split into 4x 1920x1080 monitors).
  - ✅ Monitor spawning: Menu button click spawns next disabled monitor.
  - ✅ Monitor removal: Right grip held + Menu button click removes monitor under pointer.
  - ✅ Dynamic monitor management (enable/disable without destroying GameObjects).

### Platform Support
- ❌ **PC VR Support**:
  - ❌ Support for PC VR headsets via SteamVR runtime.
  - ❌ Support for PC VR headsets via OpenXR runtime.
  - ❌ Abstract VR input system to support multiple VR platforms (currently uses OVRInput directly).
  - ❌ Test and validate on PC VR headsets (Index, Vive, Reverb, etc.).
  - Status: Currently Quest-only (uses OVRInput). PC VR support would enable streaming VR games from PC to PC VR headsets.

### Rendering Fixes
- ✅ **Eye-Level Positioning**:
  - ✅ Fixed Quad positioning and visibility management.
  - ✅ All quads disabled on startup, only first monitor enabled when stream starts (prevents white screens).
  - ✅ Proper VR camera settings and positioning configured.

- ✅ **Visual Quality Improvements**:
  - ✅ Render scale increased to 1.5 (`maxRenderScale`) for better text clarity.
  - ✅ LOD bias increased to 2.0 for higher quality LODs.
  - ✅ Texture filtering changed from Trilinear to Bilinear (external textures don't support mipmaps).
  - ✅ Quad uses appropriate shader for consistent lighting.

- ✅ **Additional Rendering Improvements**:
  - ✅ Stream texture uses `FilterMode.Bilinear` (Trilinear was inappropriate for external textures).
  - ✅ Texture resolution dynamically adapts to negotiated stream resolution (eliminates letterboxing).

### Ported Features from Moonlight Android
- ⚠️ **Settings**:
  - ✅ Resolution options (default 7680x1080 for multi-monitor).
  - ✅ Framerate settings (90 FPS default).
  - ✅ Refresh rate (90 Hz default).
  - ✅ Audio preferences (DSP buffer size increased to 4096, Android audio buffers increased to prevent clipping).
  - ❌ Bitrate, codec settings (not yet exposed in UI).
  - ❌ Network optimizations (not yet exposed in UI).

- ❌ **UI Port**:
  - ⚠️ Basic app list UI exists and works.
  - ❌ Port Moonlight Android's polished UI (app grid, settings screens) to Unity.
  - ❌ Convert Android layouts (XML) to Unity UI (Canvas, prefabs).
  - ❌ Adapt gestures (e.g., swipe for app list) to VR interactions.
  - Status: Core functionality works, but full UI polish not yet ported.

- ⚠️ **Other Features**:
  - ❌ Performance overlays (not yet implemented).
  - ❌ USB driver support for peripherals (not yet implemented).
  - ✅ Shortcut helpers (Super+O shortcut for Onboard keyboard implemented).

## Implementation Notes
- **JNI Integration**: Extend existing JNI layer to send input packets (mouse, keyboard, controller) to the server, mirroring Moonlight Android's `NvConnection`.
- **Comparison to Moonlight Android**: Reuse packet structures and sending logic; adapt input capture from Android native to Unity VR.
- **Platform Support**: Currently Quest-only (uses OVRInput). Future PC VR support would require abstracting VR input to support SteamVR/OpenXR runtimes.
- **Testing**: Validate on Quest with physical mouse/keyboard fallback. PC VR support would enable streaming VR games from Linux PC to PC VR headsets.

## Roadmap

### ✅ Completed
1. ✅ Implement VR mouse input (`StreamPointer.cs`).
2. ✅ Fix rendering positioning and visual quality.
3. ✅ Add multiple displays (4-monitor support with UV cropping).
4. ✅ Add screen manipulation (grab, move, resize, curve).
5. ✅ Add controller button mapping (`InputManager.cs`).
6. ✅ Add keyboard input methods (JNI integration).
7. ✅ Add pointer stabilization (smoothing).
8. ✅ Add quad borders for visibility.

### 🚧 In Progress / Partial
1. ⚠️ Virtual keyboard (assets exist, but not integrated with streaming).
2. ⚠️ Settings UI (basic settings implemented, full UI not ported).

### ❌ Planned / Not Started
1. ❌ Add PC VR support (SteamVR and/or OpenXR runtime) for streaming to PC VR headsets.
2. ❌ Abstract VR input system to support multiple VR platforms (currently Quest-only with OVRInput).
3. ❌ Add virtual keyboard integration (connect Visual Keyboard to streaming input).
4. ❌ Add blue light filter shader.
5. ❌ Add headlock mode (screen follows head movement).
6. ❌ Add UI toggle button (show/hide app list).
7. ❌ Add gamepad support (`ControllerHandler.java` port).
8. ❌ Port full settings UI from Moonlight Android.
9. ❌ Add performance overlays.
10. ❌ Add USB driver support for peripherals.
11. ❌ Full UI port from Moonlight Android (polished app grid, settings screens).

This plan will be updated as features are implemented.