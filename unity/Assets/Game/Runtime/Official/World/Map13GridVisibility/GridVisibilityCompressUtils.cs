// Migrated original shared visibility source; see Map13 source receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;
using System;
using System.Buffers.Binary;




namespace Zjcs.Official.World.Map13GridVisibility
{
public class GridVisibilityCompressUtils
{
	public const int kIntSize = 4;

	[ThreadStatic]
	private static byte[] mBuffer;

	private static byte[] GetBuffer()
	{
		if (mBuffer == null)
		{
			mBuffer = new byte[65536];
		}
		return mBuffer;
	}

	public static byte[] DoMyZip(MapGroundGridBoolData visData)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		Span<byte> val = new Span<byte>(GetBuffer());
		int num = 0;
		BinaryPrimitives.WriteInt32LittleEndian(val.Slice(num, 4), visData.Height);
		num += 4;
		BinaryPrimitives.WriteInt32LittleEndian(val.Slice(num, 4), visData.Width);
		num += 4;
		int num2 = 0;
		for (int i = 0; i < visData.Height; i++)
		{
			for (int j = 0; j < visData.Width; j++)
			{
				if (visData.GetPosValue(j, i))
				{
					num2++;
				}
				else if (num2 > 0)
				{
					int index = visData.GetIndex(j, i);
					BinaryPrimitives.WriteInt32LittleEndian(val.Slice(num, 4), index - num2);
					num += 4;
					BinaryPrimitives.WriteInt32LittleEndian(val.Slice(num, 4), num2);
					num += 4;
					num2 = 0;
				}
			}
		}
		if (num2 > 0)
		{
			BinaryPrimitives.WriteInt32LittleEndian(val.Slice(num, 4), visData.Height * visData.Width - num2);
			num += 4;
			BinaryPrimitives.WriteInt32LittleEndian(val.Slice(num, 4), num2);
			num += 4;
		}
		return val.Slice(0, num).ToArray();
	}

	public static MapGroundGridBoolData DoMyUnzip(byte[] bytes)
	{
		int num = bytes.Length;
		int num2 = 0;
		int num3 = BitConverter.ToInt32(bytes, num2);
		num2 += 4;
		int num4 = BitConverter.ToInt32(bytes, num2);
		num2 += 4;
		MapGroundGridBoolData mapGroundGridBoolData = new MapGroundGridBoolData(num3, num4);
		int num5 = num4 * num3;
		if (bytes.Length <= num2)
		{
			for (int i = 0; i < num5; i++)
			{
				int y = i / num4;
				int x = i % num4;
				mapGroundGridBoolData.SetPosValue(x, y, value: true);
			}
			return mapGroundGridBoolData;
		}
		try
		{
			do
			{
				int num6 = BitConverter.ToInt32(bytes, num2);
				num2 += 4;
				int num7 = BitConverter.ToInt32(bytes, num2);
				num2 += 4;
				int num8 = num6 + num7;
				for (int j = num6; j < num8; j++)
				{
					int y2 = j / num4;
					int x2 = j % num4;
					mapGroundGridBoolData.SetPosValue(x2, y2, value: true);
				}
			}
			while (num2 < num);
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
		return mapGroundGridBoolData;
	}
}

}
