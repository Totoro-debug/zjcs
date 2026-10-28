using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionNativeSkinValidation
    {
        [Serializable] private sealed class Payload { public Body[] cases; }
        [Serializable] private sealed class Body
        {
            public string sex; public int slotCount, boneCount, constraintCount, identityCount;
            public Skin[] skins; public Combination[] combinations;
        }
        [Serializable] private sealed class Skin { public string name; public Entry[] entries; public int[] bones, constraints; }
        [Serializable] private sealed class Entry { public int slot, id; public string key; }
        [Serializable] private sealed class Combination { public int first, second; public Entry[] entries; public int[] bones, constraints; }
        private const string Active="Zjcs.ProfessionNativeSkin.Active";
        private static bool ready;
        static ProfessionNativeSkinValidation()
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
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+".Deadline",0))throw new TimeoutException("Skin fixture did not enter Play Mode.");
                if(!ready||!EditorApplication.isPlaying)return;
                var source=JsonUtility.FromJson<Payload>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("native-skins-validation.json")));
                Require(source.cases.Length==2,"both original skin lists");int skins=0,attachments=0,combinations=0;
                foreach(Body body in source.cases)
                {
                    object[] identities=Enumerable.Range(0,body.identityCount).Select(_=>new object()).ToArray();
                    var native=body.skins.Select(s=>new ProfessionNativeSkin(s.name,body.slotCount,body.boneCount,body.constraintCount,
                        s.entries.Select(e=>new ProfessionNativeSkin.Entry(e.slot,e.key,identities[e.id])).ToArray(),s.bones,s.constraints)).ToArray();
                    var catalog=new ProfessionNativeSkinCatalog(native);
                    for(int i=0;i<native.Length;i++)
                    {
                        Skin original=body.skins[i];CheckSkin(native[i],original.entries,original.bones,original.constraints,identities);
                        Require(ReferenceEquals(catalog.FindSkin(original.name),native[i]),"original ordinal skin identity");
                        skins++;attachments+=original.entries.Length;
                    }
                    foreach(Combination combination in body.combinations)
                    {
                        var combined=new ProfessionNativeSkin("combineSkin",body.slotCount,body.boneCount,body.constraintCount,Array.Empty<ProfessionNativeSkin.Entry>(),Array.Empty<int>(),Array.Empty<int>());
                        combined.AddSkin(native[combination.first]);combined.AddSkin(native[combination.second]);
                        CheckSkin(combined,combination.entries,combination.bones,combination.constraints,identities);
                        combined.AddSkin(native[combination.second]);CheckSkin(combined,combination.entries,combination.bones,combination.constraints,identities);
                        combined.Clear();Require(combined.AttachmentCount==0&&combined.Bones.Count==0&&combined.Constraints.Count==0,"complete clear");
                        combined.AddSkin(native[combination.second]);Skin second=body.skins[combination.second];CheckSkin(combined,second.entries,second.bones,second.constraints,identities);
                        combinations++;
                    }
                    Require(catalog.FindSkin("__absent_original_skin__")==null,"missing skin lookup");
                    for(int i=0;i<native.Length;i++)CheckSkin(native[i],body.skins[i].entries,body.skins[i].bones,body.skins[i].constraints,identities);
                }
                Synthetic();Debug.Log($"Profession native source skin tables passed: {skins} skins, {attachments} shared attachment identities and {combinations} ordered combinations. Source keys/membership, last-skin overwrite, duplicate-free unions, clear/reuse, immutable source tables, default fallback, ordinal names, input ownership and invalid layouts pass. Attachment objects are identities, not production geometry; actual skin-change slot migration, constraints, character rendering and full profession UI are not validated.");Finish(0);
            }
            catch(Exception exception){Debug.LogException(exception);Finish(1);}
        }
        private static void CheckSkin(ProfessionNativeSkin skin,Entry[] entries,int[] bones,int[] constraints,object[] identities)
        {
            Require(skin.AttachmentCount==entries.Length&&skin.Bones.SequenceEqual(bones)&&skin.Constraints.SequenceEqual(constraints),"original attachment count and ordered membership");
            foreach(Entry entry in entries)Require(ReferenceEquals(skin.GetAttachment(entry.slot,entry.key),identities[entry.id]),"source attachment identity at compound key");
        }
        private static void Synthetic()
        {
            object defaultA=new object(),activeA=new object(),defaultB=new object();
            var defaults=new ProfessionNativeSkin("default",2,3,2,new[]{new ProfessionNativeSkin.Entry(0,"a",defaultA),new ProfessionNativeSkin.Entry(1,"b",defaultB)},Array.Empty<int>(),Array.Empty<int>());
            int[] bones={2,0},constraints={1};var entries=new[]{new ProfessionNativeSkin.Entry(0,"a",activeA)};
            var active=new ProfessionNativeSkin("Active",2,3,2,entries,bones,constraints);bones[0]=1;constraints[0]=0;entries[0]=new ProfessionNativeSkin.Entry(0,"a",defaultA);
            var catalog=new ProfessionNativeSkinCatalog(new[]{defaults,active});
            Require(ReferenceEquals(catalog.ResolveAttachment(active,0,"a"),activeA)&&ReferenceEquals(catalog.ResolveAttachment(active,1,"b"),defaultB)&&ReferenceEquals(catalog.ResolveAttachment(null,0,"a"),defaultA),"active skin before default fallback");
            Require(catalog.ResolveAttachment(active,0,null)==null&&catalog.ResolveAttachment(active,0,"missing")==null&&catalog.FindSkin("active")==null,"null/absent attachment and ordinal skin name");
            Require(active.Bones.SequenceEqual(new[]{2,0})&&active.Constraints.SequenceEqual(new[]{1})&&ReferenceEquals(active.GetAttachment(0,"a"),activeA),"source array ownership with shared attachment reference");
            active.AddSkin(active);Require(active.AttachmentCount==1&&active.Bones.Count==2,"self merge retains contents");
            Throws<ArgumentException>(()=>new ProfessionNativeSkinCatalog(new[]{active,active}));
            Throws<ArgumentException>(()=>active.AddSkin(new ProfessionNativeSkin("wrong",1,3,2,Array.Empty<ProfessionNativeSkin.Entry>(),Array.Empty<int>(),Array.Empty<int>())));
            Throws<ArgumentException>(()=>new ProfessionNativeSkin("bad",2,3,2,new[]{new ProfessionNativeSkin.Entry(0,"a",defaultA),new ProfessionNativeSkin.Entry(0,"a",activeA)},Array.Empty<int>(),Array.Empty<int>()));
            Throws<ArgumentException>(()=>new ProfessionNativeSkin("bad",2,3,2,Array.Empty<ProfessionNativeSkin.Entry>(),new[]{3},Array.Empty<int>()));
            Throws<ArgumentException>(()=>active.GetAttachment(-1,"a"));Throws<ArgumentException>(()=>active.GetAttachment(0,null));
        }
        private static void Throws<T>(Action action)where T:Exception{try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
        private static void Require(bool condition,string label){if(!condition)throw new Exception(label);}
        private static void Finish(int code){SessionState.SetBool(Active,false);EditorApplication.Exit(code);}
    }
}
