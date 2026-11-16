namespace Zjcs.Official.HomeCamera
{
public struct OnGameObjectDragEndResult : IInputOperationResult
{
	public GameObjectInputResult Value { get; private set; }
	public static implicit operator OnGameObjectDragEndResult(GameObjectInputResult result)
	{
		return new OnGameObjectDragEndResult
		{
			Value = result
		};
	}
}
}
