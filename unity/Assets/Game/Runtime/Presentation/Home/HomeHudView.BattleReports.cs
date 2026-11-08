using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Combat;
using Zjcs.Official.BattleResultUI;
using System.Collections.Generic;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        [Serializable] private sealed class ResultReportBindings
        { public ResultReportWindow[] windows; public ReportProfessionIcon[] professionIcons; }
        [Serializable] private sealed class ResultReportWindow
        { public string resource, report, share, save, thumbsUp; }
        [Serializable] private sealed class ReportProfessionIcon
        { public int profession; public string address; }

        private void BindDailyChallengeBattleReport(GameObject result, string resource,
            FightBattleCheckpoint checkpoint, FightBattleReport finalReport)
        {
            string originalName = resource.Substring(resource.LastIndexOf('/') + 1);
            var actionPrefab = Resources.Load<GameObject>("Combat/ResultActions/" + originalName);
            if (actionPrefab == null) throw new InvalidOperationException("Original result action module is absent: " + originalName);
            var actions = UnityEngine.Object.Instantiate(actionPrefab, result.transform, false);
            actions.name = "OriginalResultActions";
            var native = actions.GetComponent<GameEndPanelBase>();
            if (native == null || native.ReportButton == null)
                throw new InvalidOperationException("Original result action controls are unbound.");
            foreach (var sound in actions.GetComponentsInChildren<Zjcs.Official.WorldUI.WorldButtonSound>(true))
                sound.Post = (clip, emitter) => {
                    var source = emitter.GetComponent<AudioSource>();
                    if (source == null) source = emitter.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = 0f;
                    source.PlayOneShot(clip);
                };
            // The module retains the exact source ancestor transforms. Its
            // original subtree replaces the legacy export's omitted scripts.
            var legacy = result.transform.Find("Content/BtnGroup");
            if (legacy != null) legacy.gameObject.SetActive(false);
            BindNativeBattleResultReport(result, native, checkpoint, finalReport);
            native.InitMsg(new BattleResultData {
                StageId = checkpoint?.StageId ?? 0,
                Win = checkpoint?.Outcome != null && checkpoint.Outcome.Won,
                Players = Array.Empty<BattleResultPlayer>()
            });
        }

        private void BindNativeBattleResultReport(GameObject result, GameEndPanelBase native,
            FightBattleCheckpoint checkpoint, FightBattleReport finalReport)
            => BindNativeBattleResultReport(result, native, checkpoint, finalReport, null);

        private void BindNativeBattleResultReport(GameObject result, GameEndPanelBase native,
            FightBattleCheckpoint checkpoint, FightBattleReport finalReport,
            Action<OfficialBattleReportView.Presentation, CharacterSkillPanel, GameObject> bindIdentity)
        {
            var data = Resources.Load<TextAsset>("Combat/BattleReport/result-report-bindings");
            if (data == null) throw new InvalidOperationException("Original result report bindings are absent.");
            var bindings = JsonUtility.FromJson<ResultReportBindings>(data.text);
            var report = OfficialBattleResultReport.Attach(result, new OfficialBattleResultReport.Binding {
                Report = native.ReportButton, BindReportButton = false,
                PopupParent = canvas.transform, Checkpoint = checkpoint, FinalReport = finalReport,
                Presentation = lifetime => {
                    // No skill page is opened. Its existing original item/detail
                    // adapter supplies only the art and read-only skill snapshot.
                    var adapter = lifetime.AddComponent<CharacterSkillPanel>();
                    adapter.Initialize(OfficialCharacterAssetCatalog.LoadDefault(), host.DailyDungeonProgression,
                        host.ActiveProfileSlotId, host.SavesRoot);
                    var presentation = adapter.CreateBattleReportPresentation(lifetime, checkpoint.MaxRound, canvas.transform);
                    if (bindIdentity != null)
                    {
                        bindIdentity(presentation, adapter, lifetime);
                        return presentation;
                    }
                    var partners = OfficialPartnerAssetCatalog.LoadDefault();
                    var images = Resources.Load<OfflineProfessionImageCatalog>("Partners/OfficialPartnerNpcIconImages");
                    if (images == null) throw new InvalidOperationException("Original NPC report icons are absent.");
                    Sprite Image(string address) => images.Entries.Single(value => value.Path == address).Asset as Sprite
                        ?? throw new InvalidOperationException("Original report sprite is unbound: " + address);
                    System.Collections.Generic.Dictionary<string, object> Npc(int id) {
                        var unit = checkpoint.Units.Single(value => value.Id == id);
                        return partners.Rows("npc").Single(value => Convert.ToInt32(value["Id"], CultureInfo.InvariantCulture) == unit.NpcClassId);
                    }
                    string Name(int id) {
                        if (id == 1) return host.Profile.Save.Player.CharacterName;
                        string key = Convert.ToString(Npc(id)["NameKey"], CultureInfo.InvariantCulture);
                        return host.Catalog.LocalizedText(key, null)
                            ?? throw new InvalidOperationException("Original report NPC name is unbound: " + key);
                    }
                    presentation.PlayerName = Name;
                    presentation.PlayerIcon = (icon, id) => {
                        if (id == 1) { adapter.BindReportPlayerIcon(icon); return; }
                        var unit = checkpoint.Units.Single(value => value.Id == id);
                        var npc = Npc(id);
                        OfficialBattleReportIdentityView.Bind(icon, new OfficialBattleReportIdentityView.Identity {
                            Name = Name(id), IsNpc = true, Profession = unit.Profession,
                            NpcIcon = Image(Convert.ToString(npc["Icon"], CultureInfo.InvariantCulture)),
                            ProfessionIcon = unit.Profession == 0 ? null : Image(bindings.professionIcons
                                .Single(value => value.profession == unit.Profession).address)
                        });
                    };
                    return presentation;
                }
            });
            native.Bind(new LocalResultActions(checkpoint, report));
            // Attach owns availability before InitMsg; the native setter requires
            // ResultMsg and would hide a freshly bound result at this point.
        }

        // Confirmed offline choice: original local statistics remain available;
        // sharing, online record management and multiplayer thumbs-up are hidden
        // through the original support-type branches.
        private sealed class LocalResultActions : IBattleResultBaseHost
        {
            private readonly FightBattleCheckpoint checkpoint;
            private readonly OfficialBattleResultReport report;
            private readonly ISet<int> unsupported = new HashSet<int>();
            public LocalResultActions(FightBattleCheckpoint checkpoint, OfficialBattleResultReport report)
            { this.checkpoint = checkpoint; this.report = report; }
            public BattleResultStage GetStage(int id) => checkpoint != null && checkpoint.StageId == id
                ? new BattleResultStage { Id = id, Type = checkpoint.StageType } : null;
            public ISet<int> ShareStageTypes => unsupported;
            public ISet<int> SaveRecordStageTypes => unsupported;
            public ISet<int> ThumbsUpStageTypes => unsupported;
            public bool IsPlayback => false;
            public bool IsAutoSaveRecordStage(int id) => false;
            public bool HasLocalCapturedRecord(long id) => false;
            public IEnumerable<long> TeamMemberTargetIds => Array.Empty<long>();
            public long ThumbsUpGameId { get; private set; }
            public void SetThumbsUpGameId(long id) => ThumbsUpGameId = id;
            public void OpenReport(BattleResultData result) => report.Open();
            public void OpenShare(long id) => throw new NotSupportedException("Online result sharing is unavailable offline.");
            public bool ActiveSaveRecord(long id, int type) => throw new NotSupportedException("Online result record management is unavailable offline.");
            public void ShowSaveRecordSuccess() => throw new NotSupportedException("Online result record management is unavailable offline.");
        }
    }
}
