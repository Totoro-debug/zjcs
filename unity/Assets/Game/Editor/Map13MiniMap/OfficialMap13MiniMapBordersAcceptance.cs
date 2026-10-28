using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Editor.Map13MiniMap
{
    public static class OfficialMap13MiniMapBordersAcceptance
    {
        private const string Key="Zjcs.Map13.MiniMapBorders.Running";
        private static IEnumerator routine;private static int errors;
        [Serializable] private sealed class Fixture{public Region[] regions;}
        [Serializable] private sealed class Region{public int id;public Cell[] cells;}
        [Serializable] private sealed class Cell{public int x,y;}
        [Serializable] private sealed class Proof{public string editor;public int regions,cells,edges,polygons,village_vertices,village_area,containment_cases,grid_cases;public bool original_area_traversal,full_source_classes;}
        private static string Workspace=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        private static void Require(bool condition,string detail){if(!condition)throw new InvalidDataException(detail);}
        private static void Near(Vector2 actual,Vector2 expected,string detail)=>Require(Vector2.Distance(actual,expected)<.0001f,detail+": "+actual+" expected "+expected);
        private static void Observe(string condition,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        public static void Run(){Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");errors=0;Application.logMessageReceived+=Observe;SessionState.SetBool(Key,true);EditorApplication.playModeStateChanged+=State;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] private static void Resume(){if(SessionState.GetBool(Key,false)){errors=0;Application.logMessageReceived+=Observe;EditorApplication.playModeStateChanged+=State;}}
        private static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){routine=Verify();EditorApplication.update+=Advance;}}
        private static void Advance(){try{if(routine.MoveNext())return;Require(errors==0,"Runtime errors: "+errors);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(1);}}
        private static float Area(List<Vector2> points){float area=0;for(int i=0;i<points.Count;i++){var a=points[i];var b=points[(i+1)%points.Count];area+=a.x*b.y-a.y*b.x;}return Mathf.Abs(area)/2;}
        private static IEnumerator Verify()
        {
            yield return null;
            var fixture=JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Workspace,"minimap-border-grid-fixture.json")));
            Require(fixture.regions.Length==106,"All original map regions");
            int cells=0,edges=0,containment=0,polygons=0,gridCases=0,villageVertices=0,villageArea=0;
            Direction[] dirs={Direction.Up,Direction.Down,Direction.Left,Direction.Right};
            Pos2d[] steps={new Pos2d(0,1),new Pos2d(0,-1),new Pos2d(-1,0),new Pos2d(1,0)};
            Vector2[] starts={new Vector2(-.5f,.5f),new Vector2(.5f,-.5f),new Vector2(-.5f,-.5f),new Vector2(.5f,.5f)};
            Vector2[] ends={new Vector2(.5f,.5f),new Vector2(-.5f,-.5f),new Vector2(-.5f,.5f),new Vector2(.5f,-.5f)};
            foreach(var region in fixture.regions)
            {
                var positions=region.cells.Select(c=>new Pos2d(c.x,c.y)).ToList();var set=new HashSet<Pos2d>(positions);var removed=new List<Pos2d>();var border=PrivateRegionUtility.GetGridsBorderLineDict(positions,removed);var ordered=border.Keys.ToArray();int index=0;
                foreach(var p in positions)
                {
                    int count=0;for(int i=0;i<4;i++)if(!set.Contains(p+steps[i]))count++;
                    if(count==0){Require(removed.Contains(p),"Interior grid removed: "+region.id);continue;}
                    Require(ordered[index++].Equals(p),"Original reverse Y/X and area grouping order");Require(border[p].Count==count,"Exposed sides only");int side=0;
                    foreach(var pair in border[p])
                    {
                        while(side<4&&set.Contains(p+steps[side]))side++;
                        Require(pair.Key==dirs[side],"Original Up/Down/Left/Right edge order");var center=new Vector2(p.X,p.Y);Near(pair.Value.Item1,center+starts[side],"Native boundary start");Near(pair.Value.Item2,center+ends[side],"Native boundary end");edges++;side++;
                    }
                }
                Require(index==border.Count,"All boundary cells visited");cells+=positions.Count;
                if(region.id==38)
                {
                    var polygon=PrivateRegionUtility.GetGridsBorderPoints(positions);villageVertices=polygon.Count;villageArea=positions.Count;
                    Require(Mathf.Abs(Area(polygon)-positions.Count)<.001f,"Actual village contour area");
                    for(int y=-1;y<=240;y++)for(int x=-1;x<=240;x++){Require(PrivateRegionUtility.IsInsidePolygons(polygon,new Vector2(x,y))==set.Contains(new Pos2d(x,y)),"Actual village polygon contains exactly source cells: "+x+","+y);containment++;}
                    for(int i=0;i<polygon.Count;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Count];Require(a.x==b.x||a.y==b.y,"Village contour remains axis aligned");}polygons++;
                }
                if(region.id%20==0)yield return null;
            }
            // Rotate and reflect an independently specified concave L contour, testing both inset and outset.
            foreach(float offset in new[]{-.2f,0,.2f})for(int rotation=0;rotation<4;rotation++)foreach(int reflection in new[]{-1,1})
            {
                Func<Vector2,Vector2> transform=p=>{p.x*=reflection;for(int i=0;i<rotation;i++)p=new Vector2(-p.y,p.x);return p+new Vector2(17,-13);};
                var positions=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,1)}.Select(transform).Select(p=>new Pos2d((int)p.x,(int)p.y)).ToList();
                var expected=new[]{new Vector2(-.5f+offset,-.5f+offset),new Vector2(1.5f-offset,-.5f+offset),new Vector2(1.5f-offset,.5f-offset),new Vector2(.5f-offset,.5f-offset),new Vector2(.5f-offset,1.5f-offset),new Vector2(-.5f+offset,1.5f-offset)}.Select(transform).ToList();
                var actual=PrivateRegionUtility.GetGridsBorderPoints(positions,null,offset);Require(actual.Count==6,"Concave polygon simplification");foreach(var point in expected)Require(actual.Any(p=>Vector2.Distance(p,point)<.0001f),"Original offset corner: "+point+" actual "+string.Join(";",actual));Require(Mathf.Abs(Area(actual)-Area(expected))<.001f,"Concave offset area");polygons++;
            }
            foreach(var row in new[]{new[]{0f,-.5f,0f},new[]{.2f,.5f,0f},new[]{-.2f,-.5f,0f},new[]{1.3f,1.5f,1f},new[]{-1.3f,-1.5f,-1f}})
            {Near(PrivateRegionUtility.GetGridPos(Vector2.one*row[0]),Vector2.one*row[1],"Original truncation and zero sign");Near(PrivateRegionUtility.GetCloseGridCornerPos(Vector2.one*row[0]),Vector2.one*row[2],"Original closest corner");gridCases+=2;}
            var proof=new Proof{editor=Application.unityVersion,regions=fixture.regions.Length,cells=cells,edges=edges,polygons=polygons,village_vertices=villageVertices,village_area=villageArea,containment_cases=containment,grid_cases=gridCases,original_area_traversal=true,full_source_classes=true};
            File.WriteAllText(Path.Combine(Workspace,"minimap-borders-rules.json"),JsonUtility.ToJson(proof,true));Debug.Log("MAP13_MINIMAP_BORDERS_PASS "+JsonUtility.ToJson(proof));
        }
    }
}
