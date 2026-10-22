"""Package the decoded official creation layouts and their explicit sprite closure.

The AssetRipper export lacks serialized MonoBehaviour fields. Use the pinned,
decoded layout data instead; retain that export as a source reference.
"""
import hashlib
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'unity/Assets/Official/CharacterCreation/RuntimeUI'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build():
    creation = ROOT / 'unity/Assets/Official/CharacterCreation/creation-performance.json'
    panels = ROOT / 'assets/official/ui.json'
    source_manifest = json.loads((creation.parent / 'source-manifest.json').read_text(encoding='utf-8'))
    expected = next(v['sha256'] for v in source_manifest['outputs'] if v['name'] == creation.name)
    if digest(creation) != expected or source_manifest['manifest'] != '854_156383':
        raise ValueError('Creation layout differs from its original extraction receipt')
    ui = json.loads(panels.read_text(encoding='utf-8'))
    ui_receipt = json.loads((ROOT / 'assets/reference/official-city/source-manifest.json').read_text(encoding='utf-8'))
    if digest(panels) != ui_receipt['data_sha256']['ui.json'] or ui_receipt['manifest'] != '854_156383':
        raise ValueError('Official DIY/confirmation layouts differ from their original extraction receipt')
    data = dict(manifest='854_156383',
                creation=json.loads(creation.read_text(encoding='utf-8'))['root'],
                diy=ui['creation_diy']['root'], confirm=ui['common_confirm']['root'])
    from official_apk import Apk
    from official_unity_schema import ScriptSchemas
    from npc_travel_language import language
    apk = Apk()
    bundle = 'defaultpackage_assets_prefabs_ui_createplayer.bundle'
    component = next(o for o in apk.objects(bundle) if o.path_id == -3964310077427183409)
    title_keys = ScriptSchemas().read(component)['StepTitles']
    localized, language_sources = language(title_keys)
    data['page_titles'] = [localized[key] for key in title_keys]
    sprites = {}

    def visit(node):
        for field in ("image", "text", "layout_element", "layout", "grid", "aspect_fitter", "size_fitter", "scroll", "safe_area"):
            node["has_" + field] = field in node
        node["has_canvas_group"] = "alpha" in node
        node["alpha"] = node.get("alpha", 1)
        image = node.get('image', {})
        sprite = image.get('sprite')
        if sprite:
            key = sprite['file']
            if not key.startswith('res://assets/official/sprites/'):
                raise ValueError('Unexpected official sprite source: ' + key)
            source = ROOT / key.removeprefix('res://')
            target = DEST / 'Sprites' / source.name
            record = dict(source=key.removeprefix('res://'), source_sha256=digest(source),
                          path=target.relative_to(ROOT).as_posix(), sha256=digest(source),
                          border=sprite.get('border', [0, 0, 0, 0]), ppu=sprite.get('ppu', 100))
            if key in sprites and sprites[key] != record:
                raise ValueError('Conflicting official sprite metadata: ' + key)
            sprites[key] = record
        for child in node.get('children', []):
            visit(child)

    for key in ('creation', 'diy', 'confirm'):
        visit(data[key])
    DEST.mkdir(parents=True, exist_ok=True)
    outputs = []
    for record in sprites.values():
        target = ROOT / record['path']
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists() and digest(target) != record['sha256']:
            raise ValueError('Refusing to overwrite changed sprite: ' + str(target))
        if not target.exists():
            shutil.copyfile(ROOT / record['source'], target)
        outputs.append(record)
    nodes = []
    def flatten(node):
        index = len(nodes)
        nodes.append(node)
        children = node.pop('children', [])
        node['child_ids'] = [flatten(child) for child in children]
        return index
    for key in ('creation', 'diy', 'confirm'):
        data[key + '_root'] = flatten(data.pop(key))
    data['nodes'] = nodes
    data['sprites'] = [dict(key=key, path=record['path'].removeprefix('unity/'),
                            border=record['border'], ppu=record['ppu']) for key, record in sorted(sprites.items())]
    path = DEST / 'runtime-ui.json'
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    outputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=digest(path)))
    receipt = dict(manifest='854_156383', operation='Restore original decoded creation UI fields as native Unity UGUI/TMP components; preserve hierarchy, coordinates, sprites and text.',
                   inputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p)) for p in (creation, panels)],
                   outputs=outputs)
    receipt['title_source'] = dict(bundle=bundle, bundle_md5=apk.index[bundle],
                                  component_path_id=str(component.path_id), keys=title_keys,
                                  language_sources=language_sources)
    encoded = json.dumps(receipt, ensure_ascii=False, indent=2) + '\n'
    for path in (DEST / 'source-manifest.json', ROOT / 'assets/reference/character-creation/runtime-ui-source-manifest.json'):
        path.write_text(encoded, encoding='utf-8', newline='\n')
    print(f'Packaged official creation UI: {len(sprites)} sprites, creation/DIY/confirmation layouts.')


if __name__ == '__main__':
    build()
