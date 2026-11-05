using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.PartnersUI
{
    public interface IPartnerAdventureTipServices
    {
        Camera UiCamera { get; }
        IProfessionAssetOperation<Sprite> LoadIcon(int category, int value);
        void HookGlobalPointerDown(Action<Vector2> handler);
        void UnhookGlobalPointerDown(Action<Vector2> handler);
        void Close(GameObject window);
    }
}
