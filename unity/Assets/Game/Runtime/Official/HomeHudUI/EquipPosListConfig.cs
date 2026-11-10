namespace Zjcs.Official.HomeHudUI
{
using System;
using System.Collections.Generic;
using UnityEngine;

public class EquipPosListConfig : MonoBehaviour
{
	public List<EquipPos> EquipPosList;

	public event Action<List<EquipPos>> OnEquipPosListUpdate;

	public void SetEquipPos(EquipPos equipPos)
	{
		EquipPosList.Clear();
		EquipPosList.Add(equipPos);
		OnEquipPosListUpdate?.Invoke(EquipPosList);
	}

	public void SetEquipPos(List<EquipPos> equipPoses)
	{
		EquipPosList.Clear();
		EquipPosList.AddRange(equipPoses);
		OnEquipPosListUpdate?.Invoke(EquipPosList);
	}
}

}
