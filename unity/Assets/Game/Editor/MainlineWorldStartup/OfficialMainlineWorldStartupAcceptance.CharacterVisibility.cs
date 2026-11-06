using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Offline.World.NativeMap14ClientComponents;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
using Zjcs.Offline.World.NativeMap14Visibility;

namespace Zjcs.Editor.MainlineWorldStartup
{
    public static partial class OfficialMainlineWorldStartupAcceptance
    {
        // Real shared character flags/events and original MyVisibility bodies;
        // the controlled handler/supporter ports observe lifecycle, not rendering.
        private sealed class CharacterVisibilityComponents : IDisposable
        {
            private readonly NativeMap14SharedViewCache views;
            private readonly NativeMap14ClientComponentBindings bindings;
            private readonly GameObject host;
            private readonly MyEntity entity;
            private readonly MyVisibilityCustom visibility;
            private readonly MyNpcFollowerController followers;
            private readonly CharacterVisibilityFactory factory = new CharacterVisibilityFactory();
            private readonly CharacterVisibilityRequests requests = new CharacterVisibilityRequests();
            private NativeMap14SharedViewEntity previous;
            private NativeMap14VisibilityData data;
            private NpcFollowerControllerComponentData followerData;
            private bool running;
            private int notifications;

            public CharacterVisibilityComponents(MainlineSharedWorld world)
            {
                bindings = new NativeMap14ClientComponentBindings(world.Assemblies);
                views = new NativeMap14SharedViewCache(world.Assemblies, bindings.Bind);
                host = new GameObject("Original character visibility binding fixture"); host.SetActive(false);
                entity = host.AddComponent<MyEntity>(); visibility = host.AddComponent<MyVisibilityCustom>();
                followers = host.AddComponent<MyNpcFollowerController>();
                visibility.EntityClientVisibilityHandlerFactory = factory;
                visibility.OnVisibilityChanged += () => notifications++;
                entity.SetClientVisibilitySupporter(requests); host.SetActive(true);
            }

            private static void Require(bool value, string message)
            { if (!value) throw new InvalidDataException("Original character visibility: " + message); }

            public void Verify(MainlineSharedWorld world)
            {
                object NativeComponent(string name) => world.ClientCharacter.GetType().GetMethod("GetComponent", new[] { typeof(Type) })
                    .Invoke(world.ClientCharacter, new object[] { world.Assemblies.CommonType(name) });
                var original = NativeComponent("VisibilityCustomComponent");
                var dispatcher = NativeMap14SharedPrefabs.Get(original, "OnClientVisibilityUpdate");
                int originalReceivers = ActionReceiverCount(dispatcher);
                var view = views.Wrap(world.ClientCharacter);
                var projected = view.GetComponent<Map13VisibilityData>() as NativeMap14VisibilityData;
                bool isNew = previous == null;
                Require(projected != null && ReferenceEquals(projected.Original, original), "custom visibility source differs");
                if (!isNew)
                    Require(ReferenceEquals(view, previous) && ReferenceEquals(projected, data), "transition replaced retained state");
                int expectedNativeReceivers = originalReceivers + (isNew ? 1 : 0);
                data = projected;
                bindings.Bind(view);
                Require(ActionReceiverCount(dispatcher) == expectedNativeReceivers, "rebind added another native receiver");
                entity.Bind(null, view, isNew);
                Require(ReferenceEquals(visibility.Component, data) && visibility is IEmptyInfoMyComponent
                    && ReferenceEquals(visibility.Info, data.ComponentInfo)
                    && ReferenceEquals(data.ComponentInfo, NativeMap14SharedPrefabs.Get(original, "_Info")), "visibility Info or empty-info contract differs");
                Require(ReferenceEquals(followers.Component.Original, NativeComponent("NpcFollowerControllerComponent"))
                    && ReferenceEquals(followers.Info, NativeMap14SharedPrefabs.Get(followers.Component.Original, "_Info")), "follower component/Info identity differs");
                if (followerData != null) Require(ReferenceEquals(followerData, followers.Component), "transition replaced follower binding");
                followerData = followers.Component;
                var initialFlags = data.ClientVisibilityType;
                bool initialFade = data.IsShowNeedFadeIn;
                int initialNotifications = notifications, initialRequests = requests.Count, returned = factory.Returned;
                try
                {
                    data.ClientVisibilityType = ClientVisibilityType.None;
                    factory.Entity = view; factory.IsNew = isNew;
                    Require(!visibility.IsVisible, "unstarted visibility is visible");
                    entity.MyStart(); running = true;
                    Require(visibility.IsVisible && notifications == initialNotifications && requests.Count == initialRequests
                        && ActionReceiverCount(data.OnClientVisibilityUpdate) == 1, "start order or no-change notification differs");
                    data.AddClientVisibilityRequest(ClientVisibilityType.Hide | ClientVisibilityType.WaitRefreshAction);
                    Require(!visibility.IsVisible && !requests.Visible && notifications == initialNotifications + 1
                        && factory.Current.Count == 2, "original flags did not rebuild and hide the character");
                    var first = factory.Current[0]; var second = factory.Current[1];
                    first.SetVisible(true);
                    // Original rebuild aggregates all handlers, but a handler's
                    // later event passes its bool directly to VisibleUpdate.
                    Require(visibility.IsVisible && requests.Visible && !second.Visible
                        && notifications == initialNotifications + 2, "original direct handler event branch differs");
                    second.SetVisible(true);
                    Require(visibility.IsVisible && requests.Visible && notifications == initialNotifications + 2, "unchanged direct handler event republished visibility");
                    second.SetVisible(true);
                    Require(notifications == initialNotifications + 2, "unchanged handler state republished visibility");
                    first.SetVisible(false);
                    Require(!visibility.IsVisible && notifications == initialNotifications + 3, "handler change did not hide the character");
                    data.AddClientVisibilityRequest(ClientVisibilityType.Hide);
                    Require(factory.Returned == returned + 2 && first.Releases == 1 && second.Releases == 1
                        && first.Subscribers == 0 && second.Subscribers == 0 && notifications == initialNotifications + 3,
                        "request rebuild did not unsubscribe before release or published a transient show");
                    data.RemoveClientVisibilityType(ClientVisibilityType.Hide | ClientVisibilityType.WaitRefreshAction);
                    Require(visibility.IsVisible && requests.Visible && notifications == initialNotifications + 4
                        && factory.Returned == returned + 4, "clearing the original request did not release handlers/show");
                    End();
                    Require(!visibility.IsVisible && ActionReceiverCount(data.OnClientVisibilityUpdate) == 0,
                        "end retained visibility or a client receiver");
                    int endedRequests = requests.Count;
                    data.AddClientVisibilityRequest(ClientVisibilityType.Hide);
                    Require(requests.Count == endedRequests && !visibility.IsVisible, "ended character received a request");
                    Require(ActionReceiverCount(dispatcher) == expectedNativeReceivers, "component end released another adapter's native receiver");
                    previous = view;
                    Debug.Log($"MAINLINE_CHARACTER_VISIBILITY_PASS map={world.Data.MapClassId} original_flags_events=True handler_order=True info_identity=True retained={!isNew}");
                }
                finally
                {
                    if (running) End();
                    data.ClientVisibilityType = initialFlags; data.IsShowNeedFadeIn = initialFade;
                }
            }

            private void End()
            {
                running = false; entity.End(null); entity.Remove(null); entity.OnGameObjectPoolReset();
            }

            public void Dispose()
            {
                try { if (running) End(); }
                finally
                {
                    var original = data?.Original;
                    int before = original == null ? 0 : ActionReceiverCount(NativeMap14SharedPrefabs.Get(original, "OnClientVisibilityUpdate"));
                    views.Dispose();
                    if (original != null)
                        Require(ActionReceiverCount(NativeMap14SharedPrefabs.Get(original, "OnClientVisibilityUpdate")) == before - 1,
                            "view cache retained its native receiver");
                    UnityEngine.Object.Destroy(host);
                }
            }
        }

        private sealed class CharacterVisibilityFactory : IEntityClientVisibilityHandlerFactory
        {
            public ECViewEntityData Entity; public bool IsNew; public int Returned;
            public readonly List<CharacterVisibilityHandler> Current = new List<CharacterVisibilityHandler>();
            public void GetHandlers(ClientVisibilityType flags, ref List<IClientVisibilityHandler> handlers)
            {
                Current.Clear();
                foreach (var type in new[] { ClientVisibilityType.Hide, ClientVisibilityType.WaitRefreshAction })
                    if ((flags & type) != 0)
                    {
                        var handler = new CharacterVisibilityHandler(type, this); Current.Add(handler); handlers.Add(handler);
                    }
            }
            public IClientVisibilityHandler GetHandler(ClientVisibilityType type) => throw new NotSupportedException();
            public void ReturnHanlder(IClientVisibilityHandler handler)
            {
                if (((CharacterVisibilityHandler)handler).Subscribers != 0) throw new InvalidDataException("Returned handler still has receivers.");
                Returned++;
            }
        }

        private sealed class CharacterVisibilityHandler : IClientVisibilityHandler
        {
            private readonly CharacterVisibilityFactory owner;
            private Action<bool> changed;
            public int Releases;
            public int Subscribers => changed?.GetInvocationList().Length ?? 0;
            public ClientVisibilityType VisibilityType { get; }
            public bool Visible { get; private set; }
            public ECViewEntityData Entity { get; private set; }
            public CharacterVisibilityHandler(ClientVisibilityType type, CharacterVisibilityFactory owner)
            { VisibilityType = type; this.owner = owner; }
            public event Action<bool> OnVisibleChanged { add => changed += value; remove => changed -= value; }
            public void Init(ECViewEntityData entity, bool isNew)
            {
                if (!ReferenceEquals(entity, owner.Entity) || isNew != owner.IsNew) throw new InvalidDataException("Original visibility handler owner/new-entity flag differs.");
                Entity = entity;
            }
            public void SetVisible(bool value)
            { if (Visible == value) return; Visible = value; changed?.Invoke(value); }
            public void Release() { Releases++; SetVisible(true); Entity = null; }
        }

        private sealed class CharacterVisibilityRequests : IClientVisibilitySupporter
        {
            public int Count; public bool Visible = true;
            public bool IsVisible => Visible;
            public event Action OnVisibleUpdate { add { } remove { } }
            public event Action OnShow { add { } remove { } }
            public event Action OnHide { add { } remove { } }
            public bool SetEntityVisible(int key, bool isVisible) { Count++; Visible = isVisible; return true; }
            public void SetForceVisible(bool value) => throw new NotSupportedException();
            public void ResetVisibility() => throw new NotSupportedException();
            public void Register(ECViewEntityBinding owner) => throw new NotSupportedException();
            public void Unregister() => throw new NotSupportedException();
            public IEntityFadeAnimationSupporter GetFadeAnimationSupporter() => throw new NotSupportedException();
            public bool NeedStop() => false;
        }
    }
}
