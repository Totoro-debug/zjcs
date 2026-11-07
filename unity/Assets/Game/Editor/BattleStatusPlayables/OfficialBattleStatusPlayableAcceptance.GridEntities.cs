using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Official.BattleGridItemEntities;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Editor
{
    public static partial class OfficialBattleStatusPlayableAcceptance
    {
        // Fixture-owned entity events exercise the original view driver and
        // registry. They do not substitute for the production battle binding.
        private sealed class GridViewData : ECViewEntityData, IBattleEntityEndEvents
        {
            public readonly GridRoleData Role = new GridRoleData();
            public int Type;
            public override int ClassId => Type;
            public override EcEntityIdentifier Id => new EcEntityIdentifier(1, Type);
            private Action<ECViewEntityData, object> ended;
            public int EndListeners => ended?.GetInvocationList().Length ?? 0;
            public event Action<ECViewEntityData, object> Ended { add => ended += value; remove => ended -= value; }
            public void End() => ended?.Invoke(this, null);
            public override T GetComponent<T>() => typeof(T) == typeof(BattleRoleCharacterViewData)
                ? (T)(object)Role : typeof(T) == typeof(IBattleEntityEndEvents) ? (T)(object)this : default;
        }

        private sealed class GridRoleData : BattleRoleCharacterViewData
        {
            private Action<bool, long> changed;
            private bool die;
            public int Listeners => changed?.GetInvocationList().Length ?? 0;
            public override BattleRoleCharacterIdentity Character => null;
            public override EcEntityIdentifier MasterId => default;
            public override bool Die => die;
            public override bool IsNew => false;
            public override event Action<bool, long> OnDie { add => changed += value; remove => changed -= value; }
            public void SetDie(bool value) { die = value; changed?.Invoke(value, 0); }
        }

        private sealed class GridViewBinding : ECViewEntityBinding
        {
            public GridViewData Data;
            private readonly List<IMyEntityStartEventReceiver> starts = new List<IMyEntityStartEventReceiver>();
            private readonly List<IMyEntityRemoveEventReceiver> removes = new List<IMyEntityRemoveEventReceiver>();
            private readonly List<IMyEntityStartPostEventReceiver> posts = new List<IMyEntityStartPostEventReceiver>();
            private readonly List<IMyEntityRemovePreEventReceiver> pres = new List<IMyEntityRemovePreEventReceiver>();
            public override ECViewEntityData Entity => Data;
            public int ListenerCount => starts.Count + removes.Count + posts.Count + pres.Count;
            public override void SetClientVisibilitySupporter(IClientVisibilitySupporter value) { }
            public override void AddStartReceiver(IMyEntityStartEventReceiver value) => starts.Add(value);
            public override void RemoveStartReceiver(IMyEntityStartEventReceiver value) => starts.Remove(value);
            public override void AddRemoveReceiver(IMyEntityRemoveEventReceiver value) => removes.Add(value);
            public override void RemoveRemoveReceiver(IMyEntityRemoveEventReceiver value) => removes.Remove(value);
            public override void AddStartPostReceiver(IMyEntityStartPostEventReceiver value) => posts.Add(value);
            public override void RemoveStartPostReceiver(IMyEntityStartPostEventReceiver value) => posts.Remove(value);
            public override void AddRemovePreReceiver(IMyEntityRemovePreEventReceiver value) => pres.Add(value);
            public override void RemoveRemovePreReceiver(IMyEntityRemovePreEventReceiver value) => pres.Remove(value);
            public void StartViews(bool fresh)
            {
                foreach (var value in starts.ToArray()) value.MyEntity_OnStart(this, fresh);
                foreach (var value in posts.ToArray()) value.MyEntity_OnStartPost(this);
            }
            public void RemoveViews()
            {
                foreach (var value in pres.ToArray()) value.MyEntity_RemovePre(this);
                foreach (var value in removes.ToArray()) value.MyEntity_OnRemove(this);
            }
        }

        private sealed class GridViewServices : IServiceProvider, IECViewInjection, IBattleGridPlayableEnvironment
        {
            public Zjcs.Official.StoryPlayableManager.IMyPlayableManager Manager;
            public bool IsFastforward => false;
            public bool NotPlayGeneratePlayable => false;
            public object GetService(Type type) => null;
            public void Inject(object value)
            {
                if (value is ECFightRoleCharacterView role)
                { role.MyPlayableManager = Manager; role.GamePlayManager = this; role.FightRoot = this; }
            }
        }

        private sealed partial class Scope
        {
            private IEnumerator GridEntitySteps()
            {
                var data = Resources.Load<TextAsset>("Combat/grid-entities");
                Require(data != null, "Native grid entity catalog is missing.");
                var entries = JsonUtility.FromJson<OrbitCatalog>(data.text).graphs;
                Require(entries.Length == 4, "Native grid/status prefab count differs.");
                var staging = new GameObject("Inactive original grid prefab staging");
                staging.transform.SetParent(root.transform, false); staging.SetActive(false);
                foreach (var entry in entries)
                {
                    var prefab = Resources.Load<GameObject>(entry.resource);
                    Require(prefab != null, "Native grid/status prefab is unavailable: " + entry.id);
                    Require(prefab.GetComponentsInChildren<Transform>(true).Length == (entry.id < 10000 ? 2 : 1),
                        "Original grid/status hierarchy changed.");
                    Require(prefab.GetComponentsInChildren<Component>(true).All(value => value != null),
                        "Native grid/status prefab contains a missing script.");
                    var go = UnityEngine.Object.Instantiate(prefab, staging.transform, false);
                    if (entry.id >= 10000) { UnityEngine.Object.Destroy(go); continue; }
                    var attach = new UnityEditor.SerializedObject(prefab.GetComponent<AttachAgent>());
                    var names = attach.FindProperty("myBoneNameList");
                    Require(names.arraySize == 2 && names.GetArrayElementAtIndex(0).stringValue == ""
                        && names.GetArrayElementAtIndex(1).stringValue == "3300",
                        "Original grid bone names lost their string values or order.");
                    var grid = new UnityEditor.SerializedObject(
                        prefab.GetComponent<Zjcs.Official.EntityLifecycle.MyGridTransform>());
                    Require(grid.FindProperty("ClientOverrideEntityDimensionBounds").arraySize == 0,
                        "Original grid body override must remain empty.");
                    var driver = go.GetComponentInChildren<ECViewDriver>(true);
                    var role = driver.GetView<ECFightRoleCharacterView>();
                    Require(role != null && driver.GetView<ECFightRoleStatusDisplayView>() != null,
                        "Original SerializeReference view registry was not restored.");
                    var graphs = new HashSet<GameObject>(); role.TryGetPlayables(ref graphs);
                    Require(graphs.Count == 1 && graphs.Single().name == "playable13061_fire_die",
                        "Grid view lost the original fire-specific death graph.");
                    var binding = go.AddComponent<GridViewBinding>(); binding.Data = new GridViewData { Type = entry.id };
                    driver.ServiceProvider = new GridViewServices { Manager = manager };
                    driver.ClientVisibilitySupporterFactory = new ConstantClientVisibilitySupporterFactory();
                    go.transform.SetParent(root.transform, false);
                    yield return null;
                    Require(binding.ListenerCount == 4, "Original view driver/visibility subscriptions did not register.");
                    binding.StartViews(true);
                    Require(binding.Data.Role.Listeners == 1 && binding.Data.EndListeners == 1,
                        "Original character/status views did not subscribe to entity events.");
                    var handle = typeof(ECFightRoleCharacterView).GetField("mDiePlayableHandle", BindingFlags.Instance | BindingFlags.NonPublic);
                    Require(handle.GetValue(role) == null, "Living grid incorrectly played its death graph.");
                    binding.Data.Role.SetDie(true);
                    for (int frame = 0; frame < 8; frame++) { yield return null; Flush(); }
                    Require(handle.GetValue(role) is MyPlayable, "Original death view did not dispatch its configured graph.");
                    Capture("grid-" + entry.id + "-death");
                    binding.Data.Role.SetDie(false);
                    Require(handle.GetValue(role) == null, "Original view failed to stop its death graph.");
                    binding.Data.End(); binding.RemoveViews();
                    Require(binding.Data.Role.Listeners == 0 && binding.Data.EndListeners == 0 && binding.ListenerCount == 0,
                        "Native grid view unload leaked subscriptions.");
                    go.SetActive(false); go.SetActive(true); binding.StartViews(false);
                    Require(binding.Data.Role.Listeners == 1 && binding.Data.EndListeners == 1
                        && binding.ListenerCount == 4 && handle.GetValue(role) == null,
                        "Reusing the original native grid view duplicated listeners or replayed death.");
                    binding.Data.End(); binding.RemoveViews();
                    Require(binding.Data.Role.Listeners == 0 && binding.Data.EndListeners == 0 && binding.ListenerCount == 0,
                        "Reused native grid view leaked subscriptions.");
                    UnityEngine.Object.Destroy(go);
                    manager.RemoveAllPlayable(true); center.RemoveAll(); yield return null;
                }
                UnityEngine.Object.Destroy(staging);
                Debug.Log("Native grid entity prefabs passed: complete hierarchy, script bindings, original managed view registry, configured death graph, actual driver load/unload and event cleanup. Entity events are fixture adapters; production binding remains separate.");
            }
        }
    }
}
