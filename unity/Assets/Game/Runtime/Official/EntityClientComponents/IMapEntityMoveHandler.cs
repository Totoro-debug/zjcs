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
public interface IMapEntityMoveHandler : IDisposable
{
	SafeAction OnStartMove { get; set; }

	SafeAction OnStopMove { get; set; }

	bool IsMoving { get; }

	void Move(List<Pos2d> logicPath, float logicSpeed, long moveStartTick, bool forceStartTick, MapMoveType moveType);

	void UpdatePath(int index, List<Pos2d> appendPath);

	void Update(float deltaTime);

	void Stop(bool forceStop, Vector2? stopPos);
}

}
