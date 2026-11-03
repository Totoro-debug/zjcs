using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Character
{
    public sealed partial class CharacterSkillPanel
    {
        private Action auxiliaryReturn;

        private OfficialBattleReportView battleReport;

        // The character page supplies its profile appearance and skill-detail adapter.
        private void OpenOriginalBattleReport(FightBattleCheckpoint checkpoint, int maxRound, Action back)
        {
            CloseAuxiliary();
            var lease = new GameObject("BattleReportFontLease");
            try
            {
                var font = lease.AddComponent<OfficialSkillFontLease>().Acquire(skillAssets.NativeUiFont);
                battleReport = new OfficialBattleReportView(panel != null ? panel.transform.parent : transform.parent,
                    checkpoint, new OfficialBattleReportView.Presentation {
                        MaximumRound = maxRound,
                        Font = font,
                        PlayerIcon = (icon, id) => BindReportPlayerIcon(icon),
                        SkillIcon = (slot, skill, snapshot) => BuildSkillIcon(skill.SkillItemId, slot,
                            "ReportSkillIcon", snapshot?.Rank ?? 0, snapshot?.SkillSkinId ?? 0),
                        SkillDetail = OpenSnapshotSkillDetail,
                        Closed = () => { CloseAuxiliary(); back?.Invoke(); }
                    });
                auxiliaryDialog = battleReport.Root;
                lease.transform.SetParent(auxiliaryDialog.transform, false);
                auxiliaryReturn = back;
                GetComponentInParent<OfficialCharacterPanelView>()?.SetSkillAuxiliaryFullScreen(true);
            }
            catch { Destroy(lease); throw; }
        }

        // SkillDetailDialog.Init(SnapshotSkill): a report opens the generic,
        // read-only prefab with its recorded level, rank, sub-rank and skin.
        private void OpenSnapshotSkillDetail(FightBattleSkillDefinition snapshot)
        {
            CloseSkillDetail();
            detailDialog = BuildOfficialDialog("skill_plain_detail", out var detailNodes, out _);
            int effectId = snapshot.UseItemId > 0 ? snapshot.UseItemId : snapshot.ItemId;
            var item = progression.Catalog.FindItem(effectId);
            SetTextValue(DialogNode(detailNodes, "NameText")?.GetComponent<TMP_Text>(), Convert.ToString(item["Name"]));
            BindOriginalSkillDetail(snapshot.ItemId, detailNodes, false, snapshot, "skill_plain_detail");
            var root = AuxObject(AuxObject(skillUiTemplates["skill_plain_detail"])["root"]);
            var binding = FindOriginalComponent(root, "SkillDetailDialog");
            var slot = detailNodes[Convert.ToString(binding["ItemIconSlot"])];
            var icon = BuildSkillIcon(snapshot.ItemId, slot, "SnapshotSkillIcon", snapshot.Rank, snapshot.SkillSkinId);
            foreach (string name in new[] { "SkillStarInfo", "QualityImg", "BottomText", "ShadowObj" })
                FindDeep(icon.transform, name)?.gameObject.SetActive(false);
            var itemBinding = FindOriginalComponent(root, "ItemDetailDialog");
            foreach (string field in new[] { "ItemCountGo", "ItemSourceObj" })
                if (itemBinding.TryGetValue(field, out var path) && path is string key && detailNodes.TryGetValue(key, out var node))
                    node.gameObject.SetActive(false);
            var share = FindOriginalComponent(root, "ShareItemBtn");
            if (share != null && share.TryGetValue("ShareBtn", out var sharePath) && sharePath is string shareKey)
                detailNodes[shareKey].gameObject.SetActive(false);
        }

        private void BindReportPlayerIcon(Component icon)
        {
            if (icon == null) return;
            foreach (TMP_Text text in icon.GetComponentsInChildren<TMP_Text>(true)) {
                string value = text.name.IndexOf("Name", StringComparison.OrdinalIgnoreCase) >= 0
                    ? progression.Player.CharacterName : text.name.IndexOf("Level", StringComparison.OrdinalIgnoreCase) >= 0
                    ? progression.Player.Level.ToString(CultureInfo.InvariantCulture) : null;
                if (value == null) continue;
                text.GetComponent<WorldMultiLanguageText>()?.SetKey(string.Empty);
                text.text = value;
            }
            var head = icon.GetComponentsInChildren<RectTransform>(true).First(value => value.name == "HeadImg");
            var raw = head.GetComponent<RawImage>() ?? head.gameObject.AddComponent<RawImage>();
            OfficialSkillComponentBindings.Apply(raw, AuxObject(AuxiliaryData["report_portrait"]), new Dictionary<string, RectTransform>());
            // Report windows can open before the shared skill header exists.
            // Photograph this profile instead of selecting a placeholder HeadImg
            // from another window with the same node name.
            var portrait = OfficialCharacterSkillHeader.CaptureOriginalPortrait(progression, profileSlotId, profileSavesRoot);
            icon.gameObject.AddComponent<OfficialSkillScreenshotLease>().Texture = portrait;
            raw.texture = portrait;
            raw.enabled = true;
            for (Transform node = head; node != icon.transform && node != null; node = node.parent)
                node.gameObject.SetActive(true);
        }


    }

    internal sealed class OfficialSkillScreenshotLease : MonoBehaviour
    {
        public Texture2D Texture;
        private void OnDestroy() { if (Texture != null) Destroy(Texture); }
    }
}
