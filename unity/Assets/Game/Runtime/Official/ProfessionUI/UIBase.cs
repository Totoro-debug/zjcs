namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class UIBase : MonoBehaviour, IWindowOperator
{
	private PopupWindow mPopupWindow;

	protected bool _Inited;

	public PopupWindow PopupWindow
	{
		get
		{
			if ((Object)(object)mPopupWindow == (Object)null)
			{
				mPopupWindow = ((Component)this).GetComponentInParent<PopupWindow>();
			}
			return mPopupWindow;
		}
	}

	public GameObject Operator => ((Component)this).gameObject;


	protected virtual void Start()
	{
	}

	protected virtual void OnDestroy()
	{
	}

	protected void InitWrapper(Action callback)
	{
		InitStart();
		callback?.Invoke();
		InitFinish();
	}

	protected bool IsEnableAndInited()
	{
		if (_Inited)
		{
			return ((Behaviour)this).isActiveAndEnabled;
		}
		return false;
	}

	protected void InitStart()
	{
		if (IsEnableAndInited())
		{
			_OnDisableAndInited();
		}
	}

	protected void InitFinish()
	{
		_Inited = true;
		if (IsEnableAndInited())
		{
			_OnEnableAndInited();
		}
	}

	protected virtual void OnEnable()
	{
		if (IsEnableAndInited())
		{
			_OnEnableAndInited();
		}
	}

	private void _OnEnableAndInited()
	{
		try
		{
			OnEnableAndInited();
		}
		catch (Exception ex)
		{
			Debug.LogError("OnEnableAndInited excep, object name:" + gameObject.name, this);
			Debug.LogException(ex, this);
		}
	}

	protected virtual void OnEnableAndInited()
	{
	}

	protected virtual void OnDisable()
	{
		if (_Inited)
		{
			_OnDisableAndInited();
		}
	}

	private void _OnDisableAndInited()
	{
		try
		{
			OnDisableAndInited();
		}
		catch (Exception ex)
		{
			Debug.LogError("OnDisableAndInited excep, object name:" + GetFullName(gameObject), this);
			Debug.LogException(ex, this);
		}
	}

	public string GetFullName(GameObject go)
	{
		Transform val = go.transform;
		List<Transform> list = new List<Transform>();
		while ((Object)(object)val != (Object)null)
		{
			list.Add(val);
			val = val.parent;
		}
		StringBuilder stringBuilder = new StringBuilder();
		for (int num = list.Count - 1; num >= 0; num--)
		{
			stringBuilder.Append("/" + ((Object)((Component)list[num]).gameObject).name);
		}
		return stringBuilder.ToString();
	}

	protected virtual void OnDisableAndInited()
	{
	}

	public virtual void ResetInitState()
	{
		ResetInitState(needDisable: true);
	}

	public void ResetInitState(bool needDisable)
	{
		if (_Inited)
		{
			_Inited = false;
			if (needDisable && ((Component)this).gameObject.activeInHierarchy)
			{
				_OnDisableAndInited();
			}
		}
	}
}

}
