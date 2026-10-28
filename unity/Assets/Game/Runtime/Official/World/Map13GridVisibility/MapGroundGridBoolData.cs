// Migrated original shared visibility source; see Map13 source receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;
using System;
using System.Collections;


namespace Zjcs.Official.World.Map13GridVisibility
{
public class MapGroundGridBoolData : VisibilitySerializable
{

	public int Width;

	public int Height;

	public BitArray BitArray;

	public MapGroundGridBoolData(int height, int width)
	{
		if (height <= 0)
		{
			throw new ArgumentOutOfRangeException("height", "Height must be positive");
		}
		if (width <= 0)
		{
			throw new ArgumentOutOfRangeException("width", "Width must be positive");
		}
		Height = height;
		Width = width;
		BitArray = new BitArray(height * width);
	}

	public void SetPosValue(int x, int y, bool value)
	{
		Validate(x, y);
		int index = GetIndex(x, y);
		BitArray[index] = value;
	}

	public bool GetPosValue(int x, int y)
	{
		Validate(x, y);
		return BitArray[GetIndex(x, y)];
	}

	private void Validate(int x, int y)
	{
		if (x < 0 || x >= Width)
		{
			throw new ArgumentOutOfRangeException("x", $"X{x} must be between 0 and {Width - 1}");
		}
		if (y < 0 || y >= Height)
		{
			throw new ArgumentOutOfRangeException("y", $"Y{y} must be between 0 and {Height - 1}");
		}
	}

	public int GetIndex(int x, int y)
	{
		return y * Width + x;
	}

	public override ushort GetOpCode()
	{
		return 29830;
	}
}

}
