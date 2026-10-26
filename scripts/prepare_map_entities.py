"""Convert original map EC placement rules, interactions, rewards and actions."""
import hashlib
import json
import re
from official_apk import Apk,ROOT
from official_ec_codec import Formats
from official_map_codec import unpack
from prepare_map_regions import save,ART,REF


def build():
    a=Apk();f=Formats();templates={};maps={};records={};failures={};selected={};superseded=[]
    for path,ptr in a.container('config_ec'):
        name=path.rsplit('/',1)[-1]
        if not name.startswith(('World','ActionWorld','ActionCommon','Map','map_','ActivityMusicFestiva')):continue
        obj=ptr.deref();raw=obj.read().m_Script.encode('utf8','surrogateescape')
        prefix=''
        try:rows=unpack(raw)[0]
        except Exception:
            # Legacy singleton EC files carry a two-byte type/version prefix,
            # including in each nested component. Keep those bytes in receipts.
            rows=unpack(raw[2:])[0];prefix=raw[:2].hex()
        for parent,identity,components in rows:
            target=maps.setdefault(str(parent),{}) if parent else templates
            key=str(parent)+'/'+str(identity)
            source=dict(path=path,legacy=bool(prefix))
            if key in selected:
                # The client retains obsolete singleton EC files alongside
                # the current generated part files. Directory order must not
                # allow those old components to overwrite current rules.
                if prefix and not selected[key]['legacy']:
                    superseded.append(dict(key=key,discarded=source,selected=selected[key]));continue
                superseded.append(dict(key=key,discarded=selected[key],selected=source))
            selected[key]=source
            values={}
            for typ,data in components:
                try:values[typ.removeprefix('Common.')]=f.component(typ,data[2:] if prefix and data else data)
                except Exception as ex:
                    failures[path+':'+str(identity)+':'+typ]=str(ex)
            target[str(identity)]=values
        records[path]=dict(a.provenance(obj),sha256=hashlib.sha256(raw).hexdigest(),legacy_prefix=prefix)
    for mid,values in maps.items():save(ART/'regions'/(mid+'-entities.json'),values)
    save(ART/'map-entities.json',dict(templates=templates,maps={k:len(v) for k,v in maps.items()}))
    save(REF/'entities-source-manifest.json',dict(manifest='854_156383',inputs=records,selected=selected,superseded=superseded,unconverted_components=failures,formatters={k:dict(path=str(v.relative_to(ROOT)),sha256=hashlib.sha256(v.read_bytes()).hexdigest()) for k,v in f.sources.items()},output_sha256=hashlib.sha256((ART/'map-entities.json').read_bytes()).hexdigest()))
    path=ROOT/'assets/reference/official-city/source-manifest.json';m=json.loads(path.read_text(encoding='utf8'));m['data_sha256']['map-entities.json']=hashlib.sha256((ART/'map-entities.json').read_bytes()).hexdigest()
    for mid in maps:
        path=ART/'regions'/(mid+'-entities.json');m['data_sha256']['regions/'+path.name]=hashlib.sha256(path.read_bytes()).hexdigest()
    save(ROOT/'assets/reference/official-city/source-manifest.json',m)
    print('EC templates',len(templates),'maps',len(maps),'unsupported',len(failures),flush=True)


if __name__=='__main__':build()
