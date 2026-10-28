"""Read current official Wwise sources with APK-first, manifest and bank identity checks."""
from __future__ import annotations
import hashlib
import base64
import struct
from pathlib import Path
import sys
import tempfile
import zipfile
from official_source import OfficialSource
from official_cloud_cache import CloudCache
from prepare_character_creation_opening_audio import parse_pck, read_field
from prepare_unity_map14_audio import fields
from verify_current_audio_sources import mapping
ROOT = Path(__file__).resolve().parents[1]
def sha(raw): return hashlib.sha256(raw).hexdigest()
class AudioSource:
    def __init__(self):
        self.selected = OfficialSource()
        if self.selected.version != "871_156598": raise ValueError("Inspect selected Map14 audio version")
        with self.selected.apk.open("rb") as stream:
            if hashlib.file_digest(stream,"sha256").hexdigest() != self.selected.apk_sha256: raise ValueError("APK hash mismatch")
        self.archive = zipfile.ZipFile(self.selected.apk)
        self.inventory = {b["name"]:b for b in self.selected.manifest("WwiseBnk",self.archive)["bundles"]}
        self.cloud = CloudCache(self.selected.cloud_path,self.selected.version)
        self.packages = {}
        self.temporary = tempfile.TemporaryDirectory(prefix="zjcs-map14-official-audio-")
        self.by_id = {}
        self.object_payloads = {}
        self.media_search_complete = False
        self.bank_sources = {}
        self.mapping = mapping(self.package("wwisebnk_assets_wwisedata_android_eventbankmapping_bytes.rawfile"))
    def close(self):
        self.archive.close(); self.temporary.cleanup()
    def package(self,name):
        if name in self.packages: return self.packages[name][0]
        b = self.inventory[name]; entry = "assets/yoo/WwiseBnk/"+b["file"]
        packaged = entry in self.archive.namelist()
        if packaged: raw,origin = self.archive.read(entry),dict(origin="apk_zip",entry=entry,apkSha256=self.selected.apk_sha256)
        else:
            cached = self.cloud.read("WwiseBnk",b)
            if cached is None: raise FileNotFoundError("Current original audio package is not cached: "+name)
            raw,origin = cached
        if len(raw)!=b["size"] or hashlib.md5(raw).hexdigest()!=b["hash"]: raise ValueError("Wwise package size/MD5 mismatch: "+name)
        self.packages[name]=(raw,dict(bundle=b,source=origin,sha256=sha(raw),apkCheckedFirst=dict(entry=entry,packaged=packaged)))
        return raw
    def objects(self,name):
        if name in self.bank_sources: return
        sys.path.insert(0,str(ROOT/".tools/wwiser"))
        from wwiser.parser import wparser
        raw=self.package(name); paths=[]
        for bank_id,_,size,offset,_ in parse_pck(raw)[0]:
            path=Path(self.temporary.name)/(str(bank_id)+".bnk"); bank=raw[offset:offset+size]
            path.write_bytes(bank);paths.append(str(path))
            cursor=0
            while cursor<len(bank):
                tag=bank[cursor:cursor+4];length=struct.unpack_from("<I",bank,cursor+4)[0]
                if cursor+8+length>len(bank):raise ValueError("Truncated original bank chunk")
                if tag==b"HIRC":
                    position=cursor+12;count=struct.unpack_from("<I",bank,cursor+8)[0]
                    for _ in range(count):
                        kind=bank[position];size=struct.unpack_from("<I",bank,position+1)[0]
                        identity=struct.unpack_from("<I",bank,position+5)[0]
                        data=bank[position:position+5+size]
                        if len(data)!=size+5:raise ValueError("Truncated original HIRC payload")
                        self.object_payloads.setdefault(identity,[]).append(dict(package=name,bankId=bank_id,bankOffset=position,packageOffset=offset+position,type=kind,bytes=len(data),sha256=sha(data),base64=base64.b64encode(data).decode("ascii")))
                        position+=5+size
                    if position!=cursor+8+length:raise ValueError("Unconsumed original HIRC bytes")
                cursor+=8+length
        parser=wparser.Parser();parser.parse_banks(paths)
        records=[]
        for bank in parser.get_banks():
            bank_id=int(Path(bank.get_attrs()["filename"]).stem)
            for h in bank.get_children() or []:
                if h.get_name()!="HircChunk":continue
                for group in h.get_children() or []:
                    if group.get_name()!="listLoadedItem":continue
                    for obj in group.get_children() or []:
                        identity=read_field(obj,"ulID"); data=fields(obj)
                        if identity in self.by_id and self.by_id[identity][1]!=data:
                            previous=self.by_id[identity][1]
                            # Shared Actor-Mixers repeat only this bank's child list.
                            ignored={"dwSectionSize","ulNumChilds","ulChildID"}
                            if [(n,v,f) for n,v,f in previous if n not in ignored] != [(n,v,f) for n,v,f in data if n not in ignored]:
                                raise ValueError("Conflicting Wwise HIRC identity: "+str((identity,previous,data)))
                        self.by_id[identity]=(obj,data)
            records.append(dict(bankId=bank_id,sha256=sha((Path(self.temporary.name)/(str(bank_id)+".bnk")).read_bytes())))
        self.bank_sources[name]=records
    def event_banks(self,key):
        sys.path.insert(0,str(ROOT/".tools/wwiser"))
        from wwiser import wfnv
        identity=wfnv.Fnv().get_hash(key)
        return identity,[self.mapping["banks"][i]["name"] for i in self.mapping["events"].get(identity,[])]
    def load_event(self,key):
        identity,banks=self.event_banks(key)
        for name in banks:
            suffix="_"+name.lower()+"_pck.rawfile"
            matches=[b for b in self.inventory if b.endswith(suffix) or b.endswith("_zh_cn"+suffix)]
            if len(matches)!=1:raise ValueError("Ambiguous event bank package: "+name)
            self.objects(matches[0])
        if banks and identity not in self.by_id:raise ValueError("Mapped event is absent from its actual bank: "+key)
        return identity,banks
    def routing(self, sound_id):
        self.objects("wwisebnk_assets_wwisedata_android_minimum_pck.rawfile")
        queue=[sound_id];result={}
        while queue:
            identity=queue.pop(0)
            if not identity or str(identity) in result:continue
            if identity not in self.by_id:raise ValueError("Unresolved original Wwise routing object: "+str(identity))
            obj,data=self.by_id[identity]; result[str(identity)]=dict(fields=data,rawObjects=self.object_payloads[identity])
            for name,value,_ in data:
                if name in ("DirectParentID","OverrideBusId") and value:
                    queue.append(value)
                elif name=="fxID" and value in self.by_id and read_field(obj,"eHircType") not in (17,18):
                    queue.append(value)
        return result
    def media(self,media_id):
        import struct
        found=[]
        for name,(raw,receipt) in self.packages.items():
            if not name.endswith("_pck.rawfile"):continue
            banks,files=parse_pck(raw)
            for identity,_,size,offset,_ in files:
                if identity==media_id:found.append((raw[offset:offset+size],dict(package=name,offset=offset,bytes=size,mediaId=identity)))
            for bank_id,_,size,offset,_ in banks:
                data=raw[offset:offset+size];cursor=0;chunks={}
                while cursor<len(data):
                    tag=data[cursor:cursor+4];length=struct.unpack_from("<I",data,cursor+4)[0]
                    if cursor+8+length>len(data):raise ValueError("Truncated original bank chunk")
                    chunks[tag]=(cursor+8,data[cursor+8:cursor+8+length]);cursor+=8+length
                if b"DIDX" not in chunks:continue
                if b"DATA" not in chunks:raise ValueError("Bank media index has no DATA")
                for identity,relative,length in struct.iter_unpack("<III",chunks[b"DIDX"][1]):
                    if identity==media_id:
                        absolute=offset+chunks[b"DATA"][0]+relative
                        if relative+length>len(chunks[b"DATA"][1]):raise ValueError("Truncated original bank media")
                        found.append((raw[absolute:absolute+length],dict(package=name,bankId=bank_id,offset=absolute,bytes=length,mediaId=identity)))
        if not found and not self.media_search_complete:
            # Wwise shares media across loaded banks; the Play action bank can
            # own the Sound while its WEM is physically in another package.
            for name in sorted(self.inventory):
                if name.endswith("_pck.rawfile"):self.package(name)
            self.media_search_complete=True
            return self.media(media_id)
        if not found:raise ValueError("Original Wwise media is unresolved: "+str(media_id))
        # A streaming source may repeat its leading bytes as an in-bank
        # prefetch fragment. Return the complete matching original WEM.
        complete=max((raw for raw,_ in found),key=len)
        if any(not complete.startswith(raw) for raw,_ in found):raise ValueError("Conflicting original Wwise media: "+str(media_id))
        return complete,[dict(record,sha256=sha(raw),prefetchFragment=len(raw)<len(complete)) for raw,record in found]
    def receipts(self):
        return {name:dict(record,banks=self.bank_sources.get(name,[])) for name,(_,record) in sorted(self.packages.items())}
