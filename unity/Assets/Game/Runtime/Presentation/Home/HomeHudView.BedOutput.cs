using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        FurnitureOutputInfo bedOutput;
        OfflineHomeFurnitureOutput bedOutputData;
        IDisposable bedOutputScope;
        FontAssetFontService bedOutputFonts;
        readonly List<GameObject> bedOutputRows = new List<GameObject>();
        [Serializable] sealed class BedOutputText { public string key, text; }
        [Serializable] sealed class BedOutputTextData { public string manifest; public BedOutputText[] texts; }
        Dictionary<string, string> bedOutputTexts;
        string BedOutputTextFor(string key)
        {
            if (bedOutputTexts == null)
            {
                var asset = Resources.Load<TextAsset>("Home/home-bed-output-texts");
                var data = asset ? JsonUtility.FromJson<BedOutputTextData>(asset.text) : null;
                if (data == null || data.manifest != "884_156692")
                    throw new InvalidOperationException("Selected bed output language is unavailable.");
                bedOutputTexts = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var row in data.texts) bedOutputTexts.Add(row.key, row.text);
            }
            return bedOutputTexts.TryGetValue(key, out string text) ? text : key;
        }

        void BindNativeBedOutput(GameObject panel)
        {
            var assets = OfficialHomeHangUpWindowAssetCatalog.LoadDefault();
            var original = assets.bedPrefab.GetComponent<FurnitureOutputInfo>();
            if (!original) throw new InvalidOperationException("Original bed output controller is unavailable.");
            var staging = new GameObject("BedOutputDependencyBinding", typeof(RectTransform));
            staging.transform.SetParent(panel.transform, false); staging.SetActive(false);
            try
            {
                bedOutputData = new OfflineHomeFurnitureOutput(host.Rules,
                    host.HomeIncomeProperties,
                    id => host.Rules.HangUp(id).useBlessingLevel ? host.BlessingLevel : host.Session.HangUpLevel(id),
                    host.FormatTopBarCurrency, BedOutputTextFor);
                // FurnitureOutputInfo lives on the original bed root, with eight
                // serialized row references across its two foldouts. Keep that
                // data owner inactive while its exact referenced rows are mounted
                // at the same paths in the existing window. Other original bed
                // controllers are activated only by their complete future owner.
                bedOutput = Object.Instantiate(original, staging.transform, false);
                bedOutput.gameObject.SetActive(false);
                bedOutput.OutputServices = bedOutputData;
                bedOutputFonts = OfflineHomeHudAssets.LoadDefault().CreateFontService();
                bedOutputFonts.Initialize();
                bedOutputScope = OfflineUiBindingModule.Register(panel.transform, new OfflineUiBindings {
                    Localize = BedOutputTextFor, Font = () => OfflineHomeHudAssets.LoadDefault().defaultFont,
                    Pool = (_, prefab, slot, world) => Object.Instantiate(prefab, slot, world) });
                foreach (var row in bedOutput.OutputItems)
                {
                    if (!row) continue;
                    string path = row.transform.name;
                    for (var parent = row.transform.parent; parent != bedOutput.transform; parent = parent.parent)
                        path = parent.name + "/" + path;
                    var previous = panel.transform.Find(path);
                    if (!previous) throw new InvalidOperationException("Original bed output row path is unavailable: " + path);
                    foreach (var text in row.GetComponentsInChildren<TMP_Text>(true)) bedOutputFonts.SetupFont(text.font);
                    foreach (var text in row.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = BedOutputTextFor;
                    int index = previous.GetSiblingIndex();
                    previous.gameObject.SetActive(false);
                    row.transform.SetParent(previous.parent, false);
                    row.transform.SetSiblingIndex(index);
                    bedOutputRows.Add(row.gameObject);
                }
                bedOutput.transform.SetParent(panel.transform, false);
                RefreshNativeBedOutput();
            }
            catch { ReleaseNativeBedOutput(); throw; }
            finally { Object.Destroy(staging); }
        }
        void RefreshNativeBedOutput()
        {
            if (!bedOutput) return;
            bedOutputData.Refresh();
            bedOutput.ShowInfo(1, host.BlessingLevel);
        }
        void ReleaseNativeBedOutput()
        {
            if (bedOutput) { bedOutput.gameObject.SetActive(false); Object.Destroy(bedOutput.gameObject); }
            foreach (var row in bedOutputRows) if (row) { row.SetActive(false); Object.Destroy(row); }
            bedOutputRows.Clear();
            bedOutput = null; bedOutputData = null;
            bedOutputScope?.Dispose(); bedOutputScope = null;
            bedOutputFonts?.Dispose(); bedOutputFonts = null;
        }
    }
}
