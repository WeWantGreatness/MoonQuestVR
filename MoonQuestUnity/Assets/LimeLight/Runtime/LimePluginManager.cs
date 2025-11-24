using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace PCP.LibLime
{
	public class LimePluginManager : MonoBehaviour
	{
		public static LimePluginManager Instance;

		private readonly string mTag = "LimePluginManager";
		private readonly float laodingTimeout = 10f;
		private AndroidJavaObject mPluginManager;
		private StreamManager mStreamManager;
		private PcManager mPcManager;
		private AppManager mAppManger;
		private InputManager mInputManager;
		private bool shouldResume = false;
		[SerializeField] private GameObject mPanelCanvas;
		public enum PluginType
		{
			Pc,
			App,
			Stream,
		}
		public bool Blocking { private set; get; } = true;

		//LifeCycle///////////
		private void Awake()
		{
			if (Instance != null)
			{
				Debug.LogError(mTag + ":Double Instance Found");
				return;
			}
			Instance = this;
			mStreamManager = GetComponent<StreamManager>();
			mPcManager = GetComponent<PcManager>();
			mAppManger = GetComponent<AppManager>();
			mInputManager = GetComponent<InputManager>();
			OnJavaCallback += ChangeUIHandler;
			Debug.Log(mTag + ": Initialized");
		}

		private void Start()
		{
			StartCoroutine(WaitForPermissionsAndInitialize());
		}

		private System.Collections.IEnumerator WaitForPermissionsAndInitialize()
		{
			// Wait for permissions (adjust time as needed)
			Debug.Log(mTag + ": Waiting for permissions...");
			yield return new WaitForSeconds(8f); // Delay to allow permission prompts
			Debug.Log(mTag + ": Proceeding with initialization");
			CreatePluginObject();
			StartPc();
		}
		private void CreatePluginObject()
		{
			mPluginManager = new AndroidJavaObject("com.liblime.PluginManager");
			mPluginManager.Call("Init");
			Blocking = false;
			Debug.Log(mTag + ": Plugin created, Blocking = " + Blocking);
		}

		private void OnApplicationPause(bool pause)
		{
			if (pause)
			{
				if (mStreamManager.IsInitialized)
					shouldResume = true;
				DoReset(false);
			}
			else if (shouldResume)
			{
				shouldResume = false;
				Debug.Log("Resuming Last Stream");
				StartLastApp();
			}
		}


		private void OnDestroy()
		{
			/* StopAllCoroutines(); */
			/* DestroyAllPluginObjects(); */
			DoReset(false);
			mPluginManager.Call("Destroy");
			mPluginManager.Dispose();
			mPluginManager = null;
		}
		//Plugin Methods///////////
		public void StartPc()
		{
			mPluginManager.Call("StartPC");
			StartManager(PluginType.Pc);
		}
		public bool IsAlive()
		{
			if (mPluginManager == null)
			{
				Debug.LogError(mTag + " is null");
				return false;
			}
			try
			{
				mPluginManager.Call("Poke");
			}
			catch (Exception e)
			{
				Debug.LogWarning(mTag + " poking failed:" + e.Message);
				return false;
			}
			return true;
		}
		public bool HasRunningPlugin()
		{
			return mPluginManager.Call<bool>("HasRunningPlugin");
		}

		private void DestroyPluginObject(PluginType t)
		{
			mPluginManager.Call("DestroyPlugin", (int)t);
		}
		public void DestroyAllPluginObjects()
		{
			mPluginManager.Call("DestroyAllPlugins");
		}

		public void DoReset(bool wait = true)
		{
			if (CheckBlocking())
				return;
			//Check if there is any running manager
			StopAllCoroutines();
			StopManagers();
			ResetPlugin(wait);
		}
		public void ResetPlugin(bool wait)
		{
			Blocking = true;
			if (mPluginManager == null)
			{
				Debug.LogError("PluginManager is null");
				return;
			}
			DestroyAllPluginObjects();
			if (wait)
				StartCoroutine(TaskResetPlugin());
			else
			{
				Debug.Log("Reset Plugin without waiting");
				Blocking = false;
				Blocker.SetActive(false);
			}
		}
		private IEnumerator TaskResetPlugin()
		{
			yield return new WaitForEndOfFrame();
			while (HasRunningPlugin())
			{
				yield return new WaitForEndOfFrame();
			}
			Debug.Log("All Plugin Destroyed");
			Blocking = false;
			Blocker.SetActive(false);
		}
		//Manager Methods///////////
		public void StartManager(PluginType t)
		{
			/* if (t == PluginType.Pc) */
			/* 	mPluginManager.Call("StartPC"); */
			StartCoroutine(InitManager(t));
		}
		private IEnumerator InitManager(PluginType t)
		{
			Debug.Log("Try to find Plugin:" + t);
			if (CheckBlocking())
				yield break;
			float timer = 0;
			//TODO: if we are using plugin callback to start manager, we can remove this.but what if
			//plugin failed to load?
			AndroidJavaObject o = mPluginManager.Call<AndroidJavaObject>("GetPlugin", (int)t);
			while (o == null)
			{
				if (timer > laodingTimeout)
				{
					Debug.LogError("LIME: Loading Timeout:Cannot get plugin for " + t);
					yield break;
				}
				Debug.Log("Foudning plugin:" + t + "Time:" + timer);
				yield return new WaitForSeconds(1);
				o = mPluginManager.Call<AndroidJavaObject>("GetPlugin", (int)t);
				timer += Time.deltaTime;
			}
			switch (t)
			{
				case PluginType.Pc:
					mPcManager.Init(o, this);
					break;
				case PluginType.Stream:
					mStreamManager.Init(o, this);
					break;
				case PluginType.App:
					mAppManger.Init(o, this);
					break;
				default:
					break;
			}
		}
		private void StopManagers()
		{
			mAppManger.enabled = false;
			mPcManager.enabled = false;
			mStreamManager.enabled = false;
			Debug.Log("All Managers Stopped");
		}
		//Utils
		private bool CheckBlocking()
		{
			if (Blocking)
			{
				Debug.LogWarning(mTag + " is blocking");
				return true;
			}
			return false;
		}
		//Message Handlers
		public delegate void JavaCallbackHandler(string msg);
		public JavaCallbackHandler OnJavaCallback;
		public void OnCallback(string msg)
		{
			Debug.Log(mTag + "JavaCallback Received:" + msg);
			OnJavaCallback?.Invoke(msg);
		}

		//Dialog and Notification
		public GameObject Blocker;
		public GameObject DialogWindow;
		public TMP_Text DialogText;
		public void OnDialog(string m)
		{
			string[] msglsit = m.Split('|');
			string msg = msglsit[0];
			int level = int.Parse(msglsit[1]);
			DialogText.text = msg;
			DialogWindow.SetActive(true);
			switch (level)
			{
				case 0:
					/* MessageManager.Instance.Info(msg); */
					break;
				case 1:
					/* MessageManager.Instance.Warn(msg); */
					break;
				case 2:
					/* MessageManager.Instance.Error(msg); */
					DoReset();
					break;
				default:
					break;
			}
		}
		public NotificationPool notificationPool;

		public void OnNotify(string message)
		{
			Notification notification = notificationPool.Get();
			if (notification != null)
			{
				notification.Display(message);
			}
		}
		//Shortcut
		internal void StartShortcut(ShortcutData sd)
		{
			StartManager(PluginType.Stream);
			mPluginManager.Call("DoShortcut", sd.uuid, sd.appName, sd.appID.ToString());
		}
		//TODO:use file and array to store the last app,should check out if can access the persistent
		//data path
		/* internal void UpdateLastApp(ShortcutData sd) */
		/* { */
		/* 	PlayerPrefs.SetString("LastApp", JsonUtility.ToJson(sd)); */
		/* } */
		public void StartLastApp()
		{
			string la = PlayerPrefs.GetString("LastApp", "");
			if (la == "")
				return;
			Blocker.SetActive(true);
			StartShortcut(JsonUtility.FromJson<ShortcutData>(la));
		}
		//UI
		public void ChangeUIHandler(string msg)
		{
			if (!msg.StartsWith("UI"))
				return;
			msg = msg[2..];
			switch (msg)
			{
				case "PC":
					ChangeUIRoot(PluginType.Pc);
					break;
				case "APP":
					ChangeUIRoot(PluginType.App);
					break;
				case "STM":
					ChangeUIRoot(PluginType.Stream);
					break;
				default:
					break;
			}
			Debug.Log("UI Changed to:" + msg);
		}
		public void ChangeUIRoot(PluginType t)
		{
			Debug.Log("ChangeUIRoot:" + t);
			//TODO: need a map for that
			if (t != PluginType.Pc)
				mPcManager.enabled = false;
			if (t != PluginType.App)
				mAppManger.enabled = false;
			if (t != PluginType.Stream)
				mStreamManager.enabled = false;
		}
		//TRY Debug
		public void TestDialog(bool t)
		{
			mPluginManager.Call("TestDialog", t);
		}
		public void TestNotify(string m)
		{
			mPluginManager.Call("TestNotify", m);
		}

		// UI management for streaming
		public void HideUI()
		{
			if (mPanelCanvas != null)
			{
				// Disable GraphicRaycaster FIRST to prevent ray interactor from detecting hidden UI
				UnityEngine.UI.GraphicRaycaster raycaster = mPanelCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
				if (raycaster != null)
				{
					raycaster.enabled = false;
					Debug.Log(mTag + ": Disabled GraphicRaycaster");
				}
				
				// Disable CanvasGroup interactable/raycastTarget if present
				CanvasGroup canvasGroup = mPanelCanvas.GetComponent<CanvasGroup>();
				if (canvasGroup != null)
				{
					canvasGroup.interactable = false;
					canvasGroup.blocksRaycasts = false;
					Debug.Log(mTag + ": Disabled CanvasGroup raycast");
				}
				
				// Disable ALL colliders on canvas and all children (including inactive ones)
				Collider[] allColliders = mPanelCanvas.GetComponentsInChildren<Collider>(true);
				foreach (Collider col in allColliders)
				{
					col.enabled = false;
				}
				if (allColliders.Length > 0)
				{
					Debug.Log(mTag + ": Disabled " + allColliders.Length + " colliders");
				}
				
				// Disable ALL Canvas components in children (in case there are nested canvases)
				Canvas[] allCanvases = mPanelCanvas.GetComponentsInChildren<Canvas>(true);
				foreach (Canvas canvas in allCanvases)
				{
					if (canvas != null && canvas.gameObject != mPanelCanvas)
					{
						canvas.enabled = false;
					}
				}
				
				// Finally, deactivate the entire GameObject and all children
				mPanelCanvas.SetActive(false);
				
				Debug.Log(mTag + ": UI completely hidden (canvas, raycasters, colliders all disabled)");
			}
		}

		public void ShowUI()
		{
			if (mPanelCanvas != null)
			{
				// Activate the GameObject first
				mPanelCanvas.SetActive(true);
				
				// Re-enable ALL Canvas components in children
				Canvas[] allCanvases = mPanelCanvas.GetComponentsInChildren<Canvas>(true);
				foreach (Canvas canvas in allCanvases)
				{
					if (canvas != null)
					{
						canvas.enabled = true;
					}
				}
				
				// Re-enable ALL colliders on canvas and all children
				Collider[] allColliders = mPanelCanvas.GetComponentsInChildren<Collider>(true);
				foreach (Collider col in allColliders)
				{
					col.enabled = true;
				}
				
				// Enable CanvasGroup interactable/raycastTarget if present
				CanvasGroup canvasGroup = mPanelCanvas.GetComponent<CanvasGroup>();
				if (canvasGroup != null)
				{
					canvasGroup.interactable = true;
					canvasGroup.blocksRaycasts = true;
				}
				
				// Enable GraphicRaycaster LAST to allow ray interactor to detect UI
				UnityEngine.UI.GraphicRaycaster raycaster = mPanelCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
				if (raycaster != null)
				{
					raycaster.enabled = true;
				}
				
				Debug.Log(mTag + ": UI shown (canvas, raycasters, colliders all enabled)");
			}
		}
		
		/// <summary>
		/// Toggles the UI panel visibility. Opens if closed, closes if open.
		/// Also properly enables/disables raycast interaction.
		/// </summary>
		public void ToggleUI()
		{
			if (mPanelCanvas != null)
			{
				bool isCurrentlyActive = mPanelCanvas.activeSelf;
				
				if (isCurrentlyActive)
				{
					HideUI(); // Use HideUI to properly disable raycast
				}
				else
				{
					ShowUI(); // Use ShowUI to properly enable raycast
				}
			}
			else
			{
				Debug.LogWarning(mTag + ": Cannot toggle UI - mPanelCanvas is null");
			}
		}
		
		/// <summary>
		/// Checks if the UI panel is currently visible.
		/// </summary>
		public bool IsUIVisible()
		{
			return mPanelCanvas != null && mPanelCanvas.activeSelf;
		}
	}
}
