using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using Zjcs.Official.ProfessionUI;
using Object=UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionOffScreenPlayerValidation
    {
        private const string Active="Zjcs.ProfessionOffscreen.Active";
        private static bool ready;private static int phase,afterFrame;private static GameObject root;private static OffScreenUISpinePlayer player;
        private static PartProbe part;private static GraphicProbe self,child,inactive,grandchild;
        static ProfessionOffScreenPlayerValidation(){EditorApplication.update+=Update;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode)ready=true;};}
        public static void Run()
        {
            try{ProfessionValidationWorkspace.AssertIsolatedProject();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool(Active,true);SessionState.SetFloat(Active+".Deadline",(float)(EditorApplication.timeSinceStartup+120));EditorApplication.EnterPlaymode();}
            catch(Exception exception){Debug.LogException(exception);Finish(1);}
        }
        private static void Update()
        {
            if(!SessionState.GetBool(Active,false))return;
            try
            {
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+".Deadline",0))throw new TimeoutException("Offscreen player fixture did not finish.");
                if(!ready||!EditorApplication.isPlaying)return;
                if(phase==0)
                {
                    root=new GameObject("Original offscreen update fixture");root.SetActive(false);part=root.AddComponent<PartProbe>();self=root.AddComponent<GraphicProbe>();
                    child=Create("active child",root.transform,true);grandchild=Create("active grandchild",child.transform,true);inactive=Create("inactive child",root.transform,false);
                    player=root.AddComponent<OffScreenUISpinePlayer>();root.SetActive(true);
                    part.OnSkinChanged.Invoke();CancellationToken first=Token().Token;part.OnSkinChanged.Invoke();Require(first.IsCancellationRequested,"replacement cancels original wait before starting another");
                    Require(player.ListUISpines.Count==0&&child.Bounds==0&&self.Mode==2,"source update waits two frames");afterFrame=Time.frameCount+3;phase=1;return;
                }
                if(Time.frameCount<afterFrame)return;
                if(phase==1)
                {
                    Require(player.ListUISpines.Count==4&&ReferenceEquals(player.ListUISpines[0],self)&&ReferenceEquals(player.ListUISpines[1],child)&&ReferenceEquals(player.ListUISpines[2],grandchild)&&ReferenceEquals(player.ListUISpines[3],inactive),"source hierarchy order includes inactive graphics");
                    Require(self.Mode==0&&child.Mode==0&&grandchild.Mode==0&&inactive.Mode==0,"source offscreen mode zero on all live graphics");
                    Require(self.Bounds==0&&child.Bounds==1&&grandchild.Bounds==1&&inactive.Bounds==0&&Token()==null,"only active non-root bounds and completion cancels/disposes/clears token");
                    part.OnSkinChanged.Invoke();CancellationToken wait=Token().Token;root.SetActive(false);Require(wait.IsCancellationRequested&&Token()==null,"disable cancels and clears pending wait");
                    part.OnSkinChanged.Invoke();Require(Token()==null,"inactive hierarchy does not schedule");afterFrame=Time.frameCount+3;phase=2;return;
                }
                if(phase==2)
                {
                    Require(child.Bounds==1&&grandchild.Bounds==1,"cancelled wait never resizes");root.SetActive(true);player.enabled=false;part.OnSkinChanged.Invoke();
                    Require(Token()!=null,"original guard uses hierarchy activity, not component enabled state");afterFrame=Time.frameCount+3;phase=3;return;
                }
                Require(child.Bounds==2&&grandchild.Bounds==2&&self.Bounds==0&&inactive.Bounds==0,"source disabled-component event still updates active hierarchy");
                Object.DestroyImmediate(player);part.OnSkinChanged.Invoke();Require(child.Bounds==2,"destroy removes original runtime listener");
                Object.DestroyImmediate(root);root=null;
                Debug.Log("Profession original offscreen player passed in real Play Mode frames: replacement cancellation, two-frame timing, ordered hierarchy collection including inactive children, mode zero on all graphics, active non-root bounds only, disable cancellation, inactive hierarchy guard, original disabled-component behavior, token disposal and destroy unsubscription. Graphic/part endpoints are fixtures; source character rendering and full profession UI are not validated.");Finish(0);
            }
            catch(Exception exception){Debug.LogException(exception);Finish(1);}
        }
        private static GraphicProbe Create(string name,Transform parent,bool active){var go=new GameObject(name);go.transform.SetParent(parent,false);var graphic=go.AddComponent<GraphicProbe>();go.SetActive(active);return graphic;}
        private static CancellationTokenSource Token()=>(CancellationTokenSource)typeof(OffScreenUISpinePlayer).GetField("mCancellationTokenSource",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(player);
        public sealed class PartProbe:MonoBehaviour,IProfessionOffScreenPartHandler{public UnityEvent OnSkinChanged{get;}=new UnityEvent();}
        public sealed class GraphicProbe:MonoBehaviour,IProfessionOffScreenGraphic
        {public int Mode=2,Bounds;public Component Instance=>this;public int updateWhenInvisible{get=>Mode;set=>Mode=value;}public void MatchRectTransformWithBounds(){Bounds++;}}
        private static void Require(bool condition,string label){if(!condition)throw new Exception(label);}
        private static void Finish(int code){SessionState.SetBool(Active,false);if(root)Object.DestroyImmediate(root);EditorApplication.Exit(code);}
    }
}
