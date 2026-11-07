using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.World;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;
using UnityEngine;
using TeamUi = Zjcs.Official.TeamUI;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private const int RoadblockAssistPlayType = (int)NpcAssistPlayType.PropGate;
        private OfficialMap14RoadblockDialogView roadblockDialogView;
        private OfficialBattleAssistSelectionView roadblockAssistView;
        private OfficialMainlineRoadblockRule roadblockRule;
        private Map10RuntimeEntity roadblockEntity;
        private string roadblockAssistEntryId;
        private OfficialBattleNpcPortraitCatalog roadblockPortraits;
        private bool roadblockSourceValidated;
        private IProfessionPropertyBindings roadblockPropertyBindings;
        private readonly Dictionary<int, Dictionary<int, long>> roadblockNpcPropertyCache =
            new Dictionary<int, Dictionary<int, long>>();

        private bool TryInteractRoadblock(Map10RuntimeEntity entity)
        {
            if (mapClassId != 14 || entity == null) return false;
            OfficialMainlineRoadblockRule rule = OfficialMainlineRoadblockRules.Find(entity.PlacementId);
            if (rule == null) return false;
            ValidateRoadblockEntity(rule, entity);
            if (entity.State != 0 || localSave.completedRoadblockPlacements.Contains(entity.PlacementId))
                return true;
            OpenRoadblockDialog(entity, rule);
            return true;
        }

        private void ApplyCompletedRoadblockOverlays()
        {
            if (mapClassId != 14 || localSave == null) return;
            mainlineAssistProgress ??= OfflineNpcAssistanceProgress.Open(
                new ProfileSlotCatalog(persistentDataPath), slotId);
            // The usage receipt is durable before the map file is written. Recover
            // an interrupted gate commit without charging the invitation twice.
            localSave.completedRoadblockPlacements = localSave.completedRoadblockPlacements
                .Concat(OfficialMainlineRoadblockRules.All.Where(rule => mainlineAssistProgress.HasRecordedUsage(
                    RoadblockAssistPlayType, "map14:roadblock:" + rule.PlacementId.ToString(CultureInfo.InvariantCulture)))
                    .Select(rule => rule.PlacementId)).Distinct().OrderBy(value => value).ToArray();
            if (!(session?.Movement is OfficialMapGroundRules movement))
                throw new InvalidOperationException("Map14 roadblock movement rules are unavailable.");

            foreach (int placementId in OfficialMainlineRoadblockRules.All.Select(rule => rule.PlacementId))
            {
                OfficialMainlineRoadblockRule rule = OfficialMainlineRoadblockRules.Find(placementId);
                if (rule == null || !mapEntities.TryGetValue(placementId, out Map10RuntimeEntity entity))
                    throw new InvalidDataException("Saved Map14 roadblock does not exist: " + placementId);
                ValidateRoadblockEntity(rule, entity);
                if (localSave.completedRoadblockPlacements.Contains(placementId)) entity.State = 1;
                AddRoadblockOverlays(movement, entity, rule);
            }
        }

        private void ValidateRoadblockEntity(OfficialMainlineRoadblockRule rule,
            Map10RuntimeEntity entity)
        {
            if (entity.EntityClassId != rule.EntityClassId || entity.Region != rule.Region)
                throw new InvalidDataException("Map14 roadblock source identity differs at placement "
                    + rule.PlacementId + ".");
        }

        private void ValidateRoadblocksAgainstOriginalMapData()
        {
            if (mainlineSharedWorld?.IsLoaded != true || mainlineSharedWorld.Data.MapClassId != 14)
                throw new InvalidOperationException("The selected original Map14 metadata is not loaded.");

            Type metadataType = mainlineSharedWorld.Assemblies.CommonType("MapRegionEntityInfoManager");
            object metadata = metadataType.GetProperty("Instance").GetValue(null);
            var lookup = metadataType.GetMethod("GetContainerEntityInfo");
            if (lookup == null) throw new MissingMethodException(metadataType.FullName, "GetContainerEntityInfo");

            foreach (OfficialMainlineRoadblockRule rule in OfficialMainlineRoadblockRules.All)
            {
                if (!mapEntities.TryGetValue(rule.PlacementId, out Map10RuntimeEntity entity))
                    throw new InvalidDataException("Map14 roadblock placement is missing: " + rule.PlacementId);
                ValidateRoadblockEntity(rule, entity);

                object source = lookup.Invoke(metadata, new object[] { 14, rule.PlacementId });
                if (source == null)
                    throw new InvalidDataException("Selected original Map14 placement is missing: " + rule.PlacementId);
                object positionInfo = NativeMap14SharedPrefabs.Get(source, "PosInfo");
                object position = NativeMap14SharedPrefabs.Get(positionInfo, "Pos");
                object spawn = NativeMap14SharedPrefabs.Get(source, "DefSpawnEntityInfo");
                int sourceClassId = spawn == null ? 0
                    : Convert.ToInt32(NativeMap14SharedPrefabs.Get(spawn, "ClassId"), CultureInfo.InvariantCulture);
                int sourceRegion = Convert.ToInt32(
                    NativeMap14SharedPrefabs.Get(positionInfo, "Region"), CultureInfo.InvariantCulture);
                int sourceX = Convert.ToInt32(NativeMap14SharedPrefabs.Get(position, "X"), CultureInfo.InvariantCulture);
                int sourceY = Convert.ToInt32(NativeMap14SharedPrefabs.Get(position, "Y"), CultureInfo.InvariantCulture);
                if (sourceClassId != entity.EntityClassId || sourceRegion != entity.Region
                    || sourceX != entity.Grid.X || sourceY != entity.Grid.Y)
                    throw new InvalidDataException("Selected 884 Map14 placement differs from roadblock "
                        + rule.PlacementId + ".");
            }
        }

        private void OpenRoadblockDialog(Map10RuntimeEntity entity, OfficialMainlineRoadblockRule rule)
        {
            if (roadblockDialogView != null || roadblockAssistView != null) return;
            mainlineAssistProgress ??= OfflineNpcAssistanceProgress.Open(
                new ProfileSlotCatalog(persistentDataPath), slotId);
            mainlineAssistCatalog ??= OfficialCombatAssetCatalog.LoadDefault().LoadNpcAssistance();
            roadblockPortraits ??= OfficialBattleNpcPortraitCatalog.Load();
            roadblockNpcPropertyCache.Clear();
            roadblockEntity = entity;
            roadblockRule = rule;
            roadblockAssistEntryId = "map14:roadblock:" + rule.PlacementId.ToString(CultureInfo.InvariantCulture);
            roadblockDialogView = new OfficialMap14RoadblockDialogView(hud.ui.OverlayRoot,
                NativeUi.Text, OpenRoadblockAssistPicker, RemoveRoadblockAssist,
                CompleteRoadblock, CloseRoadblockDialog, RoadblockStageContext(entity), hud.playerHeadTexture, progressionSave.Save.Player.CharacterName);
            RefreshRoadblockDialog();
        }

        private void RefreshRoadblockDialog()
        {
            if (roadblockDialogView == null || roadblockRule == null) return;
            long playerValue = RoadblockPlayerProperty(roadblockRule.PropertyType);
            IReadOnlyList<int> selected = mainlineAssistProgress.Selected(
                RoadblockAssistPlayType, roadblockAssistEntryId);
            long total = playerValue;
            foreach (int npcId in selected)
                total = checked(total + RoadblockNpcProperty(npcId, roadblockRule.PropertyType));

            string propName = RoadblockPropertyName(roadblockRule.PropertyType);
            string title = RoadblockText("entity_" + roadblockRule.EntityClassId + "_name");
            string description = RoadblockText("entity_" + roadblockRule.EntityClassId + "_funcdesc");
            string threshold = RoadblockPropertyValue(roadblockRule.PropertyType, roadblockRule.PropertyStandard);
            string current = RoadblockPropertyValue(roadblockRule.PropertyType, total);
            roadblockDialogView.Refresh(title, description,
                "ui_roadblock_stage_team_prop_describe", propName, threshold, current,
                RoadblockPropertyValue(roadblockRule.PropertyType, playerValue),
                selected, RoadblockName, id => RoadblockPropertyValue(
                    roadblockRule.PropertyType, RoadblockNpcProperty(id, roadblockRule.PropertyType)),
                id => roadblockPortraits.Find(id),
                (float)total >= (float)roadblockRule.PropertyStandard);
        }

        private void OpenRoadblockAssistPicker(int _)
        {
            if (roadblockAssistView != null || roadblockDialogView == null) return;
            List<OfficialBattleAssistSelectionView.Candidate> candidates =
                BuildRoadblockAssistCandidates(out int maximum);
            roadblockAssistView = new OfficialBattleAssistSelectionView(hud.ui.OverlayRoot,
                candidates, maximum, SelectRoadblockAssistCandidate,
                ConfirmRoadblockAssistSelection, CloseRoadblockAssistPicker,
                RoadblockPropertyName(roadblockRule.PropertyType),
                RoadblockPlayerProperty(roadblockRule.PropertyType));
        }

        private List<OfficialBattleAssistSelectionView.Candidate> BuildRoadblockAssistCandidates(
            out int maximum)
        {
            if (roadblockRule == null || string.IsNullOrEmpty(roadblockAssistEntryId)
                || mainlineAssistProgress == null || mainlineAssistCatalog == null)
                throw new InvalidOperationException("Map14 roadblock assistance is not initialized.");

            FightOfficialNpcInviteRule invite = mainlineAssistCatalog.InviteRule(RoadblockAssistPlayType);
            maximum = Math.Max(0, Math.Min(3, invite.MaxPlayer));
            var friendships = (progressionSave.Save.Player.CharacterProperties.NpcFriendships
                    ?? new List<OfflineNpcFriendship>())
                .Where(value => value != null && value.NpcClassId > 0)
                .ToDictionary(value => value.NpcClassId, value => value.Level);
            int[] existing = mainlineAssistProgress.Selected(RoadblockAssistPlayType,
                roadblockAssistEntryId).ToArray();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            IReadOnlyList<OfflineNpcAssistCandidate> candidates = OfflineNpcAssistanceCandidates.Build(
                mainlineAssistCatalog, OfflineNpcAssistCandidateMode.TeamInvite,
                RoadblockAssistPlayType, progression.Player.Level, friendships,
                progressionSave.StoryCount, null,
                (npcId, cycle, timestamp) => mainlineAssistProgress.AssistCount(
                    RoadblockAssistPlayType, npcId, cycle, timestamp), existing, maximum, now, true);
            int[] retained = OfflineNpcAssistanceCandidates.SelectedIds(candidates).ToArray();
            if (!existing.SequenceEqual(retained))
                mainlineAssistProgress.SetSelection(RoadblockAssistPlayType,
                    roadblockAssistEntryId, retained, maximum);

            roadblockPortraits ??= OfficialBattleNpcPortraitCatalog.Load();
            var prop = (TeamUi.PropType)roadblockRule.PropertyType;
            var rows = candidates.ToDictionary(value => (long)value.NpcClassId, value =>
                new TeamUi.TeamInvitePlayerInfoData
                {
                    TargetData = new RoadblockSortTarget(value.NpcClassId),
                    PropTypes = new Dictionary<TeamUi.PropType, long>
                        { [prop] = RoadblockNpcProperty(value.NpcClassId, roadblockRule.PropertyType) }
                });
            var comparer = new TeamUi.TeamInviteRobotComparer(id => rows[id],
                id => mainlineAssistProgress.AssistCount(RoadblockAssistPlayType, (int)id,
                    (NpcAssistRefreshCycle)invite.RefreshCycle, now), false,
                new RoadblockSortServices(this, candidates), prop);
            // Preserve the original priority sequence, including collected time
            // and remaining assistance, instead of sorting only by property.
            return candidates
                .OrderBy(value => (long)value.NpcClassId,
                    Comparer<long>.Create(comparer.RobotElementPropTypeComparer))
                .Select(candidate => new OfficialBattleAssistSelectionView.Candidate
                {
                    Id = candidate.NpcClassId,
                    Name = RoadblockName(candidate.NpcClassId),
                    Portrait = roadblockPortraits.Find(candidate.NpcClassId),
                    Available = candidate.Available,
                    Selected = candidate.Selected,
                    FriendshipLevel = candidate.FriendshipLevel,
                    FriendshipBlocked = candidate.FriendshipBlocked,
                    PropertyAmount = rows[candidate.NpcClassId].PropTypes[prop],
                    PropertyValue = RoadblockPropertyValue(roadblockRule.PropertyType,
                        rows[candidate.NpcClassId].PropTypes[prop]),
                    PropertyIcon = RoadblockPropertyIcon(roadblockRule.PropertyType),
                    Reason = MainlineAssistReason(candidate)
                }).ToList();
        }

        private sealed class RoadblockSortTarget : IProfessionInfoNpcTarget
        {
            public RoadblockSortTarget(int id) { NpcClassId = id; }
            public int NpcClassId { get; }
            public ActorTargetType TargetType => ActorTargetType.Npc;
            public long TargetId => NpcClassId;
            public long ServerId => -1;
            public bool IsNpc => true;
            public string GetFigurePath(Sex sex) => throw new NotSupportedException("Sorting does not load figures.");
            public bool CheckActorCanAssist(int playType) => throw new NotSupportedException("Use candidate eligibility.");
        }

        private sealed class RoadblockSortServices : TeamUi.ITeamInviteComparerServices
        {
            private readonly OfficialMainlineMapRuntime owner;
            private readonly IReadOnlyList<OfflineNpcAssistCandidate> candidates;
            public RoadblockSortServices(OfficialMainlineMapRuntime owner,
                IReadOnlyList<OfflineNpcAssistCandidate> candidates)
            { this.owner = owner; this.candidates = candidates; }
            public int MainPlayerProfession => owner.progression.Player.Wardrobe.Profession;
            public bool IsNpcPinLimited => !owner.progression.CanPinNpc;
            public TeamUi.TeamInviteNpcRecord GetNpc(int npcClassId)
            {
                var record = owner.progression.Player.CharacterProperties.NpcFriendships
                    ?.FirstOrDefault(value => value.NpcClassId == npcClassId);
                return record == null ? null : new TeamUi.TeamInviteNpcRecord
                {
                    NpcClassId = npcClassId, IsCollected = record.IsCollected,
                    CollectedTime = new DateTime(record.CollectedTimeTicks, DateTimeKind.Utc)
                };
            }
            public bool IsNpcAssistLimit(int id) => candidates.First(value => value.NpcClassId == id).FriendshipBlocked;
            public bool CanJointTogether(int target, int player) => throw new NotSupportedException("Roadblocks are not joint strikes.");
            public (long ToTarget, long ToPlayer)? ToFriendshipData(IProfessionInfoTarget target)
                => throw new NotSupportedException("Element sorting does not query player friendship.");
            public void RefreshFestivalActivityData() => throw new NotSupportedException("Roadblocks are not festivals.");
            public bool HasFestivalBossNpc(int activity, int npc) => throw new NotSupportedException("Roadblocks are not festivals.");
            public void Error(string message) => Debug.LogError(message);
        }

        private void SelectRoadblockAssistCandidate(int npcId, bool selected)
        {
            if (roadblockAssistView == null || mainlineAssistProgress == null) return;
            List<OfficialBattleAssistSelectionView.Candidate> candidates =
                BuildRoadblockAssistCandidates(out int maximum);
            OfficialBattleAssistSelectionView.Candidate candidate = candidates.SingleOrDefault(
                value => value.Id == npcId);
            if (candidate == null) return;
            List<int> next = mainlineAssistProgress.Selected(RoadblockAssistPlayType,
                roadblockAssistEntryId).ToList();
            if (selected && !next.Contains(npcId))
            {
                if (!candidate.Available) return;
                if (next.Count >= maximum)
                {
                    hud.ShowOfficialNotice("最多选择 " + maximum + " 名助战伙伴");
                    return;
                }
                next.Add(npcId);
            }
            else if (!selected)
            {
                next.Remove(npcId);
            }

            if (!mainlineAssistProgress.SetSelection(RoadblockAssistPlayType,
                roadblockAssistEntryId, next, maximum))
                return;
            roadblockAssistView.Refresh(BuildRoadblockAssistCandidates(out maximum), maximum);
            RefreshRoadblockDialog();
        }

        private void RemoveRoadblockAssist(int slot)
        {
            if (mainlineAssistProgress == null || string.IsNullOrEmpty(roadblockAssistEntryId)) return;
            List<int> selected = mainlineAssistProgress.Selected(RoadblockAssistPlayType,
                roadblockAssistEntryId).ToList();
            if (slot < 0 || slot >= selected.Count) return;
            selected.RemoveAt(slot);
            int maximum = Math.Max(0, Math.Min(3,
                mainlineAssistCatalog.InviteRule(RoadblockAssistPlayType).MaxPlayer));
            mainlineAssistProgress.SetSelection(RoadblockAssistPlayType,
                roadblockAssistEntryId, selected, maximum);
            RefreshRoadblockDialog();
        }

        private void ConfirmRoadblockAssistSelection()
        {
            CloseRoadblockAssistPicker();
            RefreshRoadblockDialog();
        }

        private void CloseRoadblockAssistPicker()
        {
            roadblockAssistView?.Dispose();
            roadblockAssistView = null;
            RefreshRoadblockDialog();
        }

        private void CompleteRoadblock()
        {
            if (roadblockRule == null || roadblockEntity == null || localSave == null
                || localSave.completedRoadblockPlacements.Contains(roadblockRule.PlacementId))
                return;

            int playType = RoadblockAssistPlayType;
            FightOfficialNpcInviteRule invite = mainlineAssistCatalog.InviteRule(playType);
            int maximum = Math.Max(0, Math.Min(3, invite.MaxPlayer));
            List<OfficialBattleAssistSelectionView.Candidate> candidates =
                BuildRoadblockAssistCandidates(out maximum);
            var available = new HashSet<int>(candidates.Where(value => value.Available)
                .Select(value => value.Id));
            int[] selected = mainlineAssistProgress.Selected(playType, roadblockAssistEntryId).ToArray();
            if (selected.Length > maximum || selected.Any(value => !available.Contains(value)))
            {
                hud.ShowOfficialNotice("所选伙伴当前不满足助战条件");
                RefreshRoadblockDialog();
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long total = RoadblockPlayerProperty(roadblockRule.PropertyType);
            foreach (int npcId in selected)
                total = checked(total + RoadblockNpcProperty(npcId, roadblockRule.PropertyType));
            if ((float)total < (float)roadblockRule.PropertyStandard)
            {
                hud.ShowOfficialNotice("队伍属性未达到通行要求");
                RefreshRoadblockDialog();
                return;
            }

            mainlineAssistProgress.RecordUsage(playType, roadblockAssistEntryId,
                maximum, invite.OneNpcMaxInviteCount, invite.MaxInviteCount,
                (NpcAssistRefreshCycle)invite.RefreshCycle, now);
            localSave.completedRoadblockPlacements = localSave.completedRoadblockPlacements
                .Append(roadblockRule.PlacementId).Distinct().OrderBy(value => value).ToArray();
            roadblockEntity.State = 1;
            AddRoadblockOverlays((OfficialMapGroundRules)session.Movement, roadblockEntity, roadblockRule);
            RefreshEntityActivity();
            SaveLocalProgress();
            CloseRoadblockDialog();
        }

        private long RoadblockPlayerProperty(int propertyType)
        {
            IReadOnlyDictionary<int, long> properties = progression.EvaluateProps(
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).OutOfFightBattleProps;
            return properties.TryGetValue(propertyType, out long value) ? value : 0L;
        }

        private long RoadblockNpcProperty(int npcId, int propertyType)
        {
            if (!roadblockNpcPropertyCache.TryGetValue(npcId, out Dictionary<int, long> properties))
            {
                FightBattleUnitState unit = mainlineAssistCatalog.CreateTeamInviteUnit(1, 1,
                    Math.Max(1, progression.Player.Level), Math.Max(0, progression.Player.Rank),
                    Math.Max(0, progression.Player.SubRank), 0, 0, 1, npcId);
                properties = unit.Properties.GroupBy(value => value.Id)
                    .ToDictionary(value => value.Key, value => value.Last().Value);
                roadblockNpcPropertyCache.Add(npcId, properties);
            }
            return properties.TryGetValue(propertyType, out long value) ? value : 0L;
        }

        private string RoadblockName(int npcId)
        {
            string key = mainlineAssistCatalog.NpcNameKey(npcId);
            if (!OfficialRoadblockUiData.Load().TryLocalize(key, out string localized)
                || string.IsNullOrWhiteSpace(localized))
                throw new InvalidDataException("Original roadblock NPC name is missing: " + key);
            return localized;
        }

        private string RoadblockPropertyName(int propertyType)
        {
            string key = "PropType." + ((ProfessionPropertyType)propertyType).ToString();
            return RoadblockText(key);
        }

        private string RoadblockPropertyValue(int propertyType, long value)
        {
            var previous = ProfessionPropertyBindings.Current;
            try
            {
                if (previous == null)
                {
                    if (roadblockPropertyBindings == null)
                    {
                        var friendship = OfficialPartnerFriendshipAssetCatalog.LoadDefault();
                        var detail = OfficialPartnerDetailPropertyAssetCatalog.LoadDefault();
                        roadblockPropertyBindings = new OfflineProfessionPropertyBindings(friendship.PropertyConfiguration,
                            new OfflineProfessionImageResourceLoader(detail.Common.Images),
                            new OfflinePartnerLocalizationService(detail.Texts, detail.Common.GiftTexts, friendship.PropertyTexts, friendship.DialogTexts),
                            (a,b,c,d,e) => throw new InvalidOperationException("Unexpected roadblock entry popup."),
                            (a,b,c,d,e,f) => throw new InvalidOperationException("Unexpected roadblock blessing popup."));
                    }
                    ProfessionPropertyBindings.Current = roadblockPropertyBindings;
                }
                return ProfessionPropertyLanguage.FormatPropString(
                    (ProfessionPropertyType)propertyType, value, false, false, -1L);
            }
            finally { ProfessionPropertyBindings.Current = previous; }
        }

        private Sprite RoadblockPropertyIcon(int propertyType)
        {
            RoadblockPropertyValue(propertyType, 0);
            var bindings = roadblockPropertyBindings ?? ProfessionPropertyBindings.Required;
            string path = bindings.GetProperty((ProfessionPropertyType)propertyType).SmallIcon;
            var icons = new OfflineProfessionImageResourceLoader(
                Resources.Load<OfflineProfessionImageCatalog>("ProfessionUI/property-images"));
            using (var operation = icons.LoadAsset<Sprite>("Assets/Image/" + path))
                return operation.Result ?? throw new InvalidDataException("Original roadblock property icon is unbound: " + path);
        }

        private WorldStageUiContext RoadblockStageContext(Map10RuntimeEntity entity)
        {
            var catalog = Resources.Load<OfficialRoadblockDisplayCatalog>("Map14/Native/roadblock-display-catalog");
            int area = session.Definition.Navigation.Rows[entity.Grid.Y].Cells[entity.Grid.X].RawValues[0];
            string tag = catalog.areas.Single(r => r.area == area).areaTag;
            var background = catalog.backgrounds.FirstOrDefault(r => r.areaTag == tag)
                ?? catalog.backgrounds.Single(r => r.areaTag == "IgnoreKey");
            return new WorldStageUiContext {
                NameKey = "entity_" + entity.EntityClassId + "_name", DescriptionKey = "entity_" + entity.EntityClassId + "_desc",
                Background = background.sprite, DialogIconPrefab = catalog.entries.Single(e => e.classId == entity.EntityClassId).dialogIcon,
                IsActive = () => entity.State == 0, Close = CloseRoadblockDialog };
        }

        private string RoadblockText(string key)
        {
            string value = OfficialRoadblockUiData.Load().Localize(key);
            if (value != key) return value;
            try
            {
                return NativeUi.Text(key);
            }
            catch (KeyNotFoundException)
            {
                return key;
            }
        }

        private static void AddRoadblockOverlays(OfficialMapGroundRules movement,
            Map10RuntimeEntity entity, OfficialMainlineRoadblockRule rule)
        {
            for (int index = 0; index < rule.BodyOffsets.Count; index++)
            {
                Vector2Int offset = rule.BodyOffsets[index];
                long overlayId = (14L << 56) | ((long)rule.PlacementId << 16) | (uint)index;
                movement.RemoveOverlay(overlayId);
                // Original StageResultOverrideBodyGroundType applies Block to
                // all three body cells in stage 0, then reverts to the original
                // body: None at the center, Block on the two stone pillars.
                int groundType = entity.State == 1 && offset == Vector2Int.zero ? 0 : 2;
                movement.AddOverlay(overlayId,
                    new MapGridPoint(entity.Grid.X + offset.x, entity.Grid.Y + offset.y), groundType, true);
            }
        }

        private void CloseRoadblockDialog()
        {
            CloseRoadblockAssistPicker();
            OfficialMap14RoadblockDialogView view = roadblockDialogView;
            roadblockDialogView = null;
            roadblockRule = null;
            roadblockEntity = null;
            roadblockAssistEntryId = null;
            view?.Close();
        }

        private void DisposeRoadblockDialog()
        {
            roadblockAssistView?.Dispose();
            roadblockAssistView = null;
            roadblockDialogView?.Dispose();
            roadblockDialogView = null;
            roadblockRule = null;
            roadblockEntity = null;
            roadblockAssistEntryId = null;
            roadblockNpcPropertyCache.Clear();
            roadblockPortraits = null;
        }
    }
}
