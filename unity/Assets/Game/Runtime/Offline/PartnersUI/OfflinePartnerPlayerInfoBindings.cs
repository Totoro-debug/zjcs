using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.NativeLabelUI.Framework;

namespace Zjcs.Offline.PartnersUI
{
    // The local committed player replaces MeManager's data boundary. Existing
    // window/configuration services retain resource, team and popup behavior.
    public sealed class OfflinePartnerPlayerInfoBindings : IProfessionPlayerInfoBindings
    {
        private readonly OfflinePartnerPlayerInfoSource source;
        private readonly IProfessionPlayerInfoBindings services;

        public OfflinePartnerPlayerInfoBindings(OfflinePartnerPlayerInfoSource source,
            IProfessionPlayerInfoBindings services)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IProfessionInfoPlayer MainPlayer => source.MainPlayer;
        public bool ManagerAvailable => services.ManagerAvailable;
        public SafeAction OnPlayerUpdate => source.OnPlayerUpdate;
        public IProfessionImageResourceLoader Resources => services.Resources;
        public PlayerInfoData BuildData(IProfessionInfoPlayer player) => source.BuildData(player);
        public object CreateNameArg(string name, bool translate) => new CultureTextArg(name, null, translate);
        public object CompactNumber(long value) => CultureNumberArg.Compact(value);

        public PlayerInfoData BuildLiteData(object info, IProfessionInfoTarget target) => services.BuildLiteData(info, target);
        public object BuildBriefInfo(IProfessionInfoPlayer player, bool online) => services.BuildBriefInfo(player, online);
        public string SubRankKey(int rank) => services.SubRankKey(rank);
        public string ProfessionNameKey(int profession) => services.ProfessionNameKey(profession);
        public string GetFullBodyGuid(int profession, Sex sex) => services.GetFullBodyGuid(profession, sex);
        public string FirstProfessionIcon(int profession) => services.FirstProfessionIcon(profession);
        public string ThirdProfessionIcon(int profession) => services.ThirdProfessionIcon(profession);
        public ProfessionInfoText NameWithPlanes(object name, long server) => services.NameWithPlanes(name, server);
        public bool HasHonorTitle(object appearance) => services.HasHonorTitle(appearance);
        public ProfessionInfoNpcDefinition GetNpc(int classId) => services.GetNpc(classId);
        public GameObject InstantiateKeepScale(GameObject prefab, Transform parent) => services.InstantiateKeepScale(prefab, parent);
        public void OverrideNpcInitialAnimation(GameObject model, int npcId) => services.OverrideNpcInitialAnimation(model, npcId);
        public TeamTargetCatalog? TeamCatalog(Component component) => services.TeamCatalog(component);
        public ProfessionInfoRobotInvite RobotInvite(Component component) => services.RobotInvite(component);
        public ProfessionInfoRobotInvite DefaultRobotInvite() => services.DefaultRobotInvite();
        public bool CanJointTogether(int target, int main) => services.CanJointTogether(target, main);
        public int DuelSkillGroup(long player, long target) => services.DuelSkillGroup(player, target);
        public void Popup(Component owner, Action<IProfessionPlayerInfoDetailPanel> initialized) => services.Popup(owner, initialized);
    }
}
