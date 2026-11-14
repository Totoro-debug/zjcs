using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeLevelUpUI
{
    public interface IHomeLevelUpServices
    {
        int CurrentLevel { get; }
        Dictionary<ProfessionPropertyType, long> CalcCharacterBattleProps(int level);
        long GetRawProp(Dictionary<ProfessionPropertyType, long> properties, ProfessionPropertyType property);
        ProfessionPropertyDefinition GetProperty(ProfessionPropertyType property);
        IProfessionImageResourceLoader Resources { get; }
        void Popup(Action<LevelUpNotifyTip> initialized);
        void Close(GameObject window);
    }
}
