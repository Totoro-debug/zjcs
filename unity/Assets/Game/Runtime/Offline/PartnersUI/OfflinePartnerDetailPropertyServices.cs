using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Zjcs.Offline.Progression;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    public interface IOfflinePartnerDetailPresentation
    {
        bool CanNavigateNpc(int npcId);
        PartnerDetailNpcTarget CreateNpcTarget(int npcId);
        void GetLiteInfo(PartnerDetailNpcTarget target, PartnerDetailLiteInfoCallback callback);
        void PopupNpcBrief(IWindowOperator owner, PartnerDetailNpcTarget target);
        void PopupFriendship(IWindowOperator owner, bool starGod, Action<IPartnerGiftFriendshipDialog> initialize);
        void NavigateNpcPosition(int npcId, Action<PartnerDetailNavigateResult> complete);
        bool IsNavigateTipValid(string tip);
        void BroadcastNavigateTip(string tip);
        string GetText(string key);
        void SystemMessage(string text);
        void ShowProgressionResult(ProgressionResult result);
    }
    public sealed class OfflinePartnerDetailPropertyServices : IPartnerDetailPropertyServices
    {
        private readonly OfficialPartnerCatalog catalog;
        private readonly OfflinePartnerFriendshipProperties properties;
        private readonly OfflinePartnerDetailPropertySource source;
        private readonly OfflinePartnerRosterUiServices roster;
        private readonly Func<int, bool> treasureActive;
        private readonly IOfflinePartnerDetailPresentation presentation;
        public OfflinePartnerDetailPropertyServices(OfficialPartnerCatalog catalog,
            OfflinePartnerFriendshipProperties properties, OfflinePartnerDetailPropertySource source,
            OfflinePartnerRosterUiServices roster, Func<int, bool> treasureActive,
            IOfflinePartnerDetailPresentation presentation)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.properties = properties ?? throw new ArgumentNullException(nameof(properties));
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.roster = roster ?? throw new ArgumentNullException(nameof(roster));
            this.treasureActive = treasureActive ?? throw new ArgumentNullException(nameof(treasureActive));
            this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        }
        public IProfessionPropertyBindings Properties => properties.Properties;
        public List<ProfessionPropertyType> TravelNpcPropTypeList => properties.TravelNpcPropTypeList;
        public bool IsTravelFunctionLimited => properties.IsTravelFunctionLimited;
        public bool IsPinFunctionLimited => !roster.CanPinNpc;
        public IReadOnlyDictionary<ProfessionPropertyType, long> GetFriendshipLevelPropDiff(PartnerFriendshipNpcInfo npc, int fromLevel, int toLevel)
            => properties.GetFriendshipLevelPropDiff(npc, fromLevel, toLevel);
        public IReadOnlyDictionary<ProfessionPropertyType, long> GetLevelProps(int levelPropId, int level)
            => properties.GetLevelProps(levelPropId, level);
        public IReadOnlyDictionary<ProfessionPropertyType, long> CalcPlayerNpcProps(int npcId) => source.CalcPlayerNpcProps(npcId);
        public bool IsUnlockTravelNpcProp(ProfessionPropertyType property)
            => !TravelNpcPropTypeList.Contains(property) || !IsTravelFunctionLimited;
        public bool IsTreasureActive(int treasureId) => treasureActive(treasureId);
        public double GetTreasureToNpcFriendshipPercent(int treasureId)
        {
            var item = catalog.Rows("item").FirstOrDefault(value => Convert.ToInt32(value["ClassId"]) == treasureId);
            if (item == null) return 0.0;
            var settings = catalog.Rows("game_settings").Single();
            var qualities = (Dictionary<string, object>)settings["TreasureNpcFriendshipQuality"];
            return qualities.TryGetValue(Convert.ToInt32(item["Quality"]).ToString(System.Globalization.CultureInfo.InvariantCulture), out var value)
                ? Convert.ToInt64(value) / 10000.0 : 0.0;
        }
        public async void SetCollected(int npcId, bool collected, Action<PartnerDetailCollectedReply, int> complete)
        {
            await Task.Yield();
            ProgressionResult result;
            try { result = roster.SetCollected(npcId, collected); }
            catch (IOException exception) { Fail(exception); return; }
            catch (UnauthorizedAccessException exception) { Fail(exception); return; }
            if (result.IsApplied || result.Status == ProgressionResultStatus.Unchanged)
                complete(new PartnerDetailCollectedReply { isCollected = collected }, 0);
        }
        private void Fail(Exception exception) => presentation.ShowProgressionResult(new ProgressionResult {
            Status = ProgressionResultStatus.InvalidState, Message = exception.Message });
        public bool CanNavigateNpc(int npcId) => presentation.CanNavigateNpc(npcId);
        public PartnerDetailNpcTarget CreateNpcTarget(int npcId) => presentation.CreateNpcTarget(npcId);
        public void GetLiteInfo(PartnerDetailNpcTarget target, PartnerDetailLiteInfoCallback callback) => presentation.GetLiteInfo(target, callback);
        public void PopupNpcBrief(IWindowOperator owner, PartnerDetailNpcTarget target) => presentation.PopupNpcBrief(owner, target);
        public void PopupFriendship(IWindowOperator owner, bool starGod, Action<IPartnerGiftFriendshipDialog> initialize)
            => presentation.PopupFriendship(owner, starGod, initialize);
        public void NavigateNpcPosition(int npcId, Action<PartnerDetailNavigateResult> complete) => presentation.NavigateNpcPosition(npcId, complete);
        public bool IsNavigateTipValid(string tip) => presentation.IsNavigateTipValid(tip);
        public void BroadcastNavigateTip(string tip) => presentation.BroadcastNavigateTip(tip);
        public string GetText(string key) => presentation.GetText(key);
        public void SystemMessage(string text) => presentation.SystemMessage(text);
    }
}
