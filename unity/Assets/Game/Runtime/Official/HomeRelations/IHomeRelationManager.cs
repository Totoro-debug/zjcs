// Selected original home overlap/light relation implementation; see its source receipt.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace Zjcs.Official.HomeRelations
{
using Object = UnityEngine.Object;
public interface IHomeRelationManager
{
	void RegisterHomeMutex(HomeMutex homeMutex);

	void UnregisterHomeMutex(HomeMutex homeMutex);

	void ForceCheck();
}
}
