// Selected original home overlap/light relation implementation; see its source receipt.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace Zjcs.Official.HomeRelations
{
using Object = UnityEngine.Object;
[ExecuteAlways]
public class HomeMutex : HomeRelation
{
	public enum EMutexRole
	{
		Master,
		Slave
	}

	public EMutexRole MutexRole;

	public Rect Rect;
	public IHomeRelationManager InjectRelationManager { get; set; }

	public IHomeRelationManager RelationManager => InjectRelationManager;

	private void OnEnable()
	{
		RelationManager.RegisterHomeMutex(this);
	}

	private void OnDisable()
	{
		RelationManager.UnregisterHomeMutex(this);
	}

	public bool IsMutex(HomeMutex other)
	{
		if (RelationGroup != other.RelationGroup || MutexRole == other.MutexRole)
		{
			return false;
		}
		Vector3 position = ((Component)this).transform.position;
		Rect val = new Rect(Rect.position + (Vector2)position, Rect.size);
		Vector3 position2 = ((Component)other).transform.position;
		Rect val2 = new Rect(other.Rect.position + (Vector2)position2, other.Rect.size);
		return val.Overlaps(val2);
	}

	public virtual void OnChange()
	{
	}

	public virtual void OnRestore()
	{
	}
}
}
