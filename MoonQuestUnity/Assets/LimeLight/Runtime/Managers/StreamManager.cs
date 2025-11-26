using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace PCP.LibLime
{
	public class StreamManager : BasePluginBridge
	{
		[Header("4 Quad Setup (MeshRenderer)")]
		[Tooltip("Drag your 4 manually created quads with MeshRenderer here. Order: DP-2, HDMI-0, DP-0, DP-4")]
		public List<MeshRenderer> quadRenderers = new List<MeshRenderer>();
		
		[Header("Quad Borders")]
		[Tooltip("Automatically add sky blue borders around all monitor quads for visibility")]
		public bool enableQuadBorders = true;
	
		private List<Texture> mPausingTextures = new List<Texture>();
	private int mTexWidth;
	private int mTexHeight;
		private int mLastTexWidth = 0; // Track last known resolution to detect changes
		private int mLastTexHeight = 0;
		private Texture2D mStreamTexture; // Reference to current stream texture

		[Header("Stream Texture Filtering")]
		[SerializeField]
		private FilterMode mStreamFilterMode = FilterMode.Bilinear;

		[SerializeField, Range(0, 16)]
		private int mStreamAnisoLevel = 16;
	private IntPtr mRawObject;

	private void Awake()
	{
		Type = LimePluginManager.PluginType.Stream;
		mTag = "StreamManager";
		
		// Save pause textures from all quads for OnStop
		mPausingTextures.Clear();
		if (quadRenderers != null && quadRenderers.Count > 0)
		{
			foreach (var quad in quadRenderers)
			{
				if (quad != null && quad.material != null)
		{
					mPausingTextures.Add(quad.material.mainTexture);
				}
				else
				{
					mPausingTextures.Add(null);
				}
			}
		}
		
		// IMPORTANT: Disable all quads on startup to prevent white screens
		// Only enable the first one after stream starts (in OnCreate)
		// This prevents quads from showing white material before stream texture is ready
		if (quadRenderers != null && quadRenderers.Count > 0)
		{
			for (int i = 0; i < quadRenderers.Count; i++)
			{
				if (quadRenderers[i] != null)
				{
					// Add border component if enabled
					if (enableQuadBorders)
					{
						QuadBorder border = quadRenderers[i].GetComponent<QuadBorder>();
						if (border == null)
						{
							border = quadRenderers[i].gameObject.AddComponent<QuadBorder>();
							Debug.Log(mTag + ": Added border to quad: " + quadRenderers[i].gameObject.name);
						}
					}
					
					// Disable ALL quads on startup - they'll be enabled when stream starts
					quadRenderers[i].gameObject.SetActive(false);
					Debug.Log(mTag + ": Disabled quad on startup: " + quadRenderers[i].gameObject.name);
				}
			}
		}
	}

	protected override void OnCreate()
	{
		Debug.Log(mTag + ": OnCreate called - stream starting");
		
		// Enable only the first monitor when stream starts
		// All quads were disabled in Awake() to prevent white screens on startup
		if (quadRenderers != null && quadRenderers.Count > 0)
		{
			for (int i = 0; i < quadRenderers.Count; i++)
			{
				if (quadRenderers[i] != null)
				{
					if (i == 0)
					{
						// Enable the first quad when stream starts
						quadRenderers[i].gameObject.SetActive(true);
						Debug.Log(mTag + ": Enabled first monitor on stream start: " + quadRenderers[i].gameObject.name);
					}
					else
					{
						// Ensure all other quads remain disabled (they may have been enabled in Unity editor)
						quadRenderers[i].gameObject.SetActive(false);
					}
				}
			}
		}
		
		GetResolution();
			Debug.Log(mTag + ": Initial resolution " + mTexWidth + "x" + mTexHeight);
		mRawObject = mPlugin.GetRawObject();
		
			// Create initial texture (may be updated when negotiated resolution is known)
			CreateStreamTexture(mTexWidth, mTexHeight);
		
		SaveLastApp();
		LimePluginManager.Instance.HideUI();
	}
		
		private void CreateStreamTexture(int width, int height)
		{
			// Destroy old texture if it exists and dimensions changed
			if (mStreamTexture != null && (mStreamTexture.width != width || mStreamTexture.height != height))
			{
				Debug.Log(mTag + ": Recreating texture due to resolution change from " + mStreamTexture.width + "x" + mStreamTexture.height + " to " + width + "x" + height);
				
				// Unassign from all quads before destroying
				if (quadRenderers != null)
				{
					foreach (var quad in quadRenderers)
					{
						if (quad != null && quad.material != null && quad.material.mainTexture == mStreamTexture)
						{
							quad.material.mainTexture = null;
						}
					}
				}
				
				DestroyImmediate(mStreamTexture);
				mStreamTexture = null;
			}
			
			// Create new texture if it doesn't exist or was destroyed
			if (mStreamTexture == null)
			{
				mStreamTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
				{
					filterMode = mStreamFilterMode,
					anisoLevel = mStreamAnisoLevel
				};
				// Mipmaps are disabled via the 'false' parameter in Texture2D constructor (mipChain = false) - external textures don't support mipmaps
				mLastTexWidth = width;
				mLastTexHeight = height;
				
				Debug.Log(mTag + ": Created stream texture with resolution " + width + "x" + height);
			}
			
			// Assign to all quads with UV cropping for 4 monitors
			SetupQuadMonitors();
		}
		
	/// <summary>
		/// Exposes the current stream texture so external scripts can use it.
	/// </summary>
	public Texture2D StreamTexture
	{
		get
		{
				if (mStreamTexture == null)
			{
					Debug.LogWarning(mTag + ": StreamTexture property accessed but mStreamTexture is null!");
				}
				return mStreamTexture;
			}
		}
		
		private void SetupQuadMonitors()
		{
			if (quadRenderers == null || quadRenderers.Count == 0 || mStreamTexture == null) return;
			
			// Hardcoded for 4 monitors: DP-2, HDMI-0, DP-0, DP-4
			// Based on xrandr: DP-2 at 0, HDMI-0 at 1920, DP-0 at 3840, DP-4 at 5760
			float[] desktopXOffsets = { 0f, 1920f, 3840f, 5760f };
			float monitorWidth = 1920f;
			float monitorHeight = 1080f;
			
			for (int i = 0; i < quadRenderers.Count && i < 4; i++)
			{
				var quad = quadRenderers[i];
				// Only set up monitors that are active (enabled)
				if (quad == null || !quad.gameObject.activeSelf || quad.material == null) continue;
				
				// Assign the shared texture to the quad's material
				quad.material.mainTexture = mStreamTexture;
				
				// Calculate UV Rect (x, y, width, height in 0-1 space) for texture offset
				// Width: how much of the texture this monitor uses
				float uvWidth = monitorWidth / mTexWidth;  // 1920 / 7680 = 0.25
				float uvHeight = monitorHeight / mTexHeight; // 1080 / 1080 = 1.0
				
				// X offset: where this monitor starts horizontally
				float uvX = desktopXOffsets[i] / mTexWidth;
				
				// Y offset: All monitors are at desktopY=0 (top of desktop)
				float uvY = 0f;
				
				// Create UV offset using material property (tiling and offset)
				// UV offset = (uvX, uvY), UV scale = (uvWidth, uvHeight)
				quad.material.mainTextureOffset = new Vector2(uvX, uvY);
				quad.material.mainTextureScale = new Vector2(uvWidth, uvHeight);
				
				Debug.Log(mTag + ": Quad " + i + " (" + quad.gameObject.name + ") - uvOffset: (" + uvX + ", " + uvY + "), uvScale: (" + uvWidth + ", " + uvHeight + ")");
			}
		}
		
		protected override void OnStop()
		{
			// Clean up stream texture
			if (mStreamTexture != null)
			{
				// Restore original textures to all quads
				if (quadRenderers != null && mPausingTextures != null)
					{
					for (int i = 0; i < quadRenderers.Count && i < mPausingTextures.Count; i++)
					{
						if (quadRenderers[i] != null && quadRenderers[i].material != null)
						{
							quadRenderers[i].material.mainTexture = mPausingTextures[i];
							// Reset UV offsets
							quadRenderers[i].material.mainTextureOffset = Vector2.zero;
							quadRenderers[i].material.mainTextureScale = Vector2.one;
						}
					}
				}
				
				DestroyImmediate(mStreamTexture);
				mStreamTexture = null;
						}
			
			mLastTexWidth = 0;
			mLastTexHeight = 0;
			
			LimePluginManager.Instance.ShowUI();
		}
		
		//Get Shared Texture
		private void GetResolution()
		{
			string resolution = mPlugin.Call<string>("GetResolution");
			string[] res = resolution.Split('x');
			mTexWidth = int.Parse(res[0]);
			mTexHeight = int.Parse(res[1]);
		}
		
		private IntPtr GetTexturePtr()
		{
			return !IsInitialized ? IntPtr.Zero : JNIUtil.GetTexturePtr((int)mRawObject);
		}
		
	public void UpdateFrame()
	{
		if (!IsInitialized)
			return;
			
			// Check if resolution has changed (negotiated resolution may differ from requested)
			GetResolution();
			if ((mTexWidth != mLastTexWidth || mTexHeight != mLastTexHeight) && mTexWidth > 0 && mTexHeight > 0)
			{
				Debug.Log(mTag + ": Resolution changed from " + mLastTexWidth + "x" + mLastTexHeight + " to " + mTexWidth + "x" + mTexHeight);
				CreateStreamTexture(mTexWidth, mTexHeight);
			}
			
			if (mStreamTexture == null)
				return;
			
		if (SystemInfo.renderingThreadingMode == UnityEngine.Rendering.RenderingThreadingMode.MultiThreaded)
		{
			GL.IssuePluginEvent(JNIUtil.UpdateSurfaceFunc(), (int)mRawObject);
		}
		else
		{
			JNIUtil.UpdateSurface((int)mPlugin.GetRawObject());
		}
		IntPtr newPtr = GetTexturePtr();
			IntPtr oldPtr = mStreamTexture.GetNativeTexturePtr();

			if ((newPtr != IntPtr.Zero) && (newPtr != oldPtr))
			{
				// Update the texture with new native pointer (shared between all 4 quads)
				mStreamTexture.UpdateExternalTexture(newPtr);
		}
	}
		
		private void Update()
		{
			try
			{
				UpdateFrame();
			}
			catch (Exception e)
			{
				Debug.LogError(mTag + "UpdateFrame failed:" + e.Message);
				MessageManager.Instance.Error("UpdateFrame failed:" + e.Message);
				enabled = false;
			}
		}

		private void OnValidate()
		{
			mStreamAnisoLevel = Mathf.Clamp(mStreamAnisoLevel, 0, 16);
			ApplyTextureFilteringSettings();
		}

		private void ApplyTextureFilteringSettings()
		{
			if (mStreamTexture == null)
			{
				return;
			}

			mStreamTexture.filterMode = mStreamFilterMode;
			mStreamTexture.anisoLevel = mStreamAnisoLevel;
			mStreamTexture.Apply(false, false);
			Debug.Log(mTag + ": Updated stream texture filtering - mode: " + mStreamFilterMode + ", aniso: " + mStreamAnisoLevel);
		}

		public void SetTextureFiltering(FilterMode filterMode, int anisoLevel)
		{
			mStreamFilterMode = filterMode;
			mStreamAnisoLevel = Mathf.Clamp(anisoLevel, 0, 16);
			ApplyTextureFilteringSettings();
		}
		
		private void SaveLastApp()
		{
			if (mPlugin == null)
				return;
			var raw = mPlugin.Call<string>("GetShortcut");
			Debug.Log(mTag + "SaveLastApp:" + raw);
			PlayerPrefs.SetString("LastApp", raw);
		}
		
		// --- INPUT METHODS ---

		/// <summary>
		/// Send mouse position to the stream (UV coordinates 0.0-1.0).
		/// </summary>
		public void SendMousePosition(float uvX, float uvY)
		{
			if (!IsInitialized || mPlugin == null) return;

			// Convert 0.0-1.0 to Screen Pixels (full desktop size from StreamPlugin)
			// Invert Y because Unity UV starts at bottom, Screens start at top
			int x = (int)(uvX * mTexWidth);
			int y = (int)((1.0f - uvY) * mTexHeight);

			mPlugin.Call("MoveMouse", x, y);
		}

		/// <summary>
		/// Send mouse button event (1=Left, 2=Middle, 3=Right).
		/// </summary>
		public void SendMouseButton(int buttonID, bool isDown)
		{
			if (!IsInitialized || mPlugin == null) return;
			mPlugin.Call("MouseButton", buttonID, isDown);
		}

		/// <summary>
		/// Send keyboard input (keyMap = scancode, upDown: 0=Down, 1=Up).
		/// </summary>
		public void SendKeyboardInput(int keyMap, int upDown)
		{
			if (!IsInitialized || mPlugin == null) return;
			mPlugin.Call("SendKeyboardInput", keyMap, upDown);
		}

		/// <summary>
		/// Send keyboard input with modifier (Shift/Ctrl/Alt/Super).
		/// modifier: 0x01=Shift, 0x02=Ctrl, 0x04=Alt, 0x08=Meta/Super
		/// </summary>
		public void SendKeyboardInputWithModifier(int keyMap, int upDown, int modifier)
		{
			SendKeyboardInputWithModifierAndFlags(keyMap, upDown, modifier, 0);
		}

		/// <summary>
		/// Send keyboard input with modifier and flags (for Sunshine extensions).
		/// modifier: 0x01=Shift, 0x02=Ctrl, 0x04=Alt, 0x08=Meta/Super
		/// flags: 0x01=SS_KBE_FLAG_NON_NORMALIZED (Sunshine - send raw keycode)
		/// </summary>
		public void SendKeyboardInputWithModifierAndFlags(int keyMap, int upDown, int modifier, int flags)
		{
			if (!IsInitialized || mPlugin == null) return;
			mPlugin.Call("SendKeyboardInputWithModifierAndFlags", keyMap, upDown, modifier, flags);
		}

		/// <summary>
		/// Send mouse scroll wheel (amount: positive=up, negative=down, in "clicks").
		/// </summary>
		public void SendMouseScroll(int amount)
		{
			if (!IsInitialized || mPlugin == null) return;
			mPlugin.Call("SendMouseScroll", amount);
		}

		/// <summary>
		/// Send horizontal mouse scroll (amount: positive=right, negative=left, in "clicks").
		/// This is the native horizontal scroll event (like tilting mouse wheel left/right).
		/// </summary>
		public void SendMouseHScroll(int amount)
		{
			if (!IsInitialized || mPlugin == null) return;
			mPlugin.Call("SendMouseHScroll", amount);
		}

		/// <summary>
		/// Enable the next disabled monitor from the quadRenderers list.
		/// Used by InputManager when menu button is clicked.
		/// </summary>
		public void SpawnNextMonitor()
		{
			if (quadRenderers == null || quadRenderers.Count == 0)
			{
				Debug.LogWarning(mTag + ": SpawnNextMonitor called but quadRenderers list is empty!");
				return;
			}
			
			// Find the first disabled monitor in the list
			foreach (var quad in quadRenderers)
			{
				if (quad != null && !quad.gameObject.activeSelf)
				{
					// Enable this monitor
					quad.gameObject.SetActive(true);
					Debug.Log(mTag + ": Enabled monitor " + quad.gameObject.name);
					
					// If stream is already active, assign the texture to this newly enabled monitor
					if (mStreamTexture != null)
					{
						SetupQuadMonitors(); // This will assign texture to all enabled quads
					}
					return;
				}
			}
			
			// All monitors are already enabled
			Debug.Log(mTag + ": SpawnNextMonitor called but all monitors are already enabled!");
		}

		/// <summary>
		/// Finds a monitor under the given ray by raycasting and checking for a StreamPointer.
		/// This works with monitors spawned by MonitorSpawner or placed manually.
		/// </summary>
		public GameObject GetMonitorUnderPointer(Ray ray, float maxDistance = 100f)
		{
			RaycastHit hit;
			if (Physics.Raycast(ray, out hit, maxDistance))
			{
				var pointer = hit.collider.GetComponentInParent<StreamPointer>();
				if (pointer != null)
				{
					return pointer.gameObject;
				}
			}
			return null;
		}

	/// <summary>
	/// Removes a monitor GameObject from the scene by disabling it. Used by InputManager
	/// when holding the menu button. Disabled monitors can be re-enabled via SpawnNextMonitor().
	/// </summary>
	public void RemoveMonitor(GameObject monitorToRemove)
	{
		if (monitorToRemove == null) return;
		
		// Clear grab state if this monitor was being grabbed
		ScreenManipulator.ClearGrabState();
		
		// Disable the monitor instead of destroying it, so it can be re-enabled later
		monitorToRemove.SetActive(false);
		Debug.Log(mTag + ": Disabled monitor " + monitorToRemove.name);
	}
	
	}
}
