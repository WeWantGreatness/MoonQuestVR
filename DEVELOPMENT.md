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