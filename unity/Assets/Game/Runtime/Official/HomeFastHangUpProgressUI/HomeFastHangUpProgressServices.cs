using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeFastHangUpUI;

namespace Zjcs.Official.HomeFastHangUpProgressUI
{
    public interface IHomeFastHangUpProgressServices
    {
        IHomeFastHangUpServices FastHangUp { get; }
        IProfessionImageResourceLoader Resources { get; }
        AppearanceConfig AppearanceConfig { get; }
        ProfessionAppearanceTables AppearanceTables { get; }
        ProfessionAppearanceResolver AppearanceResolver { get; }
        long MainPlayerId { get; }
        int RandomRange(int min, int max);
        void PublishBedFastHangUp();
        void SetActiveUIVisible(UIBase owner, bool visible);
        void Close(GameObject root);
    }
}
