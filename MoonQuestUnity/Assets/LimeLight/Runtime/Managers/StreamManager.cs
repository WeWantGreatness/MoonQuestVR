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
	
		private List<Texture> mPausingTextures = new List<Texture>();
	private int mTexWidth;
	private int mTexHeight;
		private int mLastTexWidth = 0; // Track last known resolution to detect changes
		private int mLastTexHeight = 0;
		private Texture2D mStreamTexture; // Reference to current stream texture
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
		}

	protected override void OnCreate()
	{
		Debug.Log(mTag + ": OnCreate called");
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
					filterMode = FilterMode.Trilinear,
					anisoLevel = 16
				};
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
				if (quad == null || quad.material == null) continue;
				
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
			if (!IsInitialized || mPlugin == null) return;
			mPlugin.Call("SendKeyboardInputWithModifier", keyMap, upDown, modifier);
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
		/// Stub for dynamic spawning used by InputManager. With MonitorSpawner,
		/// all 4 monitors are spawned at startup, so this currently only logs.
		/// </summary>
		public void SpawnNextMonitor()
		{
			Debug.LogWarning(mTag + ": SpawnNextMonitor called, but dynamic spawning is handled by MonitorSpawner.");
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
	/// Removes a monitor GameObject from the scene. Used by InputManager
	/// when holding the menu button.
	/// </summary>
	public void RemoveMonitor(GameObject monitorToRemove)
	{
		if (monitorToRemove == null) return;
		Destroy(monitorToRemove);
	}
	
	}
}
