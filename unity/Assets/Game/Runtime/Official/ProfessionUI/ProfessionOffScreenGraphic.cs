using UnityEngine;
using UnityEngine.Events;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionOffScreenGraphic
    {
        Component Instance { get; }
        int updateWhenInvisible { get; set; }
        void MatchRectTransformWithBounds();
    }
    public interface IProfessionOffScreenPartHandler { UnityEvent OnSkinChanged { get; } }
}
