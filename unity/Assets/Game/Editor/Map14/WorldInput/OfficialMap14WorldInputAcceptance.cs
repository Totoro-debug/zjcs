using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.EntityWorldInput;
using Zjcs.Official.EntityViews;
using Zjcs.Official.LegacyWorldStages;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.NativeMap14WorldInput;
using Zjcs.Offline.World.NativeMap14SharedViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTipDotViews;
using Zjcs.Offline.World.NativeMap14ClientComponents;
using Zjcs.Offline.World.NativeMap14ClientEntities;

using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14WorldInputAcceptance
    {
        private const string Key = "Map14.WorldInput";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            if (Environment.GetEnvironmentVariable("ZJCS_ISOLATED_ENTITY_VIEWS") != "1")
                Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            else Debug.Log("Explicit entity container assembly isolation; full ProjectTools.Validate is not executed.");
            SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
            QualitySettings.renderPipeline = null; PlayerSettings.colorSpace = ColorSpace.Linear;
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 240);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); Hook(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            started = false; errors = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode; EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            Application.logMessageReceived -= Observe;
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".pipeline", ""));
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".quality", ""));
            PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(Key + ".color", 0);
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Observe(string text, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Original entity container check timed out."); EditorApplication.Exit(1); return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Original entity container check").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Steps());
            while (stack.Count > 0)
            {
                object value;
                try
                {
                    var top = stack.Peek(); if (!top.MoveNext()) { stack.Pop(); continue; }
                    value = top.Current;
                    if (value is IEnumerator nested && !(value is CustomYieldInstruction)) { stack.Push(nested); continue; }
                }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return value;
            }
            if (errors == 0)
            {
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Map14 original world input Play Mode passed: team guard order, all four native statue input states/priorities, original player interaction query, redirect identity/lookup/cancellation, original geometry, click arguments, behaviour lookup and sprite-operation lifetime; prior component/index/container/loading/statue display regression passed. Formal pointer dispatch, native root prefabs, UI/save and Player remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string text)
        { if (!value) throw new InvalidDataException("Original entity container: " + text); }
        private static IEnumerator Steps()
        {
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            using (var shared = new NativeMap14SharedAssemblies(catalog))
            using (var configuration = new NativeMap14SharedConfiguration(shared))
            using (var data = new NativeMap14SharedData(shared, configuration))
            {
                yield return data.LoadAll(false);
                using (var root = new NativeMap14SharedRoot(shared, configuration, data, 14))
                {
                    var components = new NativeMap14ClientComponentBindings(shared);
                    var queries = new NativeMap14WorldInputQueries(shared);
                    using (var state = new NativeMap14ClientEntityState(shared, root, data, configuration, 123, () => true,
                        bindComponents: view => { components.Bind(view); queries.Bind(view); }))
                    {
                        Require(queries.GetPosListGridRect(new List<Pos2d>()) == null, "original empty rectangle differs");
                        var rect = queries.GetPosListGridRect(new List<Pos2d> { new Pos2d(3, -2), new Pos2d(-5, 7), new Pos2d(1, 1) });
                        Require(rect.InRange(new Pos2d(-5, -2)) && rect.InRange(new Pos2d(3, 7)) && !rect.InRange(new Pos2d(4, 7)), "original rectangle inclusive bounds differ");
                        var services = new Services();
                        var hosts = new List<GameObject>(); var inputs = new List<WorldStageInput>();
                        var views = new List<NativeMap14SharedViewEntity>();
                        var interaction = Activator.CreateInstance(shared.CommonType("EntityPlayerInteractionComponent"));
                        for (int id = 6570; id <= 6573; id++)
                        {
                            var native = root.Instantiate(data.GetCommonPrefab(id));
                            if (id == 6570) native.GetType().GetMethod("AddComponent", new[] { shared.CommonType("EcComponent") }).Invoke(native, new[] { interaction });
                            var view = state.Wrap(native);
                            state.SetIdentifier(view, new EcEntityIdentifier(14, id)); views.Add(view);
                            var host = new GameObject("Original world input fixture " + id); hosts.Add(host);
                            var entity = host.AddComponent<MyEntity>(); entity.Entity = view;
                            var source = host.AddComponent<MyEntitySource>(); source.Bind();
                            var visible = host.AddComponent<Visibility>();
                            var grid = host.AddComponent<MyGridTransform>(); grid.Bind();
                            var input = host.AddComponent<WorldStageInput>(); inputs.Add(input);
                            input.Geometry = queries; input.EntityState = queries;
                            input.TeamContext = services; input.MapMyCharacterManager = services;
                            input.MapRootManagerProxy = services; input.BaseMapRootManagerProxy = services;
                            input.MapInput = services; input.ResourceLoader = services; input.Presentation = services;
                            services.Entities.Add(view.Id, entity);
                            Require(!input.ClickOverriden && !input.ClickByColliderCheck(), "original collider policy differs");
                            Require(input.ClickByGridCheck(), "active statue was rejected");
                            Require(input.Priority == WorldEntityInputPriority.Lowest, "unowned priority differs");
                            state.Source(view).Ownership = new EntityOwnershipData { Type = EntityOwnershipType.Player, OwnerId = 123 };
                            Require(input.Priority == WorldEntityInputPriority.Medium, "owner priority differs");
                            state.Source(view).Ownership = new EntityOwnershipData { Type = EntityOwnershipType.Player, OwnerId = 124 };
                            Require(input.Priority == WorldEntityInputPriority.Lowest, "other owner priority differs");
                            // Check team rejection happens before shared entity state reads.
                            services.Team = new object(); services.Leader = false; input.EntityState = null;
                            Require(!input.ClickByGridCheck(), "nonleader did not stop at the team guard");
                            services.Leader = true; services.HaveStatus = false;
                            Require(!input.ClickByGridCheck(), "missing team status was accepted");
                            services.HaveStatus = true; services.Allowed = false;
                            Require(!input.ClickByGridCheck() && services.CheckedMap == 14 && services.CheckedContainer == 14, "original team context differs");
                            services.Allowed = true; services.Team = null; input.EntityState = queries;
                            Set(view.Original, "Removing", true); Require(!input.ClickByGridCheck(), "removing entity was accepted");
                            Set(view.Original, "Removing", false); Set(view.Original, "Ended", true);
                            Require(!input.ClickByGridCheck(), "ended entity was accepted"); Set(view.Original, "Ended", false);
                            services.Controlled = new Character { Leader = false };
                            Require(!input.ClickByGridCheck(), "controlled follower was accepted"); services.Controlled = null;
                            visible.Value = false; Require(!input.ClickByGridCheck(), "invisible entity was accepted"); visible.Value = true;
                            Set(view.Stage.Original, "State", -1); Require(!input.ClickByGridCheck(), "inactive stage was accepted");
                            Set(view.Stage.Original, "State", 0); Require(input.ClickByGridCheck(), "restored stage was rejected");
                            var selected = input.GetEntityGridRect(); Require(selected != null, "original choose body is absent");
                            input.OnClick(new Pos2d(11, 12), false);
                            Require(ReferenceEquals(services.Clicked, view.Stage) && services.Position.X == 11 && !services.Logic, "original click arguments differ");
                            input.OnClick(new Pos2d(13, 14), true); Require(services.Position.Y == 14 && services.Logic, "logic click arguments differ");
                            Require(input.GetRedirectInput() == null && input.GetEntitySprite() == null, "no redirect/view fallback differs");
                        }
                        var first = views[0]; var target = views[1];
                        Call(first.Stage.Original, "SetInteractionRedirectTarget", target.Stage.Original);
                        Require(ReferenceEquals(first.Stage.InteractionRedirectStage, target.Stage) && ReferenceEquals(inputs[0].GetRedirectInput(), inputs[1]), "original redirect identity differs");
                        services.Entities.Remove(target.Id); Require(inputs[0].GetRedirectInput() == null, "missing redirect target did not fall back");
                        services.Entities.Add(target.Id, inputs[1].MyEntity);
                        Call(first.Stage.Original, "CancelInteractionRedirectTarget"); Require(inputs[0].GetRedirectInput() == null, "cancel redirect differs");
                        queries.Bind(first); queries.Bind(first);
                        var clientInteraction = hosts[0].AddComponent<MyEntityPlayerInteraction>(); clientInteraction.Bind();
                        Require(ReferenceEquals(clientInteraction.Component.Original, interaction), "original interaction component identity differs");
                        Call(interaction, "AddInteractingPlayer", 123L);
                        Require(!inputs[0].ClickByGridCheck(), "already interacting player was accepted");
                        Call(interaction, "RemoveInteractingPlayer", 123L); Require(inputs[0].ClickByGridCheck(), "removed interaction still blocks input");
                        var behaviour = new Behaviour(); first.BindAdditionalComponent<IMyStageBehaviour>(behaviour);
                        Require(ReferenceEquals(MyStageBehaviourUtils.GetMyStageBehaviour(services, first), behaviour), "shared behaviour fallback differs");
                        inputs[0].GetEntitySprite(); Require(services.SpriteName == "" && services.Operations.Count == 1, "monster -1 sprite request differs");
                        behaviour.Monster = 77; inputs[0].GetEntitySprite();
                        Require(services.MonsterId == 77 && services.SpriteName == "fixture-icon:9", "monster icon query differs");
                        var playerHost = new GameObject("Original player conflict fixture"); hosts.Add(playerHost);
                        var player = playerHost.AddComponent<PlayerInput>(); player.Mine = true;
                        Require(inputs[0].ConflictWithInput(player), "own player conflict differs"); player.Mine = false;
                        Require(!inputs[0].ConflictWithInput(player) && !inputs[0].ConflictWithInput(inputs[1]), "other input conflict differs");
                        foreach (var host in hosts) UnityEngine.Object.Destroy(host);
                        yield return null;
                        Require(services.Operations.All(operation => operation.Disposed == 1), "original input sprite operations were not disposed once");
                    }
                }
            }
            yield return (IEnumerator)typeof(OfficialMap14ClientComponentsAcceptance).GetMethod("Steps", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }
        private static object Call(object owner, string name, params object[] arguments) => owner.GetType().GetMethods()
            .Single(method => method.Name == name && method.GetParameters().Length == arguments.Length &&
                method.GetParameters().Select((p, i) => arguments[i] == null || p.ParameterType.IsInstanceOfType(arguments[i])).All(value => value)).Invoke(owner, arguments);
        private static void Set(object owner, string name, object value) => NativeMap14SharedPrefabs.Set(owner, name, value);
        private sealed class Visibility : MonoBehaviour, IMyVisibility
        { public bool Value = true; public bool IsVisible => Value; public event Action OnVisibilityChanged { add { } remove { } } }
        private sealed class Character : IWorldInputMapCharacter, IWorldInputCharacter
        { public bool Leader; public bool IsLeader => Leader; IWorldInputCharacter IWorldInputMapCharacter.Character => this; }
        private sealed class Behaviour : IMyStageBehaviour
        {
            public int Monster = -1; public AwardDialogType AwardDialogType => AwardDialogType.None;
            public float DelayTime => 0; public int GetMonsterId() => Monster;
            public object GetMonsterList() => null; public int GetCurrentStageId() => -1;
        }
        private sealed class Operation : IWorldInputSpriteOperation
        { public int Disposed; public Sprite Result => null; public void Dispose() { Disposed++; } }
        private sealed class Services : IWorldInputSupport, IWorldInputRootLookup, IWorldInputTeamContext,
            IWorldInputPlayerContext, IWorldInputPresentation, IWorldInputResourceLoader
        {
            public readonly Dictionary<EcEntityIdentifier, MyEntity> Entities = new Dictionary<EcEntityIdentifier, MyEntity>();
            public readonly List<Operation> Operations = new List<Operation>();
            public object Team; public bool Leader = true, HaveStatus = true, Allowed = true;
            public int CheckedMap, MonsterId; public long CheckedContainer; public string SpriteName;
            public Character Controlled; public LegacyWorldStageComponentData Clicked; public Pos2d Position; public bool Logic;
            public int CurrentMapClassId => 14; public long MainPlayerId => 123;
            public object MainTeam => Team; public bool IsLeader => Leader;
            public bool TryGetStatusByContainerId(long containerId, out object status) { CheckedContainer = containerId; status = this; return HaveStatus; }
            public bool TeamCheck(int mapClassId, object status) { Require(ReferenceEquals(status, this), "team status changed"); CheckedMap = mapClassId; return Allowed; }
            public bool TryGetEntity(EcEntityIdentifier id, out MyEntity entity) => Entities.TryGetValue(id, out entity);
            public IWorldInputMapCharacter GetMapCharacterFromController() => Controlled;
            public void ClickStage(LegacyWorldStageComponentData stage, Pos2d position, bool isLogicClick) { Clicked = stage; Position = position; Logic = isLogicClick; }
            public WorldInputMonsterInfo GetMonsterInfo(int id) { MonsterId = id; return new WorldInputMonsterInfo { IconId = 9 }; }
            public string GetMonsterIcon(int id) => "fixture-icon:" + id;
            public string GetMapEntityNameKey(ECViewEntityData entity, int mapClassId) => "fixture:" + entity.ClassId;
            public IWorldInputSpriteOperation LoadSprite(string name) { SpriteName = name; var operation = new Operation(); Operations.Add(operation); return operation; }
        }
        private sealed class PlayerInput : AWorldEntityInput, IWorldPlayerInput
        {
            public bool Mine; public bool IsMyself => Mine; public override bool ClickOverriden => false;
            public override MyEntity MyEntity => null; public override WorldEntityInputPriority Priority => WorldEntityInputPriority.Lowest;
            public override Sprite GetEntitySprite() => null; public override string GetEntityMenuItemKey() => null;
            public override bool ClickByColliderCheck() => false; public override bool ClickByGridCheck() => false;
            public override bool ConflictWithInput(AWorldEntityInput other) => false; public override void OnClick(Pos2d pos, bool logic) { }
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
