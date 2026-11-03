using UnityEngine;

public struct GrapplingLineNode
{
	public Vector2 Position { get; private set; }
	public Vector2 LastPosition { get; private set; }
	public Vector2 Direction { get; private set; }
	public float Z { get; private set; }
	public Vector3 Position3D => new Vector3(Position.x, Position.y, Z);

	public GrapplingLineNode(Vector3 startPos, Vector2 direction)
	{
		Position = startPos;
		LastPosition = startPos;
		Direction = direction;
		Z = startPos.z;
	}

	public void Update(Vector2 position)
	{
		LastPosition = Position;
		Position = position;
	}

	public void Update(Vector2 position, Vector2 lastPosition)
	{
		Position = position;
		LastPosition = lastPosition;
	}

	public void UpdateZ(float z) => Z = z;
}
