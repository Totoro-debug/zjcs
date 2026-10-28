"""Recover original entity Timeline tracks, bubble text and split voice routing."""
import json
from pathlib import Path
import re
import struct
import xxhash
from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_unity_map14_playable_audio import load_verified, native_uses, record
from prepare_unity_map13_sources import encode, meta, sha, write

OUTPUT=ROOT/"unity/Assets/Official/World/Map/14/Source/entity-timelines.json"
RECEIPT=ROOT/"assets/reference/official-map/map14-entity-timelines-source-manifest.json"

def build():
    base=OUTPUT.parent
    paths=[base/"story-playables.json",base/"native-story-playable-components.json",base/"native-entity-components.json"]
    proofs=[ROOT/"assets/reference/official-map"/name for name in (
        "map14-story-playables-source-manifest.json","map14-native-story-playable-input-source-manifest.json","map14-native-entity-input-source-manifest.json")]
    playable,components,entity=[load_verified(p,r) for p,r in zip(paths,proofs)]
    audio=[dict(r,eventKey=r["event"]["objectName"]) for r in native_uses(playable,components,entity) if r.get("entityTimeline")]
    objects={(r["cab"],int(r["path_id"])):r for r in entity["components"]}
    timelines=[]
    for root in entity["components"]:
        if root["type"]!="UnityEngine.Timeline.TimelineAsset":continue
        retained=[];seen=set()
        def capture(pointer):
            if not pointer["m_PathID"]:return
            key=(root["cab"],pointer["m_PathID"])
            if pointer["m_FileID"] or key not in objects:raise ValueError("Unresolved original Timeline track/clip: "+str(key))
            if key in seen:return
            seen.add(key);obj=objects[key];retained.append(obj)
            f=obj["fields"]
            for child in f.get("m_Children",[]):capture(child)
            for clip in f.get("m_Clips",[]):capture(clip["m_Asset"])
        for track in root["fields"]["m_Tracks"]:capture(track)
        timelines.append(dict(asset=root,objects=retained))
    if len(timelines)!=5:raise ValueError("Original entity Timeline scope changed")
    talks=[obj for row in timelines for obj in row["objects"] if obj["type"]=="EntityTalkAsset"]
    needed={(r["fields"]["Template"]["DialogId"],r["fields"]["Template"]["Order"]) for r in talks}
    apk=Apk.current();code=ROOT/".tools/source-packages/current"/apk.version/"official-code"
    constructors=Constructors(code)
    mapping={name:cls for cls,name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)',constructors.source("Tables"))}
    obj=apk.asset("config_luban","/bubble_dialog.g.bytes")
    raw=obj.read().m_Script.encode("utf8","surrogateescape")
    bubbles=[r for r in constructors.table(raw,mapping["bubble_dialog"]) if (r["Id"],r["Order"]) in needed]
    if {(r["Id"],r["Order"]) for r in bubbles}!=needed:raise ValueError("Original Timeline bubble configuration unresolved")
    wanted={xxhash.xxh64_intdigest(f"bubble_dialog_{i}_{o}".encode()):f"bubble_dialog_{i}_{o}" for i,o in needed}
    from map14_audio_source import AudioSource
    source=AudioSource();localized={}
    for name in ("wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile","wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile"):
        text=source.package(name);count=struct.unpack_from("<i",text)[0];start=4+count*12
        for index in range(count):
            identity,offset=struct.unpack_from("<Qi",text,4+index*12)
            if identity not in wanted:continue
            position=start+offset;size=struct.unpack_from("<H",text,position)[0]
            if offset<0 or position+2+size>len(text):raise ValueError("Original bubble text outside rawfile")
            localized[wanted[identity]]=text[position+2:position+2+size].decode("utf8")
    if set(localized)!=set(wanted.values()):raise ValueError("Original Timeline bubble text unresolved")
    chant=[r for r in audio if r["clips"][0]["trackName"]=="chapter_5_main_36_17"]
    chant.sort(key=lambda r:r["clips"][0]["start"])
    if [r["eventKey"] for r in chant]!=["chapter_5_main_36_17_1","chapter_5_main_36_17_2"]:raise ValueError("Original split chant routing changed")
    route=dict(eventKey="chapter_5_main_36_17",timeline="SuiMo_StoryShow02Timeline",events=chant)
    write(OUTPUT,encode(dict(manifest=apk.version,mapClassId=14,timelines=timelines,bubbles=bubbles,localization=localized,audio=audio,timelineRoutedEvents=[route])))
    meta(OUTPUT)
    codepaths=[code/"common-source/Common/LanguageUtils.cs",code/"client-source/EntityTalkAsset.cs",code/"client-source/EntityTalkBehaviour.cs",code/"client-source/EntityTrack.cs",code/"client-source/WwiseEventPlayableBehaviour.cs"]
    write(RECEIPT,encode(dict(manifest=apk.version,mapClassId=14,inputs=[record(p) for p in paths+proofs],
        bubbleSource=dict(source=apk.provenance(obj),payloadSha256=sha(raw),objectSha256=sha(obj.get_raw_data())),
        localizationSources=source.receipts(),code=[record(p) for p in codepaths],
        tools=[record(Path(__file__)),record(ROOT/"scripts/prepare_unity_map14_playable_audio.py")],
        outputs=[record(OUTPUT),record(OUTPUT.with_name(OUTPUT.name+".meta"))],
        counts=dict(timelines=len(timelines),bubbles=len(bubbles),audioEvents=len(audio)),
        operation="Preserve original entity Timeline track order, clip timing and fields, BubbleDialog configuration/text and split voice references. Native binding and live performance remain pending.")))
    source.close();apk.archive.close()
    print("Map14 entity Timelines: 5 assets, %d bubbles, 6 audio references; original split chant resolved."%len(bubbles))

if __name__=="__main__":build()
