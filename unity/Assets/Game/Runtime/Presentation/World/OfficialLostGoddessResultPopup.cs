using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    /// <summary>
    /// Binds the original lost-goddess result prefabs to the already persisted
    /// offline award. The server response classes are not available offline,
    /// so this adapter only fills fields represented by the local transaction.
    /// </summary>
    public sealed class OfficialLostGoddessResultPopup : MonoBehaviour
    {
        private GameObject canvasObject;
        private WorldPopupWindow popup;
        private Action closed;
        public bool IsShown { get; private set; }

        public static OfficialLostGoddessResultPopup Open(Transform parent,
            OfflineExploreLostGoddessAward award, OfflineCharacterProgressionService progression,
            Func<string, string> text, Action onClosed)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (award == null || progression == null) throw new ArgumentNullException(nameof(award));
            if (text == null) throw new ArgumentNullException(nameof(text));
            string key = award.Type == 2 ? "LostGoddessResultSkillRankDialog"
                : award.Type == 3 ? "LostGoddessResultCharacterLevelDialog" : "LostGoddessResultPlayerPropDialog";
            GameObject prefab = Resources.Load<GameObject>("Map11/UI/" + key);
            if (prefab == null) throw new InvalidOperationException("Official lost goddess result prefab is missing: " + key);

            GameObject canvas = new GameObject("Map11LostGoddessResultCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(parent, false);
            Canvas overlay = canvas.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.overrideSorting = true;
            overlay.sortingOrder = 122;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;

            GameObject root = Instantiate(prefab, canvas.transform, false);
            root.name = key;
            var view = canvas.AddComponent<OfficialLostGoddessResultPopup>();
            view.canvasObject = canvas;
            view.closed = onClosed;
            view.Bind(root, award, progression, text);
            view.popup = root.GetComponent<WorldPopupWindow>();
            if (view.popup == null) throw new InvalidOperationException("Official lost goddess result popup binding is missing.");
            view.popup.CloseByClickMask = false;
            view.popup.SetCanvasSortingOrder(overlay.sortingOrder + 1);
            view.popup.OnShow += _ => view.IsShown = true;
            view.popup.OnHide += _ => view.DestroyPopup();
            view.BindClose(root);
            view.popup.Show();
            return view;
        }

        private void Bind(GameObject root, OfflineExploreLostGoddessAward award,
            OfflineCharacterProgressionService progression, Func<string, string> text)
        {
            Transform transformRoot = root.transform;
            if (award.Type == 2)
            {
                SetText(transformRoot, "Content/MiddleGo/Props/ResultTitle", text("lostGoddess_SkillRank_title"));
                SetText(transformRoot, "Content/MiddleGo/Props/AniSkill/UpgradeResult/UpgradeRank/UpgradeText",
                    text("lostGoddess_SkillRank_title"));
                SetText(transformRoot, "Content/MiddleGo/Props/AniSkill/UpgradeResult/UpgradeRank/SkillStarInfo/StarNum/Star/Count",
                    award.TargetRank.ToString(CultureInfo.InvariantCulture));
                SetText(transformRoot, "Content/MiddleGo/Props/AniSkill/UpgradeResult/SkillSlot/SkillName",
                    SkillName(progression, award.SkillId));
                Sprite icon = null;
                try { icon = OfficialCharacterAssetCatalog.LoadDefault().FindSkillSkinSprite(progression.Catalog, award.SkillId); }
                catch (Exception) { }
                SetFirstImage(transformRoot, "Content/MiddleGo/Props/AniSkill/UpgradeResult/SkillSlot", icon);
                SetActive(transformRoot, "Content/MiddleGo/Props/AniSkill/UpgradeResultOld", false);
                SetActive(transformRoot, "Content/MiddleGo/Props/AniSkill/UpgradeResult", true);
            }
            else if (award.Type == 3)
            {
                SetText(transformRoot, "Content/MiddleGo/Props/ResultTitle",
                    string.Format(CultureInfo.CurrentCulture, text("lostGoddess_CharacterLevel_title"), award.TargetLevel - award.SourceLevel));
                SetText(transformRoot, "Content/MiddleGo/Props/UpgradeResult/Content/FormLevelTitle",
                    award.SourceLevel.ToString(CultureInfo.InvariantCulture));
                SetText(transformRoot, "Content/MiddleGo/Props/UpgradeResult/Content/ToLevelTitle",
                    award.TargetLevel.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                SetText(transformRoot, "Content/MiddleGo/Props/ResultTitle",
                    string.Format(CultureInfo.CurrentCulture, text("lostGoddess_PlayerProp_title"), award.Properties.Length));
                Transform slot = Find(transformRoot, "Content/MiddleGo/Props/UpgradeResult/LoopVerticalScrollRect/ViewPort/Content");
                GameObject itemPrefab = Resources.Load<GameObject>("Map11/UI/LostGoddessResultPlayerPropDialogItem");
                if (slot == null || itemPrefab == null) throw new InvalidOperationException("Official lost goddess property result binding is missing.");
                foreach (OfflineExtraProperty property in award.Properties)
                {
                    GameObject item = Instantiate(itemPrefab, slot, false);
                    OfficialPropDefinition definition = progression.Catalog.Props.Get(property.PropType);
                    long value = progression.Catalog.GetLevelProps(property.PropId, property.Level)
                        .TryGetValue(property.PropType, out long raw) ? raw : 0;
                    SetText(item.transform, "Content/PropTypeText", definition?.Name ?? ("属性 " + property.PropType));
                    SetText(item.transform, "Content/FormPropText", "0");
                    SetText(item.transform, "Content/ToPropText", OfficialPropFormat.Signed(definition, value));
                    item.SetActive(true);
                }
            }
            SetActive(transformRoot, "Content/MiddleGo/EnterTip", true);
        }

        private void BindClose(GameObject root)
        {
            string[] paths = { "Content/MiddleGo/CloseObjSkillRank", "Content/MiddleGo/CloseObjCharacterLevel",
                "Content/MiddleGo/CloseObjPlayerProp" };
            foreach (string path in paths)
            {
                Button button = Find(root.transform, path)?.GetComponent<Button>();
                if (button == null) continue;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(Close);
            }
        }

        public void Close()
        {
            if (popup != null) popup.Hide(null);
            else DestroyPopup();
        }

        private void DestroyPopup()
        {
            closed?.Invoke();
            if (canvasObject != null) Destroy(canvasObject);
        }

        private static string SkillName(OfflineCharacterProgressionService progression, int skillId)
        {
            Dictionary<string, object> item = progression.Catalog.FindItem(skillId);
            return item != null && item.TryGetValue("Name", out object value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture) : skillId.ToString(CultureInfo.InvariantCulture);
        }

        private static Transform Find(Transform root, string path) => root.Find(path);

        private static void SetText(Transform root, string path, string value)
        {
            Transform node = Find(root, path);
            if (node == null) return;
            TMP_Text text = node.GetComponent<TMP_Text>();
            if (text != null) text.text = value ?? string.Empty;
        }

        private static void SetActive(Transform root, string path, bool active)
        {
            Transform node = Find(root, path);
            if (node != null) node.gameObject.SetActive(active);
        }

        private static void SetFirstImage(Transform root, string path, Sprite sprite)
        {
            if (sprite == null) return;
            Image image = Find(root, path)?.GetComponentsInChildren<Image>(true).FirstOrDefault();
            if (image != null) image.sprite = sprite;
        }
    }
}
