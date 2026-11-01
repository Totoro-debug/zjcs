using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    public interface IOfflinePartnerGiftPresentation
    {
        void ConfigureGiftIcon(ItemIconNpcGift icon);
        void ShowError(int error, object args, bool flag);
        string GetText(string key);
        void SystemMessage(string text);
        void PopupInput(Action<IPartnerGiftInputDialog> initialize);
        void PopupConfirm(Action<IPartnerGiftConfirmDialog> initialize);
        void PopupFriendship(IWindowOperator owner, bool starGod, Action<IPartnerGiftFriendshipDialog> initialize);
        object Compact(long count);
        void Close(GameObject window);
        void ShowProgressionResult(ProgressionResult result);
    }

    // Official UI decisions remain in NpcDetailPanelGiftContent. This boundary
    // supplies current local state and completes responses after atomic saving.
    public sealed class OfflinePartnerGiftUiServices : IPartnerGiftUiServices
    {
        private readonly OfflinePartnerGiftPlayer player;
        private readonly OfflinePartnerGiftConfiguration configuration;
        private readonly OfflinePartnerFriendshipTags tags;
        private readonly OfflinePartnerFriendshipProperties properties;
        private readonly IProfessionImageResourceLoader images;
        private readonly IOfflinePartnerGiftPresentation presentation;
        public OfflinePartnerGiftUiServices(OfflineMainlineProgressSession session,
            OfflinePartnerGiftPlayer player, OfflinePartnerGiftConfiguration configuration,
            OfflinePartnerFriendshipProperties properties, IProfessionUpgradeBindings playerBindings,
            IProfessionImageResourceLoader images, IOfflinePartnerGiftPresentation presentation)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            tags = new OfflinePartnerFriendshipTags(session, configuration);
            this.properties = properties ?? throw new ArgumentNullException(nameof(properties));
            PlayerBindings = playerBindings ?? throw new ArgumentNullException(nameof(playerBindings));
            this.images = images ?? throw new ArgumentNullException(nameof(images));
            this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        }
        public IPartnerGiftPlayer Player => player;
        public IProfessionUpgradeBindings PlayerBindings { get; }
        public IPartnerGiftLevels Levels => configuration;
        public IPartnerGiftExperience Gifts => configuration;
        public double NpcGiftDefaultPreferenceFactor => configuration.NpcGiftDefaultPreferenceFactor;
        public int CanCustomTagFriendShipLevel => configuration.CanCustomTagFriendShipLevel;
        public IProfessionPropertyBindings Properties => properties.Properties;
        public List<ProfessionPropertyType> TravelNpcPropTypeList => properties.TravelNpcPropTypeList;
        public bool IsTravelFunctionLimited => properties.IsTravelFunctionLimited;
        public IReadOnlyDictionary<ProfessionPropertyType, long> GetFriendshipLevelPropDiff(PartnerFriendshipNpcInfo npc, int from, int to)
            => properties.GetFriendshipLevelPropDiff(npc, from, to);
        public IReadOnlyDictionary<ProfessionPropertyType, long> GetLevelProps(int id, int level) => properties.GetLevelProps(id, level);
        public PartnerGiftNpcInfo GetNpcInfo(int id) => configuration.GetNpcInfo(id);
        public PartnerGiftInfo GetGiftInfo(IProfessionItemInfo item) => configuration.GetGiftInfo(item.ClassId);
        public bool IsNpcGift(IProfessionItemInfo item) => configuration.IsNpcGift(item.ClassId);
        public bool IsUnlockTravelNpcProp(ProfessionPropertyType property)
            => !TravelNpcPropTypeList.Contains(property) || !IsTravelFunctionLimited;
        public PartnerGiftTagInfo GetTagInfoByRoleTypeAndLevel(int role, int level) => configuration.GetTagInfoByRoleTypeAndLevel(role, level);
        public string GetNpcFriendshipTagNameKey(int id) => $"npc_friendship_tag_{id}_name";
        public bool IsAllCharacterValid(string input) => configuration.IsAllCharacterValid(input);
        public int CheckNpcFriendshipTagLegal(string input) => configuration.CheckNpcFriendshipTagLegal(input);
        public IProfessionAssetOperation<Sprite> LoadSprite(string path) => images.LoadAsset<Sprite>("Assets/Image/" + path);
        public void ConfigureGiftIcon(ItemIconNpcGift icon) => presentation.ConfigureGiftIcon(icon);
        public void ShowError(int error, object args, bool flag) => presentation.ShowError(error, args, flag);
        public string GetText(string key) => presentation.GetText(key);
        public void SystemMessage(string text) => presentation.SystemMessage(text);
        public void PopupInput(Action<IPartnerGiftInputDialog> initialize) => presentation.PopupInput(initialize);
        public void PopupConfirm(Action<IPartnerGiftConfirmDialog> initialize) => presentation.PopupConfirm(initialize);
        public void PopupFriendship(IWindowOperator owner, bool starGod, Action<IPartnerGiftFriendshipDialog> initialize)
            => presentation.PopupFriendship(owner, starGod, initialize);
        public object Compact(long count) => presentation.Compact(count);
        public void Close(GameObject window) => presentation.Close(window);

        public async void GiveGift(int npcId, Dictionary<int, long> gifts, Action success)
        {
            if (gifts == null) throw new ArgumentNullException(nameof(gifts));
            if (success == null) throw new ArgumentNullException(nameof(success));
            var command = gifts.ToDictionary(row => row.Key, row => row.Value);
            // DoGiveGift is reached after the original overflow confirmation.
            await Complete(() => player.GiveGifts(npcId, command, "partner-ui-gift:" + Guid.NewGuid().ToString("N"), true), success);
        }
        public async void SetCustomTag(int npcId, string input, Action<string> success)
        {
            if (success == null) throw new ArgumentNullException(nameof(success));
            await Complete(() => tags.Set(npcId, input, "partner-ui-tag:" + Guid.NewGuid().ToString("N")),
                () => success(tags.Get(npcId)));
        }
        private async Task Complete(Func<ProgressionResult> command, Action success)
        {
            await Task.Yield();
            ProgressionResult result;
            try { result = command(); }
            catch (IOException error) { result = WriteFailure(error); }
            catch (UnauthorizedAccessException error) { result = WriteFailure(error); }
            if (result.IsApplied || result.Status == ProgressionResultStatus.Unchanged) success();
            else presentation.ShowProgressionResult(result);
        }
        private static ProgressionResult WriteFailure(Exception error) => new ProgressionResult {
            Status = ProgressionResultStatus.InvalidState, Message = error.Message };
    }
}
