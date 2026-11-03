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

        private OfficialBattleResultReport resultReport;

        // This adapter supplies skill art/detail and this local profile. Window
        // selection, native controls and disposal stay in the battle module.
        public OfficialBattleReportView.Presentation CreateBattleReportPresentation(
            GameObject lifetime, int maximumRound, Transform popupParent)
        {
            return new OfficialBattleReportView.Presentation {
                MaximumRound = maximumRound,
                Font = lifetime.AddComponent<OfficialSkillFontLease>().Acquire(skillAssets.NativeUiFont),
                PlayerName = id => id == 1 ? progression.Player.CharacterName : null,
                PlayerIcon = (icon, id) => {
                    if (id != 1) throw new InvalidOperationException("A report actor needs its host identity adapter.");
                    BindReportPlayerIcon(icon);
                },
                SkillIcon = (slot, skill, snapshot) => BuildSkillIcon(skill.SkillItemId, slot,
                    "ReportSkillIcon", snapshot?.Rank ?? 0, snapshot?.SkillSkinId ?? 0),
                SkillDetail = snapshot => OpenSnapshotSkillDetail(snapshot, popupParent),
                Closed = CloseSkillDetail
            };
        }

        // SkillDetailDialog.Init(SnapshotSkill): a report opens the generic,
        // read-only prefab with its recorded level, rank, sub-rank and skin.
        private void OpenSnapshotSkillDetail(FightBattleSkillDefinition snapshot, Transform popupParent)
        {
            CloseSkillDetail();
            detailDialog = BuildOfficialDialog("skill_plain_detail", out var detailNodes, out _);
            detailDialog.transform.SetParent(popupParent, false);
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

        public void BindReportPlayerIcon(Component icon)
        {
            if (icon == null) return;
            var portrait = OfficialCharacterSkillHeader.CaptureOriginalPortrait(progression, profileSlotId, profileSavesRoot);
            icon.gameObject.AddComponent<OfficialSkillScreenshotLease>().Texture = portrait;
            int profession = progression.Player.Wardrobe.Profession;
            OfficialBattleReportIdentityView.Bind(icon, new OfficialBattleReportIdentityView.Identity {
                Name = progression.Player.CharacterName,
                Profession = profession,
                ProfessionIcon = assets.FindSprite(progression.Catalog.ProfessionIconUri(profession)),
                PlayerPortrait = portrait
            });
        }


    }

    internal sealed class OfficialSkillScreenshotLease : MonoBehaviour
    {
        public Texture2D Texture;
        private void OnDestroy() { if (Texture != null) Destroy(Texture); }
    }
}
