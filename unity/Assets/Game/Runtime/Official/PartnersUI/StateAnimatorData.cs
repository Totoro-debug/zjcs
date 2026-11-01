// Selected original roster helper; see partner-roster-native-source-manifest.json.
using UnityEngine;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.PartnersUI
{
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StateAnimatorData
{
	[Serializable]
	public class ParameterData
	{
		public string ParameterName;

		public AnimatorControllerParameterType ParameterType;

		public int IntValue;

		public float FloatValue;

		public bool BoolValue;
	}

	[Serializable]
	public class ParameterKV
	{
		public WorldStateConfigValue State;

		public ParameterData Parameter;
	}

	public Animator Animator;

	public List<ParameterKV> Parameters;
}

}
