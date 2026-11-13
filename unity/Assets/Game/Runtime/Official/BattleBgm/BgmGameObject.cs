using UnityEngine;

namespace Zjcs.Official.BattleBgm
{
public class BgmGameObject : MonoBehaviour
{
	public IBgmGameObjectProvider mProvider { get; set; }

	private void Awake()
	{
		mProvider.GameObject = ((Component)this).gameObject;
	}
}
}
