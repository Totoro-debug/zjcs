using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Home;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private const string HomeTutorialPortrait = "npc_portrait_8402-80389f7e01.png";
        private GameObject tutorialDialogueLayer;
        private HomeUiRenderer tutorialDialogueRenderer;
        private IReadOnlyList<HomeTutorialDialogue> tutorialDialogueLines;
        private IReadOnlyList<HomeTutorialStoryOption> tutorialDialogueOptions;
        private Action tutorialDialogueCompleted;
        private Func<int, bool> tutorialOptionSelected;
        private int tutorialDialogueIndex;
        private string tutorialDialogueRoot = "/TalkDialog";
        private GameObject tutorialMaskObject;
        private HomeFruitTreeTutorialMaskGraphic tutorialMask;
        private readonly Dictionary<GameObject, bool> hiddenTutorialUi = new Dictionary<GameObject, bool>();

        public int SelectedTabIndex => selectedTab;
        public string ActivePanelKey => activePanelKey;
        internal bool HasTutorialDialogue => tutorialDialogueLayer != null;
        public bool HasTutorialHomeTab => bottomRenderer != null
            && bottomRenderer.Find("/PublicBottomBar")?.activeInHierarchy == true
            && bottomRenderer.Find<Toggle>("/PublicBottomBar/Content/Group/Home/UnlockObj") != null;

        internal void ShowOfficialTutorialDialogue(IReadOnlyList<HomeTutorialDialogue> lines, Action completed)
        {
            ShowOfficialTutorialDialogue(lines, null, null, completed);
        }

        internal void ShowOfficialTutorialOptionDialog(IReadOnlyList<HomeTutorialDialogue> lines,
            IReadOnlyList<HomeTutorialStoryOption> options, Func<int, bool> selected)
        {
            if (options == null || options.Count == 0 || selected == null)
                throw new InvalidOperationException("The official tutorial option choices are unavailable.");
            ShowOfficialTutorialDialogue(lines, options, selected, null);
        }

        private void ShowOfficialTutorialDialogue(IReadOnlyList<HomeTutorialDialogue> lines,
            IReadOnlyList<HomeTutorialStoryOption> options, Func<int, bool> selected, Action completed)
        {
            ClearOfficialTutorialDialogue();
            tutorialDialogueLines = (lines ?? Array.Empty<HomeTutorialDialogue>()).ToArray();
            tutorialDialogueOptions = options;
            tutorialOptionSelected = selected;
            tutorialDialogueCompleted = completed;
            if (tutorialDialogueLines.Count == 0 && tutorialDialogueOptions == null)
            {
                completed?.Invoke();
                return;
            }
            if (tutorialDialogueLines.Any(line => line == null || line.roleId != 840200
                || string.IsNullOrWhiteSpace(line.text)))
                throw new InvalidOperationException("The official home tutorial dialogue speaker or text is unavailable.");

            tutorialDialogueIndex = 0;
            bool hasOptions = tutorialDialogueOptions != null;
            tutorialDialogueRoot = hasOptions ? "/OptionTalkDialog" : "/TalkDialog";
            tutorialDialogueRenderer = CreatePetLayer(hasOptions ? "talk_options" : "talk_talk",
                out tutorialDialogueLayer,
                ContinueOfficialTutorialDialogue);
            if (tutorialDialogueLines.Count > 0)
            {
                string portraitPath = FruitTreeTutorialPortraitPath.Replace("/TalkDialog", tutorialDialogueRoot);
                GameObject portraitParent = tutorialDialogueRenderer.Find(portraitPath);
                Sprite portrait = catalog.FindSprite(HomeTutorialPortrait);
                if (portraitParent == null || portrait == null)
                    throw new InvalidOperationException("The official home tutorial portrait is unavailable.");
                var portraitObject = new GameObject("HomeTutorialPortrait", typeof(RectTransform), typeof(Image));
                portraitObject.transform.SetParent(portraitParent.transform, false);
                RectTransform portraitRect = (RectTransform)portraitObject.transform;
                portraitRect.anchorMin = Vector2.zero;
                portraitRect.anchorMax = Vector2.one;
                portraitRect.offsetMin = Vector2.zero;
                portraitRect.offsetMax = Vector2.zero;
                Image portraitImage = portraitObject.GetComponent<Image>();
                portraitImage.sprite = portrait;
                portraitImage.preserveAspect = true;
                portraitImage.raycastTarget = false;
            }

            Button next = tutorialDialogueRenderer.Find<Button>(tutorialDialogueRoot + "/NextBtn");
            if (next == null) throw new InvalidOperationException("The official tutorial next button is unavailable.");
            next.onClick.AddListener(ContinueOfficialTutorialDialogue);
            if (hasOptions)
            {
                tutorialDialogueRenderer.SetActive(tutorialDialogueRoot + "/OptionSlots", false);
                tutorialDialogueRenderer.SetActive(tutorialDialogueRoot + "/DarkMask", false);
                tutorialDialogueRenderer.SetActive(tutorialDialogueRoot + "/ClickMask", false);
            }
            if (tutorialDialogueLines.Count > 0) SetOfficialTutorialDialogueLine();
            else ShowOfficialTutorialOptions();
        }

        internal void ClearOfficialTutorialDialogue()
        {
            ClosePetLayer(ref tutorialDialogueLayer);
            tutorialDialogueRenderer = null;
            tutorialDialogueLines = null;
            tutorialDialogueOptions = null;
            tutorialDialogueCompleted = null;
            tutorialOptionSelected = null;
            tutorialDialogueIndex = 0;
            tutorialDialogueRoot = "/TalkDialog";
        }

        internal void ShowTutorialFurnitureMask(int furnitureType, TutorialMaskSetting setting)
        {
            if (host.Scene == null || !host.Scene.TryGetFurnitureClickBounds(furnitureType, out Rect bounds))
                throw new InvalidOperationException("The official tutorial furniture hit area is unavailable: " + furnitureType);
            Vector2 offset = new Vector2(setting?.Offset?.x ?? 0f, setting?.Offset?.y ?? 0f);
            Vector2 scale = new Vector2(setting?.Scale?.x ?? 1f, setting?.Scale?.y ?? 1f);
            bounds.position += offset;
            bounds.size = Vector2.Scale(bounds.size, scale);
            ShowTutorialWorldMask(bounds);
        }

        internal void ShowTutorialButtonMask(Button button, TutorialMaskSetting setting = null)
        {
            ShowTutorialTargetMask(button == null ? null : button.transform as RectTransform, setting);
        }

        internal void ShowTutorialTargetMask(RectTransform target, TutorialMaskSetting setting = null)
        {
            if (target == null) throw new InvalidOperationException("The official tutorial button target is unavailable.");
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(eventCamera, corner);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }
            Rect bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            if (setting != null)
            {
                Vector2 offset = new Vector2(setting.Offset?.x ?? 0f, setting.Offset?.y ?? 0f)
                    * (canvas == null ? 1f : canvas.scaleFactor);
                Vector2 scale = new Vector2(setting.Scale?.x ?? 1f, setting.Scale?.y ?? 1f);
                bounds.position += offset;
                bounds.size = Vector2.Scale(bounds.size, scale);
            }
            ShowTutorialScreenMask(bounds);
        }

        internal void ShowTutorialFullMask()
        {
            EnsureTutorialMask();
            tutorialMaskObject.transform.SetAsLastSibling();
            tutorialMask.ShowFull();
        }

        internal void HideTutorialMask()
        {
            if (tutorialMask != null) tutorialMask.Hide();
        }

        public void SetTutorialMainUiVisible(bool visible)
        {
            if (canvas == null) return;
            if (!visible)
            {
                if (hiddenTutorialUi.Count > 0) return;
                foreach (Transform child in canvas.transform)
                {
                    GameObject target = child.gameObject;
                    if (target == tutorialDialogueLayer || target == tutorialMaskObject) continue;
                    hiddenTutorialUi[target] = target.activeSelf;
                    target.SetActive(false);
                }
                return;
            }

            foreach (KeyValuePair<GameObject, bool> entry in hiddenTutorialUi.ToArray())
                if (entry.Key != null) entry.Key.SetActive(entry.Value);
            hiddenTutorialUi.Clear();
        }

        public Button FindTutorialButton(string storyUiKey)
        {
            if (string.IsNullOrWhiteSpace(storyUiKey)) return null;
            HomeUiRenderer renderer = activeDialogRenderer;
            string buttonName = storyUiKey.Substring(storyUiKey.LastIndexOf('.') + 1);
            if (storyUiKey == "FurnitureCartDialog.CommonBtn") buttonName = "CommonBtn_12";
            else if (storyUiKey == "FurnitureBedDialog.CollectBtn") buttonName = "CollectBtn";
            else if (storyUiKey == "FurnitureBedDialog.FastHangUpBtn") buttonName = "SpeedBtn";
            else if (storyUiKey == "FurnitureRepairDialog.RepaireBtn") buttonName = "RepaireBtn";
            if (renderer == null) return null;
            foreach (GameObject node in renderer.AllNodes)
            {
                if (node == null || node.name != buttonName || !node.activeInHierarchy) continue;
                Button button = node.GetComponent<Button>();
                if (button != null) return button;
            }
            return null;
        }

        public Button FindTutorialUnlockButton()
        {
            if (activeDialogRenderer == null || activePanelKey != "cart") return null;
            foreach (GameObject node in activeDialogRenderer.AllNodes)
            {
                if (node == null || node.name != "UnlockBtn" || !node.activeInHierarchy) continue;
                Button button = node.GetComponent<Button>();
                if (button != null && button.interactable) return button;
            }
            return null;
        }

        public bool IsTutorialStoryUiActive(string storyUiKey)
        {
            if (storyUiKey == "PublicBottomBar.HomeTabItem") return selectedTab == 2 && bottomRenderer != null;
            if (storyUiKey == "FurnitureCartDialog") return activePanelKey == "cart";
            if (storyUiKey == "FurnitureBedDialog") return activePanelKey == "bed";
            if (storyUiKey == "FurnitureRepairDialog") return activePanelKey == "repair";
            if (storyUiKey == "CookingPanel") return host.CookingRuntime != null;
            if (storyUiKey == "BagualuPanel" || storyUiKey == "PlayerBagPanel") return false;
            return false;
        }

        internal void ShowTutorialStoryUiMask(string storyUiKey, TutorialMaskSetting setting)
        {
            if (storyUiKey == "Home.EquipHeap")
            {
                ShowTutorialFurnitureMask(10, setting);
                return;
            }
            if (storyUiKey == "Home.Bagualu")
            {
                ShowTutorialFurnitureMask(3, setting);
                return;
            }
            if (storyUiKey == "PublicBottomBar.HomeTabItem")
            {
                Toggle tab = bottomRenderer?.Find<Toggle>("/PublicBottomBar/Content/Group/Home/UnlockObj");
                if (tab == null) throw new InvalidOperationException("The official Home tab target is unavailable.");
                ShowTutorialTargetMask(tab.transform as RectTransform, setting);
                return;
            }
            if (storyUiKey == "FurnitureCartDialog.CommonBtn")
            {
                Button button = FindTutorialButton(storyUiKey);
                ShowTutorialButtonMask(button, setting);
                return;
            }
            throw new InvalidOperationException("The official tutorial UI key is not present in the local Home HUD: "
                + storyUiKey);
        }

        private void ContinueOfficialTutorialDialogue()
        {
            if (tutorialDialogueRenderer == null || tutorialDialogueLines == null) return;
            tutorialDialogueIndex++;
            if (tutorialDialogueIndex < tutorialDialogueLines.Count)
            {
                SetOfficialTutorialDialogueLine();
                return;
            }
            if (tutorialDialogueOptions != null)
            {
                ShowOfficialTutorialOptions();
                return;
            }
            Action completed = tutorialDialogueCompleted;
            ClearOfficialTutorialDialogue();
            completed?.Invoke();
        }

        private void ShowOfficialTutorialOptions()
        {
            if (tutorialDialogueRenderer == null || tutorialDialogueOptions == null
                || tutorialOptionSelected == null)
                throw new InvalidOperationException("The official tutorial option presentation was lost.");
            Transform content = tutorialDialogueRenderer.Find(tutorialDialogueRoot + "/OptionSlots/Content")?.transform;
            if (content == null) throw new InvalidOperationException("The official option slot list is missing.");
            var buttons = new List<Button>();
            foreach (Transform child in content)
            {
                Button button = child.GetComponent<Button>();
                if (button != null) buttons.Add(button);
            }
            if (tutorialDialogueOptions.Count > buttons.Count)
                throw new InvalidOperationException("The official option panel has too few choice slots.");

            tutorialDialogueRenderer.SetActive(tutorialDialogueRoot + "/NextBtn", false);
            tutorialDialogueRenderer.SetActive(tutorialDialogueRoot + "/DarkMask", true);
            tutorialDialogueRenderer.SetActive(tutorialDialogueRoot + "/ClickMask", true);
            tutorialDialogueRenderer.SetActive(tutorialDialogueRoot + "/OptionSlots", true);
            Sprite optionIcon = null;
            for (int index = 0; index < tutorialDialogueOptions.Count; index++)
            {
                HomeTutorialStoryOption option = tutorialDialogueOptions[index];
                Button button = buttons[index];
                Transform optionTextRoot = button.transform.Find("Content/OptionText");
                TMP_Text text = optionTextRoot == null ? null : optionTextRoot.Find("Text")?.GetComponent<TMP_Text>();
                TMP_Text tip = optionTextRoot == null ? null : optionTextRoot.Find("Tip")?.GetComponent<TMP_Text>();
                Image icon = button.transform.Find("Content/TypeImg")?.GetComponent<Image>();
                if (text == null || icon == null)
                    throw new InvalidOperationException("The official option row is missing its text or icon binding.");
                text.text = option.text;
                if (tip != null) tip.gameObject.SetActive(false);
                button.transform.Find("Content/OptionRemainTimeText")?.gameObject.SetActive(false);
                button.transform.Find("Content/DotGroup")?.gameObject.SetActive(false);
                optionIcon ??= LoadOfficialTutorialOptionIcon(option.icon);
                if (optionIcon == null)
                    throw new InvalidOperationException("The official tutorial option icon is unavailable: " + option.icon);
                icon.sprite = optionIcon;
                button.onClick.RemoveAllListeners();
                int selectedIndex = index;
                button.onClick.AddListener(() =>
                {
                    if (tutorialOptionSelected == null || !tutorialOptionSelected(selectedIndex)) return;
                    ClearOfficialTutorialDialogue();
                });
            }
            for (int index = tutorialDialogueOptions.Count; index < buttons.Count; index++)
                buttons[index].gameObject.SetActive(false);
        }

        private static Sprite LoadOfficialTutorialOptionIcon(string iconPath)
        {
            const string sourcePrefix = "UI/Dialogue/";
            if (string.IsNullOrEmpty(iconPath) || !iconPath.StartsWith(sourcePrefix, StringComparison.Ordinal))
                return null;
            return Resources.Load<Sprite>("Map10/Dialogue/" + iconPath.Substring(sourcePrefix.Length));
        }

        private void SetOfficialTutorialDialogueLine()
        {
            HomeTutorialDialogue line = tutorialDialogueLines[tutorialDialogueIndex];
            tutorialDialogueRenderer.SetText(FruitTreeTutorialTextPath.Replace("/TalkDialog", tutorialDialogueRoot),
                line.text);
            TMP_Text name = tutorialDialogueRenderer.Find<TMP_Text>(
                FruitTreeTutorialNamePath.Replace("/TalkDialog", tutorialDialogueRoot));
            if (name != null) name.text = string.Empty;
        }

        private void ShowTutorialWorldMask(Rect worldBounds)
        {
            Camera camera = host.Scene.Camera;
            Vector3[] corners =
            {
                new Vector3(worldBounds.xMin, worldBounds.yMin, 0f),
                new Vector3(worldBounds.xMin, worldBounds.yMax, 0f),
                new Vector3(worldBounds.xMax, worldBounds.yMax, 0f),
                new Vector3(worldBounds.xMax, worldBounds.yMin, 0f)
            };
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector3 screen = camera.WorldToScreenPoint(corner);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }
            ShowTutorialScreenMask(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
        }

        private void ShowTutorialScreenMask(Rect screenBounds)
        {
            if (canvas == null) throw new InvalidOperationException("The official Home canvas is unavailable.");
            EnsureTutorialMask();
            tutorialMaskObject.transform.SetAsLastSibling();
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            tutorialMask.Show(screenBounds, eventCamera);
        }

        private void EnsureTutorialMask()
        {
            if (tutorialMask != null) return;
            tutorialMaskObject = new GameObject("HomeTutorialMask", typeof(RectTransform),
                typeof(HomeFruitTreeTutorialMaskGraphic));
            tutorialMaskObject.transform.SetParent(canvas.transform, false);
            RectTransform rect = (RectTransform)tutorialMaskObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            tutorialMask = tutorialMaskObject.GetComponent<HomeFruitTreeTutorialMaskGraphic>();
        }
    }
}
