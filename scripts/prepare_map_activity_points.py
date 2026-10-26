"""Convert the original MessagePack activity spawn points for fourteen maps."""
import hashlib
from official_apk import Apk, ROOT
from official_map_codec import unpack
from prepare_map_regions import ART, REF, save


def build():
    apk=Apk();maps={};inputs={}
    for name,ptr in apk.container('defaultpackage_assets_config_map_mapactivitypoint.bundle'):
        if not name.endswith('.g.bytes'):continue
        obj=ptr.deref();raw=obj.read().m_Script.encode('utf8','surrogateescape')
        value=unpack(raw)
        if len(value)!=2 or any(len(row)!=4 or len(row[2])!=2 for row in value[1]):raise ValueError('Original activity point schema differs: '+name)
        maps[str(value[0])]=[dict(id=row[0],point_class=row[1],position=row[2],region=row[3]) for row in value[1]]
        inputs[str(value[0])]=dict(apk.provenance(obj),sha256=hashlib.sha256(raw).hexdigest(),bytes=len(raw),consumed_bytes=len(raw))
    target=ART/'map-activity-points.json';save(target,maps)
    source=ROOT/'.tools/source-packages/official-code/common-source/MessagePack.Formatters.Common'
    save(REF/'activity-points-source-manifest.json',dict(manifest='854_156383',inputs=inputs,formatters={name:hashlib.sha256((source/name).read_bytes()).hexdigest() for name in ['MapActivityPointConfigFormatter.cs','MapActivityLoPointInfoFormatter.cs']},outputs={'map-activity-points.json':hashlib.sha256(target.read_bytes()).hexdigest()},operation='Original map IDs, point IDs, point classes, grid coordinates and regions decoded in native formatter field order'))
    print('Native activity points',len(maps),'maps',sum(len(v) for v in maps.values()),'points')


if __name__=='__main__':build()
