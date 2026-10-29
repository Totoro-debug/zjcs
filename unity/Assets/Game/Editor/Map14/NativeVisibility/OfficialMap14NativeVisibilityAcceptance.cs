using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Zjcs.Offline.World.NativeMap14Visibility;
using Zjcs.Offline.World.NativeMap14SharedViews;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Official.World.Map13EntityVisibilityViews;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14NativeVisibilityAcceptance
    {
        private const string Key = "Map14.NativeVisibility";
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
                Debug.Log("Map14 original native visibility projection Play Mode passed: direct original flag reads/writes, unchanged Console Add/Remove and event dispatch, stable projection identity, missing component, fade flag forwarding, view-owned native subscription disposal; assembled four-statue roots, original visibility handlers/requests, actual display frames and cost/radar/input/container/loading regressions passed. Formal visibility handler services/map/player/UI/input/save and Windows Player remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string text)
        { if (!value) throw new InvalidDataException("Original entity container: " + text); }

        private static IEnumerator Steps()
        {
            var projections=new List<NativeMap14VisibilityData>();
            var previous=OfficialMap14StatueEntityAcceptance.VisibilityDataFactory;
            Require(previous==null,"visibility source fixture is already configured");
            OfficialMap14StatueEntityAcceptance.VisibilityDataFactory=(shared,view)=>
            {
                var native=NativeMap14VisibilityData.Bind(shared,view);
                Require(native!=null && ReferenceEquals(native,view.GetComponent<Map13VisibilityData>()) && ReferenceEquals(native,NativeMap14VisibilityData.Bind(shared,view)),"original visibility projection identity differs");
                Require(ReceiverCount(native)==1,"duplicate bind registered another native receiver");
                var receiver=new Receiver(native);native.OnClientVisibilityUpdate+=receiver;
                native.ClientVisibilityType=ClientVisibilityType.NPCVisibility;
                Require(receiver.Count==0 && Convert.ToInt32(NativeMap14SharedPrefabs.Get(native.Original,"ClientVisibilityType"))==8,"direct original visibility field write fired an event or was not applied");
                native.AddClientVisibilityRequest(ClientVisibilityType.ConditionVisibility);
                Require(receiver.Count==1 && native.ClientVisibilityType==(ClientVisibilityType.NPCVisibility|ClientVisibilityType.ConditionVisibility),"native Add flags/event differed");
                native.Original.GetType().GetMethod("AddClientVisibilityRequest").Invoke(native.Original,new[]{Enum.ToObject(shared.CommonType("ClientVisibilityType"),0)});
                Require(receiver.Count==2,"original unchanged flags did not still dispatch the event");
                native.RemoveClientVisibilityType(ClientVisibilityType.ConditionVisibility);
                Require(receiver.Count==3 && native.ClientVisibilityType==ClientVisibilityType.NPCVisibility,"native Remove flags/event differed");
                native.OnClientVisibilityUpdate-=receiver;
                native.IsShowNeedFadeIn=true;Require((bool)NativeMap14SharedPrefabs.Get(native.Original,"IsShowNeedFadeIn"),"fade flag write missed native component");
                native.Original.GetType().GetField("IsShowNeedFadeIn").SetValue(native.Original,false);
                Require(!native.IsShowNeedFadeIn,"direct native fade flag change was hidden by a stale copy");
                if(projections.Count==0)
                {
                    using(var empty=new NativeMap14SharedViewEntity(shared,Activator.CreateInstance(shared.CommonType("EcEntity"))))
                        Require(NativeMap14VisibilityData.Bind(shared,empty)==null,"absent original visibility component created state");
                }
                projections.Add(native);return native;
            };
            try
            {
                yield return (IEnumerator)typeof(OfficialMap14StatueEntityAcceptance).GetMethod("Steps",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
                Require(projections.Count==4,"four original statue visibility components were not projected");
                foreach(var native in projections)
                {
                    Require(ReceiverCount(native)==0,"view disposal retained an original visibility receiver");
                    bool disposed=false;try{var flags=native.ClientVisibilityType;}catch(ObjectDisposedException){disposed=true;}
                    Require(disposed,"view disposal did not release its visibility projection");
                    native.Dispose();
                }
            }
            finally
            {
                OfficialMap14StatueEntityAcceptance.VisibilityDataFactory=previous;
                foreach(var native in projections)native.Dispose();
            }
        }
        private static int ReceiverCount(NativeMap14VisibilityData native)
        {
            var dispatcher=NativeMap14SharedPrefabs.Get(native.Original,"OnClientVisibilityUpdate");
            for(var type=dispatcher.GetType();type!=null;type=type.BaseType)
            {
                var field=type.GetField("mRecordEventReceivers",BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly);
                if(field!=null)return ((ICollection)field.GetValue(dispatcher)).Count;
            }
            throw new MissingFieldException("Original visibility dispatcher receiver storage is absent.");
        }
        private sealed class Receiver:IEcEntityClientVisibilityUpdatedEventReceiver
        {
            private readonly NativeMap14VisibilityData owner;public int Count;
            public Receiver(NativeMap14VisibilityData owner){this.owner=owner;}
            public void EntityClientVisibilityComponent_Updated(Map13VisibilityData value)
            {Require(ReferenceEquals(value,owner),"original visibility event did not preserve the projected owner");Count++;}
        }
        private sealed class Runner:MonoBehaviour{}
    }
}
