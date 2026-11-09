using UnityEngine;
using Zjcs.Official.ProfessionUI;

public interface IAfterimageRTParamsProvider
{
	int RTWidth { get; }
	int RTHeight { get; }
	float OrthoSize { get; }
	Vector3 GetTargetPos(IProfessionAfterimageTarget target);
}
