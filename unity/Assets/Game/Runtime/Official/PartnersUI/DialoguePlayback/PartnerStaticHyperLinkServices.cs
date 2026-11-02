using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.PartnersUI
{
    // Original HyperLinkInfo fields; row parsing and navigation are host ports.
    public sealed class PartnerStaticHyperLinkInfo
    {
        public int Id, HyperLinkType;
        public string HyperLinkData;
    }
    public interface IHyperLinkInfoProvider
    {
        PartnerStaticHyperLinkInfo GetHyperLinkInfo(string linkId);
    }
    public interface IPartnerStaticHyperLinkHandler
    {
        void Init(IWindowOperator owner);
        void OnClick();
    }
    public interface IPartnerStaticHyperLinkServices
    {
        PartnerStaticHyperLinkInfo GetHyperLinkInfo(int id);
        IPartnerStaticHyperLinkHandler CreateHandler(int type, string data);
        IEnumerable<RaycastResult> ForeachRaycastResults(Vector2 point);
    }
}
