using UnityEngine;

namespace PCP.LibLime
{
	public class AppManager : BasePluginBridge, IListPluginManager
	{

		private NvAppLIstUpdater mListUpdater;
		[SerializeField] private Transform mListParent;
		[SerializeField] private GameObject mListItemPrefab;
		public GameObject ListItemPrefab => mListItemPrefab;
		public Transform ListParent => mListParent;
		private void Awake()
		{
			Type = LimePluginManager.PluginType.App;
			mTag = "AppManger";
			mListUpdater = new NvAppLIstUpdater(this, mListItemPrefab, mListParent);
		}
		protected override void OnCreate()
		{
			//update app list once at start
			mListUpdater.UdpateListHandler("applist1");
			mCallBackHanlder += mListUpdater.UdpateListHandler;
		}
		protected override void OnStop()
		{
			//Cleear CallBack
			mCallBackHanlder = null;
			//Clear Item List
			mListUpdater.Clear();
		}

		public void StartApp(int appid)
		{
			if (!enabled)
				return;
			Debug.Log("LIME: " + mTag + ": Request to start app id=" + appid);
			Debug.Log("LIME: " + mTag + ": StartManager queueing Stream plugin");
			Debug.Log(mTag + ": StartManager queueing Stream plugin");
			Blocker.SetActive(true);
			mPluginManager.StartManager(LimePluginManager.PluginType.Stream);
			Debug.Log("LIME: " + mTag + ": StartManager called");
			if (mPlugin == null)
			{
				Debug.LogError(mTag + ": mPlugin is null - can't call StartApp");
				return;
			}
			// Validate the app exists in the current list to prevent null app crashes on the Java side
			try
			{
				string raw = mPlugin.Call<string>("GetList", false);
				if (!string.IsNullOrEmpty(raw))
				{
					Debug.Log("LIME: " + mTag + ": Raw applist length=" + raw.Length);
					NvAppData[] apps = JsonUtility.FromJson<NvAppDataWrapper>(raw).data;
					foreach (NvAppData a in apps)
					{
						if (a.appId == appid)
						{
							Debug.Log("LIME: " + mTag + ": App found: " + a.appName + " (id=" + a.appId + ")");
							Debug.Log("LIME: " + mTag + ": App validation success");
							Debug.Log(mTag + ": App validation success");
							break;
						}
					}
				}
			}
			catch (System.Exception e)
			{
				Debug.LogWarning(mTag + ": Could not validate app from list: " + e.Message);
			}
			try
			{
				// Wrap call to plugin to catch and log any exception raised in Java side
				Debug.Log("LIME: " + mTag + ": Calling Java StartApp(" + appid + ")...");
				mPlugin.Call("StartApp", appid);
				Debug.Log("LIME: " + mTag + ": mPlugin.Call(StartApp) returned");
			}
			catch (System.Exception e)
			{
				Debug.LogError(mTag + ": StartApp threw exception: " + e.Message + "\n" + e.StackTrace);
				// keep blocker visible so we can debug failures on the device
			}
		}

		public string GetRawlist(bool choice)
		{
			return mPlugin.Call<string>("GetList", choice);
		}
	}
}
