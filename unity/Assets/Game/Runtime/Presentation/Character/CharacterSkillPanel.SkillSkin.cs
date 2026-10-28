using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;

namespace Zjcs.Presentation.Character
{
    public sealed partial class CharacterSkillPanel
    {
        private Dictionary<string, object> appearanceTexts;
        private GameObject skillVideoDialog;

        private void CloseSkillVideo()
        {
            if (skillVideoDialog != null) { skillVideoDialog.SetActive(false); Destroy(skillVideoDialog); }
            skillVideoDialog = null;
        }

        private void OpenOriginalSkillVideo(string path)
        {
            CloseSkillVideo();
            skillVideoDialog = BuildOfficialDialog("skill_video_preview", out var videoNodes, out var videoButtons);
            var root = AuxObject(AuxObject(skillUiTemplates["skill_video_preview"])["root"]);
            var binding = FindOriginalComponent(root, "PlayVideoDialog");
            var videoBinding = FindOriginalComponent(root, "UIVideoPlayer");
            RectTransform videoNode = videoNodes[Convert.ToString(binding["VideoPlayer"])];
            RectTransform aspectNode = videoNodes[Convert.ToString(binding["AspectRatioFitter"])];
            var fitter = aspectNode.gameObject.AddComponent<AspectRatioFitter>();
            OfficialSkillComponentBindings.Apply(fitter, FindOriginalComponent(root, "AspectRatioFitter"), videoNodes);
            var video = videoNode.gameObject.AddComponent<OfficialSkillSkinVideo>();
            video.Initialize(videoNodes[Convert.ToString(videoBinding["mRawImage"])], FindOriginalComponent(root, "VideoPlayer"));
            video.VideoPrepared += (width, height) => {
                fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
                fitter.aspectRatio = (float)width / height;
            };
            foreach (var node in AuxiliaryNodes(root)) {
                if (!node.TryGetValue("original_bindings", out var raw) || !AuxObject(raw).TryGetValue("MultiLanguageText", out var language)) continue;
                string key = Convert.ToString(AuxObject(language)["myKey"]);
                SetAuxiliaryText(videoNodes[Convert.ToString(node["path"])], OriginalDetailText(key) ?? key);
            }
            foreach (var button in videoButtons.Where(pair => pair.Key.EndsWith("/CloseBtn", StringComparison.Ordinal)))
                button.Value.onClick.AddListener(CloseSkillVideo);
            video.Play(path);
        }

        private int OriginalEffectSkillId(int skillId)
        {
            int skinId = progression.EquippedSkillSkinId(skillId);
            var skin = skinId > 0 ? progression.Catalog.Find("skill_skin", skinId) : null;
            return skin == null ? skillId : AuxInt(skin, "EffectSkillId");
        }

        // SkillSkinBtn.Refresh/OnClickBtn, with the confirmed offline opening policy.
        private void BindOriginalSkillSkinButton(int skillId, Dictionary<string, object> root,
            Dictionary<string, RectTransform> detailNodes)
        {
            var binding = FindOriginalComponent(root, "SkillSkinBtn");
            if (binding == null) return;
            bool owned = progression.Catalog.Rows("skill_skin").Any(row => AuxInt(row, "SkillId") == skillId
                && AuxInt(row, "SkinType") != 0 && progression.Player.Inventory.Any(stack => stack.ItemId == AuxInt(row, "ClassId") && stack.Count > 0));
            SetOfficialState("skill_panel_detail", "SkillSkinBtn", "mHasSkinStateSwitcher", owned ? 1 : 0, detailNodes);
            // SkillPanelSkillDetailDialog.RefreshBtn overrides the skin state
            // visibility in HomeEdit, where this skill panel is hosted.
            detailNodes[Convert.ToString(binding["mSkillSkinBtn"])].gameObject.SetActive(true);
            detailNodes[Convert.ToString(binding["mSkillSkinBtn"])].GetComponent<Button>().onClick.AddListener(() => OpenOriginalSkillSkin(skillId));
        }

        private string SkillSkinText(string key)
        {
            if (appearanceTexts == null)
            {
                var data = AuxObject(OfficialEquipmentJson.Parse(Resources.Load<TextAsset>("Character/skill-appearance-ui").text));
                appearanceTexts = AuxObject(data["texts"]);
            }
            return appearanceTexts.TryGetValue(key, out object value) ? Convert.ToString(value) : OriginalDetailText(key);
        }

        private void OpenOriginalSkillSkin(int skillId)
        {
            GameObject dialog = BuildAuxiliary("skill_skin_replace", out var skinNodes, out _);
            var root = AuxObject(AuxObject(skillUiTemplates["skill_skin_replace"])["root"]);
            var binding = FindOriginalComponent(root, "SkillSkinReplaceDialog");
            RectTransform Bound(string field) => skinNodes[Convert.ToString(binding[field])];
            var skins = progression.Catalog.Rows("skill_skin").Where(row => AuxInt(row, "SkillId") == skillId).ToList();
            var slots = ((List<object>)binding["mSkillIcons"]).Select(Convert.ToString).ToList();
            if (skins.Count > slots.Count) throw new InvalidOperationException("Original skill skin icon capacity exceeded.");
            var videoBinding = FindOriginalComponent(root, "UIVideoPlayer");
            RectTransform videoNode = skinNodes[Convert.ToString(binding["mUIVideoPlayer"])];
            RectTransform screen = skinNodes[Convert.ToString(videoBinding["mRawImage"])];
            var video = videoNode.gameObject.AddComponent<OfficialSkillSkinVideo>();
            video.Initialize(screen, FindOriginalComponent(root, "VideoPlayer"));
            string selectedVideo = null;
            var playBinding = FindOriginalComponent(root, "UIPlayVideoBtn");
            skinNodes[Convert.ToString(playBinding["PlayBtn"])].GetComponent<Button>().onClick.AddListener(() => {
                if (!string.IsNullOrEmpty(selectedVideo)) OpenOriginalSkillVideo(selectedVideo);
            });
            int selected = 0;
            int Equipped() {
                int equipped = progression.EquippedSkillSkinId(skillId);
                return equipped != 0 ? equipped : skins.Where(row => AuxInt(row, "SkinType") == 0).Select(row => AuxInt(row, "ClassId")).FirstOrDefault();
            }
            void RefreshState() {
                var row = skins.FirstOrDefault(value => AuxInt(value, "ClassId") == selected);
                bool have = row != null && (AuxInt(row, "SkinType") == 0
                    || progression.Player.Inventory.Any(stack => stack.ItemId == selected && stack.Count > 0));
                SetOfficialState("skill_skin_replace", "SkillSkinReplaceDialog", "mDialogStateSwitcher",
                    !have ? 1 : selected == Equipped() ? 2 : 0, skinNodes);
            }
            void Select(int index) {
                selected = AuxInt(skins[index], "ClassId");
                Vector3 position = Bound("mArrowGo").position;
                position.x = skinNodes[slots[index]].position.x;
                Bound("mArrowGo").position = position;
                for (int i = 0; i < slots.Count; i++) {
                    var source = AuxiliaryNodes(root).First(value => Convert.ToString(value["path"]) == slots[i]);
                    var iconBinding = FindOriginalComponent(source, "ItemIconSkill");
                    skinNodes[Convert.ToString(iconBinding["SelectedObj"])].gameObject.SetActive(skins.Count > 1 && i == index);
                }
                SetAuxiliaryText(Bound("mSkillSkinName"), SkillSkinText("item_" + selected + "_name"));
                SetAuxiliaryText(Bound("mSkillSkinDesc"), SkillSkinText("skill_skin_" + selected + "_desc"));
                RefreshState();
                selectedVideo = Path.Combine(Application.streamingAssetsPath, "Character", "SkillSkinVideos",
                    AuxInt(skins[index], "EffectSkillId").ToString(CultureInfo.InvariantCulture) + ".mp4");
                video.Play(selectedVideo);
            }
            for (int i = 0; i < slots.Count; i++) {
                RectTransform icon = skinNodes[slots[i]];
                icon.gameObject.SetActive(i < skins.Count);
                if (i >= skins.Count) continue;
                int itemId = AuxInt(skins[i], "ClassId");
                var source = AuxiliaryNodes(root).First(value => Convert.ToString(value["path"]) == slots[i]);
                var iconBinding = FindOriginalComponent(source, "ItemIconSkill");
                var image = icon.Find("Content/mask/icon").GetComponent<Image>();
                image.sprite = assets.FindSkillSkinSprite(progression.Catalog, itemId);
                image.enabled = image.sprite != null;
                if (!image.enabled) throw new InvalidOperationException("Official skill skin sprite is absent: " + itemId);
                icon.Find("Content/QualityImg").GetComponent<Image>().sprite = assets.FindQualityBackgroundSprite(Quality(itemId));
                skinNodes[Convert.ToString(iconBinding["SkillStarInfo"])].gameObject.SetActive(false);
                icon.Find("Content/BgCountText").gameObject.SetActive(false);
                int index = i;
                icon.GetComponent<Button>().onClick.AddListener(() => Select(index));
            }
            Bound("mCloseBtn").GetComponent<Button>().onClick.AddListener(CloseAuxiliary);
            Bound("mUseBtn").GetComponent<Button>().onClick.AddListener(() => {
                var row = skins.First(value => AuxInt(value, "ClassId") == selected);
                var result = AuxInt(row, "SkinType") == 0
                    ? progression.TakeOffSkillSkin(skillId, "skill-skin-off-" + Guid.NewGuid().ToString("N"))
                    : progression.WearSkillSkin(selected, "skill-skin-on-" + Guid.NewGuid().ToString("N"));
                LastMessage = result.Message ?? result.Status.ToString();
                if (!result.IsApplied) return;
                RefreshState();
                Refresh();
                if (detailDialog != null && currentDetailSkillId == skillId && currentDetailNodes != null) {
                    BindOriginalSkillDetail(skillId, currentDetailNodes, false);
                    SetTextValue(DialogNode(currentDetailNodes, "NameText")?.GetComponent<Text>(), SkillName(skillId));
                    Transform iconSlot = DialogNode(currentDetailNodes, "ItemIconSkill") ?? DialogNode(currentDetailNodes, "IconSlot");
                    if (iconSlot != null) {
                        Transform oldIcon = iconSlot.Find("OfficialSkillIcon");
                        if (oldIcon != null) { oldIcon.gameObject.SetActive(false); Destroy(oldIcon.gameObject); }
                        BuildSkillIcon(skillId, iconSlot, "OfficialSkillIcon");
                    }
                }
                Changed?.Invoke();
            });
            StartCoroutine(SelectAfterLayout());
            IEnumerator SelectAfterLayout() {
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                if (dialog == null || !dialog.activeInHierarchy) yield break;
                if (skins.Count > 0) {
                    int index = skins.FindIndex(row => AuxInt(row, "ClassId") == Equipped());
                    Select(Math.Max(0, index));
                } else RefreshState();
            }
        }
    }
}
