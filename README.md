MoonQuest VR — patched fork

Summary
-------
This is a user-maintained fork of the MoonQuest Unity project with a self-contained Android plugin (limelight_plugin) for Moonlight streaming into Unity. This fork contains enhancements and debug logging used by WeWantGreatness for development and troubleshooting.

What's in this repo
-------------------
- MoonQuestUnity/: The Unity project with your MoonQuest content and scripts.
- limelight_plugin/: Android plugin that provides the Moonlight / NVidia streaming integration.
  - Latest built AAR is copied to `MoonQuestUnity/Assets/Plugins/Android/liblime-release.aar` for convenience.
  - Source for the plugin is in `limelight_plugin/liblime/`.

Building the limelight plugin AAR
--------------------------------
If you want to build the AAR locally (recommended for changes or automation):

1. Ensure Android SDK & NDK are installed, with `ndkVersion` matching the gradle config (see `limelight_plugin/liblime/build.gradle`).
2. From the repository root:

```bash
cd limelight_plugin
# Build debug or release
./gradlew :liblime:assembleRelease
```

3. The AAR will appear at `limelight_plugin/liblime/build/outputs/aar/liblime-release.aar`. Copy it into the Unity project (or configure your CI to do it):

```bash
cp liblime/build/outputs/aar/liblime-release.aar \
  ../MoonQuestUnity/Assets/Plugins/Android/
```

Why I added `APP_STL := c++_shared`
---------------------------------
- The native part of the plugin (e.g., `libshared-texture.so`) relies on the shared C++ runtime `libc++_shared.so` from the NDK.
- Without `APP_STL := c++_shared` (or equivalent configuration in `Application.mk`), the shared library would not be included in the AAR and the app crashed with `java.lang.UnsatisfiedLinkError`.
- This repo includes the fix. If you build locally and want to test that change, rebuild with the above gradle command.

Unity integration
-----------------
- The built AAR is expected under `MoonQuestUnity/Assets/Plugins/Android/`.
- When you rebuild the Unity Android player, this AAR will be packed into the APK with native libs.
- You may want to add a pre-build script or CI step to ensure the AAR is always rebuilt and moved into Unity before the player build.

Submodule or nested repo note
----------------------------
- `limelight_plugin/liblime/src/main/jni/moonlight-core/moonlight-common-c` is a git submodule/nested repo. Cloning the outer repo won't automatically fetch its contents.
  - Option A: Keep it as a submodule by running `git submodule update --init --recursive` after cloning.
  - Option B: If you want to include the sources directly in this repo (so there's no additional step), remove the nested submodule metadata and commit the files directly.

How to run/debug
----------------
- Use `adb logcat` to filter for plugin logs; I used the `LIME:` prefix and `LimeLog` Java tag for easier grepping. For example:

```bash
adb logcat -v time | grep -E "(LIME:|LimeLog|startapp_failed|UISTM|applist1|SharedTexture-JNI|UpdateSurface|GetTexturePtr|failed|timeout|exception|error)"
```

- If you make native code changes, rebuild the AAR and copy the new artifact to the Unity plugins folder.

Licensing & upstream
--------------------
- This fork contains upstream code under the Moonlight / third-party licenses in `limelight_plugin`. Respect upstream licenses when redistributing.
- If you plan to cleanly merge back upstream changes, consider maintaining `limelight_plugin` as a proper submodule pointing to the original upstream repo.

Contributing / Notes
--------------------
- This repository is for personal development. Keep main or master stable and use `development` branch for ongoing changes.
- If you want, I can add a CI workflow to automatically build `liblime-release.aar` on push and attach it as a release artifact.

Contact
-------
Repo owner: WeWantGreatness
