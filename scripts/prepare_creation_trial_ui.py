"""Recover the APK's native trial item hierarchy and dynamic quality sprites."""
import hashlib
import json
from pathlib import Path
from official_apk import Apk, ROOT
from prepare_official_panels import PanelExporter
from prepare_official_city import PanelSchemas
import prepare_official_city
from UnityPy.classes.PPtr import PPtr

DEST = ROOT / 'unity/Assets/Official/CharacterCreation/TrialUI'

def build():
    prepare_official_city.ART = DEST
    with Apk() as apk:
        # The shared exporter constructor is restricted to its city version.
        # This independent extraction records the currently selected APK inputs.
        e = PanelExporter.__new__(PanelExporter)
        e.apk = apk
        e.schemas = e.ui_schemas = PanelSchemas()
        for key in ('records', 'resources', 'inputs', 'source_records', 'unsupported',
                    'material_inputs', 'layout_inputs', 'language_sources', '_sprites', '_mono_names'):
            setattr(e, key, {})
        e._language = e._guid_paths = None
        e._guid_sprites = {}
        panel = e.ui('prefabs_ui_item', '/ItemIconAppendAppearance.prefab')
        bundle = next(n for n in apk.index if 'scriptableconfig' in n and 'itemicon' in n)
        config = apk.asset(bundle, '/ItemIconConfig.asset')
        fields = e.schemas.read(config)
        quality = [e.sprite(PPtr(**ptr, assetsfile=config.assets_file)) for ptr in fields['QualityBgImages']]
        # The existing trial group manifest pins the item IDs and conditions.
        table_path = ROOT / '.tools/source-packages/official-data/tables.json'
        tables = json.loads(table_path.read_text('utf-8'))
        wanted = {35506,35556,35599,35600,36008,36108,36001,36101,35891,35893,39815,39816}
        items = {str(r['ClassId']): r['Quality'] for r in tables['item'] if r['ClassId'] in wanted}
        if len(items) != len(wanted): raise ValueError('Missing pinned trial item quality')
        nodes, sprites = [], {}
        def flatten(n):
            i = len(nodes); nodes.append(n)
            for animation in n.get('animations', []):
                for track in animation['tracks']:
                    track['keys'] = [dict(time=k[0],value=k[1],in_tangent=k[2],out_tangent=k[3]) for k in track['keys']]
            for field in ('image','text','layout_element','layout','grid','aspect_fitter','size_fitter','scroll','safe_area'):
                n['has_' + field] = field in n
            n['has_canvas_group'] = 'alpha' in n
            n['show_mask_graphic'] = n.get('mask', False)
            if 'mask' in n: n['mask'] = True
            sprite = n.get('image',{}).get('sprite')
            if sprite: sprites[sprite['file']] = sprite
            n['child_ids'] = [flatten(c) for c in n.pop('children')]
            return i
        root = flatten(panel['root'])
        for s in quality: sprites[s['file']] = s
        outputs = []
        def record(path):
            return dict(path=path.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(path.read_bytes()).hexdigest())
        bindings = []
        for key,s in sprites.items():
            source = DEST / 'sprites' / Path(key).name
            target = DEST / 'sprites' / source.name
            target.parent.mkdir(parents=True,exist_ok=True)
            bindings.append(dict(key=key,path=target.relative_to(ROOT/'unity').as_posix(),border=s['border'],ppu=s['ppu']))
            outputs.append(record(target))
        data = dict(manifest=apk.version,nodes=nodes,creation_root=root,sprites=bindings,
                    quality_keys=[s['file'] for s in quality],trial_items=[dict(id=int(k),quality=v) for k,v in items.items()])
        target = DEST/'trial-ui.json'
        target.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
        outputs.append(record(target))
        receipt = dict(manifest=apk.version,source=panel['source'],config_source=apk.provenance(config),
                       trial_item_input=record(table_path),
                       operation='Decode official ItemIconAppendAppearance hierarchy, mask and ItemIconConfig quality bindings.',
                       objects=e.inputs,sprites=e.records,outputs=outputs)
        path=ROOT/'assets/reference/character-creation/trial-ui-source-manifest.json'
        path.write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
        print('Recovered trial icon hierarchy and',len(bindings),'sprites')

if __name__ == '__main__': build()
