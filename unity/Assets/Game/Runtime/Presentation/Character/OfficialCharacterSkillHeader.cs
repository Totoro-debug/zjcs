using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Skills;
using Zjcs.Presentation.CharacterCreation;

namespace Zjcs.Presentation.Character
{
    /// <summary>Mounts the original shared PublicPlayerInfo and PublicTopBar used by the skill page.</summary>
    internal sealed class OfficialCharacterSkillHeader : MonoBehaviour
    {
        private const string PlayerPath = "Content/Content/WorldPanel/Anim/Left/PublicPlayerInfo";
        private OfflineCharacterProgressionService progression;
        private Transform player;
        private Transform currency;
        private Texture2D portrait;
        private int currencyId;
        private Dictionary<string, object> header;

        public void Initialize(OfficialCharacterAssetCatalog assets, OfflineCharacterProgressionService service,
            string slotId, string savesRoot, Transform currencySlot)
        {
            progression = service;
            var skillCatalog = OfficialSkillAssetCatalog.LoadDefault();
            var skillLayout = CharacterModelPackage.AsObject(Offline.Equipment.OfficialEquipmentJson.Parse(
                skillCatalog.UiLayouts.text, allowNonFinite: true), "skill layouts");
            header = CharacterModelPackage.AsObject(skillLayout["shared_header"], "shared skill header");
            var staging = new GameObject("OfficialSkillHeaderStaging", typeof(RectTransform));
            staging.transform.SetParent(transform, false);
            staging.SetActive(false);
            GameObject source = Resources.Load<GameObject>("Map11/UI/WorldCanvas");
            if (source == null) throw new InvalidOperationException("The original shared player HUD prefab is missing.");
            GameObject hud = Instantiate(source, staging.transform, false);
            OfficialCharacterPanelView.StripUnavailableOfficialBehaviours(hud.transform);
            foreach (Animator animator in hud.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            player = hud.transform.Find(PlayerPath);
            if (player == null) throw new InvalidOperationException("The original PublicPlayerInfo HUD is missing.");
            KeepBranch(hud.transform, player);
            foreach (Canvas canvas in hud.GetComponentsInChildren<Canvas>(true))
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                canvas.overrideSorting = false;
            }
            hud.transform.SetParent(transform, false);
            hud.transform.localScale = Vector3.one;
            Stretch((RectTransform)hud.transform);
            Stretch((RectTransform)hud.transform.Find("Content/Content"));
            hud.SetActive(true);
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Combat/FZY4K-Dynamic");
            foreach (TMP_Text text in hud.GetComponentsInChildren<TMP_Text>(true))
            {
                if (font != null) text.font = font;
                text.raycastTarget = false;
            }
            Transform buff = player.Find("BuffGo");
            if (buff != null) buff.gameObject.SetActive(false);
            RestorePlayerGraphics(skillCatalog);
            CapturePortrait(slotId, savesRoot);

            GameObject bar = Instantiate(Resources.Load<GameObject>("Map11/UI/PublicTopBar"), staging.transform, false);
            OfficialCharacterPanelView.StripUnavailableOfficialBehaviours(bar.transform);
            bar.transform.SetParent(currencySlot, false);
            bar.SetActive(true);
            Transform resourceList = bar.GetComponentsInChildren<Transform>(true).Single(value => value.name == "ResourceList");
            GameObject money = Instantiate(Resources.Load<GameObject>("Map11/UI/MoneyUI"), staging.transform, false);
            OfficialCharacterPanelView.StripUnavailableOfficialBehaviours(money.transform);
            currency = money.transform;
            currency.SetParent(resourceList, false);
            currency.localPosition = Vector3.zero;
            currency.localScale = Vector3.one;
            // TabItemTopBarSwitcher.ShowTypeList = [18], PlayerItemUIGetItemIdFactory.SkillSeniorMaterial.
            var settings = service.Catalog.Rows("game_settings").First();
            currencyId = Convert.ToInt32(settings["SkillSeniorMaterialItemId"], CultureInfo.InvariantCulture);
            Image icon = currency.GetComponentsInChildren<Image>(true).First(value => value.name == "IconImg");

            if (currencyId != Convert.ToInt32(header["currency_id"], CultureInfo.InvariantCulture))
                throw new InvalidOperationException("The original skill top bar currency binding changed.");
            string iconUri = Convert.ToString(header["currency_icon"], CultureInfo.InvariantCulture);
            icon.sprite = assets.FindSprite(iconUri) ?? OfficialSkillAssetCatalog.LoadDefault().FindSprite(iconUri);
            if (icon.sprite == null) throw new InvalidOperationException("The original skill senior material icon is missing.");
            Transform add = currency.Find("AddBtn");
            if (add != null) add.gameObject.SetActive(false);
            foreach (TMP_Text text in currency.GetComponentsInChildren<TMP_Text>(true))
            {
                if (font != null) text.font = font;
                text.raycastTarget = false;
            }
            currency.gameObject.SetActive(true);
            Destroy(staging);
            Refresh();
        }

        public void Refresh()
        {
            if (progression == null || player == null) return;
            SetText(player, "NameText", progression.Player.CharacterName);
            Slider experience = player.Find("PublicPlayerIconHome/IconGo/ExpSliderBgImg").GetComponent<Slider>();
            if (experience != null) experience.normalizedValue = progression.Experience().Fill;
            SetText(player, "LevelBgImg/LevelTxt", progression.Player.Level.ToString(CultureInfo.InvariantCulture) + "级");
            SetText(player, "CombatRating/CRText", OfficialPropFormat.Compact(
                progression.EvaluateProps(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).CombatRating));
            long count = progression.Player.Inventory.Where(value => value.ItemId == currencyId).Sum(value => value.Count);
            foreach (TMP_Text text in currency.GetComponentsInChildren<TMP_Text>(true))
                if (text.name == "CountTxt" || text.name == "CountText") text.text = OfficialPropFormat.Compact(count);
        }

        private void RestorePlayerGraphics(OfficialSkillAssetCatalog catalog)
        {
            string sourceRoot = Convert.ToString(header["player_path"], CultureInfo.InvariantCulture);
            foreach (object raw in (List<object>)header["player_nodes"])
            {
                var node = CharacterModelPackage.AsObject(raw, "player HUD node");
                string path = Convert.ToString(node["path"], CultureInfo.InvariantCulture);
                Transform target = player.Find(path.Substring(sourceRoot.Length + 1));
                if (target == null) throw new InvalidOperationException("Original player HUD node is missing: " + path);
                Image image = target.GetComponent<Image>();
                string uri = Convert.ToString(node["image_sprite_file"], CultureInfo.InvariantCulture);
                if (image != null && uri.StartsWith("res://assets/official/", StringComparison.Ordinal))
                {
                    image.sprite = catalog.FindSprite(uri);
                    if (image.sprite == null) throw new InvalidOperationException("Original player HUD sprite is missing: " + uri);
                }
            }
            var components = CharacterModelPackage.AsObject(header["components"], "player HUD components");
            foreach (var entry in components)
            {
                Transform target = player.Find(entry.Key.Substring(sourceRoot.Length + 1));
                var bindings = CharacterModelPackage.AsObject(entry.Value, "HUD component bindings");
                if (bindings.TryGetValue("AspectRatioFitter", out object rawAspect))
                {
                    var aspect = CharacterModelPackage.AsObject(rawAspect, "original HUD aspect ratio");
                    var fitter = target.GetComponent<AspectRatioFitter>() ?? target.gameObject.AddComponent<AspectRatioFitter>();
                    fitter.aspectMode = (AspectRatioFitter.AspectMode)Convert.ToInt32(aspect["m_AspectMode"], CultureInfo.InvariantCulture);
                    fitter.aspectRatio = Convert.ToSingle(aspect["m_AspectRatio"], CultureInfo.InvariantCulture);
                }
                if (bindings.TryGetValue("RawImage", out object rawImage))
                {
                    var image = CharacterModelPackage.AsObject(rawImage, "original portrait UV");
                    var uv = (List<object>)image["m_UVRect"];
                    target.GetComponent<RawImage>().uvRect = new Rect(Convert.ToSingle(uv[0]), Convert.ToSingle(uv[1]),
                        Convert.ToSingle(uv[2]), Convert.ToSingle(uv[3]));
                }
            }
        }

        private void CapturePortrait(string slotId, string savesRoot)
        {
            var settings = CharacterModelPackage.AsObject(header["head_photography"], "head photography");
            var offset = CharacterModelPackage.AsObject(settings["mOffset"], "head offset");
            var appearance = progression.ResolveAppearance();
            foreach (object type in (List<object>)settings["mExcludeSkinTypes"])
                appearance.SkinByType.Remove(Convert.ToInt32(type, CultureInfo.InvariantCulture));
            GameObject model = CharacterCreationNativeModelFactory.CreatePreviewModel(slotId, savesRoot, appearance);
            try
            {
                var photography = new CharacterPortraitPhotography
                {
                    width = Convert.ToInt32(settings["TextureWidth"], CultureInfo.InvariantCulture),
                    height = Convert.ToInt32(settings["TextureHeight"], CultureInfo.InvariantCulture),
                    depth = Convert.ToInt32(settings["mDepthBuffer"], CultureInfo.InvariantCulture),
                    size = Convert.ToSingle(settings["Size"], CultureInfo.InvariantCulture), near = .3f, far = 1000f,
                    offset = new[] { Convert.ToSingle(offset["x"], CultureInfo.InvariantCulture),
                        Convert.ToSingle(offset["y"], CultureInfo.InvariantCulture), Convert.ToSingle(offset["z"], CultureInfo.InvariantCulture) }
                };
                portrait = CharacterNativePortraitCapture.Capture(model, photography, "home/idle_01");
                RawImage image = player.Find("PublicPlayerIconHome/IconGo/ImgMask/HeadImg").GetComponent<RawImage>();
                image.texture = portrait;
                image.SetNativeSize();
            }
            finally { Destroy(model); }
        }

        private static void KeepBranch(Transform root, Transform target)
        {
            if (root == target) { root.gameObject.SetActive(true); return; }
            for (int index = root.childCount - 1; index >= 0; index--)
            {
                Transform child = root.GetChild(index);
                if (child == target || target.IsChildOf(child)) KeepBranch(child, target);
                else DestroyImmediate(child.gameObject);
            }
            root.gameObject.SetActive(true);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }

        private static void SetText(Transform root, string path, string value)
        {
            TMP_Text text = root.Find(path)?.GetComponent<TMP_Text>();
            if (text != null) text.text = value;
        }

        private void OnDestroy()
        {
            if (portrait != null) Destroy(portrait);
        }
    }
}
