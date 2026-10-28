using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionPlayerWeaponValidation
    {
        private const string Active="Zjcs.ProfessionPlayerWeapon.Active";private static bool ready;
        static ProfessionPlayerWeaponValidation(){EditorApplication.update+=Update;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode)ready=true;};}
        public static void Run()
        {
            try{ProfessionValidationWorkspace.AssertIsolatedProject();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool(Active,true);SessionState.SetFloat(Active+".Deadline",(float)(EditorApplication.timeSinceStartup+120));EditorApplication.EnterPlaymode();}
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Update()
        {
            if(!SessionState.GetBool(Active,false))return;
            try
            {
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+".Deadline",0))throw new TimeoutException("Weapon fixture did not enter Play Mode.");if(!ready||!EditorApplication.isPlaying)return;
                Check();Debug.Log("Original PlayerWeapon application lifecycle passed in Play Mode: integrated/prebound versus standalone paths, inactive-child lookup, UI/pool flags, local transform reset, release/rent/load/animation order, cached skin, callback double exception boundary, invalid rental state, direction/base-profession/skin fallback and source part-entry lifetime/scale behavior. Hierarchies and native skeleton/resource/pool endpoints are fixtures; source weapon rendering and full profession page remain unbound.");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Check()
        {
            var graph=new Bindings();var go=graph.Own(new GameObject("Original weapon application fixture"));var weapon=go.AddComponent<WeaponProbe>();weapon.ConfigureWeapon(graph);weapon.Slot=graph.Own(new GameObject("Original external weapon slot fixture")).transform;weapon.IsUI=true;
            var data=new AppearanceRuntimeData(Sex.Male);weapon.RefreshAppearance(Sex.Male,(int)Profession.Zhanshi,data);Require(graph.Events.Count==0,"missing primary skin does not rent and missing skeleton skips nonempty animation");
            data.SkinMap[SkinType.pWeapon]="weapon-one";weapon.OnWeaponLoaded=()=>graph.Events.Add("loaded");weapon.RefreshAppearance(Sex.Male,(int)Profession.Zhanshi,data);
            Require(graph.Events.SequenceEqual(new[]{"rent:weapon-one:True:False","loaded","play:100/loop_R:True"}),"original UI standalone rent/callback/animation order");
            var first=graph.Entries.Last();var native=first.GoPart.GetComponentInChildren<SkeletonProbe>(true);
            Require(native&&!native.gameObject.activeSelf&&weapon.LoadWeapon==first.GoPart&&weapon.SkinName=="weapon-one","inactive child native component lookup and cached source skin");
            Require(first.GoPart.transform.localScale==Vector3.one&&first.GoPart.transform.localPosition==Vector3.zero&&first.GoPart.transform.localRotation==Quaternion.identity,"rented part local transform reset");
            Require(first.DefaultScale==new Vector3(2,3,4)&&first.LeftAliveTime==20&&!first.InPool&&first.Valid,"entry captures original scale, validity, pool state and 20-second lifetime");
            first.LeftAliveTime=1;first.ResetAliveTime();first.ResetLocalScale();Require(first.LeftAliveTime==20&&first.GoPart.transform.localScale==new Vector3(2,3,4),"entry timer and scale reset");
            graph.Events.Clear();weapon.RefreshAppearance(Sex.Male,(int)Profession.Zhanshi,data);Require(graph.Events.SequenceEqual(new[]{"play:100/loop_R:True"}),"cached standalone skin still plays direction animation without renting");
            weapon.Direction(false,(int)Profession.Fashi);Require(graph.Events.Last()=="play:200/loop_L:True","source left direction name");
            data.SkinMap[SkinType.pWeapon]="weapon-two";weapon.IsUI=false;graph.Events.Clear();weapon.RefreshAppearance(Sex.Female,(int)Profession.Fashi,data);
            Require(graph.Events.SequenceEqual(new[]{"return:weapon-one:False","rent:weapon-two:False:True","loaded","play:200/loop_L:True"}),"original previous-return, world pool flag, load and left animation order");
            int exceptions=0;Application.LogCallback capture=(message,stack,type)=>{if(type==LogType.Exception)exceptions++;};Application.logMessageReceived+=capture;
            weapon.OnWeaponLoaded=()=>throw new InvalidOperationException("fixture weapon loaded callback failure");data.SkinMap[SkinType.pWeapon]="weapon-three";
            try{weapon.RefreshAppearance(Sex.Male,(int)Profession.Zhanshi,data);}finally{Application.logMessageReceived-=capture;}
            Require(exceptions==2&&weapon.SkinName=="weapon-three"&&graph.Events.Last()=="play:100/loop_L:True","loaded callback logs/rethrows to refresh catch then original SetDir still runs");
            weapon.OnWeaponLoaded=null;var previous=weapon.LoadWeapon;graph.DestroyNext=true;graph.Events.Clear();int errors=0;capture=(message,stack,type)=>{if(type==LogType.Error)errors++;};Application.logMessageReceived+=capture;
            data.SkinMap[SkinType.pWeapon]="destroyed-rental";
            try{weapon.RefreshAppearance(Sex.Male,(int)Profession.Zhanshi,data);}finally{Application.logMessageReceived-=capture;}
            Require(errors==1&&weapon.LoadWeapon==previous&&weapon.SkinName=="weapon-three"&&graph.Events.Last()=="play:100/loop_L:True","null Unity rental logs and preserves prior loaded/component/skin state");
            graph.Events.Clear();data.SkinMap[SkinType.pWeapon]="weapon-four";weapon.RefreshAppearance(Sex.Male,(int)Profession.Zhanshi,data);Require(!graph.Events.Any(x=>x.StartsWith("return:")),"invalid previous pool entry is not returned");
            var current=weapon.LoadWeapon;graph.Events.Clear();UnityEngine.Object.DestroyImmediate(go);Require(graph.Events.SequenceEqual(new[]{"return:weapon-four:False"})&&current,"destroy returns valid rented entry without destroying borrowed object");
            var integrated=graph.Own(new GameObject("Original prebound weapon fixture"));var world=integrated.AddComponent<SkeletonProbe>();world.Graph=graph;var uiObject=graph.Own(new GameObject("Prebound UI fixture"));var ui=uiObject.AddComponent<SkeletonProbe>();ui.Graph=graph;
            var prebound=integrated.AddComponent<WeaponProbe>();prebound.ConfigureWeapon(graph);prebound.WeaponSkeleton=world;prebound.WeaponSkeletonUI=ui;
            data.SkinMap[SkinType.pWeapon]="prebound-100";graph.Events.Clear();prebound.RefreshAppearance(Sex.Male,(int)Profession.Zhanshi,data);prebound.RefreshAppearance(Sex.Male,(int)Profession.Zhanshi,data);
            Require(graph.Events.Count(x=>x=="skin:prebound-100")==2&&prebound.SkinName==null&&world.Value=="prebound-100"&&ui.Value==null,"prebound world takes precedence and original path does not assign standalone cache");
            var uiOnly=graph.Own(new GameObject("Original UI-only weapon fixture")).AddComponent<WeaponProbe>();uiOnly.ConfigureWeapon(graph);uiOnly.WeaponSkeletonUI=ui;graph.Events.Clear();uiOnly.RefreshAppearance(Sex.Female,(int)Profession.Fashi,data);
            Require(ui.Value=="prebound-100"&&graph.Events.SequenceEqual(new[]{"skin:prebound-100","play:200/loop_R:True"}),"prebound UI-only native component branch");
            graph.Events.Clear();prebound.Direction(true,999);Require(graph.Events.Single()=="play:100/loop_R:True","unknown profession falls back through current skin warrior identifier");world.Value="weapon-mage";graph.Events.Clear();prebound.Direction(false,999);Require(graph.Events.Single()=="play:200/loop_L:True","non-warrior skin fallback selects mage");world.Value="";graph.Events.Clear();prebound.Direction(true,999);Require(graph.Events.Count==0,"empty skin and unknown profession skip animation");
            var noSkeleton=graph.Own(new GameObject("Original missing component fixture")).AddComponent<WeaponProbe>();noSkeleton.ConfigureWeapon(graph);bool missing=false;try{noSkeleton.Direction(true,999);}catch(NullReferenceException){missing=true;}Require(missing,"original empty-animation fallback dereferences missing skeleton");
            var noHandle=graph.Own(new GameObject("Invalid part-entry fixture"));var invalid=new CharacterPartPoolEntry(noHandle,"path",null,true);Require(!invalid.Valid&&invalid.InPool,"entry validity depends on non-null handle");UnityEngine.Object.DestroyImmediate(noHandle);invalid.ResetLocalScale();Require(!invalid.Valid,"destroyed part reset is skipped");
            graph.ReleaseObjects();
        }
        public sealed class WeaponProbe:PlayerWeapon
        {public string SkinName=>mSkinName;public void Direction(bool right,int profession){SetDir(right,profession);}}
        public sealed class SkeletonProbe:MonoBehaviour,IProfessionWeaponSkeleton
        {
            public Bindings Graph;public string Value;public string SkinName=>Value;public IProfessionAppearanceSkeleton Skeleton=>throw new InvalidOperationException("No actor skeleton in application fixture.");
            public void SetSkin(string name){Value=name;Graph.Events.Add("skin:"+name);}
            public void PlayAnimation(string name,bool loop){Graph.Events.Add("play:"+name+":"+loop);}
        }
        public sealed class Bindings:IProfessionPlayerWeaponBindings,ICharacterPartEntryProvider,IProfessionPlayerAppearanceBindings
        {
            public readonly List<string> Events=new List<string>();public readonly List<CharacterPartPoolEntry> Entries=new List<CharacterPartPoolEntry>();private readonly List<GameObject> objects=new List<GameObject>();public bool DestroyNext;
            public ICharacterPartEntryProvider Parts=>this;public IProfessionPlayerAppearanceBindings Player=>this;
            public GameObject Own(GameObject go){objects.Add(go);return go;}
            public CharacterPartPoolEntry RentStandalonePart(SkinType type,string name,Transform parent,bool ui,bool pool)
            {
                Require(type==SkinType.pWeapon,"source primary weapon part type");Events.Add("rent:"+name+":"+ui+":"+pool);
                var root=Own(new GameObject(name));root.transform.SetParent(parent,false);root.transform.localScale=new Vector3(2,3,4);root.transform.localPosition=new Vector3(4,5,6);root.transform.localRotation=Quaternion.Euler(0,0,30);
                var child=new GameObject("Inactive native endpoint");child.transform.SetParent(root.transform,false);var native=child.AddComponent<SkeletonProbe>();native.Graph=this;native.Value=name;child.SetActive(false);
                var entry=new CharacterPartPoolEntry(root,name,new Handle(root),false);Entries.Add(entry);if(DestroyNext){DestroyNext=false;UnityEngine.Object.DestroyImmediate(root);}return entry;
            }
            public CharacterPartPoolEntry RentIntegratedPart(SkinType type,string name,Transform parent,bool pool,Action<GameObject> before){throw new InvalidOperationException("PlayerWeapon does not rent integrated parts.");}
            public void ReturnPart(CharacterPartPoolEntry entry,bool all){Events.Add("return:"+entry.AssetPath+":"+all);entry.GoPart.transform.SetParent(null,false);}
            public void ReleaseObjects(){foreach(var go in objects)if(go)UnityEngine.Object.DestroyImmediate(go);objects.Clear();}
            public int GetBaseProfession(int profession)=>profession==(int)Profession.Zhanshi||profession==(int)Profession.Fashi?profession:0;
            public string GetProfessionIdStr(int profession)=>profession==(int)Profession.Zhanshi?"100":profession==(int)Profession.Fashi?"200":"";
            public IProfessionPlayerAppearanceSource MainPlayer=>throw new InvalidOperationException("No player in weapon fixture.");public AppearanceConfig Config=>throw new InvalidOperationException("No config in weapon fixture.");public AppearanceAnimNameCombineRule AnimationRules=>throw new InvalidOperationException("No name-rule asset in weapon fixture.");
            public bool IsAppearanceValid(object data){throw new InvalidOperationException("Unused player data endpoint.");}public IEnumerable<string> GetSkinNames(object data){throw new InvalidOperationException("Unused player data endpoint.");}
            public void GetLiteInfo(long id,ProfessionLiteInfoCallback callback,bool force){throw new InvalidOperationException("Unused lite info endpoint.");}
            public void BuildAppearanceRuntimeData(Sex sex,int profession,object data,List<string> append,AppearanceRuntimeData target){throw new InvalidOperationException("Unused player data endpoint.");}
        }
        private sealed class Handle:IProfessionAssetOperation<GameObject>
        {public bool IsValid=>false;public int Status=>0;public GameObject Result{get;}public event Action<IProfessionAssetOperation<GameObject>> Completed;public Handle(GameObject go){Result=go;}public void Dispose(){}}
        private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException("Weapon validation failed: "+reason);}
        private static void Finish(int code){SessionState.SetBool(Active,false);EditorApplication.Exit(code);}
    }
}
