using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleTrackPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;
using Object = UnityEngine.Object;
using SampledTrack = Zjcs.Official.BattleTrackPlayables.AnimationTrack;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunTracks()
        {
            RunScenePlayerAppearance(); SessionState.SetBool(Key + ".tracks", true);
        }

        private static IEnumerator CheckTracks(Zjcs.Official.CombatAppearance.PlayerAppearance player)
        {
            var actor=player.gameObject; var native=actor.GetComponent<ProfessionNativeSceneRenderer>();
            var scope=new GameObject("Original complete native track graph scope");
            var target=new GameObject("Explicit track endpoint fixture"); target.transform.SetParent(scope.transform,false);
            target.transform.position=actor.transform.position+new Vector3(1.5f,.8f,0);
            var center=new MyPlayableCenter(); var factory=new StatusMaterialFactory(center);
            var previousFactory=StoryPlayableInstantiation.Resolve; StoryPlayableInstantiation.Resolve=_=>factory;
            var manager=new MyPlayableManager { ScopeRoot=scope,MyPlayableCenter=center,InstanceFactory=factory,
                LowMemory=new Zjcs.Official.StoryPlayables.GAction() };
            var originalPosition=actor.transform.position;
            var previousCaptureDelta=Time.captureDeltaTime;
            void Draw()
            {
                native.LateUpdateMesh();
                foreach(var follower in actor.GetComponentsInChildren<SkeletonRendererFollower>(true))follower.ForceUpdate();
                foreach(var weapon in actor.GetComponentsInChildren<ProfessionNativeSceneWeaponRenderer>(true))weapon.UpdateAnimationAndMesh();
            }
            Draw(); var geometry=actor.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            var bounds=geometry[0].bounds;foreach(var r in geometry.Skip(1))bounds.Encapsulate(r.bounds);
            var arrivalBounds=bounds;arrivalBounds.center+=target.transform.position-originalPosition+new Vector3(.8f,.4f,0);
            bounds.Encapsulate(arrivalBounds);bounds.Encapsulate(target.transform.position);
            string label="track-"+native.State.Source.Sex+"-"+player.AppearanceRuntimeData.SkinMap[SkinType.pWeapon];
            CaptureSceneRendererCamera(bounds,label+"-visibility");Draw();
            IEnumerator Pump(int count,float delta)
            {
                Time.captureDeltaTime=delta;
                for(int i=0;i<count;i++){yield return null;center.Update(delta);center.LateUpdate();}
            }
            try
            {
                Time.captureDeltaTime=.001f;
                // Capture delta changes take effect on the next engine frame.
                yield return null;
                manager.Initialize(); CheckTrackCurves(scope);
                foreach(var entry in new[]{(10950,"playable10951_2"),(11170,"playable11171")})
                {
                    var prefab=Resources.Load<GameObject>("Combat/Tracks/Prefabs/EcEntity/Fight/FightSkill/"+entry.Item1+"/"+entry.Item2);
                    Require(prefab,"complete original projectile graph "+entry.Item1);
                    var graph=manager.Play(prefab,actor,target);yield return Pump(3,.001f);
                    var leaf=graph.GetComponentInChildren<MyPlayableTrack>(true);
                    Require(leaf.myApplyComponent is Transform&&leaf.myTrack&&leaf.mySource==actor&&leaf.myTarget==target,
                        "original projectile binds its own transform and complete native curve");
                    CheckTrackPosition(leaf,false);Draw();CaptureSceneRendererCamera(bounds,label+"-"+entry.Item1+"-start");
                    var oldDuration=leaf.GetDuration();
                    if(leaf.myFollowTarget)
                    {
                        target.transform.position+=new Vector3(.4f,.2f,0);yield return Pump(2,.001f);
                        Require(leaf.GetDuration()!=oldDuration,"original dynamic target updates duration at fixed speed");
                        CheckTrackPosition(leaf,false);
                    }
                    yield return Pump(3,.02f);CheckTrackPosition(leaf,false);
                    Require(graph.GetComponentsInChildren<ParticleSystem>(true).Sum(p=>p.particleCount)>0,
                        "native particles share the controlled frame clock with trajectory "+entry.Item1);
                    Draw();CaptureSceneRendererCamera(bounds,label+"-"+entry.Item1+"-advanced");
                    graph.Stop(true);yield return Pump(3,.001f);
                    var replay=manager.Play(prefab,actor,target);yield return Pump(3,.001f);
                    Require(ReferenceEquals(graph,replay),"complete native projectile graph actually reuses its pool instance");
                    CheckTrackPosition(leaf,false);replay.Stop(true);yield return Pump(3,.001f);
                    var fixture=Object.Instantiate(prefab,scope.transform);fixture.SetActive(false);
                    var fixtureLeaf=fixture.GetComponentInChildren<MyPlayableTrack>(true);
                    var probe=fixture.AddComponent<TrackTriggerProbe>();
                    fixtureLeaf.myTrackTriggerList=new List<MyPlayableTriggerTimeCfg>{
                        new MyPlayableTriggerTimeCfg{myTime=.5f,myTrigger=probe},new MyPlayableTriggerTimeCfg{myTime=1,myTrigger=probe}};
                    var triggered=manager.Play(fixture,actor,target);yield return Pump(2,.00001f);
                    var instanceProbe=triggered.GetComponent<TrackTriggerProbe>();
                    Require(instanceProbe.Calls==0,"track fraction triggers wait for their thresholds");
                    triggered.Stop(true);yield return Pump(2,.001f);
                    Require(instanceProbe.Calls==2,"original reliable stop delivers each remaining track trigger once");
                    triggered.GetComponentInChildren<MyPlayableTrack>(true).StopBeforeResetData();
                    Require(instanceProbe.Calls==2,"reset boundary cannot duplicate delivered track triggers");
                }
                var movement=Resources.Load<GameObject>("Combat/Tracks/Prefabs/EcEntity/Fight/FightSkill/10580/playable10584_move");
                Require(movement,"complete original actor movement graph");
                var move=manager.Play(movement,actor,target);yield return Pump(4,.01f);
                var moving=move.GetComponentInChildren<MyPlayableTrack>(true);
                Require(moving.myApplyComponent==actor.transform&&moving.myFixDuration&&Mathf.Abs(moving.GetDuration()-.2f)<.0001f,
                    "original movement graph selects source Transform and preserves fixed duration");
                Require(Vector3.Distance(originalPosition,actor.transform.position)>.01f,"actual original graph moves the native character");
                Draw();CaptureSceneRendererCamera(bounds,label+"-actor-moving");
                yield return Pump(30,.01f);
                Require(Vector3.Distance(actor.transform.position,target.transform.position)<.0001f,"actual movement reaches its original curve endpoint");
                Draw();CaptureSceneRendererCamera(bounds,label+"-actor-arrived");
                move.Stop(true);yield return Pump(3,.001f);actor.transform.position=originalPosition;
                var oldParent=actor.transform.parent;var oldLocalPosition=actor.transform.localPosition;
                var oldLocalRotation=actor.transform.localRotation;var oldLocalScale=actor.transform.localScale;
                var containerHost=new GameObject("Inactive original coordinate container fixture");containerHost.SetActive(false);
                var container=containerHost.AddComponent<Zjcs.Official.EntityContainers.MyContainer>();
                var slot=new GameObject("Original entity slot coordinate space").transform;slot.SetParent(scope.transform,false);
                slot.position=new Vector3(4,2,0);slot.rotation=Quaternion.Euler(0,0,25);slot.localScale=new Vector3(.8f,1.2f,1);
                container.myEntitySlot=slot;
                Require(!actor.GetComponent<MyEntity>()&&!actor.GetComponent<MyTransform>(),"diagnostic character has no preexisting entity wrapper");
                var entity=actor.AddComponent<MyEntity>();entity.myContainer=container;
                var model=actor.AddComponent<MyTransform>();int updates=0;model.OnPosUpdated+=_=>updates++;
                try
                {
                    actor.transform.SetParent(slot,true);
                    var localMove=manager.Play(movement,actor,target);yield return Pump(4,.01f);
                    var localLeaf=localMove.GetComponentInChildren<MyPlayableTrack>(true);
                    Require(localLeaf.myApplyComponent==model&&updates>0,"original MyTransform takes priority and emits position notifications");
                    CheckTrackPosition(localLeaf,true);
                    yield return Pump(30,.01f);
                    Require(Vector3.Distance(actor.transform.position,target.transform.position)<.0001f,
                        "original real container converts world endpoints into rotated and scaled entity-slot coordinates");
                    localMove.Stop(true);yield return Pump(3,.001f);
                }
                finally
                {
                    actor.transform.SetParent(oldParent,false);actor.transform.localPosition=oldLocalPosition;
                    actor.transform.localRotation=oldLocalRotation;actor.transform.localScale=oldLocalScale;
                    Object.DestroyImmediate(model);Object.DestroyImmediate(entity);Object.DestroyImmediate(containerHost);
                }
                Debug.Log("[Original tracks] "+label+": complete native graphs, curve consumption, dynamic target, actual actor movement, reliable stop and pool replay passed.");
            }
            finally
            {
                Time.captureDeltaTime=previousCaptureDelta;
                manager.Dispose();center.RemoveAll();StoryPlayableInstantiation.Resolve=previousFactory;
                actor.transform.position=originalPosition;Draw();Object.DestroyImmediate(scope);
            }
        }

        private static void CheckTrackPosition(MyPlayableTrack leaf,bool local)
        {
            Vector3 start=Private<Vector3>(leaf,"mStartPos"),end=Private<Vector3>(leaf,"mEndPos");
            bool flip=Private<bool>(leaf,"mFlip");
            float t=Mathf.Min(1,leaf.Time/leaf.GetDuration());
            leaf.myTrack.Evaluate(t*leaf.myTrack.GetDuration(),start,end,out var point,out _,out _,leaf.myXScaleByRange,flip?-1:1);
            if(Private<bool>(leaf,"mIsZEquipY"))point.z=point.y;
            Require(Vector3.Distance(local?leaf.myApplyComponent.transform.localPosition:leaf.myApplyComponent.transform.position,point)<.0001f,
                "full graph consumes its original sampled curve at the actual playable time");
        }

        private static void CheckTrackCurves(GameObject scope)
        {
            var go=new GameObject("Original trajectory data branch fixture");go.transform.SetParent(scope.transform,false);
            var sampled=go.AddComponent<SampledTrack>();sampled.myFrameCount=3;
            sampled.myPosList=new[]{Vector3.zero,Vector3.right,Vector3.right*2};
            sampled.myRotationList=new[]{Quaternion.identity,Quaternion.identity,Quaternion.identity};
            sampled.myScaleList=new[]{Vector3.one,Vector3.one,Vector3.one};
            sampled.Evaluate(.5f/60,Vector3.up,out var p,out _,out _);
            Require(Vector3.Distance(p,Vector3.right*.5f)<.0001f,"original sampled track interpolates between 60Hz keys");
            sampled.myLoopType=MyTrack.LoopType.Loop;sampled.Evaluate(4f/60,Vector3.up,out p,out _,out _);
            Require(Vector3.Distance(p,Vector3.right)<.0001f,"original sampled track wraps by configured frame count");
            sampled.myLoopType=MyTrack.LoopType.Pingpong;sampled.Evaluate(4f/60,Vector3.up,out p,out _,out _);
            Require(Vector3.Distance(p,Vector3.right*2)<.0001f,"original sampled track pingpong reverses its frame index");
            var parabola=go.AddComponent<ParabolicTrack>();parabola.Evaluate(.5f,Vector3.up,out p,out _,out _);
            Require(Mathf.Abs(p.y-1.725f)<.0001f,"original one-shot parabola retains configured gravity");
            var bounce=go.AddComponent<ParabolicTrack>();bounce.myLoopType=MyTrack.LoopType.Pingpong;
            bounce.Evaluate(.5f,Vector3.up,out p,out _,out _);
            Require(Mathf.Abs(p.y-1.5f)<.0001f,"original bounce uses first bounce height and baseline displacement");
            var curve=Resources.Load<GameObject>("Combat/Tracks/Prefabs/EcEntity/Fight/FightSkill/10580/track10584").GetComponent<MyCurveTrack>();
            curve.Evaluate(.5f,new Vector3(2,3,0),new Vector3(6,3,0),out p,out _,out _);
            Require(Vector3.Distance(p,new Vector3(3,3,0))<.0001f,"native original Hermite curve reaches quarter displacement at half time");
        }

        public sealed class TrackTriggerProbe : MyPlayableTrigger
        {
            public int Calls;
            public override void Play(MyPlayable playable) { Calls++; }
        }

    }
}
