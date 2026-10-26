using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private OfficialMap11WitchWindow worldFunctions;
        public Action<string> FunctionRequested { get; set; }

        // FunctionEntrysPanel.OnEntryBtnClick / FunctionEntryDialog.UpdateView.
        private void OpenNativeWorldFunctions()
        {
            if (worldFunctions != null) return;
            worldFunctions = new OfficialMap11WitchWindow("FunctionEntryDialog", ui.OverlayRoot, miniMapUiData, MapProfile.NativeUiRoot);
            worldFunctions.Find(worldFunctions.Source("FunctionEntryDialog").fields.mActivityRecommondView).gameObject.SetActive(false);
            foreach (var component in miniMapUiData.components.Where(value => value.panel == "FunctionEntryDialog"))
            {
                // Original provider states: no server clock region and no online tip records.
                if (component.type == "TimeInfoUI" || component.type == "TipDotNodeLinker") worldFunctions.State(component, 0);
                if (component.type == "TipDotNodeLinker" && !string.IsNullOrEmpty(component.fields.mNodeDataText))
                    worldFunctions.Find(component.fields.mNodeDataText).gameObject.SetActive(false);
                if (component.type == "FunctionEntryBigBtnItem") worldFunctions.State(component, 0);
            }
            foreach (Button button in worldFunctions.Root.GetComponentsInChildren<Button>(true))
            {
                if (button.name == "CloseBtn") { button.onClick.AddListener(CloseNativeWorldFunctions); continue; }
                Transform item = button.transform;
                while (item != null && !item.name.StartsWith("FunctionEntry", StringComparison.Ordinal)) item = item.parent;
                if (item == null || item.name == "FunctionEntryDialog") continue;
                string key = item.name;
                button.onClick.AddListener(() =>
                {
                    CloseNativeWorldFunctions();
                    if (key == "FunctionEntryShopItem") OpenCommerceShop();
                    else FunctionRequested?.Invoke(key);
                });
            }
        }

        private void CloseNativeWorldFunctions()
        {
            if (worldFunctions != null) Destroy(worldFunctions.Root);
            worldFunctions = null;
        }
    }
}
