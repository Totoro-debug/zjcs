using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeTerrain.Common;
using Object=UnityEngine.Object;

namespace Zjcs.Editor.Map13MiniMap
{
    public static class OfficialMap13MiniMapViewAcceptance
    {
        private const string Key="Zjcs.Map13.MiniMapView.Running";
        private static int errors;
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");SessionState.SetBool(Key,true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Hook();EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if(!SessionState.GetBool(Key,false))return;errors=0;
            Application.logMessageReceived-=Observe;Application.logMessageReceived+=Observe;
            EditorApplication.playModeStateChanged-=Mode;EditorApplication.playModeStateChanged+=Mode;
        }
        private static void Observe(string message,string trace,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors++;}
        private static void Mode(PlayModeStateChange mode)
        {
            if(mode!=PlayModeStateChange.EnteredPlayMode)return;
            try{Verify();Require(errors==0,"unexpected runtime errors");SessionState.SetBool(Key,false);EditorApplication.Exit(0);}
            catch(Exception error){Debug.LogException(error);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
        }
        private static void Require(bool value,string reason){if(!value)throw new InvalidDataException("Original minimap view: "+reason);}
        private sealed class Controller:IMiniMapCharacterController
        {
            public MyGridTransform MyGridTransform{get;}
            public readonly GridData Data=new GridData();
            public Controller(Pos2d position){MyGridTransform=new GameObject("Native player grid input").AddComponent<MyGridTransform>();Data.Position=position;MyGridTransform.Component=Data;}
            public void Move(Pos2d position){var previous=Data.Position;Data.Position=position;MyGridTransform.GridTransform_OnPosUpdated(Data,previous);}
        }
        private sealed class Manager:IMiniMapCharacterManager
        {
            public IMiniMapCharacterController MyMeUiCharacter{get;private set;}
            public event Action<IMiniMapCharacterController,IMiniMapCharacterController> OnMeMapControllerUpdated;
            public void Switch(IMiniMapCharacterController controller){var previous=MyMeUiCharacter;MyMeUiCharacter=controller;OnMeMapControllerUpdated?.Invoke(previous,controller);}
        }
        private sealed class GridData:ECGridTransformData
        {
            public Pos2d Position;
            public override Pos2d Pos=>Position;
            public override Direction Direction=>Direction.Up;
            public override bool FlipX=>false;
            public override ECGridTransformInfo Info=>null;
            public override Vector3 GridToPosition(Pos2d grid)=>new Vector3(grid.X,grid.Y,0);
            public override Pos2d PositionToGrid(Vector3 pos)=>new Pos2d((int)pos.x,(int)pos.y);
            public override void AddPosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver){throw new InvalidOperationException("The visible-range consumer subscribes to the original client grid event.");}
            public override void RemovePosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver){throw new InvalidOperationException("The visible-range consumer subscribes to the original client grid event.");}
        }
        private sealed class Body:IMiniMapEntityBody
        {
            public Pos2d Pos{get;set;}
            public IEnumerable<MiniMapBodyOffset> BodyRangeInfos{get;set;}
        }
        [Serializable] private sealed class Proof{public string editor;public int initial_cells,moved_cells,rectangle_cases;public bool original_grid_events,controller_replacement,dispose_unsubscribes,body_culling,all_view;}
        private static void Verify()
        {
            var manager=new Manager();var first=new Controller(new Pos2d(20,30));var second=new Controller(new Pos2d(50,80));manager.Switch(first);
            var range=new MapCharacterViewRangeHandler(manager,new Pos2d(19,21));
            var expected=(from x in Enumerable.Range(10,21) from y in Enumerable.Range(19,23) select new Pos2d(x,y)).ToArray();
            Require(range.VisiblePosList.SequenceEqual(expected),"original ceil-half range and X-major cell order");
            int updates=0;range.OnVisibleRangeUpdate+=_=>updates++;
            first.Move(new Pos2d(20,30));Require(updates==0,"same-cell event must retain the original no-update branch");
            first.Move(new Pos2d(21,30));Require(updates==1&&range.VisiblePosList.Count==483&&range.VisiblePosList[0]==new Pos2d(11,19),"original grid movement updates the whole view rectangle");
            int rectangles=0;
            for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)
            {
                var point=new Pos2d(21+x*10,30+y*11);bool inside=Math.Abs(x)<=1&&Math.Abs(y)<=1;
                Require(range.IsPosInViewRange(point)==inside,"inclusive position bounds");
                Require(range.IsRangeInViewRange(point,point)==inside,"inclusive degenerate rectangle overlap");rectangles++;
            }
            Require(range.IsRangeInViewRange(new Pos2d(0,0),new Pos2d(11,19)),"rectangle touching the original corner must remain visible");
            Require(!range.IsRangeInViewRange(new Pos2d(0,0),new Pos2d(10,18)),"rectangle outside the original corner must remain hidden");
            var body=new Body{Pos=new Pos2d(0,30),BodyRangeInfos=new[]{new MiniMapBodyOffset{Pos=new Pos2d(11,0)}}};
            Require(range.IsEntityInViewRange(body),"an original body offset can enter the visible range before its anchor");
            body.BodyRangeInfos=new[]{new MiniMapBodyOffset{Pos=new Pos2d(10,0)}};Require(!range.IsEntityInViewRange(body),"outside body offset remains culled");
            manager.Switch(second);Require(updates==2&&range.IsPosInViewRange(new Pos2d(50,80)),"replacement controller applies its initial grid immediately");
            first.Move(new Pos2d(180,180));Require(updates==2,"old controller subscription removed before replacement");
            manager.Switch(null);second.Move(new Pos2d(51,80));Require(updates==2,"removed controller unsubscribed");
            manager.Switch(first);Require(updates==3,"controller can return after a null interval");range.Dispose();
            first.Move(new Pos2d(181,180));manager.Switch(second);Require(updates==3,"dispose removes both manager and grid subscriptions");
            IMapViewRangeHandler all=new MapAllViewRangeHandler();Require(all.VisiblePosList==null&&all.IsEntityInViewRange(null)&&all.IsPosInViewRange(new Pos2d(-999,999))&&all.IsRangeInViewRange(new Pos2d(-9,-9),new Pos2d(-8,-8)),"original full-map handler is unbounded");all.Dispose();
            Object.DestroyImmediate(first.MyGridTransform.gameObject);Object.DestroyImmediate(second.MyGridTransform.gameObject);
            var output=Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")??throw new InvalidOperationException("Isolated validation directory required.");
            File.WriteAllText(Path.Combine(output,"minimap-view-rules.json"),JsonUtility.ToJson(new Proof{editor=Application.unityVersion,initial_cells=483,moved_cells=483,rectangle_cases=rectangles,original_grid_events=true,controller_replacement=true,dispose_unsubscribes=true,body_culling=true,all_view=true},true));
            Debug.Log("Map13 original minimap visible-range Play Mode passed: 483 ordered cells, exact rectangle/body boundaries, real original MyGridTransform events, controller replacement/null/re-entry, disposal and full-map handler. Formal minimap/camera/input/save and Windows Player remain pending.");
        }
    }
}
