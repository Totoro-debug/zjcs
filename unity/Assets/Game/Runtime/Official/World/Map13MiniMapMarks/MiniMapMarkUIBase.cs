using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.StoryPlayables;
using IWindowOperator=Zjcs.Official.ProfessionUI.IWindowOperator;
public abstract class MiniMapMarkUIBase : MonoBehaviour, IGameObjectPoolItem
{
	private MiniMapMarkBase mMark;

	private List<MiniMapMarkSupportBase> mMarkSupports = new List<MiniMapMarkSupportBase>();

	public abstract MiniMapMarkType MarkType { get; }

	public virtual bool CanInteractive => false;

	public MiniMapMarkContext MiniMap { get; private set; }

	public int Region { get; private set; } = -1;

	public string MarkAssetPath { get; set; }

	public MiniMapMarkBase Mark
	{
		get
		{
			return mMark;
		}
		private set
		{
			MiniMapMarkBase obj = mMark;
			mMark = value;
			OnMarkUpdate?.Invoke(obj);
		}
	}

	public Pos2d GridPos => Mark.GridPos;

	public List<Pos2d> BodyRange => Mark.BodyRange;

	public event Action<MiniMapMarkBase> OnMarkUpdate;

	protected virtual void Awake()
	{
		((Component)this).GetComponents<MiniMapMarkSupportBase>(mMarkSupports);
	}

	public virtual void Init(MiniMapMarkContext miniMap, MiniMapMarkBase mark)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		MiniMap = miniMap;
		Mark = mark;
		Region = mark.Region;
		((Component)this).transform.localPosition = Mark.LocalPosition;
		foreach (MiniMapMarkSupportBase mMarkSupport in mMarkSupports)
		{
			mMarkSupport.Init(this);
		}
	}

	public virtual void Release()
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		foreach (MiniMapMarkSupportBase mMarkSupport in mMarkSupports)
		{
			mMarkSupport.Release(this);
		}
		((Component)this).transform.localPosition = Vector3.zero;
		Mark = null;
		MiniMap = null;
	}

	public abstract void OpenDialog();

	protected virtual void InitDialog(MiniMapMarkDialogBase dialog)
	{
		dialog.Init(this);
	}

	public void OnGameObjectPoolReset()
	{
	}

	public void OnGameObjectPoolClear()
	{
		Release();
	}

	protected virtual void OnDestroy()
	{
		Release();
	}
}

}
