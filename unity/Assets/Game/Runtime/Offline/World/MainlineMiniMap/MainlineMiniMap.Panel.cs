using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.Map13MiniMapWindows;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.WorldUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;
using Object = UnityEngine.Object;
using LabelUIBase = Zjcs.Official.World.NativeMap13Labels.UIBase;

namespace Zjcs.Offline.World.MainlineMiniMap
{
    public sealed class MainlineMiniMapPanelBindings
    {
        public MiniMapFullScreenContext State;
        public MiniMapDirectionContext Direction;
        public MainlineMiniMapHudBindings UI;
        public IMiniMapMarkFilterServices Filters;
        public IMiniMapFilterPersistentState FilterState;
        public IProfessionPlayerInfoBindings Player;
        public ICharacterPortraitManager Portraits;
        public IMap13PopupWindowHost Windows;
    }

    public sealed partial class MainlineMiniMap
    {
        private GameObject panelObject;
        private MiniMapFullScreenContext panelState;

        public void OpenFullScreen(GameObject prefab, Transform parent, MainlineMiniMapPanelBindings bindings)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MainlineMiniMap));
            if (panelObject != null) throw new InvalidOperationException("This minimap already owns a fullscreen window.");
            var ui = bindings?.UI;
            if (prefab == null || parent == null || bindings?.State == null || bindings.Direction == null
                || bindings.Filters == null || bindings.FilterState == null || bindings.Player == null
                || bindings.Portraits == null || bindings.Windows == null || ui?.LanguageUI == null
                || ui.Language == null || ui.Text == null || ui.Fonts == null || ui.LanguageGroups == null
                || ui.PlaySound == null || bindings.State.CurrentMiniMap != null
                || bindings.State.CurrentMapClassId != root.MapClassId
                || root.NonMaskableManager == null
                || bindings.Direction.FullScreen != bindings.State
                || bindings.Direction.Characters != root.Bindings.Characters)
                throw new ArgumentException("Complete original fullscreen bindings for this minimap are required.");
            var inactive = new GameObject("MiniMapPanelBinding");
            inactive.SetActive(false);
            try
            {
                panelObject = Object.Instantiate(prefab, inactive.transform, false);
                panelObject.SetActive(false);
                var panel = panelObject.GetComponent<MiniMapFullScreenPanel>();
                if (panel == null || panel.CameraController == null || panel.ScrollRect == null
                    || panelObject.GetComponent<PopupWindow>() == null
                    || panelObject.GetComponentInChildren<MiniMapMarkUIFilter>(true) == null)
                    throw new ArgumentException("The complete original fullscreen minimap prefab is required.");
                panelState = bindings.State;
                panelState.CurrentMiniMap = root;
                panel.Context = panelState;
                // Original WindowManager initializes the window before Awake.
                panel.Init(root.MapClassId);
                foreach (var label in panelObject.GetComponentsInChildren<LabelUIBase>(true))
                    label.Services = root.Bindings.State.LabelServices;
                foreach (var camera in panelObject.GetComponentsInChildren<MiniMapCameraController>(true))
                    camera.Settings = root.Bindings.Settings;
                foreach (var direction in panelObject.GetComponentsInChildren<MiniDirectionGuiderBase>(true))
                    direction.Context = bindings.Direction;
                foreach (var filter in panelObject.GetComponentsInChildren<MiniMapMarkUIFilter>(true))
                { filter.Services = bindings.Filters; filter.PersistentState = bindings.FilterState; }
                foreach (var text in panelObject.GetComponentsInChildren<MultiLanguageText>(true))
                {
                    text.GetComponent<TMPro.TMP_Text>().font = ui.Fonts.GetMultiLanguageFontAsset().FontAsset;
                    text.BindLanguageServices(ui.LanguageUI, ui.Language);
                    text.LocalizationTextService = ui.Text;
                    text.MultiLanguageFontService = ui.Fonts;
                }
                foreach (var controller in panelObject.GetComponentsInChildren<MultiLanguageTMPControler>(true))
                { controller.LanguageGroupConfig = ui.LanguageGroups; controller.LanguageTypeService = ui.Language; }
                foreach (var sound in panelObject.GetComponentsInChildren<WorldButtonSound>(true)) sound.Post = ui.PlaySound;
                foreach (var sound in panelObject.GetComponentsInChildren<MiniMapPanelClickSound>(true)) sound.Post = ui.PlaySound;
                foreach (var close in panelObject.GetComponentsInChildren<MiniMapPanelCloseWindowBtn>(true)) close.Context = panelState;
                foreach (var player in panelObject.GetComponentsInChildren<PlayerInfoUI>(true)) player.ConfigureInfo(bindings.Player);
                foreach (var player in panelObject.GetComponentsInChildren<PlayerIconMe>(true)) player.ConfigureInfo(bindings.Player);
                foreach (var portrait in panelObject.GetComponentsInChildren<TeamHeadCharacterPortrait>(true)) portrait.Bind(bindings.Portraits);
                foreach (var window in panelObject.GetComponentsInChildren<PopupWindow>(true)) window.WindowManager = bindings.Windows;
                panelObject.transform.SetParent(parent, false);
                panelObject.GetComponent<PopupWindow>().Show().Forget();
            }
            catch { CloseFullScreen(); throw; }
            finally { Object.Destroy(inactive); }
        }

        public void CloseFullScreen()
        {
            if (panelObject != null)
            {
                if (panelObject.activeSelf) panelObject.GetComponent<PopupWindow>()?.Hide(instant: true).Forget();
                panelObject.SetActive(false);
                Object.Destroy(panelObject);
                panelObject = null;
            }
            if (panelState != null) panelState.CurrentMiniMap = null;
            panelState = null;
        }
    }
}
