"""Rebuild all shared native terrain after changing its serialized schema."""
import hashlib
import json
import subprocess
import sys
from prepare_map_regions import ART,REF,ROOT,save

def build():
    rows=json.loads((ART/'adventure.json').read_text(encoding='utf8'))['tables']['Map/map']
    canonical={}
    for row in rows:
        mid=int(row['ClassId'])
        current=json.loads((ART/'regions'/f'{mid}.json').read_text(encoding='utf8'))
        canonical.setdefault(row['MapGroundBarePath'] or row['MapGroundPath'],int(current.get('terrain_id',mid)))
    report={'matrix_layout':'Unity native serialized row order','terrains':{},'shared_maps':[]}
    if (REF/'terrain-rebuild.json').exists():
        previous=json.loads((REF/'terrain-rebuild.json').read_text(encoding='utf8'))
        if previous.get('matrix_layout')==report['matrix_layout']:report=previous
    for index,mid in enumerate(canonical.values()):
        if report['terrains'].get(str(mid))==hashlib.sha256((ART/'regions'/f'{mid}.geom').read_bytes()).hexdigest():continue
        log=ROOT/'.tools'/f'rebuild-terrain-{mid}.log'
        with log.open('w',encoding='utf8') as stream:
            result=subprocess.run([sys.executable,str(ROOT/'scripts/prepare_map_regions.py'),'--maps',str(mid)],stdout=stream,stderr=subprocess.STDOUT)
        if result.returncode:raise RuntimeError(f'Terrain {mid} failed; see {log}')
        report['terrains'][str(mid)]=hashlib.sha256((ART/'regions'/f'{mid}.geom').read_bytes()).hexdigest()
        save(REF/'terrain-rebuild.json',report)
        print(f'Rebuilt terrain {index+1}/{len(canonical)}: {mid}',flush=True)
    for row in rows:
        mid=int(row['ClassId']);source=canonical[row['MapGroundBarePath'] or row['MapGroundPath']]
        if mid==source:continue
        path=ART/'regions'/f'{mid}.json';value=json.loads(path.read_text(encoding='utf8'))
        original=json.loads((ART/'regions'/f'{source}.json').read_text(encoding='utf8'))
        for field in ['geometry','counts','skeleton_instances']:value[field]=original[field]
        value['terrain_id']=source;save(path,value);report['shared_maps'].append(mid)
    subprocess.run([sys.executable,str(ROOT/'scripts/prepare_map_layers.py')],check=True)
    save(REF/'terrain-rebuild.json',report)
    print('Shared terrain and all day/night layers rebuilt',flush=True)

if __name__=='__main__':build()
