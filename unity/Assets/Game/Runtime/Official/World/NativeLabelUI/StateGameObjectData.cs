// Migrated from selected official managed code; see Map13 label-core source receipt.
namespace Zjcs.Official.World.NativeLabelUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StateGameObjectData
{
	[Serializable]
	public class StateGameObjectKV
	{
		public StateConfigValue State;

		public List<GameObject> GameObjects;
	}

	public List<StateGameObjectKV> GoConfig;
}

}
