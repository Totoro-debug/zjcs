// Complete original movement implementation; see mainline character movement source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.ProfessionUI;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using Zjcs.Official.World.NativeTerrain.Common;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.EntityClientComponents
{
[RequireComponent(typeof(MyTransform))]
[DisallowMultipleComponent]
public class MoveAgent : ECViewMoveAgent, IMyEntityRemoveEventReceiver, IEventReceiver
{
	public SafeAction<GameObject> OnStartMove = new SafeAction<GameObject>();

	public SafeAction<GameObject> OnStopMove = new SafeAction<GameObject>();

	public SafeAction<Pos2d> OnTransferEnd = new SafeAction<Pos2d>();

	[SerializeField]
	[Tooltip("禁止转向")]
	private bool mForbidTurn;

	private MyEntity mMyEnt;

	private MyGridTransform mMyGridTrans;

	private IMapEntityMoveHandler mMoveHandler;

	public override bool IsMoving => mMoveHandler?.IsMoving ?? false;

	public UnityEngine.ILogger Logger { get; set; } = Debug.unityLogger;

	public IMapEntityMovementRuntime Runtime { get; set; }

	private void Awake()
	{
		mMyEnt = ((Component)this).GetComponent<MyEntity>();
		mMyGridTrans = ((Component)this).GetComponent<MyGridTransform>();
		mMoveHandler = new MapEntityMoveHandlerCharacterOther(mForbidTurn, mMyEnt, Logger, Runtime);
		IMapEntityMoveHandler mapEntityMoveHandler = mMoveHandler;
		mapEntityMoveHandler.OnStopMove += (Action)FireOnStopMove;
		IMapEntityMoveHandler mapEntityMoveHandler2 = mMoveHandler;
		mapEntityMoveHandler2.OnStartMove += (Action)FireOnStartMove;
		mMyEnt.RemoveEvent += (IMyEntityRemoveEventReceiver)this;
	}

	private void OnDestroy()
	{
		IMapEntityMoveHandler mapEntityMoveHandler = mMoveHandler;
		mapEntityMoveHandler.OnStopMove -= (Action)FireOnStopMove;
		IMapEntityMoveHandler mapEntityMoveHandler2 = mMoveHandler;
		mapEntityMoveHandler2.OnStartMove -= (Action)FireOnStartMove;
		mMoveHandler.Dispose();
		mMoveHandler = null;
		if ((Object)(object)mMyEnt != (Object)null)
		{
			mMyEnt.RemoveEvent -= (IMyEntityRemoveEventReceiver)this;
		}
	}

	public void Move(List<Pos2d> logicPath, float logicSpeed, long startTick = 0L, bool forceStartTick = false, MapMoveType moveType = MapMoveType.Normal)
	{
		mMoveHandler.Move(logicPath, logicSpeed, (startTick > 0) ? startTick : Runtime.GetTick(), forceStartTick, moveType);
	}

	public void Move(List<Vector3> path, float speed, MapMoveType moveType = MapMoveType.Normal)
	{
		mMoveHandler.Move(PathConvertTo(path), speed, 0L, forceStartTick: false, moveType);
	}

	public void UpdatePath(int index, List<Pos2d> logicPath)
	{
		mMoveHandler.UpdatePath(index, logicPath);
	}

	public void Stop(bool forceStop = false, Vector2? pos = null)
	{
		mMoveHandler.Stop(forceStop, pos);
	}

	public void StopByGrid(bool forceStop = false, Pos2d? grid = null)
	{
		Vector2? stopPos = null;
		if (grid.HasValue)
		{
			stopPos = (Vector2)(mMyGridTrans.GridToPosition(grid.Value));
		}
		mMoveHandler.Stop(forceStop, stopPos);
	}

	private void Update() => Advance(Time.deltaTime);

	public override void Stop() => Stop(forceStop: false, pos: null);

	public void Advance(float deltaTime)
	{
		if (mMoveHandler != null && mMoveHandler.IsMoving)
		{
			mMoveHandler.Update(deltaTime);
		}
	}

	public void DoTransferByAction(IActionMovementProvider component)
	{
		Pos2d targetPos = component.GetTargetPos();
		StopByGrid(!IsMoving, targetPos);
		OnTransferEnd.Invoke(targetPos);
	}

	void IMyEntityRemoveEventReceiver.MyEntity_OnRemove(MyEntity ent)
	{
		Stop(forceStop: true, (Vector2)(mMyGridTrans.GridToPosition(mMyGridTrans.Pos)));
	}

	private void FireOnStartMove()
	{
		OnStartMove.Invoke(((Component)mMyEnt).gameObject);
	}

	private void FireOnStopMove()
	{
		OnStopMove.Invoke(((Component)mMyEnt).gameObject);
	}

	private List<Pos2d> PathConvertTo(List<Vector3> path)
	{
		if (path == null || path.Count <= 0)
		{
			return null;
		}
		List<Pos2d> list = new List<Pos2d>();
		foreach (Vector3 item in path)
		{
			list.Add(mMyGridTrans.PositionToGrid(item));
		}
		return list;
	}
}

}
