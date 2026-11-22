using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Offline.Home;
using Zjcs.Offline.HomeUI;
using Zjcs.Official.HomeOutputAddition;
using Zjcs.Presentation.Home;

namespace Zjcs.Validation.HomeOutputDetail
{
    public sealed partial class HomeOutputDetailVerification
    {
        static JToken IncomeActivityRow(bool capacity)
        {
            var configuration = JObject.Parse(Resources.Load<TextAsset>("Home/output-addition-rules").text);
            int type = (int)configuration["settings"][capacity ? "HomeHangUpMaxStoreTypeDict" : "HomeHangUpAddPercentTypeDict"]["1"];
            return configuration["rows"].First(row => (int)row["OutputAdditionPropType"] == type && (int)row["OutputAdditionPropVal"] > 0);
        }

        static PlayerOutputAdditionDataWrap IncomeActivityRecord(JToken row, DateTime start, DateTime end)
        {
            var type = (OutputAdditionPropType)(int)row["OutputAdditionPropType"];
            return new PlayerOutputAdditionDataWrap { Id = 10001, StartTime = start.AddHours(-1), EndTime = end.AddHours(2),
                OutputActionType = (OutputActionType)(int)row["OutputActionType"],
                OutputAdditionPropDict = new Dictionary<OutputAdditionPropType, PlayerOutputAdditionPropTypeDataWrap> {
                    { type, new PlayerOutputAdditionPropTypeDataWrap { ClassId = (int)row["ClassId"], StartTime = start, EndTime = end } } } };
        }

        static string IncomeActivityJson(PlayerOutputAdditionDataWrap record)
            => JsonConvert.SerializeObject(new OfflineHomeOutputAddition.State {
                Records = new List<PlayerOutputAdditionDataWrap> { record } });

        string BedIncomeText() => Windows.Bed.HangUpOutputTxt.GetComponent<TMP_Text>().text;

        IEnumerator CheckBedActivityIncome()
        {
            home.Hud.CloseDialog(); yield return null;
            var row = IncomeActivityRow(false);
            var checkpoint = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10800);
            var activity = IncomeActivityRecord(row, checkpoint.UtcDateTime.AddHours(1), checkpoint.UtcDateTime.AddHours(2));
            var definition = home.Session.Definitions.Single(value => value.HangUpId == 1);
            var properties = home.PlayerProperties();
            var flags = Field<ISet<int>>(home.Session, "floatPropertyIds");
            long ordinary = OfflineHangUpIncome.UnsettledRewardCount(definition.Rule, properties, flags, 10800, out _);
            double produced = OfflineHangUpIncome.UnsettledRewardValue(definition.Rule, properties, flags, 7200)
                - OfflineHangUpIncome.UnsettledRewardValue(definition.Rule, properties, flags, 3600);
            long expected = ordinary + (long)(produced * (double)((float)(int)row["OutputAdditionPropVal"] / 10000f));
            Require(expected > ordinary, "actual bed configuration supplies an earned activity amount");
            Require(home.Session.Mutate("bed:activity:fixture", save => {
                save.LastLocalUnixSeconds = checkpoint.ToUnixTimeSeconds(); save.LastIncomeSubsecond = 0;
                save.Income.RemoveAll(value => value.HangUpId == 1);
                save.Income.Add(new OfflineHomeIncomeEntry { HangUpId = 1 });
                save.OutputAdditionStateJson = IncomeActivityJson(activity);
            }), "isolated three-hour interval contains one active activity hour");
            home.Close(); home = null; yield return null;
            Open(); yield return new WaitForSecondsRealtime(.8f);
            Require(home.Session.PreviewAt(1, DateTimeOffset.UtcNow).Count == expected,
                "main-city cold catch-up binds activity data before settling offline time");
            home.Hud.ShowBed(); yield return new WaitForSecondsRealtime(.8f);
            var bed = Windows.Bed;
            var services = Field<OfflineHomeBedServices>(home.Hud, "nativeBedServices");
            Require(services.TryGetIncomePreview(1, out long count, out long capacity) && count == expected,
                "original bed window receives the committed local activity projection");
            Require(BedIncomeText() == expected + "/" + capacity,
                "native label shows the same earned count once, without another percentage multiplication");
            Require(services.GetOutputAdditionPropValue((int)row["OutputAdditionPropType"]) == 0,
                "expired child activity no longer advertises an effective income bonus");
            yield return Capture("bed-activity-income.png");
            long before = home.Profile.Save.Player.Experience;
            yield return WaitForButton(bed.GetHangUpRewardBtn); Click(bed.GetHangUpRewardBtn);
            yield return null;
            Require(home.Profile.Save.Player.Experience == before + expected,
                "original collection button grants exactly the displayed activity income");
            Require(home.Session.PreviewAt(1, DateTimeOffset.UtcNow).Count == 0,
                "native activity collection consumes the same local balance");
            yield return Capture("bed-activity-collected.png");
            home.Close(); home = null; yield return null;
            Open(); yield return new WaitForSecondsRealtime(.8f);
            Require(home.Profile.Save.Player.Experience == before + expected
                && home.Session.PreviewAt(1, DateTimeOffset.UtcNow).Count == 0,
                "cold reopen preserves the collected activity XP and consumed furniture balance");
        }

        IEnumerator CheckBedCapacityExpiry()
        {
            home.Hud.CloseDialog(); yield return null;
            var row = IncomeActivityRow(true);
            var checkpoint = DateTimeOffset.UtcNow;
            DateTime expiry = DateTime.UtcNow.AddSeconds(12);
            var activity = IncomeActivityRecord(row, checkpoint.UtcDateTime.AddHours(-1), expiry);
            var definition = home.Session.Definitions.Single(value => value.HangUpId == 1);
            long capacity = home.Session.Preview(1).Capacity;
            long boosted = capacity + (long)((float)capacity * ((float)(int)row["OutputAdditionPropVal"] / 10000f));
            long stored = capacity + 1;
            Require(boosted > stored, "actual capacity activity leaves room above the mature overflow fixture");
            Require(home.Session.Mutate("bed:capacity:fixture", save => {
                save.LastLocalUnixSeconds = checkpoint.ToUnixTimeSeconds();
                save.LastIncomeSubsecond = (checkpoint.UtcDateTime.Ticks % TimeSpan.TicksPerSecond) / (double)TimeSpan.TicksPerSecond;
                save.Income.RemoveAll(value => value.HangUpId == 1);
                save.Income.Add(new OfflineHomeIncomeEntry { HangUpId = 1, SettledCount = stored,
                    Activity = new HomeIncomeActivityState { Enabled = true, Capacity = boosted } });
                save.OutputAdditionStateJson = IncomeActivityJson(activity);
            }), "isolated live capacity-expiry fixture");
            home.Hud.ShowBed(); yield return new WaitForSecondsRealtime(.8f);
            var bed = Windows.Bed;
            if (!bed.OutputFoldout)
            {
                yield return WaitForButton(bed.OutputFoldoutBtn); Click(bed.OutputFoldoutBtn);
                yield return new WaitForSecondsRealtime(.4f);
            }
            Require(BedIncomeText() == stored + "/" + boosted, "native capacity label shows the active original capacity bonus");
            yield return Capture("bed-capacity-active.png");
            float deadline = Time.realtimeSinceStartup + 18;
            while (DateTime.UtcNow < expiry.AddSeconds(.2) && Time.realtimeSinceStartup < deadline) yield return null;
            Require(DateTime.UtcNow >= expiry, "live original window remains open through the actual activity boundary");
            yield return null;
            Require(BedIncomeText() == stored + "/" + capacity,
                "native window refreshes expired capacity without losing mature overflow");
            Require(bed.HangUpProgress.fillAmount == 1,
                "original progress image becomes full at the lower effective capacity");
            var detail = bed.GetComponentInChildren<Zjcs.Official.HomeHangUpWindowsUI.OutputAdditionDetailButton>(true);
            Require(!Field<GameObject>(detail, "mContext").activeSelf,
                "original activity context refreshes at expiry while its earned count is unchanged");
            yield return Capture("bed-capacity-expired.png");
        }
    }
}
