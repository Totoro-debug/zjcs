"""Replace legacy HUD image aliases with current native Sprite/texture objects."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_current_battle_hud import identity, work_directory
from prepare_unity_profession_skill_icons import ProfessionSprites


HUD = ROOT/'unity/Assets/Official/Combat/HUD'
DEST = HUD/'Images'
CATALOG = ROOT/'unity/Assets/Game/Resources/Combat/OfficialBattleHudCatalog.asset'
RECEIPT = ROOT/'assets/reference/battle-hud/current-images-source-manifest.json'
CATALOG_ROW = re.compile(rb'(  - file: ([^\r\n]+)\r?\n    sprite: )\{fileID: 21300000, guid: ([a-f0-9]{32}), type: ([23])\}')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def guid(path):
    return re.search(rb'(?m)^guid: ([a-f0-9]{32})',Path(str(path)+'.meta').read_bytes())[1].decode()


def referenced_images(apk, wanted):
    """Resolve ambiguous names through actual current prefab PPtrs, not guesses."""
    receipt_path = ROOT/'assets/reference/battle-hud/current-panel-source-manifest.json'
    receipt = json.loads(receipt_path.read_bytes())
    if receipt['manifest'] != apk.version or receipt['apk_sha256'] != apk.source.apk_sha256:
        raise ValueError('Current prefab image reference source differs')
    result,seen,inputs = {},set(),[identity(receipt_path)]
    for prefab in receipt['prefabs']:
        path = ROOT/prefab['output']
        if sha(path.read_bytes()) != prefab['output_sha256']:
            raise ValueError('Current prefab reference snapshot differs: '+str(path))
        inputs.append(identity(path))
        data = json.loads(path.read_bytes())
        for key,pointer in data['external'].items():
            if pointer.get('fileID') != 21300000 or key in seen:
                continue
            seen.add(key)
            cab,path_id = key.rsplit(':',1)
            file = apk.env.get_cab(cab) or apk.env.find_file(cab)
            obj = file.objects[int(path_id)]
            if obj.type.name != 'Sprite':
                raise ValueError('Current native Sprite reference points at another object: '+key)
            name = obj.read().m_Name
            if name in wanted:
                result.setdefault(name,{})[key] = obj
    return result,inputs


def external_consumers(work, bindings):
    patterns = work/'current-hud-previous-guids.txt'
    patterns.write_text('\n'.join(row['previous_guid'] for row in bindings)+'\n',encoding='utf-8')
    scan = subprocess.run(['rg','-l','-F','-f',str(patterns),'unity/Assets',
        '-g','*.asset','-g','*.prefab','-g','*.mat','-g','*.controller','-g','*.anim','-g','*.cs','-g','*.json'],
        cwd=ROOT,capture_output=True,text=True,encoding='utf-8')
    if scan.returncode not in (0,1):
        raise RuntimeError(scan.stderr)
    known = {row['previous_guid']:row['file'] for row in bindings}
    result = []
    for name in scan.stdout.splitlines():
        path = ROOT/name
        if path.resolve() == CATALOG.resolve():
            continue
        raw = path.read_bytes()
        result.append(dict(consumer=identity(path),images=[known[value] for value in known if value.encode() in raw]))
    return result


class HudSprites(ProfessionSprites):
    def __init__(self, apk):
        super().__init__(apk)
        self.folder = DEST

    def write(self, kind, obj, native):
        row = super().write(kind,obj,native)
        key = hashlib.md5(('zjcs:current-hud-images:'+kind+':'+row['identity']).encode()).hexdigest()
        meta = Path(str(ROOT/row['path'])+'.meta')
        self.outputs[meta] = self.outputs[meta].replace(row['guid'].encode(),key.encode())
        self.outputs[meta] = self.outputs[meta].replace(b'\r\n',b'\n').replace(b'\n',b'\r\n')
        if kind == 'Sprite':
            path = ROOT/row['path']
            self.outputs[path] = self.outputs[path].replace(b'\r\n',b'\n').replace(b'\n',b'\r\n')
            row['sha256'] = sha(self.outputs[path])
        row['guid'] = key
        return row


def stage(work):
    work = work_directory(work)
    catalog_raw = CATALOG.read_bytes()
    aliases = {row[1].decode():row[2].decode() for row in CATALOG_ROW.findall(catalog_raw)}
    files = {path.name:path for path in (HUD/'Sprites').glob('*.png')}
    logical = json.loads((HUD/'rules.json').read_bytes())['images']
    by_file = {}
    for address,value in logical.items():
        by_file.setdefault(Path(value['file']).name,[]).append('Assets/Image/'+address+'.png')
    bindings,remaining,removals = [],[],[]
    with Apk.current() as apk:
        raw = apk.source.manifest_bytes('DefaultPackage',apk.archive)
        assets = read_asset_manifest(raw)
        by_path = {row['path']:row for row in assets}
        by_name = {}
        for row in assets:
            if row['path'].startswith('Assets/Image/') and row['path'].lower().endswith('.png'):
                by_name.setdefault(Path(row['path']).stem,[]).append(row)
        wanted = {path.stem.rsplit('-',1)[0] for path in files.values()
            if len(by_name.get(path.stem.rsplit('-',1)[0],[])) != 1}
        references,prefab_inputs = referenced_images(apk,wanted)
        native = HudSprites(apk)
        for name,path in sorted(files.items()):
            if name not in aliases or aliases[name] != guid(path):
                raise ValueError('Review a legacy HUD image binding first: '+name)
            candidates = by_name.get(path.stem.rsplit('-',1)[0],[])
            exact = [by_path[address] for address in by_file.get(name,[]) if address in by_path]
            # Older image loaders omitted atlas subdirectories. Preserve the
            # logical directory to distinguish e.g. monster vs activity icons.
            directory = [row for row in candidates if any(
                row['path'].startswith(str(Path(address).parent).replace('\\','/')+'/')
                for address in by_file.get(name,[]))]
            selected = exact or directory or candidates
            selected = {row['path']:row for row in selected}
            referenced = references.get(path.stem.rsplit('-',1)[0],{})
            if len(selected) != 1 and len(referenced) != 1:
                remaining.append(dict(file=name,logical_paths=by_file.get(name,[]),
                    current_candidates=[row['path'] for row in candidates],
                    current_prefab_objects=list(referenced),
                    reason='Resolve the original prefab/font object before replacement' if not candidates else 'Resolve the original prefab reference to distinguish these current images'))
                continue
            if len(selected) == 1:
                asset, = selected.values()
                obj, = [pointer.deref() for address,pointer in apk.container(asset['bundle'])
                    if address == asset['path'] and pointer.deref().type.name == 'Sprite']
                selection = 'Current typed image address; preserve the logical directory when names collide'
            else:
                obj, = referenced.values()
                source = apk.provenance(obj)
                asset = next((row for row in candidates if row['bundle'] == source['bundle']),None)
                selection = 'Unique current native Sprite PPtr in the complete recovered HUD prefab closure'
            value = native.sprite(obj)
            pointer = dict(fileID=21300000,guid=value['guid'],type=2)
            bindings.append(dict(file=name,previous_guid=aliases[name],asset=asset,selection=selection,
                original=value['identity'],original_sha256=sha(obj.get_raw_data()),
                source=apk.provenance(obj),pointer=pointer))
            removals.extend(identity(item) for item in (path,Path(str(path)+'.meta')))
            if len(bindings)%50 == 0:
                print('Resolved current HUD images:',len(bindings),flush=True)
        replacements = {row['file']:row['pointer'] for row in bindings}
        def replace(match):
            pointer = replacements.get(match[2].decode())
            return match[1]+('{fileID: 21300000, guid: '+pointer['guid']+', type: 2}').encode() if pointer else match[0]
        native.outputs[CATALOG] = CATALOG_ROW.sub(replace,catalog_raw)
        # Folder metadata is a runtime output, never a versioned source alias.
        folder_meta = Path(str(DEST)+'.meta')
        if not folder_meta.exists():
            native.outputs[folder_meta] = ('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:current-hud-images-folder').hexdigest()+
                '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode()
        outputs = []
        for path,content in native.outputs.items():
            if path.suffix == '.meta':
                content = content.replace(b'\r\n',b'\n').replace(b'\n',b'\r\n')
            target = work/'staged'/path.relative_to(ROOT)
            target.parent.mkdir(parents=True,exist_ok=True)
            target.write_bytes(content)
            outputs.append(dict(path=path.relative_to(ROOT).as_posix(),staged=target.relative_to(ROOT).as_posix(),
                sha256=sha(content),previous_sha256=identity(path)['sha256'] if path.is_file() else None))
        retained = external_consumers(work,bindings)
        protected = {name for row in retained for name in row['images']}
        previous_resources = removals
        removals = [row for row in removals if Path(row['path'].removesuffix('.meta')).name not in protected]
        proof = dict(manifest=apk.version,apk_sha256=apk.source.apk_sha256,
            asset_table_sha256=sha(raw),bindings=bindings,sprites=list(native.sprites.values()),
            textures=list(native.textures.values()),outputs=outputs,removals=removals,remaining=remaining,
            previous_resources=previous_resources,retained_legacy_consumers=retained,
            previous_consumer_receipts=[identity(ROOT/name) for name in (
                'assets/reference/npc-icons-source-manifest.json',
                'assets/reference/battle-hud/npc-portraits-source-manifest.json',
                'assets/reference/partner-npc-icons-source-manifest.json')],
            generator=identity(Path(__file__).resolve()),inputs=[identity(HUD/'rules.json'),*prefab_inputs],
            dependencies=[identity(ROOT/'scripts'/name) for name in (
                'prepare_unity_profession_skill_icons.py','prepare_unity_map12_team_panel_sprites.py',
                'prepare_unity_map13_native_scene.py','prepare_unity_profession_reward_sprites.py',
                'official_apk.py','official_manifest.py','official_source.py')])
        fixture = dict(sprites=[row['expected'] for row in native.sprites.values()],textures=[
            dict(path=row['path'].removeprefix('unity/'),width=row['original_fields']['m_Width'],
                height=row['original_fields']['m_Height'],readable=row['original_fields']['m_IsReadable'],
                settings=row['original_fields']['m_TextureSettings'],**{k:row[k] for k in ('format','mips','pixels_sha256')})
            for row in native.textures.values()])
        fixture_path = work/'hud-images-fixture.json'
        fixture_path.write_text(json.dumps(fixture,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        proof['fixture'] = identity(fixture_path)
        proof['scope'] = 'Current original HUD image objects. Virtual image filenames remain loading aliases. Remaining legacy prefab/font images and the main panel layout require their own native migration.'
        proof_path = work/'current-hud-images-source-manifest.json'
        proof_path.write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        print('Staged',len(bindings),'current HUD aliases,',len(native.sprites),'native Sprites,',len(native.textures),'complete textures;',len(remaining),'legacy images still require exact reference recovery.',flush=True)
        return proof


def compose_npc_consumer(previous_catalog, current_catalog):
    """Update the current shared NPC receipt for an unchanged NPC alias subset."""
    canonical = ROOT/'assets/reference/npc-icons-source-manifest.json'
    proof = json.loads(canonical.read_bytes())
    key = CATALOG.relative_to(ROOT).as_posix()
    before = identity(canonical)
    if proof['installed'][key] != previous_catalog:
        raise ValueError('The preceding shared NPC catalog hash differs')
    raw = CATALOG.read_bytes()
    aliases = {row[1].decode():row[2].decode() for row in CATALOG_ROW.findall(raw)}
    for row in proof['hud_aliases']:
        if aliases[row['file']] != row['pointer']['guid']:
            raise ValueError('A current NPC alias changed during HUD image replacement')
    proof.setdefault('consumer_resource_updates',[]).append(dict(
        generator=identity(Path(__file__).resolve()),receipt=RECEIPT.relative_to(ROOT).as_posix(),
        input_receipt=before,path=key,previous_sha256=previous_catalog,sha256=current_catalog,
        operation='Keep every NPC image alias and native pointer unchanged while composing current HUD image bindings into the shared consumer catalog.'))
    proof['installed'][key] = current_catalog
    for row in proof['outputs']:
        if row['path'] == key:
            row['sha256'] = current_catalog
    write_receipt(canonical,proof)
    result = [identity(canonical)]
    for path in (ROOT/'assets/reference/battle-hud/npc-portraits-source-manifest.json',
                 ROOT/'assets/reference/partner-npc-icons-source-manifest.json'):
        data = json.loads(path.read_bytes())
        if data['canonical_receipt'] != before:
            raise ValueError('A preceding NPC consumer receipt differs: '+str(path))
        data['canonical_receipt'] = identity(canonical)
        if isinstance(data.get('inputs'),dict) and before['path'] in data['inputs']:
            data['inputs'][before['path']] = data['canonical_receipt']['sha256']
        write_receipt(path,data)
        result.append(identity(path))
    return result


def write_receipt(path, proof):
    path.write_bytes((json.dumps(proof,ensure_ascii=False,indent=2)+'\n').replace('\n','\r\n').encode('utf-8'))


def activate(work):
    work = work_directory(work)
    proof = json.loads((work/'current-hud-images-source-manifest.json').read_bytes())
    result = json.loads((work/'round-ui-result.json').read_bytes())
    if not result.get('passed'):
        raise ValueError('Complete the real HUD image/round flow validation first')
    for row in [proof['generator'],proof['fixture'],*proof['inputs'],*proof['dependencies'],*proof['previous_consumer_receipts']]:
        if identity(ROOT/row['path']) != row:
            raise ValueError('A current HUD image input changed: '+row['path'])
    project = work/'movement-effects-unity'
    for row in proof['outputs']:
        path,staged = ROOT/row['path'],ROOT/row['staged']
        if identity(staged)['sha256'] != row['sha256'] or (identity(path)['sha256'] if path.is_file() else None) != row['previous_sha256']:
            raise ValueError('A staged or previous HUD resource changed: '+row['path'])
        if not row['path'].endswith('/Images.meta') and identity(project/Path(row['path']).relative_to('unity'))['sha256'] != row['sha256']:
            raise ValueError('Validate the exact current HUD payload before activation: '+row['path'])
    if external_consumers(work,proof['bindings']) != proof['retained_legacy_consumers']:
        raise ValueError('Legacy HUD resource consumers changed before activation')
    for row in proof['removals']:
        path = ROOT/row['path']
        if path.parent.resolve() != (HUD/'Sprites').resolve() or path.is_symlink() or identity(path) != row:
            raise ValueError('Refusing a changed or unowned obsolete HUD image: '+row['path'])
    # No old payload is copied or backed up. Validated replacements become the
    # live resources; only hash-checked, unreferenced old image files are retired.
    for row in proof['outputs']:
        path = ROOT/row['path']
        path.parent.mkdir(parents=True,exist_ok=True)
        path.write_bytes((ROOT/row['staged']).read_bytes())
    for row in proof['removals']:
        (ROOT/row['path']).unlink()
    catalog_row, = [row for row in proof['outputs'] if ROOT/row['path'] == CATALOG]
    proof['consumer_receipts'] = compose_npc_consumer(catalog_row['previous_sha256'],catalog_row['sha256'])
    proof['installed'] = {row['path']:row['sha256'] for row in proof['outputs']}
    proof['outputs'] = [dict(path=row['path'],sha256=row['sha256']) for row in proof['outputs']]
    write_receipt(RECEIPT,proof)
    verify()


def verify():
    proof = json.loads(RECEIPT.read_bytes())
    from official_source import OfficialSource
    source = OfficialSource()
    if proof['manifest'] != source.version or proof['apk_sha256'] != source.apk_sha256:
        raise ValueError('Installed HUD images use another official source')
    from conversion_provenance import verify_recorded_source
    for row in [proof['generator'],*proof['dependencies']]:
        verify_recorded_source(row)
    for row in [*proof['inputs'],*proof['outputs'],*proof['consumer_receipts']]:
        if identity(ROOT/row['path'])['sha256'] != row['sha256']:
            raise ValueError('Current HUD image input or output changed: '+row['path'])
    for row in proof['removals']:
        if (ROOT/row['path']).exists():
            raise ValueError('An obsolete unreferenced HUD image remains: '+row['path'])
    print('Verified current HUD images:',len(proof['bindings']),'aliases;',len(proof['removals']),'obsolete image files removed without backups.',flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work',type=Path)
    parser.add_argument('--activate',action='store_true')
    parser.add_argument('--verify',action='store_true')
    args = parser.parse_args()
    if args.verify:
        verify()
    else:
        if args.work is None:
            parser.error('--work is required')
        (activate if args.activate else stage)(args.work.resolve())
