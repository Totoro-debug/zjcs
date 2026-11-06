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
public abstract class MapEntityMoveHandlerBase : IMapEntityMoveHandler, IDisposable
{
	protected const float kDistanceEpsilon = 0.01f;

	protected bool mForbidTurn;

	protected MyEntity mMyEnt;

	protected MyGridTransform mMyGridTrans;

	private MyTransform mMyTransform;

	private IMoveEffectSupport[] mMoveEffectSupports;

	public SafeAction OnStartMove { get; set; } = new SafeAction();

	public SafeAction OnStopMove { get; set; } = new SafeAction();

	public bool IsMoving { get; private set; }

	protected Vector3 TargetPosition
	{
		get
		{
			return mMyTransform.Position;
		}
	}

	protected UnityEngine.ILogger Logger { get; private set; }

	protected IMapEntityMovementRuntime Runtime { get; private set; }

	public MapEntityMoveHandlerBase(bool forbidTurn, MyEntity myEnt, UnityEngine.ILogger logger, IMapEntityMovementRuntime runtime)
	{
		mForbidTurn = forbidTurn;
		mMyEnt = myEnt;
		Logger = logger;
		Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
		mMoveEffectSupports = ((Component)myEnt).GetComponents<IMoveEffectSupport>();
		mMyTransform = myEnt.myTransform;
		mMyGridTrans = myEnt.myGridTransform;
	}

	public virtual void Dispose()
	{
		StoptMove();
		mMyGridTrans = null;
		mMyTransform = null;
		mMoveEffectSupports = null;
		mMyEnt = null;
	}

	protected void SetTargetPosition(Vector2 pos)
	{
		mMyTransform.Position = (Vector2)(pos);
	}

	public abstract void Move(List<Pos2d> path, float speed, long moveStartTick, bool forceStartTick, MapMoveType moveType);

	public abstract void UpdatePath(int index, List<Pos2d> newAppendPath);

	public abstract void Update(float deltaTime);

	public abstract void Stop(bool forceStop, Vector2? stopPos);

	protected virtual void StartMove(MapMoveType moveType)
	{
		IsMoving = true;
		OnStartMove.Invoke();
		if (mMoveEffectSupports == null)
		{
			return;
		}
		IMoveEffectSupport[] array = mMoveEffectSupports;
		foreach (IMoveEffectSupport moveEffectSupport in array)
		{
			try
			{
				moveEffectSupport?.PlayEffect(moveType);
			}
			catch (Exception ex)
			{
				Debug.LogException(ex);
			}
		}
	}

	protected virtual void StoptMove()
	{
		if (!IsMoving)
		{
			return;
		}
		IsMoving = false;
		OnStopMove.Invoke();
		if (mMoveEffectSupports == null)
		{
			return;
		}
		IMoveEffectSupport[] array = mMoveEffectSupports;
		foreach (IMoveEffectSupport moveEffectSupport in array)
		{
			try
			{
				moveEffectSupport?.StopEffect();
			}
			catch (Exception ex)
			{
				Debug.LogException(ex);
			}
		}
	}

	protected List<Vector2> FillPathByLogicPath(List<Pos2d> logicPath, List<Vector2> resultPath)
	{
		resultPath.Clear();
		if ((Object)(object)mMyGridTrans == (Object)null || logicPath == null || logicPath.Count <= 0)
		{
			return resultPath;
		}
		int count = logicPath.Count;
		for (int i = 0; i < count; i++)
		{
			resultPath.Add((Vector2)(mMyGridTrans.GridToPosition(logicPath[i])));
		}
		return resultPath;
	}

	protected List<Vector2> UpdatePathByLogicPath(int index, List<Pos2d> logicPath, List<Vector2> resultPath)
	{
		if ((Object)(object)mMyGridTrans == (Object)null || logicPath == null || logicPath.Count <= 0)
		{
			return resultPath;
		}
		if (resultPath.Count <= index)
		{
			return resultPath;
		}
		resultPath = resultPath.GetRange(0, index);
		int count = logicPath.Count;
		for (int i = 0; i < count; i++)
		{
			resultPath.Add((Vector2)(mMyGridTrans.GridToPosition(logicPath[i])));
		}
		return resultPath;
	}
}

}
