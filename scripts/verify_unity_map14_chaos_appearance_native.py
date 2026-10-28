"""Check the original native chaos appearance particle/renderer fields."""
from collections import Counter
import copy
import json
from pathlib import Path

from official_apk import ROOT
from official_native_yaml_verification import documents,element,unpack_array,float_bits
from verify_unity_map14_story_playable_hierarchy import shape
from prepare_unity_map14_suimo_performance import Originals,EXPORTED,GUID
from prepare_unity_map14_chaos_appearance_playable import NAMES


def verify():
    source=Originals();counts=Counter();used=set();parsed={};observed={}
    components=json.loads((ROOT/"unity/Assets/Official/World/Map/14/Source/native-story-playable-components.json").read_bytes())
    component_types={(r["cab"].casefold(),r["path_id"]):r["type"].rsplit(".",1)[-1] for r in components["components"]}
    proof=json.loads((ROOT/"assets/reference/official-map/map14-story-playable-hierarchy-source-manifest.json").read_bytes())
    bindings={r["source"]:(ROOT/r["path"],r["fileId"]) for r in proof["bindings"]}
    index={GUID.search(p.read_text(encoding="utf8"))[1]:p.with_suffix("") for p in EXPORTED.rglob("*.meta")}
    def docs(path):
        used.add(path)
        if path not in parsed:parsed[path]=documents(path.read_text(encoding="utf8"))
        return parsed[path]
    def compare(left,right,schema,owner,path,label):
        if isinstance(left,dict) and set(left)=={"m_FileID","m_PathID"}:
            counts["pointers"]+=1
            if not left["m_PathID"]:
                if right["fileID"]!=0:raise ValueError("Original null pointer changed: "+label)
                return
            cab=owner.split(":")[0]
            if left["m_FileID"]:cab=source.files[cab]["externals"][left["m_FileID"]-1].replace("\\","/").split("/")[-1].casefold()
            identity=cab+":"+str(left["m_PathID"])
            target=index[right["guid"]] if right.get("guid") else path
            if identity in bindings:
                if (target,right["fileID"])!=bindings[identity]:raise ValueError("Original component pointer changed: "+label)
            else:
                obj=source.reader(cab,left["m_PathID"]);kind,fields=docs(target)[right["fileID"]]
                if obj.type.name!=kind or obj.peek_name()!=fields.get("m_Name"):raise ValueError("Original native dependency changed: "+label)
                converted_identity=(target,right["fileID"])
                if identity in observed:
                    if observed[identity]!=converted_identity:raise ValueError("Original dependency identity changed across references: "+label)
                    return
                observed[identity]=converted_identity
                if kind=="MonoBehaviour":
                    script=index[fields["m_Script"]["guid"]]
                    if script.stem!=component_types[cab,str(left["m_PathID"])]:raise ValueError("Original Timeline MonoScript type changed: "+label)
                    counts["timelineScriptBindings"]+=1
                if kind in ("Mesh","MonoBehaviour"):
                    tree=shape(obj.serialized_type.node)
                    for key,value in obj.read_typetree(check_read=True).items():
                        if key=="m_Script":continue
                        compare(value,fields[key],tree["children"][key],identity,target,kind+"."+key)
            return
        if isinstance(left,dict):
            for key,value in left.items():
                if key not in right:raise ValueError("Original native field is missing: "+label+"."+key)
                actual=right[key]
                if isinstance(value,bytes) and key=="m_DataSize" and "_typelessdata" in right:
                    if actual!=len(value):raise ValueError("Original native data size changed: "+label)
                    actual=right["_typelessdata"]
                compare(value,actual,schema["children"][key],owner,path,label+"."+key)
        elif isinstance(left,tuple) and schema["type"]=="pair":
            if len(left)!=2:raise ValueError("Original native map pair width changed")
            if len(right)==1 and "first" not in right and "key" not in right:
                name,value=next(iter(right.items()));right={"first":name,"second":value}
            for i,key in enumerate(("first","second")):
                target=("key","value")[i] if key not in right else key
                if target not in right:raise ValueError("Inspect original pair schema: "+label+" "+repr(right))
                compare(left[i],right[target],schema["children"][key],owner,path,label+"."+key)
        elif isinstance(left,(list,tuple)):
            if not isinstance(right,list):
                packed_schema=schema
                if element(schema)["type"]=="unsigned int":
                    packed_schema=copy.deepcopy(schema);element(packed_schema)["type"]="UInt32"
                right=unpack_array(right,packed_schema,len(left))
            if len(left)!=len(right):raise ValueError("Original native array count changed: "+label)
            for i,(a,b) in enumerate(zip(left,right)):compare(a,b,element(schema),owner,path,label+"["+str(i)+"]")
        elif isinstance(left,float):
            actual=float(getattr(right,"scalar",right))
            if float_bits(left,schema)!=float_bits(actual,schema):raise ValueError("Original native float bits changed: "+label+" "+repr(left)+" -> "+repr(right))
        elif isinstance(left,bytes):
            if left.hex()!=("" if right is None else str(right)):raise ValueError("Original native packed bytes changed: "+label)
        elif isinstance(left,str) and schema["type"] in ("string","PropertyName"):
            if left!=("" if right is None else str(right)):raise ValueError("Original native string changed: "+label)
        elif left!=("" if right is None and left=="" else right):raise ValueError("Original native scalar changed: "+label+" "+repr(left)+" -> "+repr(right))
        counts["values"]+=1
    roots={p for p in EXPORTED.rglob("*.prefab") if p.stem in NAMES}
    if len(roots)!=1:raise ValueError("Original chaos appearance prefab inventory differs")
    for binding in proof["bindings"]:
        path=ROOT/binding["path"]
        if path not in roots or binding["type"] not in ("ParticleSystem","ParticleSystemRenderer","TrailRenderer","Animator","PlayableDirector"):continue
        obj=source.reader(*binding["source"].split(":"));fields=obj.read_typetree(check_read=True)
        tree=shape(obj.serialized_type.node);kind,actual=docs(path)[binding["fileId"]]
        if obj.type.name!=kind:raise ValueError("Original native component type changed")
        lightmap_defaults=dict(m_LightmapIndex=65535,m_LightmapIndexDynamic=65535,
            m_LightmapTilingOffset=dict(x=1.,y=1.,z=0.,w=0.),m_LightmapTilingOffsetDynamic=dict(x=1.,y=1.,z=0.,w=0.))
        for key,value in fields.items():
            if key not in actual and key in lightmap_defaults and kind in ("ParticleSystemRenderer","TrailRenderer"):
                if value!=lightmap_defaults[key]:raise ValueError("Original runtime lightmap field requires explicit restoration")
                counts["runtimeLightmapDefaults"]+=1
                continue
            compare(value,actual[key],tree["children"][key],binding["source"],path,kind+"."+key)
        counts[kind]+=1
    print("Original chaos appearance native fields:",dict(counts),flush=True)
    return dict(counts),sorted(used)


if __name__=="__main__":verify()
