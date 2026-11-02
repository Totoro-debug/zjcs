using System;
using UnityEngine;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.World.MainlineMiniMap
{
    public sealed class MainlineMiniMapHudBindings
    {
        public MiniMapHudContext State;
        public IMultiLanguageUIService LanguageUI;
        public ILanguageTypeService Language;
        public ILocalizationTextService Text;
        public IMultiLanguageFontService Fonts;
        public LanguageGroupConfig LanguageGroups;
        public Action<AudioClip, GameObject> PlaySound;
    }

    public sealed partial class MainlineMiniMap
    {
        private GameObject hud;

        // The parent is the original world-canvas slot. Its anchors and the
        // complete prefab hierarchy remain the responsibility of original data.
        public void AttachHud(GameObject prefab, Transform parent, MainlineMiniMapHudBindings bindings)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MainlineMiniMap));
            if (hud != null) throw new InvalidOperationException("This minimap already owns its HUD.");
            if (prefab == null || parent == null || bindings?.State == null || bindings.LanguageUI == null
                || bindings.Language == null || bindings.Text == null || bindings.Fonts == null
                || bindings.LanguageGroups == null || bindings.PlaySound == null
                || bindings.State.Root != root.Bindings.State)
                throw new ArgumentException("Complete original HUD bindings for this minimap are required.");
            var inactive = new GameObject("MiniMapHudBinding");
            inactive.SetActive(false);
            try
            {
                hud = Object.Instantiate(prefab, inactive.transform, false);
                hud.SetActive(false);
                if (hud.GetComponent<MiniMapMaskedUI>() == null || hud.GetComponent<AreaInfoUI>() == null
                    || hud.GetComponentInChildren<PublicMapBranchLineBtn>(true) == null)
                    throw new ArgumentException("The complete original minimap HUD prefab is required.");
                foreach (var ui in hud.GetComponentsInChildren<UIBase>(true)) ui.Services = root.Bindings.State.LabelServices;
                foreach (var ui in hud.GetComponentsInChildren<MapMeCharacterUIBase>(true)) ui.MapMyCharacterManager = root.Bindings.Characters;
                foreach (var small in hud.GetComponentsInChildren<MiniMapMaskedUI>(true))
                { small.Context = bindings.State; small.GetMiniMapTexture = () => Texture; }
                foreach (var area in hud.GetComponentsInChildren<AreaInfoUI>(true)) area.Context = bindings.State;
                foreach (var branch in hud.GetComponentsInChildren<PublicMapBranchLineBtn>(true)) branch.Context = bindings.State;
                foreach (var text in hud.GetComponentsInChildren<MultiLanguageText>(true))
                {
                    text.GetComponent<TMPro.TMP_Text>().font = bindings.Fonts.GetMultiLanguageFontAsset().FontAsset;
                    text.BindLanguageServices(bindings.LanguageUI, bindings.Language);
                    text.LocalizationTextService = bindings.Text;
                    text.MultiLanguageFontService = bindings.Fonts;
                }
                foreach (var controller in hud.GetComponentsInChildren<MultiLanguageTMPControler>(true))
                { controller.LanguageGroupConfig = bindings.LanguageGroups; controller.LanguageTypeService = bindings.Language; }
                foreach (var sound in hud.GetComponentsInChildren<WorldButtonSound>(true)) sound.Post = bindings.PlaySound;
                hud.transform.SetParent(parent, false);
                hud.SetActive(owner.activeSelf);
            }
            catch { DisposeHud(); throw; }
            finally { Object.Destroy(inactive); }
        }

        private void DisposeHud()
        {
            if (hud == null) return;
            hud.SetActive(false);
            Object.Destroy(hud);
            hud = null;
        }
    }
}
