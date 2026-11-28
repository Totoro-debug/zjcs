"""Migrate NPC/profession images to one current native Sprite/texture closure."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

import yaml

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest, read_manifest
from prepare_battle_scene_player_appearance import CurrentConstructors
from prepare_current_battle_hud import identity, work_directory
from prepare_unity_profession_skill_icons import ProfessionSprites
from prepare_unity_profession_reward_sprites import Dumper
from prepare_current_partner_native import documents


NATIVE = ROOT/'unity/Assets/Official/Shared/NpcImages'
RECEIPT = ROOT/'assets/reference/npc-icons-source-manifest.json'
BATTLE = ROOT/'unity/Assets/Official/Combat/NpcPortraits'
BATTLE_JSON = BATTLE/'Resources/Combat/battle-npc-portraits.json'
BATTLE_ASSET = BATTLE/'Resources/Combat/OfficialBattleNpcPortraitCatalog.asset'
BATTLE_RECEIPT = ROOT/'assets/reference/battle-hud/npc-portraits-source-manifest.json'
PARTNER = ROOT/'unity/Assets/Game/Resources/Partners/OfficialPartnerNpcIconImages.asset'
PARTNER_RECEIPT = ROOT/'assets/reference/partner-npc-icons-source-manifest.json'
HUD = ROOT/'unity/Assets/Game/Resources/Combat/OfficialBattleHudCatalog.asset'
HEADER = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'
DEPENDENCIES = ('prepare_unity_profession_skill_icons.py','prepare_unity_map12_team_panel_sprites.py',
    'prepare_unity_map13_native_scene.py','prepare_battle_scene_player_appearance.py','official_tables.py',
    'prepare_unity_profession_reward_sprites.py','official_apk.py','official_manifest.py','official_source.py',
    'prepare_current_damage_ui.py','prepare_current_battle_hud.py')


def sha(raw): return hashlib.sha256(raw).hexdigest()


def meta_guid(path):
    return re.search(rb'(?m)^guid: ([a-f0-9]{32})',Path(str(path)+'.meta').read_bytes())[1].decode()


class NpcSprites(ProfessionSprites):
    def __init__(self, apk):
        super().__init__(apk)
        self.folder = NATIVE

    def write(self, kind, obj, native):
        row = super().write(kind,obj,native)
        key = hashlib.md5(('zjcs:shared-npc-images:'+kind+':'+row['identity']).encode()).hexdigest()
        path = ROOT/row['path']; meta = Path(str(path)+'.meta')
        self.outputs[meta] = self.outputs[meta].replace(row['guid'].encode(),key.encode())
        row['guid'] = key
        return row


def missing_checks(apk, missing):
    """Check the embedded APK and current downloaded NPC objects separately."""
    wanted = {Path(row['Icon']).name for row in missing if row['NpcType'] != 2 and row['Icon']}
    embedded = apk.archive.read('assets/yoo/DefaultPackage/PackageManifest_DefaultPackage_'+apk.source.apk_version+'.v3.bytes')
    embedded_assets = read_asset_manifest(embedded)
    matching = [row for row in embedded_assets if Path(row['path']).stem in wanted]
    if matching: raise ValueError('Restore an additional embedded NPC image: '+repr(matching))
    checks = []
    import UnityPy
    for row in read_manifest(embedded)['bundles']:
        if 'image_ui_npc_npc_icon_' not in row['name']: continue
        entry = 'assets/yoo/DefaultPackage/'+row['file']
        if entry not in apk.available: continue
        raw = apk.archive.read(entry); environment = UnityPy.load(Apk.decode(raw))
        objects = list(environment.objects)
        found = [obj.read().m_Name for obj in objects if obj.type.name in ('Sprite','Texture2D') and obj.read().m_Name in wanted]
        if found: raise ValueError('Recover an embedded NPC object before deferring it: '+repr(found))
        checks.append(dict(origin='apk_zip',apk_entry=entry,raw_sha256=sha(raw),objects=len(objects),matching_objects=found))
    for name in apk.index:
        if 'image_ui_npc_npc_icon_' not in name: continue
        objects = apk.objects(name)
        found = [obj.read().m_Name for obj in objects if obj.type.name in ('Sprite','Texture2D') and obj.read().m_Name in wanted]
        if found: raise ValueError('Recover a downloaded NPC object before deferring it: '+repr(found))
        checks.append(dict(source=apk.provenance(objects[0]),objects=len(objects),matching_objects=found))
    return dict(npc_ids=[row['Id'] for row in missing if row['NpcType'] != 2],
        embedded_manifest_sha256=sha(embedded),matching_embedded_assets=matching,inspected_bundles=checks)


def stage(work):
    work = work_directory(work)
    old_battle = json.loads(BATTLE_JSON.read_bytes())
    old_partner = json.loads(PARTNER_RECEIPT.read_bytes())
    old_battle_proof = json.loads(BATTLE_RECEIPT.read_bytes())
    with Apk.current() as apk:
        code = apk.source.prepared_path/'official-code'
        code_receipt = code.parent/'code-manifest.json'; code_proof = json.loads(code_receipt.read_bytes())
        assembly = code/'managed/decoded/Console.dll'
        if code_proof['manifest'] != apk.version or identity(assembly)['sha256'] != code_proof['assemblies']['Console.dll']['decoded_sha256']:
            raise ValueError('Current NPC table assembly differs')
        decoder = CurrentConstructors(code)
        types = {match[2].rsplit('/',1)[-1]:match[1] for match in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)',decoder.source('Tables'))}
        tables, inputs = {}, []
        for name in ('npc','profession_base'):
            obj = apk.asset('config_luban','/'+name+'.g.bytes'); raw = obj.read().m_Script.encode('utf-8','surrogateescape')
            tables[name] = json.loads(json.dumps(decoder.table(raw,types[name]),ensure_ascii=False))
            inputs.append(dict(name=name,source=apk.provenance(obj),object_sha256=sha(obj.get_raw_data()),sha256=sha(raw),rows=len(tables[name])))
        manifest_bytes = apk.source.manifest_bytes('DefaultPackage',apk.archive)
        records = read_asset_manifest(manifest_bytes); by_path = {row['path']:row for row in records}
        requests, missing = {}, []
        for npc in tables['npc']:
            if not npc['Icon']: continue
            if 'Assets/Image/'+npc['Icon']+'.png' not in by_path:
                missing.append(npc); continue
            requests.setdefault(npc['Icon'],[]).append(dict(npc=npc['Id'],field='Icon'))
        for profession in tables['profession_base']:
            for field in ('Icon1','Icon3'):
                if profession[field]: requests.setdefault(profession[field],[]).append(dict(profession=profession['Profession'],field=field))
        checks = missing_checks(apk,missing)
        native = NpcSprites(apk); bindings, by_address = [], {}
        for address, consumers in sorted(requests.items()):
            asset = by_path['Assets/Image/'+address+'.png']
            obj, = [pointer.deref() for path,pointer in apk.container(asset['bundle']) if path == asset['path'] and pointer.deref().type.name == 'Sprite']
            value = native.sprite(obj)
            pointer = dict(fileID=21300000,guid=value['guid'],type=2)
            row = dict(address=address,consumers=consumers,pointer=pointer,manifest_asset=asset,
                original=value['identity'],original_sha256=sha(obj.get_raw_data()),source=apk.provenance(obj))
            bindings.append(row); by_address[address] = row
        if {row['address'] for row in old_partner['sprites']} - by_address.keys():
            raise ValueError('An installed partner image address is absent from the selected source')
        battle_data = documents(BATTLE_ASSET.read_text('utf-8-sig')); battle_fields = next(iter(battle_data.values()))['MonoBehaviour']
        npc_bindings = [dict(npcClassId=row['Id'],icon=row['Icon'],sprite=by_address[row['Icon']]['pointer'] if row['Icon'] in by_address else dict(fileID=0)) for row in tables['npc']]
        battle_fields['bindings'] = npc_bindings
        native.outputs[BATTLE_ASSET] = (HEADER+yaml.dump(next(iter(battle_data.values())),Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000)).encode('utf-8')
        sprite_rows = {row['identity']:row for row in native.sprites.values()}
        catalog_rows = []
        for row in npc_bindings:
            image = by_address.get(row['icon'])
            catalog_rows.append(dict(row,sprite=ROOT.joinpath(sprite_rows[image['original']]['path']).relative_to(ROOT/'unity').as_posix() if image else ''))
        required = set(old_battle['battleNpcIds']); known = {row['npcClassId'] for row in npc_bindings}
        if required-known: raise ValueError('Existing battle rules reference a removed NPC definition')
        unresolved = sorted(required-{row['npcClassId'] for row in npc_bindings if row['sprite'].get('guid')})
        deferred = [dict(npcClassId=row['Id'],icon=row['Icon'],npcType=row['NpcType'],reason='Original empty Icon' if not row['Icon'] else ('MapExploreBoss legacy icon path' if row['NpcType']==2 else 'Configured image absent from current APK and downloaded NPC resources'))
            for row in tables['npc'] if row['Icon'] not in by_address]
        summary_sprites = []
        for address in sorted({row['Icon'] for row in tables['npc'] if row['Icon'] in by_address}):
            value = sprite_rows[by_address[address]['original']]; e = value['expected']
            summary_sprites.append(dict(path=value['path'].removeprefix('unity/'),name=e['name'],width=e['rect']['width'],height=e['rect']['height'],ppu=e['ppu'],pivot=[e['pivot']['x']/e['rect']['width'],e['pivot']['y']/e['rect']['height']],guid=value['guid'],sha256=value['sha256']))
        native.outputs[BATTLE_JSON] = (json.dumps(dict(manifest=apk.version,bindings=catalog_rows,sprites=summary_sprites,battleNpcIds=sorted(required),unresolvedBattleNpcIds=unresolved,deferred=deferred),ensure_ascii=False,separators=(',',':'))+'\n').encode('utf-8')
        partner_data = documents(PARTNER.read_text('utf-8-sig')); partner_fields = next(iter(partner_data.values()))['MonoBehaviour']
        loader_aliases = {row['Path']:row['AssetGuid'] for row in partner_fields['Entries']}
        partner_fields['Entries'] = [dict(Path='Assets/Image/'+row['address'],AssetGuid=loader_aliases.get('Assets/Image/'+row['address'],row['manifest_asset']['guid']),Asset=row['pointer']) for row in bindings]
        native.outputs[PARTNER] = (HEADER+yaml.dump(next(iter(partner_data.values())),Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000)).encode('utf-8')
        by_id = {row['npcClassId']:row for row in npc_bindings}
        old_by_name = {Path(row['icon']).name:row['npcClassId'] for row in old_battle['bindings'] if row['sprite']}
        removals = set(old_partner['installed_output_sha256'])-{PARTNER.relative_to(ROOT).as_posix(),str(PARTNER.relative_to(ROOT)).replace('\\','/')+'.meta'}
        removals.update(str(path.relative_to(ROOT)).replace('\\','/') for path in (BATTLE/'Textures').rglob('*') if path.is_file())
        replacements, aliases = {}, []
        for path in sorted((ROOT/'unity/Assets/Official/Combat/HUD/Sprites').glob('npc_icon_*.png')):
            for item in (path,Path(str(path)+'.meta')):
                committed = subprocess.check_output(['git','cat-file','--filters','HEAD:'+item.relative_to(ROOT).as_posix()],cwd=ROOT)
                if committed.replace(b'\r\n',b'\n') != item.read_bytes().replace(b'\r\n',b'\n'):
                    raise ValueError('Preserve an edited HUD portrait: '+str(item))
            old_name = path.stem.split('-',1)[0]
            npc_id = old_by_name[old_name]; pointer = by_id[npc_id]['sprite']
            if not pointer.get('guid'): raise ValueError('Resolve an installed HUD portrait before retiring it: '+path.name)
            replacements[meta_guid(path)] = pointer
            aliases.append(dict(file=path.name,npc_id=npc_id,pointer=pointer))
            removals.update(item.relative_to(ROOT).as_posix() for item in (path,Path(str(path)+'.meta')))
        hud_raw = HUD.read_bytes()
        def replace_pointer(match):
            value = replacements.get(match[1].decode())
            return ('{fileID: 21300000, guid: '+value['guid']+', type: 2}').encode() if value else match[0]
        native.outputs[HUD] = re.sub(rb'\{fileID: 21300000, guid: ([a-f0-9]{32}), type: [23]\}',replace_pointer,hud_raw)
        for row in bindings:
            value = sprite_rows[row['original']]
            # Every old partner-owned dependency is replaced by this complete
            # current closure; its only external consumer is the image catalog.
            row['native_sprite'] = value['path']
        for folder in (NATIVE,NATIVE.parent):
            meta = Path(str(folder)+'.meta')
            native.outputs[meta] = meta.read_bytes() if meta.exists() else ('fileFormatVersion: 2\nguid: '+hashlib.md5(('zjcs:folder:'+folder.relative_to(ROOT).as_posix()).encode()).hexdigest()+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode()
        outputs = []
        for path,raw in native.outputs.items():
            target = work/'staged'/path.relative_to(ROOT); target.parent.mkdir(parents=True,exist_ok=True); target.write_bytes(raw)
            outputs.append(dict(path=path.relative_to(ROOT).as_posix(),staged=target.relative_to(ROOT).as_posix(),sha256=sha(raw),previous_sha256=identity(path)['sha256'] if path.is_file() else None))
        removals -= {row['path'] for row in outputs}
        removed = []
        for name in sorted(removals):
            path = ROOT/name
            if not path.is_file(): raise FileNotFoundError(path)
            # Earlier folder GUIDs can be retained without an old payload.
            if path.suffix=='.meta' and Path(str(path)[:-5]).is_dir(): continue
            expected = old_partner['installed_output_sha256'].get(name) or old_battle_proof['outputs'].get(name)
            if expected:
                if identity(path)['sha256'] != expected and (path.suffix=='.png' or sha(path.read_bytes().replace(b'\r\n',b'\n')) != expected):
                    raise ValueError('Preserve a changed old NPC image: '+name)
            else:
                committed = subprocess.check_output(['git','cat-file','--filters','HEAD:'+name],cwd=ROOT)
                if committed.replace(b'\r\n',b'\n') != path.read_bytes().replace(b'\r\n',b'\n'):
                    raise ValueError('Preserve an edited old NPC sidecar: '+name)
            removed.append(identity(path))
        fixture = dict(sprites=[row['expected'] for row in native.sprites.values()],textures=[dict(path=row['path'].removeprefix('unity/'),format=row['format'],mips=row['mips'],pixels_sha256=row['pixels_sha256'],width=row['original_fields']['m_Width'],height=row['original_fields']['m_Height'],readable=row['original_fields']['m_IsReadable'],settings=row['original_fields']['m_TextureSettings']) for row in native.textures.values()])
        fixture_path = work/'npc-icons-fixture.json'; fixture_raw = (json.dumps(fixture,ensure_ascii=False,indent=2)+'\n').encode('utf-8')
        if not fixture_path.exists() or fixture_path.read_bytes()!=fixture_raw: fixture_path.write_bytes(fixture_raw)
        proof = dict(manifest=apk.version,apk_sha256=apk.source.apk_sha256,code_receipt=identity(code_receipt),assembly=identity(assembly),tables=inputs,
            constructors=[identity(path) for path in decoder.paths.values()],asset_table_sha256=sha(manifest_bytes),sprites=list(native.sprites.values()),textures=list(native.textures.values()),
            bindings=bindings,hud_aliases=aliases,outputs=outputs,removals=removed,deferred=deferred,unresolvedBattleNpcIds=unresolved,missing_icon_checks=checks,
            previous_receipts=[identity(path) for path in (BATTLE_RECEIPT,PARTNER_RECEIPT)],previous_consumers=[identity(path) for path in (BATTLE_JSON,BATTLE_ASSET,PARTNER,HUD)],
            generator=identity(Path(__file__).resolve()),dependencies=[identity(ROOT/'scripts'/name) for name in DEPENDENCIES],fixture_sha256=identity(work/'npc-icons-fixture.json')['sha256'])
        (work/'current-npc-icons-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        print('Staged current NPC images:',len(bindings),'addresses;',len(native.sprites),'Sprites;',len(native.textures),'textures;',len(removed),'old files to retire.',flush=True)
        return proof


def activate(work):
    work = work_directory(work)
    proof = json.loads((work/'current-npc-icons-source-manifest.json').read_bytes())
    for row in [proof['generator'],proof['code_receipt'],proof['assembly'],*proof['constructors'],*proof['dependencies'],*proof['previous_receipts'],*proof['previous_consumers']]:
        if identity(ROOT/row['path']) != row: raise ValueError('NPC conversion input changed: '+row['path'])
    for row in proof['outputs']:
        path,staged = ROOT/row['path'],ROOT/row['staged']
        if identity(staged)['sha256'] != row['sha256'] or (identity(path)['sha256'] if path.is_file() else None) != row['previous_sha256']:
            raise ValueError('Staged or installed NPC image changed: '+row['path'])
    for row in proof['removals']:
        if identity(ROOT/row['path']) != row: raise ValueError('Retired NPC image changed: '+row['path'])
        if not any((ROOT/row['path']).resolve().is_relative_to((ROOT/folder).resolve()) for folder in
            ('unity/Assets/Official/Combat/NpcPortraits','unity/Assets/Official/Combat/HUD/Sprites','unity/Assets/Official/Partners/NpcIcons')):
            raise ValueError('Retired NPC payload is outside its owned resource folders: '+row['path'])
    old_guids = set()
    for row in proof['removals']:
        if row['path'].endswith('.meta'):
            match = re.search(rb'(?m)^guid: ([a-f0-9]{32})',(ROOT/row['path']).read_bytes())
            if match: old_guids.add(match[1].decode())
    patterns = work/'retired-npc-guids.txt'; patterns.write_text('\n'.join(sorted(old_guids))+'\n',encoding='utf-8')
    scan = subprocess.run(['rg','-l','-F','-f',str(patterns),'unity/Assets','-g','*.asset','-g','*.prefab','-g','*.json','-g','*.mat','-g','*.controller','-g','*.anim','-g','*.cs'],cwd=ROOT,capture_output=True,text=True,encoding='utf-8')
    if scan.returncode not in (0,1): raise RuntimeError(scan.stderr)
    removed = {row['path'] for row in proof['removals']}; planned = {row['path']:row for row in proof['outputs']}
    for name in scan.stdout.splitlines():
        name = name.replace('\\','/')
        if name in removed: continue
        if name not in planned: raise ValueError('Rebind another old NPC resource consumer first: '+name)
        raw = (ROOT/planned[name]['staged']).read_bytes()
        pointers = {value.decode() for value in re.findall(rb'\bguid: ([a-f0-9]{32})',raw)}
        if old_guids & pointers: raise ValueError('Staged NPC catalog retains an old resource pointer: '+name)
    before = {row['path']:row for row in proof['removals']}
    for row in proof['outputs']:
        if row['previous_sha256'] is not None: before[row['path']] = dict(path=row['path'],sha256=row['previous_sha256'])
    for row in proof['previous_receipts']: before[row['path']] = row
    # Keep only identities of the replaced files. The user's migration policy
    # explicitly retires old material without creating payload backups.
    for row in proof['outputs']:
        path = ROOT/row['path']; path.parent.mkdir(parents=True,exist_ok=True); path.write_bytes((ROOT/row['staged']).read_bytes())
    for row in proof['removals']:
        path = ROOT/row['path']
        if not any(path.resolve().is_relative_to((ROOT/folder).resolve()) for folder in
            ('unity/Assets/Official/Combat/NpcPortraits','unity/Assets/Official/Combat/HUD/Sprites','unity/Assets/Official/Partners/NpcIcons')):
            raise ValueError('Retired NPC payload is outside its owned resource folders: '+row['path'])
        path.unlink()
    from prepare_current_damage_ui import normalize_checkout
    proof['installed'] = {row['path']:identity(ROOT/row['path'])['sha256'] for row in proof['outputs']}
    normalize_checkout(proof)
    proof['outputs'] = [dict(path=name,sha256=value) for name,value in proof['installed'].items()]
    for row in [*proof['sprites'],*proof['textures']]: row['sha256'] = proof['installed'][row['path']]
    proof['previous_resource_hashes'] = list(before.values())
    proof.pop('previous_receipts'); proof.pop('previous_consumers')
    proof['scope'] = 'Current NPC/profession image addresses and complete native Sprite/atlas/mip data shared by battle, HUD and partner catalogs. Existing closed-content rules remain owned by their feature.'
    RECEIPT.write_bytes((json.dumps(proof,ensure_ascii=False,indent=2)+'\n').replace('\n','\r\n').encode('utf-8'))
    canonical = {name:value for name,value in proof['installed'].items() if name.startswith(NATIVE.relative_to(ROOT).as_posix())}
    battle_outputs = dict(canonical)
    for path in (BATTLE_ASSET,BATTLE_JSON): battle_outputs[path.relative_to(ROOT).as_posix()] = identity(path)['sha256']
    code = ROOT/'.tools/source-packages/current'/proof['manifest']/'official-code'
    consumers = {identity(code/name)['path']:identity(code/name)['sha256'] for name in
        ('client-source/GameRoundCharacterItemScene.cs','client-source/NpcPanelDialogueListNpcItem.cs','common-source/Common/NpcInfo.cs','common-source/Common/ProfessionBaseInfo.cs')}
    common = dict(manifest=proof['manifest'],canonical_receipt=identity(RECEIPT),generator=proof['generator'],
        scope=proof['scope'],retired_resource_hashes=proof['removals'])
    battle_proof = dict(common,inputs=proof['tables'],outputs=battle_outputs,deferred=proof['deferred'],unresolvedBattleNpcIds=proof['unresolvedBattleNpcIds'],
        missing_icon_checks=proof['missing_icon_checks'],official_code=consumers,runtime_sha256=identity(ROOT/'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleNpcPortraitCatalog.cs')['sha256'])
    partner_proof = dict(common,inputs={RECEIPT.relative_to(ROOT).as_posix():identity(RECEIPT)['sha256']},sprites=proof['bindings'],native_assets=proof['sprites'],
        installed_output_sha256={path.relative_to(ROOT).as_posix():identity(path)['sha256'] for path in (PARTNER,Path(str(PARTNER)+'.meta'))},reused_output_sha256=canonical,
        official_consumer_sha256=consumers,binding_source_sha256={})
    for path,data in ((BATTLE_RECEIPT,battle_proof),(PARTNER_RECEIPT,partner_proof)):
        path.write_bytes((json.dumps(data,ensure_ascii=False,indent=2)+'\n').replace('\n','\r\n').encode('utf-8'))
    verify()
    print('Installed current NPC/profession images:',len(proof['bindings']),'addresses;',len(proof['removals']),'old resource files retired.',flush=True)


def verify():
    proof = json.loads(RECEIPT.read_bytes())
    from official_source import OfficialSource
    if proof['manifest'] != OfficialSource().version: raise ValueError('Current NPC resource source differs')
    from conversion_provenance import verify_recorded_source
    for row in [proof['generator'],*proof['dependencies']]:
        verify_recorded_source(row)
    for row in [proof['code_receipt'],proof['assembly'],*proof['constructors'],*proof['outputs']]:
        if identity(ROOT/row['path']) != row: raise ValueError('NPC source or installed payload differs: '+row['path'])
    for row in proof['removals']:
        if (ROOT/row['path']).exists(): raise ValueError('An obsolete NPC payload remains: '+row['path'])
    for path in (BATTLE_RECEIPT,PARTNER_RECEIPT):
        data = json.loads(path.read_bytes())
        if data['canonical_receipt'] != identity(RECEIPT): raise ValueError('NPC consumer receipt differs: '+str(path))
    print('Verified shared current NPC image closure:',len(proof['installed']),'files.',flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work',type=Path)
    parser.add_argument('--activate',action='store_true')
    parser.add_argument('--verify',action='store_true')
    args = parser.parse_args()
    if args.verify: verify()
    else:
        if args.work is None: parser.error('--work is required')
        (activate if args.activate else stage)(args.work)
