using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Offline.World.MainlineMiniMap;
using Zjcs.Offline.World.NativeMap13MiniMapResources;
using Zjcs.Official.World;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeMap13Labels;
using Object = UnityEngine.Object;
using Status = Zjcs.Official.World.NativeMap13Labels.MapRegionStatus;

namespace Zjcs.Editor.Map13MiniMap
{
    public static partial class OfficialMainlineMiniMapRootAcceptance
    {
        private sealed class TimedLabelInputs : IMap13LabelServices, IMap13LabelPlayer
        {
            private readonly DescriptionLabels original;
            public TimedLabelInputs(DescriptionLabels original, OfficialMapDefinition map)
            {
                this.original = original;
                // Use the prepared official label projection: its explicit null
                // flag preserves null versus JsonUtility's allocated empty lists.
                Data = JsonUtility.FromJson<Map13LabelData>(File.ReadAllText(Path.Combine(
                    Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"),
                    "unity/Assets/Game/Resources/Map16/Native/region-label-data.json")));
                Require(Data.mapClassId == map.ClassId && Data.regions.Length == 43, "Original island label data identity");
            }
            public Map13LabelData Data { get; }
            public int MapClassId => Data.mapClassId;
            public object Map => this;
            public event Action Initialized { add { } remove { } }
            public IMap13LabelPlayer Player => this;
            public bool IsTeamMember => false;
            public int QueryRegion(Vector3 position) => throw new NotSupportedException();
            public MapRegionInfo GetRegion(int map, int region) => Data.regions.Single(row => row.ClassId == map && row.Region == region);
            public CombatRatingConfig Colors => original.Colors;
            public DateTime Now => throw new InvalidOperationException("The public module must supply the local clock.");
            public double CalcRemainSecond(DateTime end) => throw new InvalidOperationException("The public module must supply the local clock.");
            public int CurrentLevel = 100;
            public int Level => CurrentLevel;
            public void SetLevel(int level) { CurrentLevel = level; levels?.Invoke(level); }
            public SubRank SubRank => original.SubRank;
            public long CombatRating => original.CombatRating;
            private Action<int> levels;
            private Action<Rank> ranks;
            private Action<long, long> combat;
            private Action<int, List<int>> progress;
            private Action<int, int, Status, Status> statuses;
            private Action<long> offsets;
            public event Action<int> OnLevelUpdate { add => levels += value; remove => levels -= value; }
            public event Action<Rank> OnPlanesRankUnlock { add => ranks += value; remove => ranks -= value; }
            public event Action<long, long> OnCombatRatingChanged { add => combat += value; remove => combat -= value; }
            public event Action<int, List<int>> OnWorldMapProgressUpdate { add => progress += value; remove => progress -= value; }
            public event Action<int, int, Status, Status> OnMapStatusUpdate { add => statuses += value; remove => statuses -= value; }
            public event Action<long> OnDebugTimeOffset { add => offsets += value; remove => offsets -= value; }
            public int Subscribers => Count(levels) + Count(ranks) + Count(combat) + Count(progress) + Count(statuses) + Count(offsets);
            public string SubscriptionTargets => string.Join(", ", GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(field => typeof(Delegate).IsAssignableFrom(field.FieldType))
                .SelectMany(field => ((Delegate)field.GetValue(this))?.GetInvocationList()
                    .Select(callback => field.Name + ":" + callback.Method.DeclaringType.Name + "." + callback.Method.Name) ?? Array.Empty<string>()));
            private static int Count(Delegate action) => action?.GetInvocationList().Length ?? 0;
            public bool RegionIsActived(int map, int region) => false;
            public Status GetMapRegionStatus(int map, int region) => Status.Private;
            public int GetWorldMapRegionProgress(int map, int region) => 0;
        }

        private static T TimedField<T>(object instance, string name) => (T)instance.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(instance);

        private static IEnumerator VerifyRegionTime(GameObject prefab, MainlineMiniMapBindings bindings,
            MainlineMiniMapRootStateFixture state, DescriptionLabels original, Fixture input)
        {
            var repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            var definition = JsonUtility.FromJson<OfficialMapDefinition>(File.ReadAllText(Path.Combine(repository,
                "unity/Assets/Official/World/Map/16/map16.json")));
            var labels = new TimedLabelInputs(original, definition);
            var rows = labels.Data.regions;
            state.LabelOverride = labels;
            state.RegionsOverride = input.regions.ToDictionary(row => row.Region, row => new MiniMapRegionInfo
                { LabelInfo = labels.GetRegion(16, row.Region), Areas = row.Areas, RegionTag = (Zjcs.Official.World.Map13MiniMap.MapRegionType)row.RegionTag });
            var acquired = DateTimeOffset.Now.AddDays(-14);
            acquired = DateTimeOffset.FromUnixTimeSeconds(acquired.ToUnixTimeSeconds());
            var clock = acquired.LocalDateTime.AddDays(14).AddSeconds(-10);
            var player = new OfflinePlayerProgress { Rank = 4 };
            player.RankUnlockTimes.Add(new OfflineRankUnlockTime { Rank = 4, UnixSeconds = acquired.ToUnixTimeSeconds() });
            bindings.RegionTime = new OfflineCharacterRankRegionTime(definition, () => player, () => clock);
            var resources = Resources.Load<MiniMapResourcePointAssets>("Map16/Native/current-resource-point-assets");
            bool wrongMapRejected = false;
            try { MainlineMiniMap.Create(prefab, null, bindings, Resources.Load<MiniMapResourcePointAssets>("Map13/Native/current-resource-point-assets")); }
            catch (InvalidDataException) { wrongMapRejected = true; }
            Require(wrongMapRejected && labels.Subscribers == 0 && bindings.ResourcePoints == null,
                "A wrong-map catalog releases its temporary subscription and leaves caller bindings untouched");
            var module = MainlineMiniMap.Create(prefab, null, bindings, resources);
            yield return null;
            var root = Object.FindObjectOfType<MainlineNativeMiniMapRoot>();
            var row0 = root.GetRegion(16, 0).LabelInfo;
            Require(!ReferenceEquals(row0, rows[0]) && ReferenceEquals(row0, root.LabelServices.GetRegion(16, 0)), "Per-instance row is shared by original description and strength paths");
            Require(row0.IsLimitedByType(labels.Level) == MapRegionLimitType.RegionUnlockTime, "Original island 14-day condition before its boundary");
            foreach (var row in rows.Where(row => row.LimitServerRank != Rank.None))
                Require(root.GetRegion(16, row.Region).LabelInfo.GetMapRegionUnlockTime() == acquired.LocalDateTime.AddDays(row.LimitServerRankUnlockDayOffset), "Preserve every original island day offset");
            player.Rank = 5;
            Require(!row0.IsLimited(labels.Level), "Confirmed higher-rank immediate passage");
            player.Rank = 3;
            Require(row0.IsLimited(labels.Level), "Lower character rank retains the original time gate");
            player.Rank = 4;
            var secondClock = clock.AddDays(-1);
            bindings.RegionTime = new OfflineCharacterRankRegionTime(definition, () => player, () => secondClock);
            var second = MainlineMiniMap.Create(prefab, null, bindings, resources);
            yield return null;
            var secondRoot = Object.FindObjectsOfType<MainlineNativeMiniMapRoot>().Single(candidate => candidate != root);
            var secondRow = secondRoot.GetRegion(16, 0).LabelInfo;
            Require(!ReferenceEquals(row0, secondRow) && root.LabelServices.Now != secondRoot.LabelServices.Now,
                "Two original map views retain independent rows and local clocks");
            secondClock = acquired.LocalDateTime.AddDays(14);
            Require(!secondRow.IsLimited(labels.Level) && row0.IsLimited(labels.Level), "Changing the second view's time cannot unlock the first view");
            second.Dispose(); yield return null;
            int moduleSubscriptions = labels.Subscribers;

            // Run the complete current original label, including its description
            // coordinator, nested condition helpers and normal release path.
            var holder = new GameObject("OriginalIslandTimedLabelProbe"); holder.SetActive(false);
            var labelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/World/Map/16/NativeMiniMapRootCurrent/Dependencies/Prefabs/UI/Map/MiniMap/MiniMapMainRegionDescItem.prefab");
            var label = Object.Instantiate(labelPrefab, holder.transform, false);
            original.Bind(label);
            foreach (var ui in label.GetComponentsInChildren<UIBase>(true)) ui.Services = root.LabelServices;
            foreach (var color in label.GetComponentsInChildren<UIColorByLevel>(true)) color.Services = root.LabelServices;
            foreach (var description in label.GetComponentsInChildren<Zjcs.Official.World.Map13MiniMap.MapRegionDescItemUI>(true)) description.Services = root.LabelServices;
            var manager = holder.AddComponent<MiniMapRegionDescManager>(); manager.MiniMap = root;
            label.GetComponent<MiniMapRegionDescItem>().Init(manager, 0, true, root.MiniMapSetting.DescSetting, null);
            var strength = label.GetComponentInChildren<MapRegionStrengthDisplayUI>(true);
            holder.SetActive(true);
            for (int frame = 0; frame < 4; frame++) yield return null;
            var helper = TimedField<MapRegionDescHelper>(strength, "mRegionDescHelper");
            var limit = TimedField<MapRegionLimitStatusRefreshHelper>(helper, "mLimitStatusRefreshHelper");
            var timer = label.GetComponentInChildren<ShowRemainTimeInfo>(true);
            Require(TimedField<MapRegionLimitType>(limit, "mRegionLimitType") == MapRegionLimitType.RegionUnlockTime
                && timer.mRemainTimeText.gameObject.activeInHierarchy && timer.mRemainTimeText.Text.Contains("10"), "Original helper creates the local countdown and formats its remaining seconds");
            var camera = new GameObject("OriginalTimedLabelCamera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -100); camera.orthographic = true; camera.orthographicSize = 16;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .1f, .13f); camera.cullingMask = 1 << 20;
            var size = new RenderTexture(720, 1280, 24);
            var capture = CapturePanelFrame(size, "native-island-time-before.png"); while (capture.MoveNext()) yield return capture.Current;
            clock = acquired.LocalDateTime.AddDays(14);
            Require(!row0.IsLimited(labels.Level), "The exact original unlock timestamp is inclusive");
            float deadline = Time.realtimeSinceStartup + 4;
            while (TimedField<MapRegionLimitType>(limit, "mRegionLimitType") != MapRegionLimitType.None)
            { Require(Time.realtimeSinceStartup < deadline, "Original timed provider refresh deadline"); yield return null; }
            Require(!timer.mRemainTimeText.gameObject.activeSelf, "Original timed provider stops and hides the countdown after passage");
            clock = acquired.LocalDateTime.AddDays(14).AddSeconds(-10);
            holder.SetActive(false); holder.SetActive(true); yield return null;
            Require(timer.mRemainTimeText.gameObject.activeInHierarchy, "Original label re-entry recreates its cancelled timer");
            // Explicit mixed-condition probe: keep the island's time fields and
            // inject one actual Map13 level limit into this view's private clone.
            // This is a fixture combination, never written to official map data.
            holder.SetActive(false);
            row0.LimitLevel = original.Data.regions.First(row => row.LimitLevel > 0).LimitLevel;
            labels.CurrentLevel = row0.LimitLevel - 1;
            holder.SetActive(true); yield return null;
            Require(TimedField<MapRegionLimitType>(limit, "mRegionLimitType") == MapRegionLimitType.RegionUnlockTime, "Original time gate precedes the independent level gate");
            clock = acquired.LocalDateTime.AddDays(14);
            deadline = Time.realtimeSinceStartup + 4;
            while (TimedField<MapRegionLimitType>(limit, "mRegionLimitType") != MapRegionLimitType.Level)
            { Require(Time.realtimeSinceStartup < deadline, "Original provider changes from time to level"); yield return null; }
            labels.SetLevel(row0.LimitLevel);
            Require(TimedField<MapRegionLimitType>(limit, "mRegionLimitType") == MapRegionLimitType.None, "Original level event completes the second condition");
            holder.SetActive(false); Object.Destroy(holder); Object.Destroy(camera.gameObject); Object.Destroy(size); yield return null;
            Require(labels.Subscribers == moduleSubscriptions, "Original label releases its subscriptions while the parent minimap stays active: " + labels.SubscriptionTargets);
            bool untouched = false; try { rows[0].GetMapRegionUnlockTime(); } catch (InvalidOperationException) { untouched = true; }
            Require(untouched, "Shared original configuration never acquires a view's clock");
            module.Dispose(); yield return null;
            Require(labels.Subscribers == 0, "Disposing the public minimap also releases the original region border subscription: " + labels.SubscriptionTargets);
            state.LabelOverride = null; state.RegionsOverride = null; bindings.RegionTime = null;
            Debug.Log("MAINLINE_MINIMAP_REGION_TIME_PASS");
        }
    }
}
