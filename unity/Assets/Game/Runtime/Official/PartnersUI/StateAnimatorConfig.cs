// Selected original roster helper; see partner-roster-native-source-manifest.json.
using UnityEngine;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.PartnersUI
{
using System;
using UnityEngine;

[Serializable]
public class StateAnimatorConfig : IWorldStateConfig
{
	[SerializeField]
	private StateAnimatorData mStateAnimatorData;

	public void SetState(WorldStateConfigValue state)
	{
		if (mStateAnimatorData == null)
		{
			return;
		}
		foreach (StateAnimatorData.ParameterKV parameter in mStateAnimatorData.Parameters)
		{
			if ((int)parameter.State != (int)state)
			{
				continue;
			}
			AnimatorControllerParameterType parameterType = parameter.Parameter.ParameterType;
			switch ((int)parameterType - 1)
			{
			case 0:
				mStateAnimatorData.Animator.SetFloat(parameter.Parameter.ParameterName, parameter.Parameter.FloatValue);
				continue;
			case 2:
				mStateAnimatorData.Animator.SetInteger(parameter.Parameter.ParameterName, parameter.Parameter.IntValue);
				continue;
			case 3:
				mStateAnimatorData.Animator.SetBool(parameter.Parameter.ParameterName, parameter.Parameter.BoolValue);
				continue;
			case 1:
				continue;
			}
			if ((int)parameterType != 9)
			{
				continue;
			}
			AnimatorControllerParameter[] parameters = mStateAnimatorData.Animator.parameters;
			foreach (AnimatorControllerParameter val in parameters)
			{
				if ((int)val.type == 9)
				{
					mStateAnimatorData.Animator.ResetTrigger(val.name);
				}
			}
			mStateAnimatorData.Animator.SetTrigger(parameter.Parameter.ParameterName);
		}
		if (((Behaviour)mStateAnimatorData.Animator).isActiveAndEnabled)
		{
			mStateAnimatorData.Animator.Update(0f);
		}
	}

	public void Clear()
	{
	}
}

}
