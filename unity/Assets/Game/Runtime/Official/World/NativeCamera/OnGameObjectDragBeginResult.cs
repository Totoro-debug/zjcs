namespace Zjcs.Official.World.NativeCamera
{
public struct OnGameObjectDragBeginResult : IInputOperationResult
{
	public GameObjectInputResult Value { get; private set; }
	public static implicit operator OnGameObjectDragBeginResult(GameObjectInputResult result)
	{
		return new OnGameObjectDragBeginResult
		{
			Value = result
		};
	}
}
}
