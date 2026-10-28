using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Zjcs.Official.World.NativeTerrain.Common;
public static class PrivateRegionUtility
{
	private class GridLine
	{
		public Pos2d Grid;

		public Vector2 Point1;

		public Vector2 Point2;

		public Vector2 StartPoint;

		public Vector2 EndPoint;

		public override string ToString()
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			return $"Grid:{Grid} StartPoint:{StartPoint} EndPoint{EndPoint}";
		}
	}

	public static readonly float HalfGridSize = 0.5f;

	private static readonly Vector2 DownDistance = new Vector2(0f, -1f);

	private static readonly Vector2 RightDistance = new Vector2(1f, 0f);

	private static readonly Vector2 UpDistance = new Vector2(0f, 1f);

	private static readonly Vector2 LeftDistance = new Vector2(-1f, 0f);

	private static Dictionary<Vector2, List<Vector2>> mPreferDirListDic = new Dictionary<Vector2, List<Vector2>>
	{
		{
			Vector2.down,
			new List<Vector2> { RightDistance, UpDistance, LeftDistance, DownDistance }
		},
		{
			Vector2.right,
			new List<Vector2> { UpDistance, LeftDistance, DownDistance, RightDistance }
		},
		{
			Vector2.up,
			new List<Vector2> { LeftDistance, DownDistance, RightDistance, UpDistance }
		},
		{
			Vector2.left,
			new List<Vector2> { DownDistance, RightDistance, UpDistance, LeftDistance }
		}
	};

	public static Vector2 GetGridPos(Vector2 pos)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		return new Vector2(UnitizationCenterLength(pos.x), UnitizationCenterLength(pos.y));
	}

	public static Vector2 GetCloseGridCornerPos(Vector2 pos)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		return new Vector2(GetCloseCornerLength(pos.x), GetCloseCornerLength(pos.y));
	}

	private static float GetCloseCornerLength(float length)
	{
		float num = UnitizationCenterLength(length);
		if (!(length > num))
		{
			return num - HalfGridSize;
		}
		return num + HalfGridSize;
	}

	private static float UnitizationCenterLength(float length)
	{
		float num = ((length > 0f) ? HalfGridSize : (0f - HalfGridSize));
		return UnitizationCornerLength(length) + num;
	}

	private static float UnitizationCornerLength(float length)
	{
		return (float)(int)(length / 1f) * 1f;
	}

	public static bool IsInsidePolygons(List<Vector2> posList, Vector2 pos)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		int count = posList.Count;
		int num = 0;
		int num2 = 0;
		int index = count - 1;
		while (num2 < count)
		{
			if (posList[num2].y > pos.y != posList[index].y > pos.y && pos.x < (posList[index].x - posList[num2].x) * (pos.y - posList[num2].y) / (posList[index].y - posList[num2].y) + posList[num2].x)
			{
				num++;
			}
			index = num2++;
		}
		return num % 2 != 0;
	}

	public static Dictionary<Pos2d, List<Direction>> GetGridBorderLine(List<Pos2d> grids, List<Pos2d> removeList = null)
	{
		HashSet<Pos2d> hashSet = new HashSet<Pos2d>(grids);
		Dictionary<Pos2d, List<Direction>> dictionary = new Dictionary<Pos2d, List<Direction>>();
		Dictionary<Pos2d, List<Direction>> dictionary2 = new Dictionary<Pos2d, List<Direction>>();
		foreach (Pos2d grid in grids)
		{
			List<Direction> value = new List<Direction>
			{
				Direction.Up,
				Direction.Down,
				Direction.Left,
				Direction.Right
			};
			dictionary.Add(grid, value);
		}
		if (removeList == null)
		{
			removeList = new List<Pos2d>();
		}
		foreach (KeyValuePair<Pos2d, List<Direction>> item5 in dictionary)
		{
			Pos2d item = item5.Key + Pos2d.DirectionGuider(Direction.Up);
			Pos2d item2 = item5.Key + Pos2d.DirectionGuider(Direction.Down);
			Pos2d item3 = item5.Key + Pos2d.DirectionGuider(Direction.Left);
			Pos2d item4 = item5.Key + Pos2d.DirectionGuider(Direction.Right);
			if (hashSet.Contains(item))
			{
				item5.Value.Remove(Direction.Up);
			}
			if (hashSet.Contains(item2))
			{
				item5.Value.Remove(Direction.Down);
			}
			if (hashSet.Contains(item3))
			{
				item5.Value.Remove(Direction.Left);
			}
			if (hashSet.Contains(item4))
			{
				item5.Value.Remove(Direction.Right);
			}
			if (item5.Value.Count > 0)
			{
				dictionary2.Add(item5.Key, item5.Value);
			}
			else
			{
				removeList.Add(item5.Key);
			}
		}
		return dictionary2;
	}

	public static List<Vector2> GetGridsBorderPoints(List<Pos2d> grids, List<Pos2d> removeList = null, float offset = 0f)
	{
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<Pos2d, List<Direction>> gridBorderLine = GetGridBorderLine(grids, removeList);
		Dictionary<Pos2d, Dictionary<Direction, (Vector2, Vector2)>> gridsBorderLineSub = GetGridsBorderLineSub(gridBorderLine, grids, removeList, offset);
		if (offset != 0f)
		{
			foreach (Pos2d key in gridBorderLine.Keys)
			{
				PrivateRegionExtendUtils.ConcaveAngleHandle(gridsBorderLineSub, key);
			}
		}
		HashSet<GridLine> hashSet = new HashSet<GridLine>();
		foreach (KeyValuePair<Pos2d, Dictionary<Direction, (Vector2, Vector2)>> item in gridsBorderLineSub)
		{
			foreach (KeyValuePair<Direction, (Vector2, Vector2)> item2 in item.Value)
			{
				hashSet.Add(new GridLine
				{
					Grid = item.Key,
					Point1 = item2.Value.Item1,
					Point2 = item2.Value.Item2
				});
			}
		}
		List<GridLine> list = new List<GridLine>(hashSet);
		SortLines(list);
		List<Vector2> list2 = new List<Vector2>();
		foreach (GridLine item3 in list)
		{
			list2.Add(item3.StartPoint);
		}
		int num = 0;
		bool flag = true;
		while (flag)
		{
			flag = false;
			int num2 = -1;
			for (int i = 0; i < list2.Count; i++)
			{
				Vector2 val = list2[i];
				int index = ((i + 2 < list2.Count) ? (i + 2) : (i + 2 - list2.Count));
				Vector2 val2 = list2[index];
				if (Math.Abs(val.x - val2.x) < 0.01f || Math.Abs(val.y - val2.y) < 0.01f)
				{
					num2 = ((i + 1 < list2.Count) ? (i + 1) : (i + 1 - list2.Count));
					flag = true;
					break;
				}
			}
			if (num2 >= 0)
			{
				list2.RemoveAt(num2);
			}
			if (num++ > 10000)
			{
				Debug.LogError((object)"优化顶点时产生死循环！");
				break;
			}
		}
		return list2;
	}

	public static Dictionary<Pos2d, Dictionary<Direction, (Vector2, Vector2)>> GetGridsBorderLineDict(List<Pos2d> grids, List<Pos2d> removeList = null)
	{
		return GetGridsBorderLineSub(GetGridBorderLine(grids, removeList), grids, removeList, 0f);
	}

	private static Dictionary<Pos2d, Dictionary<Direction, (Vector2, Vector2)>> GetGridsBorderLineSub(Dictionary<Pos2d, List<Direction>> areaInfo, List<Pos2d> grids, List<Pos2d> removeList, float offset)
	{
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		new HashSet<GridLine>();
		Dictionary<Pos2d, Dictionary<Direction, (Vector2, Vector2)>> dictionary = new Dictionary<Pos2d, Dictionary<Direction, (Vector2, Vector2)>>();
		foreach (Pos2d key in areaInfo.Keys)
		{
			if (!areaInfo.TryGetValue(key, out var value))
			{
				continue;
			}
			Dictionary<Direction, (Vector2, Vector2)> value2 = null;
			if (!dictionary.TryGetValue(key, out value2))
			{
				value2 = new Dictionary<Direction, (Vector2, Vector2)>();
				dictionary.Add(key, value2);
			}
			foreach (Direction item3 in value)
			{
				Vector2 item = Vector2.zero;
				Vector2 item2 = Vector2.zero;
				switch (item3)
				{
				case Direction.Up:
					item = GetGridVertex(key, 1);
					item2 = GetGridVertex(key, 2);
					item += Vector2.down * offset;
					item2 += Vector2.down * offset;
					break;
				case Direction.Down:
					item = GetGridVertex(key, 3);
					item2 = GetGridVertex(key, 0);
					item += Vector2.up * offset;
					item2 += Vector2.up * offset;
					break;
				case Direction.Left:
					item = GetGridVertex(key, 0);
					item2 = GetGridVertex(key, 1);
					item += Vector2.right * offset;
					item2 += Vector2.right * offset;
					break;
				case Direction.Right:
					item = GetGridVertex(key, 2);
					item2 = GetGridVertex(key, 3);
					item += Vector2.left * offset;
					item2 += Vector2.left * offset;
					break;
				}
				value2.Add(item3, (item, item2));
			}
			PrivateRegionExtendUtils.DeleteCrossLine(value2);
		}
		return dictionary;
	}

	private static int FindLineIndex(List<GridLine> lines, Vector2 startPoint, int startIndex)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		for (int i = startIndex; i < lines.Count; i++)
		{
			GridLine gridLine = lines[i];
			if (gridLine.Point1 == startPoint)
			{
				gridLine.StartPoint = gridLine.Point1;
				gridLine.EndPoint = gridLine.Point2;
				return i;
			}
			if (gridLine.Point2 == startPoint)
			{
				gridLine.StartPoint = gridLine.Point2;
				gridLine.EndPoint = gridLine.Point1;
				return i;
			}
		}
		return -1;
	}

	private static void SortLines(List<GridLine> lines)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		GridLine gridLine = lines[0];
		gridLine.StartPoint = gridLine.Point1;
		gridLine.EndPoint = gridLine.Point2;
		for (int i = 0; i < lines.Count - 1; i++)
		{
			GridLine gridLine2 = lines[i];
			int num = FindLineIndex(lines, gridLine2.EndPoint, i + 1);
			if (num == -1)
			{
				Debug.LogError((object)$"生成错误！ 查找不到相邻的线,Index{i} Line:{gridLine2}");
				break;
			}
			int index = i + 1;
			int index2 = num;
			GridLine gridLine3 = lines[num];
			GridLine gridLine4 = lines[i + 1];
			GridLine gridLine5 = (lines[index] = gridLine3);
			gridLine5 = (lines[index2] = gridLine4);
		}
	}

	private static int FindPointIndex(List<Vector2> points, Vector2 target, int startIdx)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		for (int i = startIdx; i < points.Count; i++)
		{
			if (points[i] == target)
			{
				return i;
			}
		}
		return -1;
	}

	private static void SortPoints(List<Vector2> points)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		for (int i = 0; i < points.Count - 1; i++)
		{
			List<Vector2> list = mPreferDirListDic[Vector2.left];
			if (i == 0)
			{
				list = mPreferDirListDic[Vector2.left];
			}
			else
			{
				Vector2 val = points[i] - points[i - 1];
				list = mPreferDirListDic[val.normalized];
			}
			Vector2 val2 = points[i];
			int num2 = -1;
			foreach (Vector2 item in list)
			{
				num2 = FindPointIndex(points, val2 + item, i + 1);
				if (num2 >= 0)
				{
					break;
				}
			}
			if (num2 == -1)
			{
				MiniMapBorderDebugHandler message = new MiniMapBorderDebugHandler(27, 2, out var enabled);
				if (enabled)
				{
					message.AppendLiteral("生成错误！ 查找不到相邻网格,Index");
					message.AppendFormatted(i, 0, null, "i");
					message.AppendLiteral(" Point:");
					message.AppendFormatted<Vector2>(val2, 0, null, "point");
				}
				MiniMapBorderDiagnostic.Debug(ref message, null, "SortPoints", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/Map/PrivateArea/PrivateRegionUtility.cs", 356);
				break;
			}
			int index = i + 1;
			int index2 = num2;
			Vector2 val3 = points[num2];
			Vector2 val4 = points[i + 1];
			Vector2 val5 = (points[index] = val3);
			val5 = (points[index2] = val4);
			num++;
			if (num > points.Count)
			{
				throw new Exception("非正常循环!");
			}
		}
	}

	public static Vector2 GetGridVertex(Pos2d grid, int index)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		Vector2 val = Vector2.zero;
		switch (index)
		{
		case 0:
			val = new Vector2(0f - HalfGridSize, 0f - HalfGridSize);
			break;
		case 1:
			val = new Vector2(0f - HalfGridSize, HalfGridSize);
			break;
		case 2:
			val = new Vector2(HalfGridSize, HalfGridSize);
			break;
		case 3:
			val = new Vector2(HalfGridSize, 0f - HalfGridSize);
			break;
		}
		return new Vector2((float)grid.X, (float)grid.Y) * 1f + val;
	}

	public static Vector2 GetGridVertex(Vector2 pos, int index)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		Vector2 val = Vector2.zero;
		switch (index)
		{
		case 0:
			val = new Vector2(0f - HalfGridSize, 0f - HalfGridSize);
			break;
		case 1:
			val = new Vector2(0f - HalfGridSize, HalfGridSize);
			break;
		case 2:
			val = new Vector2(HalfGridSize, HalfGridSize);
			break;
		case 3:
			val = new Vector2(HalfGridSize, 0f - HalfGridSize);
			break;
		}
		return pos + val;
	}

	static PrivateRegionUtility()
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
	}
}

}
