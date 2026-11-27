"""Install verified current native inventory Sprites with stable UI aliases."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile

from official_apk import ROOT
from official_source import OfficialSource
from prepare_character_creation_group_icons import folder_meta, text_meta
from refresh_current_character_meshes import preserve
from prepare_current_inventory_sprites import PAYLOAD, CATALOG, source_records


PROOF = Path('assets/reference/inventory-current-sprites-source-manifest.json')
BINDINGS = PAYLOAD/'current-sprites.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def verify_output(row):
    raw = (ROOT/row['path']).read_bytes()
    if sha(raw) != row['sha256'] and (not row.get('sha256_lf') or sha(raw.replace(b'\r\n', b'\n')) != row['sha256_lf']):
        raise ValueError('Installed current inventory asset changed: '+row['path'])


def atomic_write(path, raw):
    if path.exists() and path.read_bytes() == raw:
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    handle, name = tempfile.mkstemp(prefix='.current-inventory-', suffix='.tmp', dir=path.parent)
    temporary = Path(name)
    try:
        with os.fdopen(handle, 'wb') as output:
            output.write(raw)
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def canonical_assets(cache, natives):
    patterns = cache/'installed-guid-patterns.txt'
    patterns.write_text(''.join('guid: '+key+'\n' for key in sorted(natives)), 'ascii')
    found = subprocess.run(['rg', '--hidden', '--no-ignore', '-l', '-F', '-f', str(patterns),
                            '-g', '*.meta', 'unity/Assets'], cwd=ROOT, capture_output=True,
                           encoding='utf8', check=False)
    if found.returncode not in (0, 1):
        raise ValueError('Cannot inspect current native GUIDs: '+found.stderr)
    result, variants = {}, {}
    for relative in found.stdout.splitlines():
        meta = ROOT/relative
        raw = meta.read_bytes()
        match = re.search(rb'(?m)^guid: ([a-f0-9]{32})\r?$', raw)
        if not match or match[1].decode() not in natives:
            continue
        guid = match[1].decode()
        asset = Path(str(meta)[:-5])
        if not asset.is_file() or asset.suffix != '.asset':
            raise ValueError('Inspect an existing native GUID owner: '+relative)
        if guid in result or guid in variants:
            raise ValueError('Duplicate installed native GUID: '+guid)
        original = (ROOT/natives[guid]['path']).read_bytes()
        if asset.read_bytes().replace(b'\r\n', b'\n') != original.replace(b'\r\n', b'\n'):
            if natives[guid]['file_id'] != 21300000:
                raise ValueError('Existing canonical texture differs from current original conversion: '+str(asset))
            # Existing consumers may retain the original zero UV stream and use
            # Unity's runtime transform. Give the explicitly resolved mesh its
            # own Sprite identity; keep the existing asset and shared texture.
            variants[guid] = dict(path=asset.relative_to(ROOT).as_posix(), sha256=sha(asset.read_bytes()))
            continue
        result[guid] = asset
    return result, variants


def install():
    source = OfficialSource()
    cache = source.prepared_path/'inventory/native-sprites'
    staged_path = cache/'source-manifest.json'
    staged_raw = staged_path.read_bytes()
    staged = json.loads(staged_raw)
    if not staged['completed'] or staged['manifest'] != source.version or staged['apk_sha256'] != source.apk_sha256:
        raise ValueError('A completed selected native inventory conversion is required')
    prior_path = ROOT/PROOF
    if prior_path.exists():
        prior = json.loads(prior_path.read_bytes())
        if prior.get('staged_sha256') == sha(staged_raw):
            for row in prior['outputs']+prior['shared_dependencies']:
                verify_output(row)
            return prior
    for row in staged['inputs']+staged['generators']:
        if sha((ROOT/row['path']).read_bytes()) != row['sha256']:
            raise ValueError('Inventory conversion input changed: '+row['path'])
    records, _ = source_records()
    aliases = {row['alias']: row for row in staged['aliases']}
    if set(records) != set(aliases):
        raise ValueError('Installed inventory alias set changed after conversion')
    natives = {row['guid']: row for row in staged['sprites']+staged['textures']}
    for native in natives.values():
        for suffix, key in [('', 'sha256'), ('.meta', 'meta_sha256')]:
            path = Path(str(ROOT/native['path'])+suffix).resolve()
            if not path.is_relative_to(cache.resolve()) or sha(path.read_bytes()) != native[key]:
                raise ValueError('Prepared native inventory asset changed: '+str(path))
    canonical, variants = canonical_assets(cache, natives)
    actual_guids = {guid: hashlib.md5(('zjcs:inventory:resolved-native:'+native['identity']+':'+native['sha256']).encode()).hexdigest()
                    if guid in variants else guid for guid,native in natives.items()}
    planned, installed, shared = {}, {}, {}
    for guid, native in natives.items():
        if guid in canonical:
            path = canonical[guid]
            for suffix in ('', '.meta'):
                item = Path(str(path)+suffix)
                shared[item.relative_to(ROOT).as_posix()] = dict(path=item.relative_to(ROOT).as_posix(), sha256=sha(item.read_bytes()))
        else:
            is_texture = native['file_id'] == 2800000
            name = ('texture_' if is_texture else 'sprite_')+sha(native['identity'].encode())[:16]+'.asset'
            path = ROOT/PAYLOAD/'NativeSprites'/('Textures' if is_texture else '')/name
            for suffix in ('', '.meta'):
                target = Path(str(path)+suffix)
                raw = Path(str(ROOT/native['path'])+suffix).read_bytes()
                if suffix and guid in variants:
                    raw = raw.replace(('guid: '+guid).encode(), ('guid: '+actual_guids[guid]).encode())
                if target.exists() and target.read_bytes() != raw:
                    raise ValueError('Preserve edited inventory native output: '+str(target))
                planned[target] = raw
        installed[guid] = path.relative_to(ROOT/'unity').as_posix()
    rows = []
    for alias in records:
        guid = aliases[alias]['native']['guid']
        path = ROOT/'unity'/installed[guid]
        raw = planned[path] if path in planned else path.read_bytes()
        rows.append(dict(alias=alias, path=installed[guid], guid=actual_guids[guid], sha256=sha(raw.replace(b'\r\n',b'\n'))))
    binding_raw = (json.dumps(dict(manifest=source.version, asset_hash_encoding='utf8-lf', aliases=rows), ensure_ascii=False, indent=2)+'\n').encode()
    planned[ROOT/BINDINGS] = binding_raw
    binding_meta = Path(str(ROOT/BINDINGS)+'.meta')
    if not binding_meta.exists():
        planned[binding_meta] = text_meta(BINDINGS.relative_to('unity').as_posix())
    for folder in (PAYLOAD/'NativeSprites', PAYLOAD/'NativeSprites/Textures'):
        path = Path(str(ROOT/folder)+'.meta')
        if not path.exists():
            planned[path] = folder_meta(folder.relative_to('unity').as_posix())
    raw = (ROOT/CATALOG).read_bytes()
    text = raw.decode('utf8')
    changed = set()

    def bind(match):
        import yaml
        alias = yaml.safe_load(match[1])
        if alias not in aliases:
            raise ValueError('Unknown inventory alias during native rebinding: '+alias)
        changed.add(alias)
        guid = actual_guids[aliases[alias]['native']['guid']]
        return match[0][:match[0].index('{')]+f'{{fileID: 21300000, guid: {guid}, type: 2}}'

    text = re.sub(r'(?m)^  - sourcePath: ([^\r\n]+)\r?\n    sprite: \{fileID: [-0-9]+, guid: [a-f0-9]{32}, type: [0-9]+\}', bind, text)
    if changed != set(records):
        raise ValueError('Some installed inventory aliases were not rebound')
    planned[ROOT/CATALOG] = text.encode('utf8')
    snapshots = {path: sha(path.read_bytes()) if path.exists() else None for path in planned}
    for path, raw in planned.items():
        if not path.resolve().is_relative_to((ROOT/'unity/Assets').resolve()):
            raise ValueError('Inventory output is outside Unity Assets')
    outputs = []
    for path, raw in planned.items():
        if (sha(path.read_bytes()) if path.exists() else None) != snapshots[path]:
            raise ValueError('Inventory output changed during installation: '+str(path))
        previous = preserve(path) if path.exists() and path.read_bytes() != raw else None
        atomic_write(path, raw)
        row = dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(raw))
        if previous:
            row['previous'] = previous
        outputs.append(row)
    result = dict(manifest=source.version, apk_sha256=source.apk_sha256, staged_sha256=sha(staged_raw),
                  staged_source=staged_path.relative_to(ROOT).as_posix(),
                  generator_sha256=sha(Path(__file__).read_bytes()),
                  aliases=[dict(alias=alias, path=installed[row['native']['guid']], guid=actual_guids[row['native']['guid']], native=row['native']) for alias,row in aliases.items()],
                  textures=[dict(path=installed[row['guid']], native=row) for row in staged['textures']],
                  outputs=outputs, shared_dependencies=list(shared.values()),
                  retained_sprite_representations=variants,
                  binding_generator=dict(path='unity/Assets/Game/Editor/Inventory/OfficialInventoryAssetTools.cs',
                      sha256=sha((ROOT/'unity/Assets/Game/Editor/Inventory/OfficialInventoryAssetTools.cs').read_bytes())),
                  retained_previous_sprites='Historical PNGs remain until their serialized and dynamic consumers have been checked after runtime validation.')
    if prior_path.exists():
        result['previous_receipt'] = preserve(prior_path)
    atomic_write(prior_path, (json.dumps(result, ensure_ascii=False, indent=2)+'\n').encode('utf8'))
    print('Installed current inventory native bindings:', len(aliases), 'shared native assets:', len(canonical), flush=True)
    return result


def record_bindings():
    """Verify installed aliases and record the final adapters without changing native art."""
    source = OfficialSource()
    path = ROOT/PROOF
    proof = json.loads(path.read_bytes())
    if proof['manifest'] != source.version or proof['apk_sha256'] != source.apk_sha256:
        raise ValueError('Record only the selected inventory payload')
    for row in proof['outputs']+proof['shared_dependencies']:
        verify_output(row)
    code = source.prepared_path/'official-code'
    source_files = [code/'client-source/ItemProgress.cs', code/'framework-firstpass-source/MultiLanguageText.cs']
    source_inputs = source.verify_code_files(source_files)
    previous = preserve(path)
    staged_path = ROOT/proof['staged_source']
    if sha(staged_path.read_bytes()) != proof['staged_sha256']:
        raise ValueError('Prepared inventory source receipt changed')
    staged = json.loads(staged_path.read_bytes())
    normalized = []
    for row in staged['generators']:
        current = (ROOT/row['path']).read_bytes()
        if sha(current) == row['sha256']:
            continue
        historical = ROOT/'.tools/source-packages/history/converted'/(row['sha256']+'.bin')
        if not historical.is_file() or sha(historical.read_bytes()) != row['sha256'] or historical.read_bytes().replace(b'\r\n',b'\n') != current.replace(b'\r\n',b'\n'):
            raise ValueError('A conversion algorithm changed after staging: '+row['path'])
        normalized.append(dict(path=row['path'], previous_sha256=row['sha256'], sha256=sha(current), snapshot=historical.relative_to(ROOT).as_posix()))
        row['sha256'] = sha(current)
    if normalized:
        staged['generator_line_ending_source'] = preserve(staged_path)
        staged['generator_line_endings'] = normalized
        atomic_write(staged_path,(json.dumps(staged,ensure_ascii=False,indent=2)+'\n').encode('utf8'))
        proof['staged_sha256'] = sha(staged_path.read_bytes())
    binding_path = ROOT/BINDINGS
    bindings = json.loads(binding_path.read_bytes())
    bindings['asset_hash_encoding'] = 'utf8-lf'
    expected = {row['alias']:row['guid'] for row in proof['aliases']}
    if {row['alias']:row['guid'] for row in bindings['aliases']} != expected:
        raise ValueError('Installed inventory native alias mapping differs')
    for row in bindings['aliases']:
        row['sha256'] = sha((ROOT/'unity'/row['path']).read_bytes().replace(b'\r\n',b'\n'))
    text = (ROOT/CATALOG).read_text('utf8')
    import yaml
    actual = [(yaml.safe_load(alias),guid) for alias,guid in re.findall(
        r'(?m)^  - sourcePath: ([^\r\n]+)\r?\n    sprite: \{fileID: 21300000, guid: ([a-f0-9]{32}), type: 2\}',text)]
    if len(actual) != len(expected) or dict(actual) != expected:
        raise ValueError('Serialized inventory catalog does not bind the current native Sprites')
    atomic_write(binding_path,(json.dumps(bindings,ensure_ascii=False,indent=2)+'\n').encode('utf8'))
    for row in proof['outputs']+proof['shared_dependencies']:
        raw = (ROOT/row['path']).read_bytes()
        row.update(sha256=sha(raw),sha256_lf=sha(raw.replace(b'\r\n',b'\n')))
    proof['runtime_label_binding'] = dict(path='unity/Assets/Game/Runtime/Presentation/Inventory/OfficialInventoryPanelView.cs',
        sha256=sha((ROOT/'unity/Assets/Game/Runtime/Presentation/Inventory/OfficialInventoryPanelView.cs').read_bytes()),
        source_inputs=source_inputs)
    binding_code = ROOT/'unity/Assets/Game/Editor/Inventory/OfficialInventoryAssetTools.cs'
    proof['binding_generator'] = dict(path=binding_code.relative_to(ROOT).as_posix(),sha256=sha(binding_code.read_bytes()))
    proof['previous_binding_receipt'] = previous
    proof['recording_generator_sha256'] = sha(Path(__file__).read_bytes())
    atomic_write(path,(json.dumps(proof,ensure_ascii=False,indent=2)+'\n').encode('utf8'))
    print('Verified current native inventory bindings:',len(expected),flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--record-bindings',action='store_true')
    args = parser.parse_args()
    (record_bindings if args.record_bindings else install)()
