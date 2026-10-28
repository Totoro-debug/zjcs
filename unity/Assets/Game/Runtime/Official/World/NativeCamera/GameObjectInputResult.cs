using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
public struct GameObjectInputResult
{
	public GameObject Target { get; private set; }
	public Vector2 WorldPos { get; private set; }
	public GameObjectInputResult(GameObject target, Vector2 worldPos)
	{
		Target = target;
		WorldPos = worldPos;
	}
}
}
