using System;
using UnityEngine;
using UnityEngine.UI;
namespace PCP.LibLime
{
	public class StreamManager : BasePluginBridge
	{
		[SerializeField] private MeshRenderer mQuadRenderer; // For 3D stream display
		private Texture mPausingTex;
		private Vector2 mPausingSize;
		private int mTexWidth;
		private int mTexHeight;
		private IntPtr mRawObject;

		private void Awake()
		{
			Type = LimePluginManager.PluginType.Stream;
			mTag = "StreamManager";
			if (mQuadRenderer != null)
			{
				mPausingTex = mQuadRenderer.material.mainTexture;
			}
		}

		protected override void OnCreate()
		{
			Debug.Log(mTag + ": OnCreate called");
			GetResolution();
			Debug.Log(mTag + ":Resolution " + mTexWidth + "x" + mTexHeight);
			mRawObject = mPlugin.GetRawObject();
			Texture2D streamTexture = new Texture2D(mTexWidth, mTexHeight, TextureFormat.RGBA32, false, true)
			{
				filterMode = FilterMode.Trilinear,
				anisoLevel = 16
			};
			if (mQuadRenderer != null)
			{
				mQuadRenderer.material.mainTexture = streamTexture;
			}
			SaveLastApp();
			LimePluginManager.Instance.HideUI();
		}
		protected override void OnStop()
		{
			if (mQuadRenderer != null)
			{
				mQuadRenderer.material.mainTexture = mPausingTex;
			}
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
			Debug.Log(mTag + ": UpdateFrame called");
			if (SystemInfo.renderingThreadingMode == UnityEngine.Rendering.RenderingThreadingMode.MultiThreaded)
			{
				GL.IssuePluginEvent(JNIUtil.UpdateSurfaceFunc(), (int)mRawObject);
			}
			else
			{
				JNIUtil.UpdateSurface((int)mPlugin.GetRawObject());
			}
			IntPtr newPtr = GetTexturePtr();
			IntPtr oldPtr = IntPtr.Zero;
			if (mQuadRenderer != null && mQuadRenderer.material.mainTexture is Texture2D quadTex)
			{
				oldPtr = quadTex.GetNativeTexturePtr();
			}

			if ((newPtr != IntPtr.Zero) && (newPtr != oldPtr))
			{
				if (mQuadRenderer != null)
				{
					((Texture2D)mQuadRenderer.material.mainTexture).UpdateExternalTexture(newPtr);
				}
				Debug.Log(mTag + ": Texture updated");
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
