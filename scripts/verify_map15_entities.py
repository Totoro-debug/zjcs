"""Compare all tower placement/template components to selected pinned EC inputs."""
import hashlib
import json
from official_apk import ROOT, Apk
from official_ec_codec import Formats
from official_map_codec import unpack


def canonical(value):return json.loads(json.dumps(value,ensure_ascii=False))


def verify():
    art=ROOT/'assets/official';receipt=json.loads((ROOT/'assets/reference/official-map/entities-source-manifest.json').read_bytes())
    adventure=json.loads((art/'adventure.json').read_bytes())
    maps=[int(v['ClassId']) for v in adventure['tables']['Map/map'] if int(v['MapGroupId'])==15]
    templates=json.loads((art/'map-entities.json').read_bytes())['templates'];wanted={};classes=set()
    for mid in maps:
        placements=json.loads((art/'regions'/f'{mid}-entities.json').read_bytes())
        for identity,value in placements.items():
            wanted[f'{mid}/{identity}']=value
            classes.add(int(value['LoEntityComponent']['SpanwInfo']['ClassId']))
    placements_count=len(wanted)
    for identity in classes:wanted[f'0/{identity}']=templates[str(identity)]
    grouped={}
    for key in wanted:grouped.setdefault(receipt['selected'][key]['path'],set()).add(key)
    apk=Apk(manifest=receipt['manifest']);formats=Formats();components=0;seen=set()
    for path,keys in grouped.items():
        obj=apk.asset('config_ec','/'+path.rsplit('/',1)[-1]);raw=obj.read().m_Script.encode('utf8','surrogateescape')
        source=receipt['inputs'][path]
        assert hashlib.sha256(raw).hexdigest()==source['sha256'],path
        assert apk.provenance(obj)['bundle_hash']==source['bundle_hash'],path
        assert str(obj.path_id)==source['path_id'],path
        prefix=source['legacy_prefix'];rows=unpack(raw[2:] if prefix else raw)[0]
        for parent,identity,values in rows:
            key=f'{parent}/{identity}'
            if key not in keys:continue
            decoded={typ.removeprefix('Common.'):formats.component(typ,data[2:] if prefix and data else data) for typ,data in values}
            assert canonical(decoded)==wanted[key],key
            components+=len(decoded);seen.add(key)
    assert seen==wanted.keys()
    for name,path in formats.sources.items():
        assert hashlib.sha256(path.read_bytes()).hexdigest()==receipt['formatters'][name]['sha256'],name
    assert len(maps)==60 and placements_count==2899
    print('Verified',len(maps),'tower maps,',placements_count,'placements,',len(classes),'templates,',components,'original EC components;',len(grouped),'pinned selected inputs and',len(formats.sources),'formatter hashes')


if __name__=='__main__':verify()
