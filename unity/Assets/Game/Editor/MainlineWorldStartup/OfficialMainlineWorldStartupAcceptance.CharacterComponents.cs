using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;
using Zjcs.Offline.World.NativeMap14ClientComponents;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;

namespace Zjcs.Editor.MainlineWorldStartup
{
    public static partial class OfficialMainlineWorldStartupAcceptance
    {
        // Original MyEntity lifecycle with real, already-started shared characters.
        // This is a component fixture, not the restored visual character prefab.
        private sealed class CharacterClientComponents : IDisposable
        {
            private readonly NativeMap14SharedViewCache views;
            private readonly NativeMap14ClientComponentBindings bindings;
            private readonly GameObject host;
            private readonly MyEntity entity;
            private readonly MyCharacter character;
            private readonly GameObject offlineIndicator;
            private readonly GameObject entryPlayable;
            private readonly CharacterPlayableRequests playables = new CharacterPlayableRequests();
            private bool joinInMiddle;
            private bool characterStarted;
            private int onlineSubscribers;
            private NativeMap14SharedViewEntity previousView;
            private object[] previousComponents;

            public CharacterClientComponents(MainlineSharedWorld world)
            {
                bindings = new NativeMap14ClientComponentBindings(world.Assemblies);
                views = new NativeMap14SharedViewCache(world.Assemblies, bindings.Bind);
                host = new GameObject("Original character component binding fixture");
                host.SetActive(false);
                entity = host.AddComponent<MyEntity>();
                host.AddComponent<MyLevel>();
                host.AddComponent<MyProp>();
                host.AddComponent<MyMapRegionStatus>();
                host.AddComponent<MyMapStatusAgent>();
                character = host.AddComponent<MyCharacter>();
                host.AddComponent<MyGridMover>();
                offlineIndicator = new GameObject("Original offline indicator state fixture");
                offlineIndicator.transform.SetParent(host.transform, false);
                entryPlayable = new GameObject("Original entry playable request fixture");
                entryPlayable.transform.SetParent(host.transform, false);
                character.PlayerOfflineObj = offlineIndicator;
                character.PlayerShowUpPlayable = entryPlayable;
                character.Configure(playables, () => joinInMiddle);
            }

            public void Verify(MainlineSharedWorld world)
            {
                var view = views.Wrap(world.ClientCharacter);
                if (previousView != null)
                {
                    End();
                    if (!ReferenceEquals(view, previousView))
                        throw new InvalidDataException("Character transition replaced the retained original adapter.");
                }
                entity.Bind(null, view, previousView == null);
                if (entity.Components.Count != 6)
                    throw new InvalidDataException("Original character components are absent.");
                var names = new[] { "LevelComponent", "PropComponent", "MapRegionStatusComponent", "MapStatusAgentComponent", "CharacterComponent", "GridMoverComponent" };
                var components = entity.Components.ToArray();
                var current = new object[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    var component = components[i];
                    var data = component.GetEcComponent() as EntityClientComponentData;
                    var original = world.ClientCharacter.GetType().GetMethod("GetComponent", new[] { typeof(Type) })
                        .Invoke(world.ClientCharacter, new object[] { world.Assemblies.CommonType(names[i]) });
                    if (original == null || data == null || !ReferenceEquals(data.Original, original))
                        throw new InvalidDataException("Character binding lost the original component: " + names[i]);
                    if (i < 5 && (!(component is IEmptyInfoMyComponent) || data.ComponentInfo != null))
                        throw new InvalidDataException("Character binding lost its empty-info contract: " + names[i]);
                    if (i == 5 && (!ReferenceEquals(data.ComponentInfo, NativeMap14SharedPrefabs.Get(original, "_Info"))
                        || !ReferenceEquals(data.ComponentInfo, host.GetComponent<MyGridMover>().Info)))
                        throw new InvalidDataException("Character grid mover copied or lost original Info.");
                    current[i] = data;
                    if (previousComponents != null && !ReferenceEquals(previousComponents[i], data))
                        throw new InvalidDataException("Character lifecycle rebind changed a retained original component adapter.");
                }
                bindings.Bind(view);
                onlineSubscribers = OnlineSubscribers(character.Component.Original);
                if (previousView == null)
                {
                    // Both fields are null in the actual retained 3001 prefab.
                    // Original short-circuiting must not query a fight root.
                    character.PlayerOfflineObj = null;
                    character.PlayerShowUpPlayable = null;
                    character.Configure(playables, () => throw new InvalidDataException("Null entry prefab queried a fight root."));
                    entity.MyStart(); characterStarted = true;
                    if (OnlineSubscribers(character.Component.Original) != onlineSubscribers + 1 || playables.Count != 0)
                        throw new InvalidDataException("Original null character prefab fields changed startup behaviour.");
                    var onlineState = character.Component.Original.GetType().GetProperty("Online");
                    bool initial = character.Component.Online;
                    onlineState.SetValue(character.Component.Original, !initial);
                    onlineState.SetValue(character.Component.Original, initial);
                    End();
                    entity.Bind(null, view, false);
                    character.PlayerOfflineObj = offlineIndicator;
                    character.PlayerShowUpPlayable = entryPlayable;
                    character.Configure(playables, () => joinInMiddle);
                }
                joinInMiddle = previousView != null;
                int requests = playables.Count;
                entity.MyStart();
                characterStarted = true;
                if (!entity.myStarted || !world.IsReady)
                    throw new InvalidDataException("Character component lifecycle did not preserve the running shared world.");
                if (OnlineSubscribers(character.Component.Original) != onlineSubscribers + 1
                    || offlineIndicator.activeSelf == character.Component.Online
                    || playables.Count != requests + (joinInMiddle ? 1 : 0))
                    throw new InvalidDataException("Original character start subscription, initial refresh or entry request differs.");
                if (joinInMiddle && (playables.Prefab != entryPlayable || playables.Source != host || playables.Target != host))
                    throw new InvalidDataException("Original character entry playable lost source/target identity.");
                var nativeCharacter = character.Component.Original;
                var onlineProperty = nativeCharacter.GetType().GetProperty("Online");
                bool online = character.Component.Online;
                onlineProperty.SetValue(nativeCharacter, !online);
                if (offlineIndicator.activeSelf != online)
                    throw new InvalidDataException("Original character online event did not update the offline indicator.");
                onlineProperty.SetValue(nativeCharacter, online);
                if (offlineIndicator.activeSelf == online)
                    throw new InvalidDataException("Original character online state did not restore.");
                previousView = view; previousComponents = current;
                Debug.Log($"MAINLINE_CHARACTER_CLIENT_COMPONENTS_PASS map={world.Data.MapClassId} components=6 join_in_middle={joinInMiddle} original_online_event=True original_null_prefab_fields=True");
            }

            private static int OnlineSubscribers(object original) =>
                ((Delegate)original.GetType().GetField("OnPlayerOnlineStateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(original))?.GetInvocationList().Length ?? 0;

            private void End()
            {
                characterStarted = false;
                var original = character.Component.Original;
                entity.End(null); entity.Remove(null); entity.OnGameObjectPoolReset();
                if (OnlineSubscribers(original) != onlineSubscribers)
                    throw new InvalidDataException("Original character end retained an online subscription.");
                bool visible = offlineIndicator.activeSelf;
                var online = original.GetType().GetProperty("Online");
                bool previous = (bool)online.GetValue(original);
                online.SetValue(original, !previous);
                if (offlineIndicator.activeSelf != visible)
                    throw new InvalidDataException("Ended original character still received online updates.");
                online.SetValue(original, previous);
            }

            public void Dispose()
            {
                try
                {
                    if (characterStarted) End();
                }
                finally { views.Dispose(); UnityEngine.Object.Destroy(host); }
            }
        }

        // Observe original playback requests; no entry effect rendering is claimed.
        private sealed class CharacterPlayableRequests : IMyPlayableManager
        {
            public int Count;
            public GameObject Prefab, Source, Target;
            public Transform PlayableSlot => throw new NotSupportedException();
            public MyPlayable Play(GameObject prefab, GameObject source, GameObject target, float speed = 1f,
                GameObject parentGo = null, Dictionary<string, object> param = null, IMyPlayableNeedStop needStopObj = null)
            {
                if (speed != 1f || parentGo != null || param != null || needStopObj != null)
                    throw new InvalidDataException("Original character entry request defaults changed.");
                Count++; Prefab = prefab; Source = source; Target = target; return null;
            }
            public void RemoveAllPlayable(bool release) => throw new NotSupportedException();
            public void SetObjectPool(GamePrefabTypeSetPools<MyPlayable, GameObjectPoolBase<MyPlayable>> pools) => throw new NotSupportedException();
            public void ForceReleaseObjectPool() => throw new NotSupportedException();
        }
    }
}
