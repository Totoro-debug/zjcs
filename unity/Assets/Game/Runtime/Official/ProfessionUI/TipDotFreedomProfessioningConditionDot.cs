// Ported from the verified official source; only dependency/serialization adapters differ.
using System;

namespace Zjcs.Official.ProfessionUI
{
[Serializable]
public class TipDotFreedomProfessioningConditionDot : TipDotConditionNodeBase
{
	public override void Init()
	{
		base.Init();
		AddEventListener();
	}

	public override void Release()
	{
		RemoveEventListener();
		base.Release();
	}

	private void AddEventListener()
	{
		Player.OnProfessionChange += Me_OnProfessionChange;
	}

	private void RemoveEventListener()
	{
		_ = Player;
		if (Player != null)
		{
			Player.OnProfessionChange -= Me_OnProfessionChange;
		}
	}

	public override bool CheckShowTip()
	{
		return Player.StartChangeProfession;
	}

	private void Me_OnProfessionChange(int from, int to)
	{
		SetDirty();
	}

	public override TipDotNodeBase Clone()
	{
		TipDotFreedomProfessioningConditionDot tipDotFreedomProfessioningConditionDot = new TipDotFreedomProfessioningConditionDot();
		tipDotFreedomProfessioningConditionDot.Fill(this);
		return tipDotFreedomProfessioningConditionDot;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotFreedomProfessioningConditionDot tipDotFreedomProfessioningConditionDot = source as TipDotFreedomProfessioningConditionDot;
		if (tipDotFreedomProfessioningConditionDot == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotFreedomProfessioningConditionDot);
	}

	public void Fill(TipDotFreedomProfessioningConditionDot obj)
	{
	}
}
}
