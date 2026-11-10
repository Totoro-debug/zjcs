namespace Zjcs.Official.BattleRoundUI
{
using System;

using UnityEngine;

public interface IGameRoundCharacterItem
{
	bool IsCurrent { get; }

	Transform Transform { get; }

	object RoundRuntimeData { get; }

	void Init(object data, bool isCurrent, BattleRoundRole gameCharacterComponent);

	void Refresh(object data, bool isCurrent, BattleRoundRole gameCharacterComponent);

	void SetHurt();

	void SetDead(Action callback);

	void SetIdle();

	void SetActing();

	void UpdateRoundData(object newData);

	void SetSiblingIndex(int index);
	void OnGameObjectPoolReset();

	void OnGameObjectPoolClear();
}

}
