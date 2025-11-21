using System;
using UnityEngine;
using UnityEngine.UI;
namespace PCP.LibLime
{
	public class StreamManager : BasePluginBridge
	{
		[SerializeField] private MeshRenderer mQuadRenderer; // For 3D stream display
		[SerializeField] private UnityEngine.UI.RawImage mRawImage; // For UI RawImage display (optional)
		private Texture mPausingTex;
		private Texture mRawImagePausingTex; // Store original RawImage texture
		private Vector2 mPausingSize;
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
			if (mQuadRenderer != null)
			{
				mPausingTex = mQuadRenderer.material.mainTexture;
			}
			if (mRawImage != null)
			{
				mRawImagePausingTex = mRawImage.texture;
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
				
				// Only destroy if we're not currently using it
				if (mQuadRenderer != null && mQuadRenderer.material.mainTexture == mStreamTexture)
				{
					mQuadRenderer.material.mainTexture = null;
				}
				if (mRawImage != null && mRawImage.texture == mStreamTexture)
				{
					mRawImage.texture = null;
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
			
			// Assign to renderers
			if (mQuadRenderer != null)
			{
				mQuadRenderer.material.mainTexture = mStreamTexture;
			}
			if (mRawImage != null)
			{
				mRawImage.texture = mStreamTexture;
			}
	}
		protected override void OnStop()
		{
			// Clean up stream texture
			if (mStreamTexture != null)
			{
			if (mQuadRenderer != null && mQuadRenderer.material.mainTexture == mStreamTexture)
			{
				mQuadRenderer.material.mainTexture = mPausingTex;
			}
			if (mRawImage != null && mRawImage.texture == mStreamTexture)
			{
				mRawImage.texture = mRawImagePausingTex;
			}
			DestroyImmediate(mStreamTexture);
			mStreamTexture = null;
		}
		
		mLastTexWidth = 0;
		mLastTexHeight = 0;
		
		LimePluginManager.Instance.HideUI();
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
				// Update the texture with new native pointer (shared between both QuadRenderer and RawImage)
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
	}
}
