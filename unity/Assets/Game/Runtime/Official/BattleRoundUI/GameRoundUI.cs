// Selected original client manager; only external driver/entity/loading queries are adapted.
namespace Zjcs.Official.BattleRoundUI
{
using Zjcs.Official.ProfessionUI;
using System;
using System.Collections.Generic;
using UnityEngine;

public class GameRoundUI : UIBase
{
	[Tooltip("头像Icon预制体")]
	public GameObject IconPrefab;

	[Tooltip("头像Icon预制体Scene")]
	public GameObject IconPrefabScene;

	[Tooltip("头像Icon预制体预先创建数量")]
	public int IconInitExtendCount;

	[Tooltip("头像背景")]
	public RectTransform IconBack;

	[Tooltip("头像Icon插槽")]
	public RectTransform IconSlot;

	[Tooltip("两个图标的最小距离占总距离的比例")]
	public float MinDist = 0.05f;

	[Tooltip("头像移动周期")]
	public float IconMoveDuration = 0.2f;

	private Vector3 mStartPos;

	private Vector3 mEndPos;

	private float mTotalTime;

	private List<IGameRoundCharacterItem> mIconList;

	private Queue<IGameRoundCharacterItem> mDyingIconQueue;

	private Stack<IGameRoundCharacterItem> mIconPool;

	private BattleRoundDriver mRoundDriver;

	private bool mInMoving;

	private float mMoveTime;

	private List<Vector3> mIconFromPosList = new List<Vector3>();

	private List<Vector3> mIconMoveList = new List<Vector3>();

	private HashSet<int> mWaitLoadEntitySet = new HashSet<int>();

	private List<float> mTimeLst = new List<float>();

	private List<Vector3> mIconTargetPosList = new List<Vector3>();

	private Transform mTransCache;

	private IBattleRoundUIBindings bindings;

	public void Initialize(IBattleRoundUIBindings value)
	{
		InitWrapper(() => bindings = value ?? throw new ArgumentNullException(nameof(value)));
	}

	private static BattleRoundData Data(IGameRoundCharacterItem item) => (BattleRoundData)item.RoundRuntimeData;

	private void Awake()
	{
		mTransCache = ((Component)this).transform;
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		InitOnStart();
		AddEvent();
		RefreshIcons();
	}

	private void Update()
	{
		if (mInMoving)
		{
			mMoveTime += Time.deltaTime;
			if (mMoveTime < IconMoveDuration)
			{
				UpdateFrameIconPos(mMoveTime / IconMoveDuration);
				return;
			}
			mInMoving = false;
			UpdateFrameIconPos(1f);
		}
	}

	protected override void OnDisableAndInited()
	{
		RemoveEvent();
		ClearIcons();
		base.OnDisableAndInited();
	}

	private void InitOnStart()
	{
		Rect rect = IconSlot.rect;
		mStartPos = new Vector3(rect.xMax, 0f, 0f);
		mEndPos = new Vector3(rect.xMin, 0f, 0f);
		mRoundDriver = bindings.Driver;
		InitPortraitIconPool();
		SetGameRoundSceneAttach();
	}

	private void InitPortraitIconPool()
	{
		mIconList = new List<IGameRoundCharacterItem>();
		mDyingIconQueue = new Queue<IGameRoundCharacterItem>();
		mIconPool = new Stack<IGameRoundCharacterItem>();
		GameObject portraitIconPrefab = GetPortraitIconPrefab();
		Transform portraitIconPoolTransform = GetPortraitIconPoolTransform();
		for (int i = 0; i < IconInitExtendCount; i++)
		{
			GameObject val = bindings.InstantiateCard(portraitIconPrefab, portraitIconPoolTransform);
			val.SetActive(false);
			mIconPool.Push(val.GetComponent<IGameRoundCharacterItem>());
		}
	}

	private GameObject GetPortraitIconPrefab()
	{
		if (bindings.Configuration.EnablePlayerPortraitETC2 && bindings.Configuration.EnablePlayerPortraitETC2_Scene)
		{
			return IconPrefabScene;
		}
		return IconPrefab;
	}

	private Transform GetPortraitIconPoolTransform()
	{
		if (bindings.Configuration.EnablePlayerPortraitETC2 && bindings.Configuration.EnablePlayerPortraitETC2_Scene)
		{
			return bindings.Scene.Transform;
		}
		return mTransCache;
	}

	private Transform GetPortraitIconSlotTransform()
	{
		if (bindings.Configuration.EnablePlayerPortraitETC2 && bindings.Configuration.EnablePlayerPortraitETC2_Scene)
		{
			return bindings.Scene.IconRoot;
		}
		return (Transform)(object)IconSlot;
	}

	private void SetGameRoundSceneAttach()
	{
		if (bindings.Configuration.EnablePlayerPortraitETC2 && bindings.Configuration.EnablePlayerPortraitETC2_Scene)
		{
			bindings.Scene.SetAttachUI(IconBack, bindings.SceneCamera);
			SpriteRenderer line = bindings.Scene.Line;
			float num = line.size.x * ((Component)line).transform.localScale.x;
			mStartPos = new Vector3(num / 2f, 0f, 0f);
			mEndPos = new Vector3((0f - num) / 2f, 0f, 0f);
		}
	}

	private void AddEvent()
	{
		mRoundDriver.OnDataRemove += OnRoundDataRemove;
		mRoundDriver.OnDataInsert += OnRoundDataInsert;
		mRoundDriver.OnDataUpdate += OnRoundDataUpdate;
		mRoundDriver.OnCurActingUpdated += OnCurrentActingUpdated;
		bindings.SameGameUpdated += OnSameGameUpdated;
		bindings.Damage += MainGame_OnDamage;
		bindings.Die += MainGame_OnDie;
		bindings.EntityAdded += MyContainer_OnEntityAdd;
	}

	private void RemoveEvent()
	{
		if (mRoundDriver != null)
		{
			mRoundDriver.OnDataRemove -= OnRoundDataRemove;
			mRoundDriver.OnDataInsert -= OnRoundDataInsert;
			mRoundDriver.OnDataUpdate -= OnRoundDataUpdate;
			mRoundDriver.OnCurActingUpdated -= OnCurrentActingUpdated;
		}
		bindings.SameGameUpdated -= OnSameGameUpdated;
		bindings.Damage -= MainGame_OnDamage;
		bindings.Die -= MainGame_OnDie;
		bindings.EntityAdded -= MyContainer_OnEntityAdd;
	}

	public void MyContainer_OnEntityAdd(int entityId)
	{
		if (mWaitLoadEntitySet.Remove(entityId))
		{
			BattleRoundData roundRuntimeData = mRoundDriver.FindData(entityId);
			if (roundRuntimeData != null)
			{
				OnRoundDataInsert(roundRuntimeData);
			}
		}
	}

	private void OnCurrentActingUpdated(int preEntityId, int curEntityId)
	{
		if (!bindings.IsPlaying)
		{
			return;
		}
		BattleRoundData roundRuntimeData = mRoundDriver.FindData(preEntityId);
		BattleRoundData data = mRoundDriver.FindData(curEntityId);
		IGameRoundCharacterItem icon = FindIcon(preEntityId);
		IGameRoundCharacterItem icon2 = FindIcon(curEntityId);
		if (preEntityId != curEntityId)
		{
			if (roundRuntimeData == null)
			{
				RemoveIcon(preEntityId);
			}
			else
			{
				UpdateIconData(icon, roundRuntimeData, isCurrent: false);
			}
		}
		UpdateIconData(icon2, data, isCurrent: true);
		UpdateAllIconPos(immediately: false);
	}

	private void MainGame_OnDamage(BattleRoundDamage args)
	{
		if (!CanHurtByDamage(ref args))
		{
			return;
		}
		for (int i = 0; i < mIconList.Count; i++)
		{
			IGameRoundCharacterItem gameRoundCharacterItem = mIconList[i];
			if (!(Data(gameRoundCharacterItem).EntityId != args.TargetId))
			{
				gameRoundCharacterItem.SetHurt();
				break;
			}
		}
	}

	private void MainGame_OnDie(int sourceId, int sourceFaction, int targetId, int targetFaction)
	{
		for (int i = 0; i < mIconList.Count; i++)
		{
			IGameRoundCharacterItem gameRoundCharacterItem = mIconList[i];
			if (!(Data(gameRoundCharacterItem).EntityId != targetId))
			{
				gameRoundCharacterItem.SetDead(OnIconDeadCallback);
				mDyingIconQueue.Enqueue(gameRoundCharacterItem);
				mIconList.RemoveAt(i);
				break;
			}
		}
		UpdateAllIconPos(immediately: false);
	}

	private void OnRoundDataInsert(BattleRoundData data)
	{
		if (!IsRoundAvailable(data.EntityId, out var entityNotExist))
		{
			if (entityNotExist)
			{
				mWaitLoadEntitySet.Add(data.EntityId);
			}
		}
		else
		{
			InsertIcon(data, isCurrent: false);
			UpdateAllIconPos(immediately: true);
		}
	}

	private void OnRoundDataRemove(int entityId)
	{
		RemoveIcon(entityId);
		UpdateAllIconPos(immediately: false);
	}

	private void OnRoundDataUpdate(BattleRoundData data, BattleRoundUpdateState state)
	{
		if (!bindings.IsPlaying)
		{
			return;
		}
		if (!IsRoundAvailable(data.EntityId, out var entityNotExist))
		{
			if (entityNotExist)
			{
				mWaitLoadEntitySet.Add(data.EntityId);
			}
		}
		else
		{
			if (mRoundDriver.CurActingEntityId == data.EntityId)
			{
				return;
			}
			bool flag = false;
			IGameRoundCharacterItem gameRoundCharacterItem = null;
			foreach (IGameRoundCharacterItem mIcon in mIconList)
			{
				if (Data(mIcon).EntityId == data.EntityId)
				{
					flag = true;
					mIcon.UpdateRoundData(data);
					gameRoundCharacterItem = mIcon;
					break;
				}
			}
			if (flag)
			{
				mIconList.Remove(gameRoundCharacterItem);
				InsertIcon(gameRoundCharacterItem);
				bool immediately = state != BattleRoundUpdateState.None;
				UpdateAllIconPos(immediately);
			}
		}
	}

	private void OnSameGameUpdated()
	{
		RemoveEvent();
		ClearIcons();
		InitOnStart();
		AddEvent();
		RefreshIcons();
	}

	private void UpdateIconData(IGameRoundCharacterItem icon, BattleRoundData data, bool isCurrent)
	{
		if (icon != null && data != null && IsRoundAvailable(data.EntityId, out var _))
		{
			BattleRoundRole fightRoleCharacterComponent = bindings.GetRole(data.EntityId);
			if (fightRoleCharacterComponent != null)
			{
				bindings.PrepareCard(icon, data.EntityId);
				icon.Refresh(data, isCurrent, fightRoleCharacterComponent);
				RemoveIcon(icon);
				InsertIcon(icon);
			}
		}
	}

	private bool CanHurtByDamage(ref BattleRoundDamage args)
	{
		long num = args.Damage + -args.Shield;
		if (num > 0) return false;
		if (num < 0) return true;
		return args.HasHitEntity && args.DamageType != 0;
	}

	private void RefreshIcons()
	{
		ClearIcons();
		BattleRoundDriver roundDriver = bindings.Driver;
		BattleRoundData curActingData = roundDriver.GetCurActingData();
		if (curActingData != null && IsRoundAvailable(curActingData.EntityId, out var entityNotExist))
		{
			InsertIcon(curActingData, isCurrent: true);
			mTotalTime = (float)curActingData.Interval;
		}
		foreach (BattleRoundData data in roundDriver.DataList)
		{
			if ((curActingData == null || !(data.EntityId == curActingData.EntityId)) && IsRoundAvailable(data.EntityId, out entityNotExist))
			{
				InsertIcon(data, isCurrent: false);
				double num = data.Interval;
				if (num > (double)mTotalTime)
				{
					mTotalTime = (float)num;
				}
			}
		}
		UpdateAllIconPos(immediately: true);
	}

	private IGameRoundCharacterItem InstanceIcon(BattleRoundData data, bool isCurrent)
	{
		BattleRoundRole fightRoleCharacterComponent = bindings.GetRole(data.EntityId);
		if (fightRoleCharacterComponent == null)
		{
			return null;
		}
		IGameRoundCharacterItem gameRoundCharacterItem = null;
		if (mIconPool.Count > 0)
		{
			gameRoundCharacterItem = mIconPool.Pop();
			gameRoundCharacterItem.Transform.SetParent(GetPortraitIconSlotTransform(), false);
			gameRoundCharacterItem.OnGameObjectPoolReset();
		}
		else
		{
			gameRoundCharacterItem = bindings.InstantiateCard(GetPortraitIconPrefab(), GetPortraitIconSlotTransform()).GetComponent<IGameRoundCharacterItem>();
		}
		bindings.PrepareCard(gameRoundCharacterItem, data.EntityId);
		gameRoundCharacterItem.Init(data, isCurrent, fightRoleCharacterComponent);
		((Component)gameRoundCharacterItem.Transform).gameObject.SetActive(true);
		return gameRoundCharacterItem;
	}

	private void DestroyIcon(IGameRoundCharacterItem icon)
	{
		if (icon != null)
		{
			icon.OnGameObjectPoolClear();
			((Component)icon.Transform).gameObject.SetActive(false);
			icon.Transform.SetParent(GetPortraitIconPoolTransform(), false);
		}
	}

	private void ClearIcons()
	{
		foreach (IGameRoundCharacterItem mIcon in mIconList)
		{
			DestroyIcon(mIcon);
		}
		mIconList.Clear();
	}

	private void InsertIcon(BattleRoundData data, bool isCurrent)
	{
		if (data != null)
		{
			IGameRoundCharacterItem gameRoundCharacterItem = InstanceIcon(data, isCurrent);
			if (gameRoundCharacterItem != null)
			{
				InsertIcon(gameRoundCharacterItem);
			}
		}
	}

	private void InsertIcon(IGameRoundCharacterItem icon)
	{
		int num = CalcInsertIndex(icon);
		if (num < mIconList.Count)
		{
			mIconList.Insert(num, icon);
		}
		else
		{
			num = mIconList.Count;
			mIconList.Add(icon);
		}
		icon.SetSiblingIndex(mIconList.Count - num - 1);
	}

	private int CalcInsertIndex(IGameRoundCharacterItem icon)
	{
		if (icon.IsCurrent)
		{
			return 0;
		}
		for (int i = 0; i < mIconList.Count; i++)
		{
			if (Data(mIconList[i]).NextTime > Data(icon).NextTime)
			{
				return i;
			}
		}
		return mIconList.Count;
	}

	private void RemoveIcon(int roundEntityId)
	{
		IGameRoundCharacterItem icon = FindIcon(roundEntityId);
		RemoveIcon(icon);
		DestroyIcon(icon);
	}

	private void RemoveIcon(IGameRoundCharacterItem icon)
	{
		if (icon != null)
		{
			mIconList.Remove(icon);
		}
	}

	private IGameRoundCharacterItem FindIcon(int roundEntityId)
	{
		return mIconList.Find((IGameRoundCharacterItem item) => Data(item).EntityId == roundEntityId);
	}

	private void UpdateAllIconPos(bool immediately)
	{
		if (mIconList.Count == 0)
		{
			return;
		}
		float num = mTotalTime * MinDist;
		mTimeLst.Clear();
		mTimeLst.Add(0f);
		double num2 = 0.0;
		for (int i = 1; i < mIconList.Count; i++)
		{
			IGameRoundCharacterItem gameRoundCharacterItem = mIconList[i];
			IGameRoundCharacterItem gameRoundCharacterItem2 = mIconList[i - 1];
			double num3 = Data(gameRoundCharacterItem).NextTime - Data(gameRoundCharacterItem2).NextTime;
			if (num3 < (double)num)
			{
				num3 = num;
			}
			num2 += num3;
			mTimeLst.Add((float)num2);
		}
		List<float> list = mTimeLst;
		float num4 = Mathf.Max(list[list.Count - 1], mTotalTime);
		if (num4 < 1E-05f)
		{
			return;
		}
		mIconFromPosList.Clear();
		mIconMoveList.Clear();
		mIconTargetPosList.Clear();
		int j = 0;
		for (int count = mIconList.Count; j < count; j++)
		{
			IGameRoundCharacterItem gameRoundCharacterItem3 = mIconList[j];
			Vector3 localPosition = gameRoundCharacterItem3.Transform.localPosition;
			Vector3 val = Vector3.Lerp(mStartPos, mEndPos, mTimeLst[j] / num4);
			mIconFromPosList.Add(localPosition);
			mIconTargetPosList.Add(val);
			if (localPosition.x <= val.x)
			{
				mIconMoveList.Add(val - localPosition);
			}
			else
			{
				mIconMoveList.Add(val - mEndPos + mStartPos - localPosition);
			}
			if (j == 0)
			{
				gameRoundCharacterItem3.SetActing();
			}
			else
			{
				gameRoundCharacterItem3.SetIdle();
			}
		}
		if (immediately)
		{
			mInMoving = false;
			UpdateFrameIconPos(1f);
		}
		else
		{
			mInMoving = true;
			mMoveTime = 0f;
		}
	}

	private void UpdateFrameIconPos(float percent)
	{
		for (int i = 0; i < mIconList.Count; i++)
		{
			Vector3 val = mIconFromPosList[i];
			Vector3 val2 = Vector3.Lerp(Vector3.zero, mIconMoveList[i], percent);
			Vector3 val3 = val + val2;
			if (val3.x > mStartPos.x)
			{
				val3.x -= mStartPos.x - mEndPos.x;
			}
			if (Mathf.Approximately(percent, 1f))
			{
				mIconList[i].Transform.localPosition = mIconTargetPosList[i];
			}
			else
			{
				mIconList[i].Transform.localPosition = val3;
			}
		}
	}

	private void OnIconDeadCallback()
	{
		if (mDyingIconQueue.Count != 0)
		{
			IGameRoundCharacterItem gameRoundCharacterItem = mDyingIconQueue.Dequeue();
			if (gameRoundCharacterItem != null)
			{
				DestroyIcon(gameRoundCharacterItem);
			}
		}
	}

	private bool IsRoundAvailable(int entityId, out bool entityNotExist)
	{
		entityNotExist = !bindings.HasEntity(entityId);
		if (entityNotExist) return false;
		return bindings.ShowOnRoundProgress(entityId);
	}
}

}
