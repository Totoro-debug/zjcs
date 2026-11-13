public sealed partial class HomeBedOutputHostRunner
{
    private IEnumerator CheckBedOutput()
    {
        SceneClick(0);
        yield return new WaitForSeconds(.5f);
        var output = home.Hud.CanvasTransform.GetComponentsInChildren<Zjcs.Official.HomeHangUpWindowsUI.FurnitureOutputInfo>(true).Single();
        Require(output.OutputServices is Zjcs.Offline.HomeUI.OfflineHomeFurnitureOutput, "formal bed uses actual native output bindings");
        yield return null;
        Capture("home-bed-output-native.png");
        var original = Zjcs.Official.HomeHangUpWindowsUI.OfficialHomeHangUpWindowAssetCatalog.LoadDefault()
            .bedPrefab.GetComponent<Zjcs.Official.HomeHangUpWindowsUI.FurnitureOutputInfo>();
        for (int index = 0; index < output.OutputItems.Count; index++)
        {
            var actualRow = (RectTransform)output.OutputItems[index].transform;
            var originalRow = (RectTransform)original.OutputItems[index].transform;
            // The original layout groups drive anchors, positions and dimensions
            // after activation. Pivot and local scaling remain authored values.
            Require(actualRow.pivot == originalRow.pivot && actualRow.localScale == originalRow.localScale,
                "every original output row retains its authored pivot and scaling");
        }
        Require(output.OutputItems.All(row => row.gameObject.activeInHierarchy), "all original output rows are actually visible");
        Require(OutputRow(output, "mBranchExplorePropItem") == "0", "plain profile has no invented exploration bonus");
        Require(!home.Hud.CanvasTransform.GetComponentsInChildren<TMPro.TMP_Text>()
            .Any(text => text.text.Contains("hang_up_1_base_output")), "dynamic original base-output label is localized");
        home.Hud.CloseDialog(); yield return null;
        Require(!home.Hud.CanvasTransform.GetComponentsInChildren<Zjcs.Official.HomeHangUpWindowsUI.FurnitureOutputInfo>(true).Any(), "closing the actual bed releases its original output component");
        home.Close(); CloseCapture(); yield return null;
        var profile = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
        var character = OfficialCharacterAssetCatalog.LoadDefault().Load();
        var lost = character.Rows("map_region").First(row => Convert.ToInt32(row["ClassId"]) == 11
            && row.TryGetValue("GoddessActivePropAwardDict", out object value)
            && value is Dictionary<string, object> values && values.Count > 0);
        int region = Convert.ToInt32(lost["Region"]);
        Require(profile.InitializeMap(11, character.Rows("map_region").Where(row => Convert.ToInt32(row["ClassId"]) == 11)
            .Select(row => Convert.ToInt32(row["Region"])).Distinct().ToArray()).IsApplied, "initialize isolated actual map");
        Require(profile.SetMapRegionProgress(11, region, 10000).IsApplied, "seed required explored region");
        Require(profile.RecordGoddessActivated(11, region, "bed-output:goddess").IsApplied, "commit real lost goddess");
        Open(); yield return new WaitForSeconds(1); SceneClick(0); yield return new WaitForSeconds(.5f);
        output = home.Hud.CanvasTransform.GetComponentsInChildren<Zjcs.Official.HomeHangUpWindowsUI.FurnitureOutputInfo>(true).Single();
        Require(OutputRow(output, "mBranchExplorePropItem").Contains("1%"), "original row reads persisted lost goddess property chunk");
        var services = output.OutputServices;
        var chunks = services.PropertyCenter.GetChunckProps(Zjcs.Official.HomeHangUpWindowsUI.EPropHandleType.LostGoddess);
        Require(chunks[(Zjcs.Official.ProfessionUI.ProfessionPropertyType)1001] == 100
            && services.GetProp(chunks, (Zjcs.Official.ProfessionUI.ProfessionPropertyType)1001) == .01,
            "selected original floating property scaling");
        double baseOutput = services.GetHangUpReward(1, 1, home.BlessingLevel, true);
        Require(services.GetHangUpReward(1, 17, home.BlessingLevel, true) == baseOutput,
            "original base preview ignores hours");
        Require(services.GetHangUpMaxRewardStore(1) == home.Session.Preview(1).Capacity,
            "native capacity reads the actual committed level and same official income formula");
        yield return null;
        Capture("home-bed-output-goddess.png");
        home.Hud.CloseDialog(); yield return null;
        SceneClick(0); yield return new WaitForSeconds(.5f);
        output = home.Hud.CanvasTransform.GetComponentsInChildren<Zjcs.Official.HomeHangUpWindowsUI.FurnitureOutputInfo>(true).Single();
        Require(OutputRow(output, "mBranchExplorePropItem").Contains("1%"), "reopening bed retains actual native source row");
        Capture("home-bed-output-reopened.png");
        home.Close(); CloseCapture(); yield return null;
        profile = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
        var cards = Zjcs.Offline.Commerce.OfficialCommerceCatalogView.LoadDefault();
        var card = cards.Data.periodicCards.First(value => value.extraProps != null
            && value.extraProps.Any(prop => prop.value != 0 && (prop.propId == 1000 || prop.propId == 1034)));
        var commerce = Zjcs.Offline.Commerce.OfflineCommerceSession.Open(new ProfileSlotCatalog(root), slot, DateTime.Now, profile);
        Require(commerce.UpdateState(state => {
            string key = "periodic-expiry:" + card.id;
            state.Bought.RemoveAll(value => value.Key == key);
            state.Bought.Add(new Zjcs.Offline.Commerce.CommerceCounter { Key = key, Count = state.Day + 1 });
        }), "commit actual periodic-card property source");
        Open(); yield return new WaitForSeconds(1); SceneClick(0); yield return new WaitForSeconds(.5f);
        output = home.Hud.CanvasTransform.GetComponentsInChildren<Zjcs.Official.HomeHangUpWindowsUI.FurnitureOutputInfo>(true).Single();
        var cardChunk = output.OutputServices.PropertyCenter.GetChunckProps(Zjcs.Official.HomeHangUpWindowsUI.EPropHandleType.PeriodIcCar);
        foreach (var prop in card.extraProps.Where(value => value.value != 0))
            Require(cardChunk[(Zjcs.Official.ProfessionUI.ProfessionPropertyType)prop.propId] == prop.value,
                "display source contains actual monthly-card values exactly once");
        Require(OutputRow(output, "mMouthCardPropItem") != "0", "original monthly-card row displays committed source bonus");
        Require(output.OutputServices.GetHangUpMaxRewardStore(1) == home.Session.Preview(1).Capacity,
            "monthly-card display capacity and actual reward ledger use the same merged source");
        Capture("home-bed-output-monthly.png");
        Debug.Log("HOME_BED_OUTPUT_PASSED: actual scene pointer, eight visible original rows with authored pivots/scales, selected formulas, persisted lost goddess and monthly-card chunks, close/reopen ownership and rendered frames. Whole bed controller, remaining source rows/secondary windows and full HUD remain unfinished.");
    }
    private static string OutputRow(Zjcs.Official.HomeHangUpWindowsUI.FurnitureOutputInfo output, string name)
    {
        var row = (Zjcs.Official.ProfessionUI.ProfessionPlayerPropItem)typeof(Zjcs.Official.HomeHangUpWindowsUI.FurnitureOutputInfo)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(output);
        return System.Text.RegularExpressions.Regex.Replace(row.myPropValueText.GetComponent<TMPro.TMP_Text>().text, "<[^>]*>", "");
    }
}
