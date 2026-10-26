using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Startup;
using Zjcs.Offline.State;
using Zjcs.Official.Startup;

namespace Zjcs.Presentation.CharacterCreation
{
    public sealed class OfficialSaveSelectionView : MonoBehaviour
    {
        private OfficialSaveSelectionAssets assets;
        private GameObject panel, mask;
        private RenderTexture background;
        public bool IsReady { get; private set; }
        public PlanesListPanel Panel { get; private set; }

        public void Initialize(CharacterCreationOfflineService service, Action create,
            Func<string, Action> open, Action<string> recoverDraft, string message)
        {
            assets = Resources.Load<OfficialSaveSelectionAssets>(OfficialSaveSelectionAssets.ResourcePath);
            if (assets == null || assets.panel == null || assets.roleItem == null || assets.blurMask == null)
                throw new InvalidOperationException("原版存档选择界面资源未绑定。");
            var startup = Resources.Load<OfficialStartupAssets>(OfficialStartupAssets.ResourcePath);
            GameObject theme = Instantiate(startup.themeCanvas, transform, false);
            theme.transform.localScale = Vector3.one;
            Stretch(theme.GetComponent<RectTransform>());
            theme.GetComponent<Canvas>().overrideSorting = false;
            panel = Instantiate(assets.panel, transform, false);
            panel.name = "OfficialSaveSelectionPanel";
            Stretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<Canvas>().overrideSorting = false;
            Panel = panel.AddComponent<PlanesListPanel>();
            string root = SaveSelectionProbe.ProfileRoot;
            string last = ProfileSelectionPreference.Read(root);
            string[] slots = service.GetSlotIds().OrderByDescending(id => id == last).ToArray();
            Panel.Init(slots.Length > 0, create);
            Panel.RoleItemSlot.GetComponentInParent<ScrollRect>(true).scrollSensitivity = 50f;
            var catalog = new ProfileSlotCatalog(root);
            foreach (string id in slots)
                AddProfile(service, catalog, id, last, open, recoverDraft);
            if (!string.IsNullOrEmpty(message))
            {
                Panel.OnTabItemChange(true, true);
                Panel.MessageText.text = message;
                Panel.CreateButton.GetComponentInChildren<TMP_Text>(true).text = "返回";
                Panel.CreateButton.onClick.RemoveAllListeners();
                Panel.CreateButton.onClick.AddListener(() =>
                {
                    Panel.CreateButton.GetComponentInChildren<TMP_Text>(true).text = "新建存档";
                    Panel.CreateButton.onClick.RemoveAllListeners();
                    Panel.CreateButton.onClick.AddListener(() => create());
                    Panel.OnTabItemChange(false);
                });
            }
            panel.SetActive(false);
            StartCoroutine(Show());
        }

        private void AddProfile(CharacterCreationOfflineService service, ProfileSlotCatalog slots, string id,
            string last, Func<string, Action> open, Action<string> recoverDraft)
        {
            GameObject row = Instantiate(assets.roleItem, Panel.RoleItemSlot, false);
            row.name = "SaveSlot_" + id;
            var view = row.AddComponent<PlanesInfoItemWithRoleInfo>();
            string character = "新角色";
            string level = "继续创建";
            string rating = null;
            bool readable = true;
            Action select = open(id);
            OfflineCharacterProgressionService progression = null;
            try
            {
                OfflineMainlineProgressSession session = OfflineMainlineProgressSession.InspectCharacter(slots, id);
                if (session.Save.Player.CharacterCreated)
                {
                    progression = new OfflineCharacterProgressionService(session, OfficialCharacterAssetCatalog.LoadDefault().Load());
                    character = progression.Player.CharacterName;
                    level = progression.Player.Level.ToString(CultureInfo.InvariantCulture) + "级";
                    rating = progression.EvaluateProps(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).CombatRating.ToString(CultureInfo.InvariantCulture);
                }
                else if (service.GetDraftStatus(id) == SaveReadStatus.Corrupt)
                {
                    character = "草稿损坏";
                    level = "保留副本并重新开始";
                    readable = false;
                    select = () => recoverDraft(id);
                }
            }
            catch (Exception error)
            {
                character = "存档无法读取";
                level = "点击查看";
                readable = false;
                Debug.LogWarning("Save preview " + id + ": " + error.Message);
            }
            view.Init("存档 " + id.Substring("slot-".Length), character, level, rating,
                readable, id == last || last == null && id == slots.GetSlotIds().FirstOrDefault(), id == last, select);
            // PlayerInfoUI shows its native head portrait rather than the unused full model.
            Transform icon = row.transform.Find("PublicPlayerIcon");
            icon.Find("ProfessionGo").gameObject.SetActive(false);
            icon.Find("IconGo/ImgMask/IconImg").gameObject.SetActive(false);
            icon.Find("IconGo/ImgMask/PlayerHeadGo/MyPlayerModel_UI").gameObject.SetActive(false);
            RawImage head = icon.Find("IconGo/ImgMask/PlayerHeadGo/HeadImg").GetComponent<RawImage>();
            head.gameObject.SetActive(true);
            head.texture = assets.defaultPortrait.texture;
            if (progression != null) CaptureHead(row, id, progression, head);
        }

        private void CaptureHead(GameObject row, string id, OfflineCharacterProgressionService progression, RawImage image)
        {
            CharacterPortraitPhotography settings = JsonUtility.FromJson<CharacterPortraitPhotography>(assets.photography.text);
            var appearance = progression.ResolveAppearance();
            foreach (int type in settings.excludes) appearance.SkinByType.Remove(type);
            GameObject model = CharacterCreationNativeModelFactory.CreatePreviewModel(id, SaveSelectionProbe.ProfileRoot, appearance);
            try
            {
                model.transform.position = new Vector3(-1024, -1024, -1024);
                string state = "Base Layer." + settings.animation;
                Texture2D texture = CharacterNativePortraitCapture.Capture(model, settings, state);
                SaveSelectionProbe.RecordPortrait(id, texture);
                row.AddComponent<CharacterNativePortraitLease>().Initialize(texture);
                image.texture = texture;
            }
            finally { Destroy(model); }
        }

        private IEnumerator Show()
        {
            // WindowBlurMask.Prepare captures the actual theme before displaying the dialog.
            yield return null;
            yield return new WaitForEndOfFrame();
            var capture = RenderTexture.GetTemporary(Screen.width, Screen.height, 0);
            background = RenderTexture.GetTemporary(Mathf.Max(1, Screen.width / 13), Mathf.Max(1, Screen.height / 13), 0);
            try
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(capture);
                Blur(capture, background);
            }
            finally { RenderTexture.ReleaseTemporary(capture); }
            mask = Instantiate(assets.blurMask, transform, false);
            mask.name = "OfficialSaveBlurMask";
            Canvas maskCanvas = mask.GetComponent<Canvas>();
            maskCanvas.enabled = true;
            maskCanvas.overrideSorting = false;
            mask.SetActive(true);
            RawImage image = mask.GetComponent<RawImage>();
            image.texture = background;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.transform.SetAsLastSibling();
            panel.SetActive(true);
            panel.GetComponent<Animation>().Play("UIDialog_Show");
            yield return new WaitForSecondsRealtime(.4f);
            IsReady = true;
        }

        // Original WindowBlurMask's four-level separable ScreenBlur passes.
        private static void Blur(RenderTexture capture, RenderTexture target)
        {
            Material material = Resources.Load<Material>("World/Mainline/ScreenBlur");
            if (material == null) throw new InvalidOperationException("Original ScreenBlur material is not bound.");
            var up = new RenderTexture[4];
            var down = new RenderTexture[4];
            RenderTexture source = capture;
            void Blit(RenderTexture from, RenderTexture to, Vector2 direction)
            {
                material.SetVector("_BlurDir", direction);
                material.SetVector("_GlobalMipBias", Vector2.zero);
                material.SetTexture("_BlitTexture", from);
                material.SetVector("_BlitTexture_TexelSize", new Vector4(1f / from.width, 1f / from.height, from.width, from.height));
                Graphics.Blit(from, to, material, 0);
            }
            try
            {
                int width = capture.width, height = capture.height;
                for (int i = 0; i < 4; i++)
                {
                    width = Mathf.Max(1, width >> 1); height = Mathf.Max(1, height >> 1);
                    up[i] = RenderTexture.GetTemporary(width, height, 0, capture.format);
                    down[i] = RenderTexture.GetTemporary(width, height, 0, capture.format);
                    up[i].filterMode = down[i].filterMode = FilterMode.Bilinear;
                    up[i].wrapMode = down[i].wrapMode = TextureWrapMode.Repeat;
                    Blit(source, up[i], Vector2.right);
                    Blit(up[i], down[i], Vector2.up);
                    source = down[i];
                }
                for (int i = 2; i >= 0; i--)
                {
                    Blit(source, down[i], Vector2.right);
                    Blit(down[i], i == 0 ? target : up[i], Vector2.up);
                    source = up[i];
                }
            }
            finally
            {
                foreach (var value in up) if (value != null) RenderTexture.ReleaseTemporary(value);
                foreach (var value in down) if (value != null) RenderTexture.ReleaseTemporary(value);
            }
        }

        private void OnDestroy()
        {
            if (background != null) RenderTexture.ReleaseTemporary(background);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
