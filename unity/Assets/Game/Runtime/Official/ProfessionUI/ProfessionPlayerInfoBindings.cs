using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionInfoTarget
    {
        ActorTargetType TargetType { get; }
        long TargetId { get; }
        long ServerId { get; }
        bool IsNpc { get; }
        string GetFigurePath(Sex sex);
        bool CheckActorCanAssist(int playType);
    }
    public interface IProfessionInfoNpcTarget:IProfessionInfoTarget { int NpcClassId { get; } }
    public interface IProfessionInfoPlayer
    {
        long Id { get; }
        string Icon { get; }
        string Name { get; }
        object AppearanceData { get; }
        int Profession { get; }
        bool IsNpcPinLimited { get; }
        bool? IsNpcCollected(int classId);
        event Action OnNameUpdate;
        event Action<int> OnSubRankUpdate;
        event Action<int> OnLevelUpdate;
        event Action<long,long> OnCombatRatingChanged;
        event Action<int,int> OnProfessionChange;
        event Action<int> OnProfessionPromote;
        event Action<AppearanceOperateSource> OnAppearanceUpdate;
        event Action OnIconUpdate;
        event Action<Sex> OnSexUpdate;
    }
    public interface IProfessionPlayerInfoHead
    {
        void GeneratePortrait(long id,Sex sex,int profession,object appearance);
        void ResetInitState();
    }
    public interface IProfessionPlayerInfoScene
    { void RefreshAppearance(long id,Sex sex,int profession,object appearance); }
    public interface IProfessionPlayerInfoTitle
    { void ClearTitleItem();void Init(object appearance); }
    public interface IProfessionPlayerInfoPlanesIcon { void Init(long serverId); }
    public interface IProfessionPlayerInfoHpSlider { void Init(); }
    public interface IProfessionPlayerInfoDetailPanel { void Init(object briefInfo,int popupScene,int skillPlanUsedPos); }
    public sealed class ProfessionInfoNpcDefinition { public int Id;public string FigureModelUI; }
    public sealed class ProfessionInfoRobotInvite { public int OnePlayerMaxInviteCount,PlayType; }
    public readonly struct ProfessionInfoText
    {
        public readonly string Key;public readonly object[] Args;
        public ProfessionInfoText(string key,params object[] args){Key=key;Args=args;}
    }
    public interface IProfessionPlayerInfoBindings
    {
        IProfessionInfoPlayer MainPlayer { get; }
        bool ManagerAvailable { get; }
        SafeAction OnPlayerUpdate { get; }
        IProfessionImageResourceLoader Resources { get; }
        PlayerInfoData BuildData(IProfessionInfoPlayer player);
        PlayerInfoData BuildLiteData(object liteInfo,IProfessionInfoTarget target);
        object BuildBriefInfo(IProfessionInfoPlayer player,bool online);
        object CreateNameArg(string name,bool translate);
        object CompactNumber(long value);
        string SubRankKey(int rank);
        string ProfessionNameKey(int profession);
        string GetFullBodyGuid(int profession,Sex sex);
        string FirstProfessionIcon(int profession);
        string ThirdProfessionIcon(int profession);
        ProfessionInfoText NameWithPlanes(object nameArg,long serverId);
        bool HasHonorTitle(object appearance);
        ProfessionInfoNpcDefinition GetNpc(int classId);
        GameObject InstantiateKeepScale(GameObject prefab,Transform parent);
        void OverrideNpcInitialAnimation(GameObject model,int npcId);
        TeamTargetCatalog? TeamCatalog(Component component);
        ProfessionInfoRobotInvite RobotInvite(Component component);
        ProfessionInfoRobotInvite DefaultRobotInvite();
        bool CanJointTogether(int targetProfession,int mainProfession);
        int DuelSkillGroup(long playerId,long targetId);
        void Popup(Component owner,Action<IProfessionPlayerInfoDetailPanel> initialized);
    }
    public static class ProfessionPlayerInfoBindings
    {
        public static IProfessionPlayerInfoBindings Current { get; set; }
        public static IProfessionPlayerInfoBindings Required => Current ??
            throw new InvalidOperationException("Original player-info data, widgets, configuration and resource services are unbound.");
    }
}
