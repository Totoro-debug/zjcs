using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionNativeClippingValidation
    {
        [Serializable] private sealed class Payload { public Body[] bodies; }
        [Serializable] private sealed class Body { public string sex;public ProfessionNativeBonePose.Bone[] bones;public int slotCount;public Clip[] clips; }
        [Serializable] private sealed class Clip { public string name;public int slotBone,count,end;public float scale;public float[] vertices,coefficients;public Vector2[] boundary;public Case[] cases; }
        [Serializable] private sealed class Case { public Vector2[] vertices,uvs,boundary;public double area;public Vector2 center;public bool reverse; }
        private const string Active="Zjcs.ProfessionNativeClipping.Active";
        private static bool ready;
        static ProfessionNativeClippingValidation()
        {
            EditorApplication.update+=Update;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode)ready=true;};
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                SessionState.SetBool(Active,true);SessionState.SetFloat(Active+".Deadline",(float)(EditorApplication.timeSinceStartup+120));EditorApplication.EnterPlaymode();
            }
            catch(Exception exception){Debug.LogException(exception);Finish(1);}
        }
        private static void Update()
        {
            if(!SessionState.GetBool(Active,false))return;
            try
            {
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+".Deadline",0))throw new TimeoutException("Clipping fixture did not enter Play Mode.");
                if(!ready||!EditorApplication.isPlaying)return;
                var source=JsonUtility.FromJson<Payload>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("native-clipping-validation.json")));
                int boundaries=0,triangles=0,vertices=0;double maxArea=0,maxCenter=0;float maxUv=0;
                foreach(Body body in source.bodies)
                {
                    var pose=new ProfessionNativeBonePose(body.bones);
                    foreach(Clip clip in body.clips)
                    {
                        var attachment=new ProfessionNativeClippingAttachment(clip.name,clip.slotBone,body.bones.Length,clip.vertices,clip.count,clip.end,body.slotCount,clip.scale);
                        Vector2[] boundary=new Vector2[attachment.VertexCount];attachment.ComputeVertices(pose,boundary);
                        foreach(Case sample in clip.cases)
                        {
                            var input=(Vector2[])boundary.Clone();if(sample.reverse)Array.Reverse(input);
                            var clipper=new ProfessionNativeConvexClipper(input);Require(clipper.HasArea,"original clipping quad has area");
                            var output=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
                            clipper.AppendTriangle(sample.vertices[0],sample.vertices[1],sample.vertices[2],sample.uvs[0],sample.uvs[1],sample.uvs[2],output,uv,indices);
                            Require(output.Count==uv.Count&&indices.Count%3==0,"clipped output attributes and triangles");
                            double area=0,cx=0,cy=0,orientation=Cross(sample.vertices[0],sample.vertices[1],sample.vertices[2]);
                            for(int i=0;i<indices.Count;i+=3)
                            {
                                Vector2 a=output[indices[i]],b=output[indices[i+1]],c=output[indices[i+2]];double signed=Cross(a,b,c);
                                Require(signed*orientation>=0,"source triangle winding");double portion=Math.Abs(signed)*.5;area+=portion;cx+=portion*(a.x+b.x+c.x)/3;cy+=portion*(a.y+b.y+c.y)/3;
                            }
                            double error=Math.Abs(area-sample.area);maxArea=Math.Max(maxArea,error);Require(error<=.0002+.00003*sample.area,"GEOS intersection area; actual="+area+", expected="+sample.area);
                            if(sample.area>1e-7)
                            {
                                Require(area>0,"nonempty independent reference");double centerError=Math.Max(Math.Abs(cx/area-sample.center.x),Math.Abs(cy/area-sample.center.y));maxCenter=Math.Max(maxCenter,centerError);
                                Require(centerError<=.0002+.00001*Math.Max(Math.Abs(sample.center.x),Math.Abs(sample.center.y)),"GEOS intersection centroid");
                            }
                            foreach(int i in indices)
                            {
                                Vector2 p=output[i];float[] k=clip.coefficients;var expected=new Vector2(k[0]*p.x+k[1]*p.y+k[2],k[3]*p.x+k[4]*p.y+k[5]);
                                float difference=Mathf.Max(Mathf.Abs(uv[i].x-expected.x),Mathf.Abs(uv[i].y-expected.y));maxUv=Mathf.Max(maxUv,difference);Require(difference<.000003f,"affine source UV at clipped vertex");
                                float distance=float.PositiveInfinity;
                                for(int edge=0;edge<sample.boundary.Length;edge++)distance=Mathf.Min(distance,Distance(p,sample.boundary[edge],sample.boundary[(edge+1)%sample.boundary.Length]));
                                Require(distance<.0005f,"GEOS intersection boundary vertex");
                            }
                            triangles++;vertices+=output.Count;
                        }
                        boundaries++;
                    }
                }
                Synthetic();Debug.Log($"Profession native convex clipping passed: {boundaries} original boundaries, {triangles} intersections, {vertices} clipped output vertices; max GEOS area difference {maxArea:G9}, centroid difference {maxCenter:G9}, UV difference {maxUv:G9}. Both boundary orientations, source winding, inside/outside/partial triangles, boundary positions, UV interpolation, ownership, degenerate and invalid input behavior pass. Synthetic geometry only; source constraints, end-slot draw lifecycle and full profession UI remain unvalidated.");Finish(0);
            }
            catch(Exception exception){Debug.LogException(exception);Finish(1);}
        }
        private static void Synthetic()
        {
            var boundary=new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)};var clipper=new ProfessionNativeConvexClipper(boundary);boundary[0]=new Vector2(50,50);
            var output=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            clipper.AppendTriangle(new Vector2(-.5f,-.5f),new Vector2(.5f,-.5f),new Vector2(0,.5f),Vector2.zero,Vector2.right,Vector2.up,output,uv,indices);Require(indices.Count==3,"boundary ownership and fully contained triangle");
            var empty=new ProfessionNativeConvexClipper(new[]{Vector2.zero,Vector2.right,Vector2.right*2});Require(!empty.HasArea,"degenerate boundary");
            empty.AppendTriangle(Vector2.zero,Vector2.right,Vector2.up,Vector2.zero,Vector2.right,Vector2.up,output,uv,indices);Require(indices.Count==3,"degenerate boundary appends nothing");
            bool concave=false;try{new ProfessionNativeConvexClipper(new[]{Vector2.zero,Vector2.right,Vector2.one*.2f,Vector2.up});}catch(NotSupportedException){concave=true;}Require(concave,"explicit concave rejection");
            bool finite=false;try{new ProfessionNativeConvexClipper(new[]{Vector2.zero,Vector2.right,new Vector2(float.NaN,1)});}catch(ArgumentException){finite=true;}Require(finite,"nonfinite boundary rejection");
        }
        private static double Cross(Vector2 a,Vector2 b,Vector2 c){return ((double)b.x-a.x)*((double)c.y-a.y)-((double)b.y-a.y)*((double)c.x-a.x);}
        private static float Distance(Vector2 p,Vector2 a,Vector2 b){Vector2 edge=b-a;float t=edge.sqrMagnitude==0?0:Mathf.Clamp01(Vector2.Dot(p-a,edge)/edge.sqrMagnitude);return Vector2.Distance(p,a+edge*t);}
        private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException("Native clipping validation failed: "+reason);}
        private static void Finish(int code){SessionState.SetBool(Active,false);EditorApplication.Exit(code);}
    }
}
