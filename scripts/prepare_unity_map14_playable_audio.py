"""Recover native Map14 playable/Timeline audio, ordered Wwise actions and exact PCM."""
from __future__ import annotations
import json
from pathlib import Path
import subprocess
import wave
from map14_audio_source import AudioSource, ROOT, sha
from prepare_character_creation_opening_audio import read_field
from prepare_unity_map14_audio import fields
from prepare_unity_map10_entities import json_bytes, write_generated, write_meta
OUTPUT=ROOT/"unity/Assets/Official/World/Map/14/Resources/Map14/Audio/Playables"
RECEIPT=ROOT/"assets/reference/official-map/map14-playable-audio-source-manifest.json"
VGMSTREAM=ROOT/".tools/vgmstream/vgmstream-cli.exe"
VALIDATION=ROOT/"unity/Assets/Game/Editor/Map14/StoryPlayables/OfficialMap14AudioAssetAcceptance.cs"

def record(path):return dict(path=path.relative_to(ROOT).as_posix(),sha256=sha(path.read_bytes()))

def load_verified(path,receipt):
    raw=path.read_bytes();expected=next(r for r in json.loads(receipt.read_bytes())["outputs"] if r["path"]==path.relative_to(ROOT).as_posix())
    if sha(raw)!=expected["sha256"]:raise ValueError("Native playable source changed: "+str(path))
    return json.loads(raw)

def action_contract(data):
    kind=read_field(data,"ulActionType");values=fields(data)
    ids=[v for n,v,_ in values if n=="pID"];props=[v for n,v,_ in values if n=="pValue"]
    counts=[v for n,v,_ in values if n=="cProps"]
    if kind not in (1027,1539,1795) or ids not in ([],[15]) or len(props)!=len(ids) or counts!=[len(ids),0]:
        raise ValueError("Inspect original Wwise action kind/properties: "+str(values))
    delay=props[0] if ids else 0
    if delay<0 or int(delay)!=delay or read_field(data,"eFadeCurve")!=4:
        raise ValueError("Inspect original Wwise action delay/curve")
    return dict(id=read_field(data,"ulID"),actionType=kind,target=read_field(data,"idExt"),delayMs=delay,fadeCurve=4,fields=values)

def native_uses(playable,components,entity):
    refs={int(c["path_id"]):c for c in components["components"] if c["type"]=="WwiseEventReference"}
    uses=[]
    def add(obj,event_ptr,**extra):
        ref=refs[event_ptr["m_PathID"]]
        identity=obj.get("identity",obj.get("cab","")+":"+obj.get("path_id",""))
        uses.append(dict(component=identity,componentType=obj["type"],event=ref["fields"],reference=ref["cab"]+":"+ref["path_id"],**extra))
    for obj in playable["objects"]:
        if obj["type"]!="MyPlayableSound":continue
        f=obj["fields"]
        if f["LoopInterval"]!=-1 or f["StopSoundEvent"]["WwiseObjectReference"]["m_PathID"]:raise ValueError("Inspect changed playable sound lifecycle")
        add(obj,f["SoundEvent"]["WwiseObjectReference"],loopInterval=f["LoopInterval"],duration=f["Duration"])
    timeline=[c for c in components["components"] if c["type"]=="WwiseEventPlayableAsset"]
    if len(uses)!=6 or len(timeline)!=5:raise ValueError("Inspect original playable audio scope")
    for obj in timeline:add(obj,obj["fields"]["wwiseEvent"]["WwiseObjectReference"])
    refs={int(c["path_id"]):c for c in entity["components"] if c["type"]=="WwiseEventReference"}
    entity_events=[c for c in entity["components"] if c["type"]=="WwiseEventPlayableAsset"]
    if len(entity_events)!=6:raise ValueError("Inspect original entity Timeline audio scope")
    for obj in entity_events:
        clips=[dict(track=track["cab"]+":"+track["path_id"],trackName=track["fields"]["m_Name"],start=clip["m_Start"],duration=clip["m_Duration"],timeScale=clip["m_TimeScale"],clipIn=clip["m_ClipIn"])
               for track in entity["components"] if track["type"]=="WwiseEventTrack" and track["cab"]==obj["cab"]
               for clip in track["fields"]["m_Clips"] if clip["m_Asset"]["m_PathID"]==int(obj["path_id"]) and clip["m_Asset"]["m_FileID"]==0]
        if len(clips)!=1:raise ValueError("Entity audio clip ownership is ambiguous")
        add(obj,obj["fields"]["wwiseEvent"]["WwiseObjectReference"],entityTimeline=True,clips=clips)
    return uses

def audio_meta(target):
    write_meta(target,"AudioImporter")
    meta=target.with_name(target.name+".meta");guid=next(line for line in meta.read_text().splitlines() if line.startswith("guid:"))
    meta.write_text("fileFormatVersion: 2\n"+guid+"\nAudioImporter:\n  externalObjects: {}\n  serializedVersion: 7\n  defaultSettings:\n    serializedVersion: 2\n    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 44100\n    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n  platformSettingOverrides: {}\n  forceToMono: 0\n  normalize: 0\n  preloadAudioData: 1\n  loadInBackground: 0\n  ambisonic: 0\n  3D: 0\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n",encoding="utf-8")
    return meta

def build():
    source_path=ROOT/"unity/Assets/Official/World/Map/14/Source/story-playables.json"
    component_path=source_path.with_name("native-story-playable-components.json")
    entity_path=source_path.with_name("native-entity-components.json")
    playable=load_verified(source_path,ROOT/"assets/reference/official-map/map14-story-playables-source-manifest.json")
    components=load_verified(component_path,ROOT/"assets/reference/official-map/map14-native-story-playable-input-source-manifest.json")
    entity=load_verified(entity_path,ROOT/"assets/reference/official-map/map14-native-entity-input-source-manifest.json")
    uses=native_uses(playable,components,entity)
    voice_mapping=OUTPUT.parent/"voices.json"
    voice_entries={entry["eventKey"]:entry for entry in json.loads(voice_mapping.read_bytes())["events"]}
    source=AudioSource();events=[];routing={};previous={};outputs=[];decoded={}
    if RECEIPT.exists():
        previous[RECEIPT.resolve()]=sha(RECEIPT.read_bytes())
        for r in json.loads(RECEIPT.read_bytes())["outputs"]:
            path=ROOT/r["path"]
            if sha(path.read_bytes())!=r["sha256"]:raise ValueError("Playable audio output changed: "+str(path))
            previous[path.resolve()]=r["sha256"]
    work=ROOT/".tools/map14-audio-conversion/playables";work.mkdir(parents=True,exist_ok=True)
    for use in uses:
        key=use["event"]["objectName"];event_id,banks=source.load_event(key)
        if event_id!=use["event"]["id"]:raise ValueError("Native event reference/FNV identity differs")
        obj=source.by_id[event_id][0];action_ids=[value for name,value,_ in fields(obj) if name=="ulActionID"]
        if not action_ids:raise ValueError("Original playable event has no action")
        actions=[]
        for action_id in action_ids:
            action=action_contract(source.by_id[action_id][0]);sound_id=action["target"];sound=source.by_id[sound_id][0]
            routing.update(source.routing(sound_id))
            if action["actionType"]!=1027:
                actions.append(action);continue
            if read_field(sound,"eHircType")!=2 or read_field(sound,"ulPluginID")!=262145 or read_field(sound,"StreamType") not in (0,1):
                raise ValueError("Inspect original playable sound codec/stream")
            media_id=read_field(sound,"sourceID")
            if media_id not in decoded:
                raw,media=source.media(media_id)
                wem=work/(str(media_id)+".wem");wav=work/(str(media_id)+".wav");wem.write_bytes(raw)
                subprocess.run([str(VGMSTREAM),"-o",str(wav),str(wem)],check=True,capture_output=True)
                with wave.open(str(wav),"rb") as pcm:
                    if pcm.getcomptype()!="NONE" or pcm.getsampwidth()!=2:raise ValueError("Unexpected original PCM format")
                    info=dict(channels=pcm.getnchannels(),sampleRate=pcm.getframerate(),frames=pcm.getnframes(),sampleWidth=2,pcmSha256=sha(pcm.readframes(pcm.getnframes())))
                reuse=voice_entries.get(key)
                if reuse:
                    clip_path=reuse["clip"];target=ROOT/"unity/Assets/Official/World/Map/14/Resources"/(clip_path+".wav")
                    if target.read_bytes()!=wav.read_bytes():raise ValueError("Original Timeline/dialogue shared PCM identity differs")
                else:
                    clip_path="Map14/Audio/Playables/"+str(media_id);target=OUTPUT/(str(media_id)+".wav")
                    write_generated(target,wav.read_bytes(),previous);meta=audio_meta(target);outputs.extend((record(target),record(meta)))
                decoded[media_id]=dict(mediaId=media_id,media=media,pcm=info,clip=clip_path)
            action.update(decoded[media_id],soundId=sound_id,streamType=read_field(sound,"StreamType"))
            actions.append(action)
        events.append(dict(eventKey=key,eventId=event_id,banks=banks,actions=actions,**{k:v for k,v in use.items() if k!="event"}))
    mapping=OUTPUT.parent/"playables.json";write_generated(mapping,json_bytes(dict(manifest=source.selected.version,mapClassId=14,events=events)),previous);write_meta(mapping,"TextScriptImporter")
    outputs.extend((record(mapping),record(mapping.with_name(mapping.name+".meta"))))
    write_meta(OUTPUT,"DefaultImporter")
    receipt=dict(manifest=source.selected.version,mapClassId=14,inputs=[record(source_path),record(component_path),record(entity_path),record(voice_mapping)],packages=source.receipts(),events=events,routingObjects=routing,
        tools=[record(Path(__file__)),record(ROOT/"scripts/map14_audio_source.py"),record(VGMSTREAM)],validationScript=record(VALIDATION),
        operation="Resolve six original MyPlayableSound, five playable Timeline and six entity Timeline references through selected bank mapping and ordered Play/Mute/Unmute actions, including 400/1000/1700/5000 ms delays. Decode full current WEM (not in-bank streaming prefetch) to exact uncompressed PCM without normalization or baked gain.",
        limitations=["Runtime sound/Timeline invocation and exact Wwise gain/RTPC/effect DSP remain pending."],outputs=sorted({r["path"]:r for r in outputs}.values(),key=lambda r:r["path"]))
    write_generated(RECEIPT,json_bytes(receipt),previous);source.close()
    print("Map14 playable audio: %d native event references, %d ordered actions, %d original PCM clips"%(len(events),sum(len(e["actions"]) for e in events),len(decoded)))
if __name__=="__main__":build()
