using System;
using System.Collections.Generic;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Official.World.Map13Navigation
{

public readonly struct FindPathArgContext
{
	public delegate bool CheckEndDel(Pos2d pos);

	public class Builder
	{
		private Pos2d mStartPos;

		private Pos2d mEndPos;

		private int mPreferDist;

		private bool mPreciseTarget;

		private int mMaxStep = -1;

		private IAStarMoveCustomContext mCustomContext;

		private List<Pos2d> mBodyRanges;

		private List<Pos2d> mFirstStepPosList;

		private CheckEndDel mCheckEndDel;

		public Builder(Pos2d startPos, Pos2d endPos)
		{
			mStartPos = startPos;
			mEndPos = endPos;
		}

		public Builder WithPreferDist(int preferDist)
		{
			mPreferDist = preferDist;
			return this;
		}

		public Builder WithPreciseTarget(bool preciseTarget)
		{
			mPreciseTarget = preciseTarget;
			return this;
		}

		public Builder WithMaxStep(int maxStep)
		{
			mMaxStep = maxStep;
			return this;
		}

		public Builder WithCustomContext(IAStarMoveCustomContext customContext)
		{
			mCustomContext = customContext;
			return this;
		}

		public Builder WithBodyRanges(List<Pos2d> bodyRanges)
		{
			mBodyRanges = bodyRanges;
			return this;
		}

		public Builder WithFirstStepPosList(List<Pos2d> firstStepPosList)
		{
			mFirstStepPosList = firstStepPosList;
			return this;
		}

		public Builder WithCheckEndDel(CheckEndDel checkEndDel)
		{
			mCheckEndDel = checkEndDel;
			return this;
		}

		public FindPathArgContext Build()
		{
			if (mBodyRanges == null)
			{
				throw new InvalidOperationException("FindPathArgContext.Builder do Build, BodyRanges is required. Call WithBodyRanges() before Build().");
			}
			if (mCustomContext == null)
			{
				throw new InvalidOperationException("FindPathArgContext.Builder do Build, CustomContext is required. Call WithCustomContext() before Build().");
			}
			return new FindPathArgContext(mStartPos, mEndPos, mPreferDist, mPreciseTarget, mMaxStep, mCustomContext, mBodyRanges, mFirstStepPosList, mCheckEndDel);
		}
	}

	public readonly Pos2d StartPos;

	public readonly Pos2d EndPos;

	public readonly int PreferDist;

	public readonly bool PreciseTarget;

	public readonly int MaxStep;

	public readonly IAStarMoveCustomContext CustomContext;

	public readonly IReadOnlyList<Pos2d> BodyRanges;

	public readonly IReadOnlyList<Pos2d> FirstStepPosList;

	private readonly CheckEndDel mCheckEndDel;

	private FindPathArgContext(Pos2d startPos, Pos2d endPos, int preferDist, bool preciseTarget, int maxStep, IAStarMoveCustomContext customContext, List<Pos2d> bodyRanges, List<Pos2d> firstStepPosList, CheckEndDel checkEndDel)
	{
		StartPos = startPos;
		EndPos = endPos;
		PreferDist = preferDist;
		PreciseTarget = preciseTarget;
		MaxStep = maxStep;
		CustomContext = customContext;
		BodyRanges = bodyRanges;
		FirstStepPosList = firstStepPosList;
		mCheckEndDel = checkEndDel;
	}

	public bool CheckPosCanEnd(float nodePredictValue, Pos2d pos)
	{
		if (nodePredictValue <= (float)PreferDist)
		{
			return true;
		}
		if (mCheckEndDel == null)
		{
			return false;
		}
		return mCheckEndDel(pos);
	}
}

}
