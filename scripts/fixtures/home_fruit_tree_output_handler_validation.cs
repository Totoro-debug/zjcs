using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.HomeFruitTreeOutput;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeFruitTreeOutputHandlerValidation
{
    private const string Active = "Zjcs.HomeFruitTreeOutputHandlerValidation";
    static HomeFruitTreeOutputHandlerValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("Original fruit output validation").AddComponent<Runner>();
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!File.Exists(Path.Combine(work, ".zjcs-validation.json")) || !Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("An owned independent Unity project is required.");
        SessionState.SetBool(Active, true); EditorApplication.EnterPlaymode();
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("HOME_FRUIT_TREE_OUTPUT_HANDLER_PASSED"); else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    private sealed class Effect : HomeFruitTreeHangUpEffectItem
    {
        public static readonly List<int> Initialized = new List<int>();
        public override void Init(int index) => Initialized.Add(index);
    }
    private sealed class Item : IHomeFruitTreeOutputItem
    {
        public long Amount;
        public long Count => Amount;
        public double Decimal => .5;
        public double IncreaseSpeed => 2;
        public DateTime LastUpdateTime => new DateTime(2026, 1, 1);
    }
    private sealed class Lease : IDisposable
    {
        private readonly Action dispose;
        public Lease(Action dispose) { this.dispose = dispose; }
        public void Dispose() => dispose();
    }
    private sealed class Services : IHomeFruitTreeOutputServices
    {
        public bool HasPlayer => true;
        public bool InMyHome { get; set; } = true;
        public int FruitId => 110;
        public readonly Item Fruit = new Item { Amount = 2 };
        public readonly List<string> Calls = new List<string>();
        public Action HomeChanged;
        public event Action<long> OnDebugTimeOffset;
        public event Action<int> OnFastHangUp;
        public GameObject OverridePrefab;
        public TimeSpan? Remaining;
        public int TimeSubscribers => OnDebugTimeOffset?.GetInvocationList().Length ?? 0;
        public int FastSubscribers => OnFastHangUp?.GetInvocationList().Length ?? 0;
        public int RandomRange(int min, int max) { Calls.Add("random:" + min + ":" + max); return max - 1; }
        public long GetItemAmount(int id) => id == 110 ? Fruit.Amount : 0;
        public IHomeFruitTreeOutputItem GetItemById(int id) { Calls.Add("item:" + id); return id == 110 ? Fruit : null; }
        public IEnumerable<IHomeFruitTreeOutputItem> FindItemsById(int id) => new[] { Fruit };
        public EFurniture FastHangUpFurniture(int id) => id == 1 ? EFurniture.Bed : EFurniture.Cart;
        public double CalcElapsedSeconds(DateTime since) => 10;
        public void ShowFruitNotRipe(TimeSpan remaining) => Remaining = remaining;
        public IDisposable SubscribePostHomeChange(Action changed) { HomeChanged = changed; return new Lease(() => HomeChanged = null); }
        public GameObject LoadEffectPrefab(string guid) { Calls.Add("load:" + guid); return OverridePrefab; }
        public GameObject InstantiateEffect(GameObject prefab, Transform parent) { Calls.Add("instantiate:" + prefab.name); return Object.Instantiate(prefab, parent, false); }
        public void Home() => HomeChanged?.Invoke();
        public void Fast(int id) => OnFastHangUp?.Invoke(id);
        public void Offset() => OnDebugTimeOffset?.Invoke(0);
    }
    private sealed class Runner : MonoBehaviour
    {
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private IEnumerator Start()
        {
            Exception error = null; var steps = Check();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception ex) { error = ex; break; }
                yield return current;
            }
            Finish(error);
        }
        private IEnumerator Check()
        {
            Effect.Initialized.Clear();
            var owner = new GameObject("Original fruit effect owner"); owner.SetActive(false);
            var defaultPrefab = new GameObject("default fruit"); defaultPrefab.SetActive(false); defaultPrefab.AddComponent<Effect>();
            var overridePrefab = new GameObject("override fruit"); overridePrefab.SetActive(false); overridePrefab.AddComponent<Effect>();
            var service = new Services { OverridePrefab = overridePrefab };
            var handler = owner.AddComponent<HomeFruitTreeHangUpHandlerFruitItem>(); handler.OutputServices = service;
            var type = typeof(HomeFruitTreeHangUpHandlerFruitItem);
            type.GetField("mEffectItem", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(handler, defaultPrefab.GetComponent<Effect>());
            Type slotType = type.GetNestedType("EffectItemSlotConfig", BindingFlags.NonPublic);
            var slots = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(slotType));
            for (int i = 0; i < 3; i++)
            {
                var slot = new GameObject("slot " + i).transform; slot.SetParent(owner.transform, false);
                object config = Activator.CreateInstance(slotType, true);
                slotType.GetField("Slot").SetValue(config, slot);
                var proxy = new AssetGuidProxy<GameObject>();
                if (i == 1) typeof(AssetGuidProxy<GameObject>).GetField("mAssetGuid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(proxy, "override");
                slotType.GetField("ItemProxy").SetValue(config, proxy); slots.Add(config);
            }
            type.GetField("mEffectItemSlotConfigs", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(handler, slots);
            owner.SetActive(true); yield return null;
            Require(string.Join(",", service.Calls.GetRange(0, 3)) == "random:0:3,random:0:2,random:0:1", "Original slot shuffle visits the configured ranges in reverse order.");
            Require(service.TimeSubscribers == 1 && service.HomeChanged != null, "Awake and enable register the original home and time events.");
            handler.Init(); yield return null;
            Require(handler.HaveReward() && service.FastSubscribers == 1 && string.Join(",", Effect.Initialized) == "0,1", "Initialization shows the two stored fruits and subscribes once.");
            Require(service.Calls.Contains("load:override") && service.Calls.Contains("instantiate:default fruit") && service.Calls.Contains("instantiate:override fruit"), "Configured fruit prefabs override the default only for their slot.");
            service.Fruit.Amount = 3;
            float deadline = Time.realtimeSinceStartup + 3;
            while (Effect.Initialized.Count < 3 && Time.realtimeSinceStartup < deadline) yield return null;
            Require(Effect.Initialized.Count == 3 && Effect.Initialized[2] == 2, "A new fruit appears in the next original slot.");
            service.InMyHome = false; service.Home(); yield return null;
            Require(owner.GetComponentsInChildren<Effect>(true).Length == 3, "Leaving home preserves the existing effect cache.");
            foreach (var effect in owner.GetComponentsInChildren<Effect>(true)) Require(!effect.gameObject.activeSelf, "Leaving home hides every fruit.");
            handler.ResetData(); service.InMyHome = true; service.Home(); yield return null;
            Require(Effect.Initialized.Count == 6 && service.FastSubscribers == 1, "Returning home reuses the three cached fruits without duplicate subscriptions.");
            int before = service.Calls.FindAll(value => value == "item:110").Count;
            service.Fast(2); Require(service.Calls.FindAll(value => value == "item:110").Count == before, "An unrelated fast hang-up does not restart refresh.");
            service.Fast(1); Require(service.Calls.FindAll(value => value == "item:110").Count == before + 1, "Bed fast hang-up restarts refresh.");
            Require(handler.TryInteractiveWithNotReward() && service.Remaining == TimeSpan.FromSeconds(890), "Unripe time uses official fraction, hourly speed and elapsed time.");
            handler.Uninit(); owner.SetActive(false); yield return null;
            Require(service.TimeSubscribers == 0 && service.FastSubscribers == 0, "Disable and uninit release player subscriptions.");
            Object.Destroy(owner); yield return null; Require(service.HomeChanged == null, "Destroy disposes the home-change subscription.");
            Object.Destroy(defaultPrefab); Object.Destroy(overridePrefab);
        }
    }
}
