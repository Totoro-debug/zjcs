using System;
using System.Collections.Generic;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Official.World.Map13Navigation
{

public class AStarUtils
{
	internal class AStarNode
	{
		public readonly Pos2d Pos;

		public readonly float Value;

		public readonly float PredictValue;

		public readonly int Step;

		public readonly AStarNode PreNode;

		public AStarNode(Pos2d pos, float value, float predictValue, int step, AStarNode preNode)
		{
			Pos = pos;
			Value = value;
			PredictValue = predictValue;
			Step = step;
			PreNode = preNode;
		}
	}

	[ThreadStatic]
	private static HashSet<Pos2d> sTmpPositions;

	[ThreadStatic]
	private static List<AStarNode> sTmpNodeList;

	private static HashSet<Pos2d> QueryTempPosSet()
	{
		if (sTmpPositions == null)
		{
			sTmpPositions = new HashSet<Pos2d>();
		}
		else
		{
			sTmpPositions.Clear();
		}
		return sTmpPositions;
	}

	private static List<AStarNode> QueryTmpNodeList()
	{
		if (sTmpNodeList == null)
		{
			sTmpNodeList = new List<AStarNode>();
		}
		else
		{
			sTmpNodeList.Clear();
		}
		return sTmpNodeList;
	}

	public static List<Pos2d> FindPath(IAStarMovableHandler checkHandler, FindPathArgContext argContext)
	{
		List<Pos2d> list = new List<Pos2d>();
		if (argContext.StartPos == argContext.EndPos)
		{
			return list;
		}
		float predictValue = CalcPredictValue(argContext.StartPos, argContext);
		AStarNode node = new AStarNode(argContext.StartPos, 0f, predictValue, 0, null);
		AStarNode aStarNode = DoFindPath(checkHandler, node, argContext);
		if (aStarNode == null)
		{
			return null;
		}
		do
		{
			list.Add(aStarNode.Pos);
			aStarNode = aStarNode.PreNode;
		}
		while (aStarNode != null);
		if (list.Count == 1)
		{
			return new List<Pos2d>();
		}
		list.Reverse();
		return list;
	}

	private static float CalcPredictValue(Pos2d pos, FindPathArgContext argContext)
	{
		if (argContext.PreciseTarget)
		{
			return Pos2d.Distance(pos, argContext.EndPos);
		}
		float num = float.MaxValue;
		foreach (Pos2d bodyRange in argContext.BodyRanges)
		{
			int num2 = Pos2d.Distance(pos + bodyRange, argContext.EndPos);
			if ((float)num2 < num)
			{
				num = num2;
			}
		}
		return num;
	}

	private static AStarNode DoFindPath(IAStarMovableHandler checkHandler, AStarNode node, FindPathArgContext argContext)
	{
		LinkedList<AStarNode> linkedList = new LinkedList<AStarNode>();
		HashSet<Pos2d> hashSet = QueryTempPosSet();
		hashSet.Add(argContext.StartPos);
		List<AStarNode> list = QueryTmpNodeList();
		list.Add(node);
		if (argContext.FirstStepPosList != null)
		{
			foreach (Pos2d firstStepPos in argContext.FirstStepPosList)
			{
				AStarNode aStarNode = new AStarNode(predictValue: CalcPredictValue(firstStepPos, argContext), pos: firstStepPos, value: node.Value + 1f, step: node.Step + 1, preNode: node);
				list.Add(aStarNode);
				InsertLeaf(linkedList, aStarNode);
			}
		}
		List<Pos2d> list2 = new List<Pos2d>(argContext.BodyRanges.Count);
		foreach (Pos2d bodyRange in argContext.BodyRanges)
		{
			list2.Add(argContext.StartPos + bodyRange);
		}
		while (true)
		{
			foreach (Pos2d fourDir in Pos2d.FourDirs)
			{
				Pos2d pos2d = fourDir + node.Pos;
				if (hashSet.Contains(pos2d) || !checkHandler.CanMoveToNext(node.Pos, pos2d))
				{
					continue;
				}
				hashSet.Add(pos2d);
				bool flag = true;
				foreach (Pos2d bodyRange2 in argContext.BodyRanges)
				{
					Pos2d pos2d2 = pos2d + bodyRange2;
					if (!list2.Contains(pos2d2) && !checkHandler.IsFit(pos2d2, argContext.CustomContext))
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					float predictValue = CalcPredictValue(pos2d, argContext);
					AStarNode aStarNode2 = new AStarNode(pos2d, node.Value + 1f, predictValue, node.Step + 1, node);
					list.Add(aStarNode2);
					if (argContext.MaxStep <= 0 || aStarNode2.Step + 1 < argContext.MaxStep)
					{
						InsertLeaf(linkedList, aStarNode2);
					}
				}
			}
			if (linkedList.Count <= 0)
			{
				break;
			}
			node = linkedList.First.Value;
			linkedList.RemoveFirst();
			if (argContext.CheckPosCanEnd(node.PredictValue, node.Pos))
			{
				return node;
			}
		}
		float num = float.MaxValue;
		AStarNode result = null;
		foreach (AStarNode item in list)
		{
			if (item.PredictValue < num)
			{
				result = item;
				num = item.PredictValue;
			}
		}
		return result;
	}

	private static void InsertLeaf(LinkedList<AStarNode> leafs, AStarNode node)
	{
		for (LinkedListNode<AStarNode> linkedListNode = leafs.First; linkedListNode != null; linkedListNode = linkedListNode.Next)
		{
			if (node.Value + node.PredictValue < linkedListNode.Value.Value + linkedListNode.Value.PredictValue)
			{
				leafs.AddBefore(linkedListNode, node);
				return;
			}
		}
		leafs.AddLast(node);
	}
}

}
