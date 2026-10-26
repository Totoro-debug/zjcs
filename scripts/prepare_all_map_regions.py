"""Convert every official map definition, retaining exact shared terrain reuse."""
import hashlib
import json
import subprocess
import sys
from official_apk import Apk,ROOT
from official_map_codec import ground,entities
from prepare_map_regions import save,ART,REF


def build():
    a=Apk();maps=json.loads((ART/'adventure.json').read_text(encoding='utf8'))['tables']['Map/map']
    known={};report={};destination=ART/'regions'
    # Nine complete mainland conversions are already available and receipted.
    for row in maps:
        mid=int(row['ClassId']);path=destination/f'{mid}.json';ground_path=row['MapGroundBarePath'] or row['MapGroundPath']
        if path.exists() and path.with_suffix('.geom').exists():known[ground_path]=mid
    for row in maps:
        mid=int(row['ClassId']);path=destination/f'{mid}.json';ground_path=row['MapGroundBarePath'] or row['MapGroundPath']
        try:
            if path.exists() and path.with_suffix('.geom').exists():report[str(mid)]=dict(status='converted',terrain=mid);continue
            if ground_path not in known:
                result=subprocess.run([sys.executable,str(ROOT/'scripts/prepare_map_regions.py'),'--maps',str(mid)],capture_output=True,text=True,encoding='utf8',errors='replace')
                if result.returncode:raise ValueError(result.stderr[-2500:])
                known[ground_path]=mid
            original=known[ground_path]
            if original!=mid:
                nav_obj=a.asset('config_ec_maprootentitylite',f'/{mid}.g.bytes');entity_obj=a.asset('config_mapinfowrap',f'/mapinfowrap_{mid}.g.bytes')
                data=json.loads((destination/f'{original}.json').read_text(encoding='utf8'))
                data.update(id=mid,terrain_id=original,navigation=ground(nav_obj.read().m_Script.encode('utf8','surrogateescape')),placement=entities(entity_obj.read().m_Script.encode('utf8','surrogateescape')))
                save(path,data)
            report[str(mid)]=dict(status='converted',terrain=original)
        except Exception as ex:report[str(mid)]=dict(status='failed',error=str(ex))
        save(REF/'all-regions-conversion.json',dict(manifest='854_156383',definitions=len(maps),maps=report))
        print(mid,report[str(mid)]['status'],flush=True)
    save(REF/'all-regions-conversion.json',dict(manifest='854_156383',definitions=len(maps),maps=report))
    path=REF/'regions-source-manifest.json';r=json.loads(path.read_text(encoding='utf8'))
    for p in destination.iterdir():
        if p.suffix in ['.json','.geom']:r['outputs'][p.name]=hashlib.sha256(p.read_bytes()).hexdigest()
    save(path,r)
    path=ROOT/'assets/reference/official-city/source-manifest.json';m=json.loads(path.read_text(encoding='utf8'))
    for p in destination.iterdir():
        if p.suffix in ['.json','.geom']:m['data_sha256']['regions/'+p.name]=hashlib.sha256(p.read_bytes()).hexdigest()
    save(path,m)


if __name__=='__main__':build()
