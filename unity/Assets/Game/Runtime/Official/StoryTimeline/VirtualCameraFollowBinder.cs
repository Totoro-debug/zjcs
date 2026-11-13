// Migrated original source; see story-timeline-source-manifest.json.
using System;
using Cinemachine;
using UnityEngine;

namespace Zjcs.Official.StoryTimeline
{
using Object = UnityEngine.Object;
[RequireComponent(typeof(CinemachineVirtualCamera))]
public class VirtualCameraFollowBinder : MonoBehaviour
{
	public int CameraStage;

	public EntityConfigType EntityConfigType;

	[SerializeReference]
	public EntityConfigBase EntityConfig;
	private IStoryTimelineBindings Bindings;
	public void Bind(IStoryTimelineBindings bindings) { Bindings = bindings; EntityConfig?.Bind(bindings); }

	private void Awake()
	{


		if (EntityConfig != null)
		{

			Transform myEntity = EntityConfig.GetMyEntity();
			if (!((Object)(object)myEntity == (Object)null))
			{
				CinemachineVirtualCamera component = ((Component)this).GetComponent<CinemachineVirtualCamera>();
				((CinemachineVirtualCameraBase)component).Follow = ((Component)myEntity).GetComponent<Transform>();
				CinemachineTransposer cinemachineComponent = component.GetCinemachineComponent<CinemachineTransposer>();
				cinemachineComponent.m_FollowOffset = new Vector3(cinemachineComponent.m_FollowOffset.x, cinemachineComponent.m_FollowOffset.y, Bindings.GetCameraZ(CameraStage));
			}
		}
	}
}

}
