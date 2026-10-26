// Migrated official Map13 world-label call path; see source receipt.
namespace Zjcs.Official.World.NativeMap13Labels
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class UIBase : MonoBehaviour
{
	public IMap13LabelServices Services { get; set; }

	protected bool _Inited;



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
			UnityEngine.Debug.LogException(ex);
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
			UnityEngine.Debug.LogException(ex);
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
