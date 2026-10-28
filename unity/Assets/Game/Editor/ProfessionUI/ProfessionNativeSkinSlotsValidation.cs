using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionNativeSkinSlotsValidation
    {
        [Serializable] private sealed class Payload { public Body[] cases; }
        [Serializable] private sealed class Body
        {
            public string sex; public int slotCount, boneCount, constraintCount, identityCount;
            public Skin[] skins; public string[] setup; public Scenario[] scenarios;
        }
        [Serializable] private sealed class Skin { public string name; public Entry[] entries; public int[] bones, constraints; }
        [Serializable] private sealed class Entry { public int slot, id; public string key; }
        [Serializable] private sealed class Scenario { public int first, second; public int[] initial, firstState, before, swapped, afterNull, fromNull, reset; }
        private const string Active="Zjcs.ProfessionNativeSkinSlots.Active";
        private static bool ready;
        static ProfessionNativeSkinSlotsValidation()
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
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+".Deadline",0))throw new TimeoutException("Skin/slot fixture did not enter Play Mode.");
                if(!ready||!EditorApplication.isPlaying)return;
                var source=JsonUtility.FromJson<Payload>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("skin-slots-validation.json")));
                Require(source.cases.Length==2,"both original slot/skin sources");int checks=0,transitions=0;
                foreach(Body body in source.cases)
                {
                    object[] identities=Enumerable.Range(0,body.identityCount+1).Select(_=>new object()).ToArray();
                    var native=body.skins.Select(s=>new ProfessionNativeSkin(s.name,body.slotCount,body.boneCount,body.constraintCount,
                        s.entries.Select(e=>new ProfessionNativeSkin.Entry(e.slot,e.key,identities[e.id])).ToArray(),s.bones,s.constraints)).ToArray();
                    var catalog=new ProfessionNativeSkinCatalog(native);
                    foreach(Scenario scenario in body.scenarios)
                    {
                        var slots=new ProfessionNativeSkinSlots(catalog,body.setup);Check(slots,scenario.initial,identities);checks+=slots.Count;
                        slots.SetSkin(native[scenario.first]);Require(ReferenceEquals(slots.Skin,native[scenario.first]),"active source skin identity");
                        Check(slots,scenario.firstState,identities);checks+=slots.Count;
                        for(int i=0;i<slots.Count;i++)slots.SetAttachment(i,scenario.before[i]<0?null:identities[scenario.before[i]]);
                        Check(slots,scenario.before,identities);checks+=slots.Count;
                        slots.SetSkin(native[scenario.second]);Check(slots,scenario.swapped,identities);checks+=slots.Count;
                        slots.SetSkin(null);Require(slots.Skin==null,"null active skin");Check(slots,scenario.afterNull,identities);checks+=slots.Count;
                        slots.SetSkin(native[scenario.second]);Check(slots,scenario.fromNull,identities);checks+=slots.Count;
                        slots.ResetSlots();Check(slots,scenario.reset,identities);checks+=slots.Count;transitions+=4;
                        foreach(ProfessionNativeSkin.Entry entry in native[scenario.second].Entries)
                            Require(ReferenceEquals(native[scenario.second].GetAttachment(entry.Slot,entry.Key),entry.Attachment),"enumeration retains compound keys and original object identities");
                    }
                }
                Synthetic();Debug.Log($"Profession native skin/slot identity passed: {transitions} transitions, {checks} source slot states. Initial setup-visible attachments, old-identity migration, missing replacements, manual/null overrides, null skin, active/default setup resolution, ordinal keys, ownership and layout rejection pass. Bone constraints, rendered characters and full profession UI are not validated.");Finish(0);
            }
            catch(Exception exception){Debug.LogException(exception);Finish(1);}
        }
        private static void Check(ProfessionNativeSkinSlots state,int[] expected,object[] identities)
        {
            Require(expected.Length==state.Count,"source slot layout");
            for(int i=0;i<expected.Length;i++)Require(ReferenceEquals(state.Attachment(i),expected[i]<0?null:identities[expected[i]]),"source attachment identity at slot "+i);
        }
        private static void Synthetic()
        {
            object a=new object(),b=new object(),fallback=new object(),manual=new object();
            var defaults=new ProfessionNativeSkin("default",3,1,0,new[]{new ProfessionNativeSkin.Entry(1,"head",fallback)},Array.Empty<int>(),Array.Empty<int>());
            var first=new ProfessionNativeSkin("first",3,1,0,new[]{new ProfessionNativeSkin.Entry(0,"body",a),new ProfessionNativeSkin.Entry(2,"other",a)},Array.Empty<int>(),Array.Empty<int>());
            var next=new ProfessionNativeSkin("next",3,1,0,new[]{new ProfessionNativeSkin.Entry(0,"body",b),new ProfessionNativeSkin.Entry(2,"other",fallback)},Array.Empty<int>(),Array.Empty<int>());
            var catalog=new ProfessionNativeSkinCatalog(new[]{defaults,first,next});string[] setup={"body","head",null};
            var state=new ProfessionNativeSkinSlots(catalog,setup);setup[0]="mutated";Require(state.SetupName(0)=="body","setup array ownership");
            state.SetSkin(first);Require(ReferenceEquals(state.Attachment(0),a)&&ReferenceEquals(state.Attachment(1),fallback)&&state.Attachment(2)==null,"only setup-visible first skin");
            state.SetAttachment(0,manual);state.SetAttachment(2,a);state.SetSkin(next);
            Require(ReferenceEquals(state.Attachment(0),manual)&&ReferenceEquals(state.Attachment(2),fallback),"identity plus exact slot/key; manual override survives");
            state.SetAttachmentByName(0,"body");Require(ReferenceEquals(state.Attachment(0),b),"named active attachment");
            state.SetAttachmentByName(1,"head");Require(ReferenceEquals(state.Attachment(1),fallback),"named default attachment");
            state.SetAttachmentByName(0,null);Require(state.Attachment(0)==null,"explicit null attachment");
            state.SetSkin(next);Require(state.Attachment(0)==null,"same active skin preserves explicit override");
            Reject(()=>state.SetAttachmentByName(0,"Body"));
            var wrong=new ProfessionNativeSkin("wrong",1,1,0,Array.Empty<ProfessionNativeSkin.Entry>(),Array.Empty<int>(),Array.Empty<int>());Reject(()=>state.SetSkin(wrong));
            Require(ReferenceEquals(state.Skin,next)&&state.Attachment(0)==null,"invalid layout does not mutate state");
        }
        private static void Reject(Action action){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Require(rejected,"invalid attachment binding is rejected");}
        private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException("Native skin/slot validation failed: "+reason);}
        private static void Finish(int code){SessionState.SetBool(Active,false);EditorApplication.Exit(code);}
    }
}
