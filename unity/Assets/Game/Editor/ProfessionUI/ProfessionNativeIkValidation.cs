using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionNativeIkValidation
    {
        [Serializable] private sealed class Payload { public Case[] cases; }
        [Serializable] private sealed class Case
        {
            public string label;
            public Vector2 target, offset;
            public float parentRotation, childRotation, childLength, mix, expectedParent, expectedChild;
            public bool positive, pair;
        }
        private const string Active = "Zjcs.ProfessionIk.Active";
        private static bool ready;
        private static float maxAngleError;

        static ProfessionNativeIkValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup+120));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active,false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active+".Deadline",0)) throw new TimeoutException("IK fixture did not enter Play Mode.");
                if (!ready || !EditorApplication.isPlaying) return;
                var payload=JsonUtility.FromJson<Payload>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("native-ik-validation.json")));
                Require(payload.cases.Length==384,"all eight original constraints, six clips and eight times");
                int pairs=0, aims=0, zeroMix=0;
                foreach (Case sample in payload.cases)
                {
                    if (sample.mix==0) zeroMix++;
                    if (sample.pair)
                    {
                        pairs++;
                        var result=ProfessionNativeIk.SolvePair(sample.target,sample.offset,sample.childLength,sample.parentRotation,sample.childRotation,sample.positive,sample.mix);
                        Angle(sample.expectedParent,result.ParentRotation,sample.label+"/parent");
                        Angle(sample.expectedChild,result.ChildRotation,sample.label+"/child");
                        if (sample.mix==1) CheckReach(sample,result);
                    }
                    else
                    {
                        aims++;
                        float result=ProfessionNativeIk.Aim(sample.target,sample.parentRotation,sample.mix);
                        Angle(sample.expectedParent,result,sample.label);
                        if (sample.mix==1)
                        {
                            Vector2 actual=new Vector2(Mathf.Cos(result*Mathf.Deg2Rad),Mathf.Sin(result*Mathf.Deg2Rad));
                            Require(Vector2.Dot(actual,sample.target.normalized)>.999999f,"original one-joint direction");
                        }
                    }
                }
                CheckSynthetic();
                Debug.Log($"Profession native idle IK geometry passed: {payload.cases.Length} cases, {pairs} pairs, {aims} single-joint aims, {zeroMix} zero-mix cases; maximum circular angle difference {maxAngleError:R}. Law-of-cosines results match independent circle intersections, reachable/clamped endpoints and bend direction; fractional mix, reflected target coordinates and explicit degenerate input rejection passed. These are geometry fixtures, not original full skeleton poses; live native pose, general constraints, character rendering and profession UI are not covered.");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void CheckSynthetic()
        {
            foreach (bool bend in new[]{false,true})
                foreach (Vector2 target in new[]{new Vector2(3,4),new Vector2(-3,4),new Vector2(20,0),new Vector2(.01f,0)})
                {
                    var sample=new Case{target=target,offset=new Vector2(4,0),childLength=3,positive=bend,mix=1};
                    var solved=ProfessionNativeIk.SolvePair(target,sample.offset,3,0,0,bend,1); CheckReach(sample,solved);
                    var half=ProfessionNativeIk.SolvePair(target,sample.offset,3,30,-20,bend,.5f);
                    var full=ProfessionNativeIk.SolvePair(target,sample.offset,3,30,-20,bend,1);
                    Angle(30+Mathf.DeltaAngle(30,full.ParentRotation)*.5f,half.ParentRotation,"parent fractional mix");
                    Angle(-20+Mathf.DeltaAngle(-20,full.ChildRotation)*.5f,half.ChildRotation,"child fractional mix");
                }
            Require(ProfessionNativeIk.Aim(Vector2.zero,721,0)==721,"zero mix does not solve a coincident target");
            var zero=ProfessionNativeIk.SolvePair(Vector2.zero,Vector2.zero,0,17,19,true,0);
            Require(zero.ParentRotation==17&&zero.ChildRotation==19,"zero mix preserves both angles");
            Throws<NotSupportedException>(()=>ProfessionNativeIk.Aim(Vector2.zero,0,1));
            Throws<NotSupportedException>(()=>ProfessionNativeIk.SolvePair(Vector2.one,Vector2.zero,3,0,0,true,1));
            Throws<ArgumentOutOfRangeException>(()=>ProfessionNativeIk.SolvePair(Vector2.one,Vector2.one,0,0,0,true,1));
            Throws<ArgumentOutOfRangeException>(()=>ProfessionNativeIk.Aim(Vector2.one,0,2));
            Throws<ArgumentException>(()=>ProfessionNativeIk.Aim(new Vector2(float.NaN,1),0,1));
        }
        private static void CheckReach(Case sample,ProfessionNativeIk.Pair result)
        {
            float parent=result.ParentRotation*Mathf.Deg2Rad,child=result.ChildRotation*Mathf.Deg2Rad;
            Vector2 first=Rotate(sample.offset,parent);
            Vector2 second=Rotate(new Vector2(sample.childLength,0),parent+child);
            float radius=Mathf.Clamp(sample.target.magnitude,Mathf.Abs(sample.offset.magnitude-sample.childLength),sample.offset.magnitude+sample.childLength);
            Vector2 expected=sample.target.normalized*radius;
            Require(Vector2.Distance(first+second,expected)<.0002f+.000005f*radius,"IK endpoint must reach the target or nearest allowed radius: "+sample.label);
            float cross=first.x*second.y-first.y*second.x;
            if (Mathf.Abs(cross)>.002f) Require((cross>0)==sample.positive,"IK bend direction: "+sample.label);
        }
        private static Vector2 Rotate(Vector2 v,float angle)
        { float c=Mathf.Cos(angle),s=Mathf.Sin(angle);return new Vector2(c*v.x-s*v.y,s*v.x+c*v.y); }
        private static void Angle(float expected,float actual,string label)
        {
            float difference=Mathf.Abs(Mathf.DeltaAngle(expected,actual));maxAngleError=Mathf.Max(maxAngleError,difference);
            Require(!float.IsNaN(actual)&&difference<=.0002f,label+" angle differs: "+expected.ToString("R")+" vs "+actual.ToString("R"));
        }
        private static void Throws<T>(Action action)where T:Exception
        {try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
        private static void Require(bool condition,string label){if(!condition)throw new Exception(label);}
        private static void Finish(int code){SessionState.SetBool(Active,false);EditorApplication.Exit(code);}
    }
}
