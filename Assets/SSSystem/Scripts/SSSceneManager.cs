﻿/**
 * Created by Anh Pham on 2013/11/13
 * Copyright (c) Anh Pham. All rights reserved.
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

#region Delegate
public delegate void SSCallBackDelegate(SSController ctrl);
#endregion

public enum SceneType
{
	SCREEN,
	SUB_SCREEN,
	POPUP,
	MENU,
	CLOSE
}

public class SceneData
{
	public string 				SceneName { get; private set; }
	public object 				Data { get; private set; }
	public SceneType 			Type { get; private set; }
	public SSCallBackDelegate 	OnActive { get; private set; }
	public SSCallBackDelegate 	OnDeactive { get; private set; }

	public SceneData(string sn, object dt, SSCallBackDelegate onActive, SSCallBackDelegate onDeactive, SceneType type)
	{
		SceneName = sn;
		Data = dt;
		Type = type;
		OnActive = onActive;
		OnDeactive = onDeactive;
	}
}

public enum Bgm
{
	NONE,
	SAME,
	PLAY
}

public class SSSceneManager : MonoBehaviour 
{	
	#region Serialize Field
	[SerializeField]
	protected GameObject m_LoadingPrefab;		// Loading indicator prefab  (optional)

	[SerializeField]
	protected int m_SceneDistance = 5000;			// The distance of loaded scenes in Base Scene

	[SerializeField]
	protected int m_DepthDistance = 20;				// The camera depth distance of popup layers

	[SerializeField]
	protected int m_ShieldTopIndex = 10;			// The index of shield top
	#endregion

	#region Singleton
	protected static SSSceneManager m_Instance;
	public static SSSceneManager Instance		// Singleton
	{
		get { return m_Instance; }
	}
	#endregion

	#region Protected Member
	protected Stack<string> 					m_Stack = new Stack<string>();					// Popup stack
	protected Queue<SceneData> 					m_Queue = new Queue<SceneData>();				// Command queue
	protected List<GameObject> 					m_ListShield = new List<GameObject>();			// List Shield
	protected Dictionary<string, GameObject> 	m_Dict = new Dictionary<string, GameObject>();	// Dictionary of loaded scenes

	protected GameObject m_Scenes;			// Scene container object
	protected GameObject m_Shields;			// Shield container object
	protected GameObject m_Menu;			// Menu object
	protected GameObject m_Sub;				// Current sub-scene object
	protected GameObject m_ShieldTop;		// Shield top object
	protected GameObject m_Loading;			// Loading object
	protected GameObject m_LoadingBack;		// Loading back object
	protected GameObject m_SolidCamera;		// Solid camera object (Lowest camera)

	protected int m_LoadingCount;			// Loading counter

	protected bool m_IsBusy;				// Busy when scene is loading or scene-animation is playing.
	#endregion

	#region Public Function
	/// <summary>
	/// Load or active a main-scene. All sub-scenes or popups which are showing will be deactive.
	/// </summary>
	/// <param name="sceneName">Scene name.</param>
	/// <param name="data">Data type is object type, allows any object.</param>
	/// <param name="onActive">OnActive callback.</param>
	/// <param name="onDeactive">OnDeactive callback.</param>
	public void Screen(string sceneName, object data = null, SSCallBackDelegate onActive = null, SSCallBackDelegate onDeactive = null)
	{
		string sn = sceneName;

		if (m_IsBusy) 
		{
			Enqueue(sn, data, onActive, onDeactive, SceneType.SCREEN);
			return;
		}

		if (IsSameScreen(sn)) 
		{
			Dequeue();
			return;
		}

		m_IsBusy = true;

		// Remove from stack and deactive
		while (m_Stack.Count > 0)
		{
			string p = m_Stack.Pop();
			DeactiveScene(p);
			ShieldOff();
		}

		// Remove current sub
		if (m_Sub != null)
		{
			DeactiveScene(m_Sub.name);
			m_Sub = null;
		}

		StartCoroutine(IEScreen(sn, data, onActive, onDeactive));
	}

	/// <summary>
	/// Load or active a sub-scene. The current sub-scenes which are showing will be deactive.
	/// </summary>
	/// <param name="sceneName">Scene name.</param>
	/// <param name="data">Data type is object type, allows any object.</param>
	/// <param name="onActive">OnActive callback.</param>
	/// <param name="onDeactive">OnDeactive callback.</param>
	public void SubScreen(string sceneName, object data = null, SSCallBackDelegate onActive = null, SSCallBackDelegate onDeactive = null)
	{
		string sn = sceneName;

		if (m_IsBusy) 
		{
			Enqueue(sn, data, onActive, onDeactive, SceneType.SUB_SCREEN);
			return;
		}

		if (m_Sub != null && string.Compare(sn, m_Sub.name) == 0)
		{
			Dequeue();
			return;
		}

		// Remove current sub
		CloseSubScene ();

		m_IsBusy = true;

		StartCoroutine(IESubScreen(sn, data, onActive, onDeactive));
	}

	/// <summary>
	/// Load or active a popup.
	/// </summary>
	/// <param name="sceneName">Scene name.</param>
	/// <param name="data">Data type is object type, allows any object.</param>
	/// <param name="onActive">OnActive callback.</param>
	/// <param name="onDeactive">OnDeactive callback.</param>
	public void PopUp(string sceneName, object data = null, SSCallBackDelegate onActive = null, SSCallBackDelegate onDeactive = null)
	{
		string sn = sceneName;

		if (m_IsBusy) 
		{
			Enqueue(sn, data, onActive, onDeactive, SceneType.POPUP);
			return;
		}

		if (IsPopUpShowed(sn)) 
		{
			return;
		}

		m_IsBusy = true;

		StartCoroutine(IEPopUp(sn, data, onActive, onDeactive));
	}

	/// <summary>
	/// Load a menu (only once).
	/// </summary>
	/// <param name="sceneName">Scene name.</param>
	/// <param name="data">Data type is object type, allows any object.</param>
	/// <param name="onActive">OnActive callback.</param>
	/// <param name="onDeactive">OnDeactive callback.</param>
	public void LoadMenu(string sceneName, object data = null, SSCallBackDelegate onActive = null, SSCallBackDelegate onDeactive = null)
	{
		string sn = sceneName;

		if (m_IsBusy) 
		{
			Enqueue(sn, data, onActive, onDeactive, SceneType.MENU);
			return;
		}

		if (m_Menu != null) 
		{
			ShowMenu ();
			Dequeue();
			return;
		}

		m_IsBusy = true;

		StartCoroutine(IEMenu(sn, data, onActive, onDeactive));
	}

	/// <summary>
	/// Shows the menu.
	/// </summary>
	public void ShowMenu()
	{
		if (m_Menu != null) 
		{
			m_Menu.SetActive (true);
		}
	}

	/// <summary>
	/// Hides the menu.
	/// </summary>
	public void HideMenu()
	{
		if (m_Menu != null) 
		{
			m_Menu.SetActive (false);
		}
	}

	/// <summary>
	/// Shows the loading indicator
	/// </summary>
	/// <param name="alpha">Alpha of shield</param>
	public void ShowLoading(float alpha = 0.2f)
	{
		if (m_LoadingPrefab == null) return;

		if (m_Loading == null) 
		{
			m_Loading = Instantiate (m_LoadingPrefab) as GameObject;
			m_Loading.name = m_LoadingPrefab.name;
			m_Loading.transform.localPosition = new Vector3((m_ShieldTopIndex+0.5f) * m_SceneDistance, 0, -0.1f);
		}

		ShieldTopOn (alpha);
		m_Loading.SetActive (true);

		m_LoadingCount++;
	}

	/// <summary>
	/// Hides the loading indicator.
	/// </summary>
	public void HideLoading()
	{
		if (m_Loading == null) return;

		m_LoadingCount--;
		
		if (m_LoadingCount == 0) 
		{
			ShieldTopOff ();
			m_Loading.SetActive (false);
		}
	}

	/// <summary>
		/// Closes the current sub-scene.
	/// </summary>
	public void CloseSubScene()
	{
		if (m_Sub != null)
		{
			StartCoroutine (DeactiveSceneFull (m_Sub.name, true));
			m_Sub = null;
		}
	}

	/// <summary>
	/// Close the top popup. If has no popup, quit app.
	/// </summary>
	/// <param name="immediate">If set to <c>true</c> immediate, close popup without animation.</param>
	public void Close(bool immediate = false)
	{
		if (m_IsBusy) 
		{
			Enqueue(null, immediate, null, null, SceneType.CLOSE);
			return;
		}

		m_IsBusy = true;

		StartCoroutine(IEClose(immediate));
	}
	#endregion

	#region Protected Function
	/// <summary>
	/// Awake this instance. Config something when app start. Override if necessary.
	/// </summary>
	protected virtual void Awake()
	{
		m_SolidCamera = Instantiate (Resources.Load ("SolidCamera")) as GameObject;
		m_SolidCamera.name = "SolidCamera";
		m_SolidCamera.transform.localPosition = new Vector3(-(m_ShieldTopIndex+0.5f) * m_SceneDistance, 0, 0);
		
		m_Scenes = new GameObject("Scenes");
		m_Shields = new GameObject("Shields");
		m_Instance = this;
	}

	/// <summary>
	/// Raises the lock event. When a popup is showed, the below scene will be locked. Override if necessary.
	/// </summary>
	/// <param name="scene">Scene which be locked</param>
	protected virtual void OnLock(GameObject scene)
	{
		SSController[] ctrls = scene.GetComponentsInChildren<SSController>(true);
		foreach (SSController ctrl in ctrls)
		{
			ctrl.OnLock();
		}
	}

	/// <summary>
	/// Raises the unlock event. When a popup is hided, the below scene will be unlocked. Override if necessary.
	/// </summary>
	/// <param name="scene">Scene which be unlocked</param>
	protected virtual void OnUnlock(GameObject scene)
	{
		SSController[] ctrls = scene.GetComponentsInChildren<SSController>(true);
		foreach (SSController ctrl in ctrls)
		{
			ctrl.OnUnlock();
		}
	}

	/// <summary>
	/// Play the BGM. Override it.
	/// </summary>
	/// <param name="bgmName">Bgm name.</param>
	protected virtual void PlayBGM(string bgmName)
	{
		Debug.LogWarning("Play BGM: " + bgmName + ". You have to override function: PlayBGM");
	}

	/// <summary>
	/// Stops the BGM. Override it
	/// </summary>
	protected virtual void StopBGM()
	{
		Debug.LogWarning("Stop BGM. You have to override function: StopBGM");
	}
	#endregion

	#region Private Function
	private IEnumerator LoadScene(string sn)
	{
		yield return Application.LoadLevelAdditiveAsync(sn);
		m_Dict.Add(sn, GameObject.Find(sn));
		m_Dict[sn].transform.parent = m_Scenes.transform;
	}

	private void ActiveScene(string sn)
	{
		m_Dict[sn].SetActive(true);
	}

	private void DeactiveScene(string sn)
	{
		bool isCache = true;

		GameObject sc = m_Dict[sn];
		SSController ct = sc.GetComponentInChildren<SSController>();
		sc.SetActive(false);

		if (ct != null)
		{
			isCache = ct.IsCache;
		}

		if (!isCache)
		{
			m_Dict.Remove(sn);
			Destroy(sc);
		}
	}

	private IEnumerator DeactiveSceneFull(string sn, bool immediate)
	{
		// Event
		SSController ct = m_Dict[sn].GetComponentInChildren<SSController>();
		if (ct != null)
		{
			ct.OnHide();
		}

		// Play Animation
		if (!immediate)
		{
			SSAnimation an = m_Dict[sn].GetComponentInChildren<SSAnimation>();
			if (an != null)
			{
				an.PlayHide();
				yield return new WaitForSeconds(an.TimeHide() + 0.1f);
			}
		}

		// Set Event
		if (ct != null)
		{
			if (ct.OnDeactive != null) ct.OnDeactive(ct);
		}

		DeactiveScene(sn);
	}

	private GameObject CreateShield(int i)
	{
		// Instantiate from resources
		GameObject sh = Instantiate(Resources.Load("Shield")) as GameObject;
		sh.name = "Shield" + i;
		sh.transform.localPosition = new Vector3((i+0.5f) * m_SceneDistance, 0, 0);
		sh.transform.parent = m_Shields.transform;

		// Set camera depth
		Camera c = sh.GetComponentInChildren<Camera>();
		c.depth = (i+1) * m_DepthDistance;

		return sh;
	}

	private void ShieldTopOn(float alpha = 0.2f)
	{
		if (m_ShieldTop == null) 
		{
			m_ShieldTop = CreateShield (m_ShieldTopIndex);
		} else 
		{
			m_ShieldTop.SetActive (true);
		}

		MeshRenderer mesh = m_ShieldTop.GetComponentInChildren<MeshRenderer> ();
		mesh.material.color = new Color (0, 0, 0, alpha);
	}

	private void ShieldTopOff()
	{
		if (m_ShieldTop != null) 
		{
			m_ShieldTop.SetActive (false);
		}
	}

	private void ShieldOn(int i)
	{
		if (i < 0) return;

		if (m_ListShield.Count <= i)
		{
			// Create shield
			GameObject sh = CreateShield (i);

			// Add to List
			m_ListShield.Add(sh);
		}
		else
		{
			m_ListShield[i].SetActive(true);
		}

		// Lock below scene
		GameObject scene = m_Dict[m_Stack.Peek()];
		OnLock(scene);

		// Lock Menu same time lock screen
		if (m_Stack.Count == 1 && m_Menu != null)
		{
			OnLock(m_Menu);
		}
	}

	private void ShieldOff()
	{
		int i = m_Stack.Count - 1;

		if (i < 0) return;

		m_ListShield[i].SetActive(false);

		// Unlock below scene
		GameObject scene = m_Dict[m_Stack.Peek()];
		OnUnlock(scene);

		// Unlock Menu same time unlock screen
		if (m_Stack.Count == 1 && m_Menu != null)
		{
			OnUnlock(m_Menu);
		}
	}

	private void SetPosition(string sn, int i)
	{
		GameObject sc = m_Dict[sn];
		sc.transform.localPosition = new Vector3(i * m_SceneDistance, m_SceneDistance, 0);
	}

	private void SetCameras(string sn, float i)
	{
		GameObject sc = m_Dict[sn];

		// Sort by depth
		List<Camera> cams = new List<Camera>(sc.GetComponentsInChildren<Camera>(true));
		cams = cams.OrderBy(n => n.depth).ToList<Camera>();

		// Re-set depth
		int c = 0;
		foreach (Camera cam in cams)
		{
			AudioListener al = cam.GetComponent<AudioListener>();
			if (al != null) al.enabled = false;

			cam.tag = "Untagged";
			cam.clearFlags = CameraClearFlags.Depth;
			cam.depth = Mathf.RoundToInt(i * m_DepthDistance) + c + 1;
			c++;
		}

		// Resort camera list by deactive then active again any camera (For NGUI Camera)
		cams[0].gameObject.SetActive(false);
		cams[0].gameObject.SetActive(true);
	}

	private string StackBottom()
	{
		string r = null;
		foreach (string s in m_Stack)
		{
			r = s;
		}
		return r;
	}

	private string StackTop()
	{
		if (m_Stack.Count == 0) return null;
		return m_Stack.Peek();
	}

	private bool IsSameScreen(string sn)
	{
		string b = StackBottom();

		if (string.IsNullOrEmpty(b)) return false;
		if (string.Compare(b, sn) == 0) return true;

		return false;
	}

	private bool IsPopUpShowed(string sn)
	{
		foreach (string s in m_Stack)
		{
			if (string.Compare(s, sn) == 0) return true;
		}

		return false;
	}

	private void BgmSceneOpen(string curBgm, SSController ctrl)
	{
		switch (ctrl.BgmType) 
		{
			case Bgm.NONE:
				StopBGM();
				break;

			case Bgm.PLAY:
				ctrl.CurrentBgm = ctrl.BgmName;
				if (!string.IsNullOrEmpty(ctrl.BgmName) )
			    {
					PlayBGM(ctrl.BgmName);
				}
				break;

			case Bgm.SAME:
				ctrl.CurrentBgm = curBgm;
				break;
		}
	}

	private void BgmSceneClose(SSController ctrl)
	{
		switch (ctrl.BgmType) 
		{
			case Bgm.NONE:
				StopBGM();
				break;

			case Bgm.PLAY:
			case Bgm.SAME:
				if (!string.IsNullOrEmpty(ctrl.CurrentBgm) )
				{
					PlayBGM(ctrl.CurrentBgm);
				}
				break;
		}
	}

	private void ShowLoadingBack()
	{
		if (m_LoadingPrefab == null) return;

		if (m_LoadingBack == null)
		{
			m_LoadingBack = Instantiate (m_LoadingPrefab) as GameObject;
			m_LoadingBack.name = m_LoadingPrefab.name + "Back";
			m_LoadingBack.transform.localPosition = new Vector3(-(m_ShieldTopIndex+0.5f) * m_SceneDistance, 0, 0.1f);
		}

		m_LoadingBack.SetActive (true);
	}

	private void HideLoadingBack()
	{
		if (m_LoadingBack != null) 
		{
			m_LoadingBack.SetActive (false);
		}
	}

	private IEnumerator IECommon(string sn, int i, object data, SSCallBackDelegate onActive, SSCallBackDelegate onDeactive, SceneType type, bool isInStack = true)
	{
		yield return StartCoroutine(IECommon(sn, i, i, data, onActive, onDeactive, type, isInStack));
	}

	private IEnumerator IECommon(string sn, int ip, float ic, object data, SSCallBackDelegate onActive, SSCallBackDelegate onDeactive, SceneType type, bool isInStack = true)
	{
		// Wait to avoid flicker
		//yield return new WaitForEndOfFrame();

		// Defaut BGM
		string curBgm = string.Empty;

		// Active Shield
		ShieldOn(ip-1);

		// Focus lost
		if (isInStack && m_Stack.Count > 0)
		{
			string s = m_Stack.Peek();
			SSController c = m_Dict[s].GetComponentInChildren<SSController>();
			if (c != null)
			{
				curBgm = c.CurrentBgm;
				c.OnFocusLost();
			}
		}

		// Animation
		SSAnimation an = null;

		// Load or active
		if (m_Dict.ContainsKey(sn))
		{
			ActiveScene(sn);

			// Animation
			an = m_Dict[sn].GetComponentInChildren<SSAnimation>();
			if (an != null)
			{
				an.transform.localPosition = new Vector3(99999, 0, 0);
				an.transform.localScale = Vector3.one;
			}
		}
		else
		{
			yield return StartCoroutine(LoadScene(sn));
		}

		// Set Position
		SetPosition(sn, ip);

		// Set Cameras
		SetCameras(sn, ic);

		if (isInStack)
		{
			// Add to Stack
			m_Stack.Push(sn);
		}

		// Set event & data
		SSController ct = m_Dict[sn].GetComponentInChildren<SSController>();
		if (ct != null)
		{
			ct.OnActive = onActive;
			ct.OnDeactive = onDeactive;

			if (ct.OnActive != null) ct.OnActive(ct);

			ct.OnSet(data);
		}

		// Play Animation
		if (an == null) an = m_Dict[sn].GetComponentInChildren<SSAnimation>();
		if (an != null)
		{
			yield return null;
			an.transform.localPosition = Vector3.zero;
			an.PlayShow();
			yield return new WaitForSeconds(an.TimeShow() + 0.1f);
		}

		// Event
		if (ct != null)
		{
			if (isInStack)
			{
				BgmSceneOpen(curBgm, ct);
			}
			ct.OnShow();
		}

		// Busy off
		m_IsBusy = false;

		// Set something by type
		SetByType(sn, type);

		// Check queue
		Dequeue();
	}

	private IEnumerator IEScreen(string sn, object data, SSCallBackDelegate onActive, SSCallBackDelegate onDeactive)
	{
		// Show system loading
		ShowLoadingBack ();

		// Common
		yield return StartCoroutine(IECommon(sn, 0, data, onActive, onDeactive, SceneType.SCREEN));
	}

	private IEnumerator IEPopUp(string sn, object data, SSCallBackDelegate onActive, SSCallBackDelegate onDeactive)
	{
		// Count
		int c = m_Stack.Count;

		// Next index
		int ni = c;

		// Common
		yield return StartCoroutine(IECommon(sn, ni, data, onActive, onDeactive, SceneType.POPUP));
	}

	private IEnumerator IEMenu(string sn, object data, SSCallBackDelegate onActive, SSCallBackDelegate onDeactive)
	{
		// Common
		yield return StartCoroutine(IECommon(sn, -1, 0.7f, data, onActive, onDeactive, SceneType.MENU, false));
	}

	private IEnumerator IESubScreen(string sn, object data, SSCallBackDelegate onActive, SSCallBackDelegate onDeactive)
	{
		// Common
		yield return StartCoroutine(IECommon(sn, -2, 0.4f, data, onActive, onDeactive, SceneType.SUB_SCREEN, false));
	}

	private IEnumerator IEClose(bool immediate)
	{
		// Nothing to close
		if (m_Stack.Count == 0) 
		{
			m_IsBusy = false;
			yield break;
		}

		// Lowest layer: Quit
		if (m_Stack.Count == 1) 
		{
			m_IsBusy = false;
			Debug.Log("Application.Quit");
			Application.Quit();
			yield break;
		}

		// Stack pop
		string sn = m_Stack.Pop();

		// Deactive Scene Full
		yield return StartCoroutine( DeactiveSceneFull (sn, immediate));

		// Focus back
		if (m_Stack.Count > 0)
		{
			string s = m_Stack.Peek();
			SSController c = m_Dict[s].GetComponentInChildren<SSController>();
			if (c != null)
			{
				c.OnFocusBack();
				BgmSceneClose(c);
			}
		}

		// Deactive Shield
		ShieldOff();

		// Busy off
		m_IsBusy = false;

		// Check queue
		Dequeue();
	}

	private void SetByType(string sn, SceneType type)
	{
		switch (type) 
		{
			case SceneType.SCREEN:
				HideLoadingBack ();
				break;
			case SceneType.SUB_SCREEN:
				m_Sub = m_Dict[sn];
				break;
			case SceneType.POPUP:
				break;
			case SceneType.MENU:
				m_Menu = m_Dict[sn];
				break;
			case SceneType.CLOSE:
				break;
			default:
				break;
		}
	}

	private void Enqueue(string sceneName, object data, SSCallBackDelegate onActive, SSCallBackDelegate onDeactive, SceneType type)
	{
		m_Queue.Enqueue(new SceneData(sceneName, data, onActive, onDeactive, type));
	}

	private void Dequeue()
	{
		if (m_Queue.Count == 0) return;

		// Check if this scene is popup & it was showed already
		SceneData sd = m_Queue.Peek ();
		if (sd.Type == SceneType.POPUP) 
		{
			if (IsPopUpShowed (sd.SceneName)) 
			{
				Debug.Log ("Wait Close: Close current popup to do next command");
				return;
			}
		}

		// Dequeue
		sd = m_Queue.Dequeue();
		switch (sd.Type) 
		{
		case SceneType.SCREEN:
				Screen(sd.SceneName, sd.Data, sd.OnActive, sd.OnDeactive);
				break;
		case SceneType.SUB_SCREEN:
				SubScreen(sd.SceneName, sd.Data, sd.OnActive, sd.OnDeactive);
				break;
		case SceneType.POPUP:
				PopUp(sd.SceneName, sd.Data, sd.OnActive, sd.OnDeactive);
				break;
		case SceneType.MENU:
				LoadMenu(sd.SceneName, sd.Data, sd.OnActive, sd.OnDeactive);
				break;
		case SceneType.CLOSE:
				Close((bool)sd.Data);
				break;
		}
	}
	#endregion

	#if UNITY_EDITOR || UNITY_ANDROID
	private void Update()
	{
		if (Input.GetKeyDown (KeyCode.Escape)) 
		{
			Close();
		}
	}
	#endif
}
