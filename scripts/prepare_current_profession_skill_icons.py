"""Stage current profession icons and their complete source configuration."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

import yaml

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_battle_scene_player_appearance import CurrentConstructors
from prepare_unity_profession_skill_icons import ProfessionSprites
from prepare_unity_profession_reward_sprites import Dumper
from current_component_schemas import CurrentSchemas


RECEIPT = ROOT / 'assets/reference/profession-ui/skill-icons-source-manifest.json'
DEPENDENCIES = ('prepare_unity_profession_skill_icons.py', 'prepare_unity_map12_team_panel_sprites.py',
    'prepare_unity_map13_native_scene.py', 'prepare_battle_scene_player_appearance.py',
    'official_tables.py', 'current_component_schemas.py')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))


def source_key(source):
    return source['bundle'], str(source['path_id'])


def validation_context(apk, fixture):
    colors = apk.asset('scriptableconfig_modules_systemsetting', '/GlobalColors.asset')
    fields = CurrentSchemas().read(colors)
    fixture['global_colors'] = fields
    fixture['font'] = dict(config_asset='Assets/Official/ProfessionUI/Fonts/ProfessionFontConfig.asset', resource_catalog='Assets/Official/ProfessionUI/Fonts/ProfessionFontResources.asset')
    return dict(colors=dict(source=apk.provenance(colors), object_sha256=sha(colors.get_raw_data()), fields=fields), fonts=[identity(ROOT / 'unity' / path) for path in fixture['font'].values()])


class StableProfessionSprites(ProfessionSprites):
    def __init__(self, apk, previous):
        super().__init__(apk)
        self.prior = {source_key(row['source']): row for row in previous['textures']}
        self.previous_outputs = {row['path']: row['sha256'] for row in previous['outputs']}

    def write(self, kind, obj, native):
        row = super().write(kind, obj, native)
        prior = self.prior.get(source_key(row['source']))
        if prior is None:
            return row
        old = ROOT / prior['path']
        meta = old.with_name(old.name + '.meta')
        if sha(old.read_bytes()) != self.previous_outputs[prior['path']] or sha(meta.read_bytes()) != self.previous_outputs[prior['path'] + '.meta']:
            raise ValueError('Preserve a changed profession icon: ' + prior['path'])
        generated = ROOT / row['path']
        raw = self.outputs.pop(generated)
        self.outputs.pop(generated.with_name(generated.name + '.meta'))
        self.outputs[old] = raw
        self.outputs[meta] = meta.read_bytes()
        row.update(path=prior['path'], guid=prior['guid'], sha256=sha(raw))
        return row


def stage(work):
    work = work.resolve()
    if work.parent != (ROOT / 'output/validation').resolve():
        raise ValueError('Use a direct owned validation workspace')
    marker = json.loads((work / '.zjcs-validation.json').read_text('utf-8-sig'))
    if Path(marker['repository']).resolve() != ROOT.resolve():
        raise ValueError('Validation workspace belongs to another repository')
    previous_raw = RECEIPT.read_bytes()
    previous = json.loads(previous_raw)
    previous_sprites = {row['identity']: row for row in previous['sprites']}
    previous_addresses = {row['original_asset']['address']: previous_sprites[row['sprite_identity']] for row in previous['bindings']}
    output = work / 'skill-icon-assets'
    with Apk.current() as apk:
        if apk.version != '911_156995':
            raise ValueError('Review the selected profession icon version')
        code = apk.source.prepared_path / 'official-code'
        code_receipt = code.parent / 'code-manifest.json'
        code_proof = json.loads(code_receipt.read_bytes())
        assembly = code / 'managed/decoded/Console.dll'
        if code_proof['manifest'] != apk.version or sha(assembly.read_bytes()) != code_proof['assemblies']['Console.dll']['decoded_sha256']:
            raise ValueError('Selected profession table assembly differs')
        decoder = CurrentConstructors(code)
        types = {match[2].rsplit('/', 1)[-1]: match[1] for match in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', decoder.source('Tables'))}
        tables, inputs = {}, []
        for name in ('profession_base', 'item', 'skill_rank', 'skill_skin', 'equip_type'):
            obj = apk.asset('config_luban', '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf-8', 'surrogateescape')
            tables[name] = json.loads(json.dumps(decoder.table(raw, types[name]), ensure_ascii=False))
            inputs.append(dict(name=name, source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()), sha256=sha(raw), bytes=len(raw), rows=len(tables[name])))
            print(name, len(tables[name]), 'rows fully decoded', flush=True)
        ids = {value for row in tables['profession_base'] for value in row['Skills']}
        skins = [row for row in tables['skill_skin'] if row['SkillId'] in ids]
        ids.update(row['ClassId'] for row in skins)
        items = [row for row in tables['item'] if row['ClassId'] in ids]
        if len(items) != len(ids):
            raise ValueError('Selected profession skill item definition is absent or duplicated')
        manifest_bytes = apk.source.manifest_bytes('DefaultPackage', apk.archive)
        manifest = read_asset_manifest(manifest_bytes)
        native = StableProfessionSprites(apk, previous)
        entries, bindings = [], []
        wanted = [(row['RawIcon'], dict(kind='skill', item_id=row['ClassId'])) for row in items]
        wanted.extend((row['Icon'], dict(kind='equipment', item_type=row['ItemType'])) for row in tables['equip_type'] if row['EquipPos'] in (10, 14))
        consumed = {}
        for icon, use in wanted:
            address = 'Assets/Image/' + icon
            matches = [row for row in manifest if row['address'] == address]
            if not matches:
                matches = [row for row in manifest if row['path'].removesuffix('.png') == address]
            if len(matches) != 1:
                raise ValueError('Resolve selected profession icon: ' + address)
            row, = matches
            obj, = [ptr.deref() for path, ptr in apk.container(row['bundle']) if path == row['path'] and ptr.deref().type.name == 'Sprite']
            prior = previous_addresses.get(address)
            if prior is not None:
                native.prior[source_key(apk.provenance(obj))] = prior
            value = native.sprite(obj)
            if row['guid'] not in consumed:
                consumed[row['guid']] = address
                entries.append(dict(Path=address, AssetGuid=row['guid'], Asset=dict(fileID=21300000, guid=value['guid'], type=2)))
            elif consumed[row['guid']] != address:
                raise ValueError('Review changed profession image aliases')
            bindings.append(dict(**use, icon=icon, original_asset=row, sprite_identity=value['identity'], installed_sprite=value['path'].removeprefix('unity/')))
        catalog = ROOT / 'unity/Assets/Game/Resources/ProfessionUI/skill-images.asset'
        catalog_text = catalog.read_text('utf-8-sig')
        data = yaml.safe_load('MonoBehaviour:' + catalog_text.split('MonoBehaviour:', 1)[1])
        retained = [row for row in data['MonoBehaviour']['Entries'] if row['Asset']['fileID'] != 21300000]
        for entry in retained:
            original, = [row for row in manifest if row['address'] == entry['Path']]
            if original['guid'] != entry['AssetGuid']:
                raise ValueError('Migrate changed profession prefab address first: ' + entry['Path'])
        data['MonoBehaviour']['Entries'] = entries + retained
        header = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'
        native.outputs[catalog] = (header + yaml.dump(data, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)).encode('utf-8')
        config = ROOT / 'unity/Assets/Game/Resources/ProfessionUI/info-config.json'
        projected = dict(professions=tables['profession_base'], items=items, ranks=tables['skill_rank'], skins=skins, equipment=tables['equip_type'])
        native.outputs[config] = (json.dumps(projected, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
        outputs = []
        for target, raw in native.outputs.items():
            relative = target.relative_to(ROOT)
            path = output / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
            outputs.append(dict(path=relative.as_posix(), staged=path.relative_to(ROOT).as_posix(), sha256=sha(raw), previous_sha256=sha(target.read_bytes()) if target.is_file() else None))
        fixture = dict(config=config.relative_to(ROOT / 'unity').as_posix(), catalog=catalog.relative_to(ROOT / 'unity').as_posix(), items=len(items), professions=len(tables['profession_base']), sprites=[row['expected'] for row in native.sprites.values()], textures=[dict(path=row['path'].removeprefix('unity/'), **{key: row[key] for key in ('format', 'mips', 'pixels_sha256')}, width=row['original_fields']['m_Width'], height=row['original_fields']['m_Height'], settings=row['original_fields']['m_TextureSettings']) for row in native.textures.values()])
        fixture_path = work / 'profession-skill-icons-fixture.json'
        context = validation_context(apk, fixture)
        fixture_path.write_text(json.dumps(fixture, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        proof = dict(manifest=apk.version, apk_sha256=apk.source.apk_sha256, code_receipt=identity(code_receipt), assembly=identity(assembly), tables=inputs, constructors=[identity(path) for path in decoder.paths.values()], asset_table_sha256=sha(manifest_bytes), previous_receipt=dict(path=RECEIPT.relative_to(ROOT).as_posix(), sha256=sha(previous_raw)), bindings=bindings, sprites=list(native.sprites.values()), textures=list(native.textures.values()), outputs=outputs, retained_prefab_bindings=retained, fixture_sha256=sha(fixture_path.read_bytes()), generator=identity(Path(__file__).resolve()), dependencies=[identity(ROOT / 'scripts' / name) for name in DEPENDENCIES])
        proof['validation_context'] = context
        (work / 'current-profession-skill-icons-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print('Staged current profession icons:', len(bindings), 'bindings;', len(native.sprites), 'Sprites;', len(native.textures), 'complete textures.', flush=True)
        return proof


def activate(work):
    from refresh_current_character_meshes import preserve
    work = work.resolve()
    if work.parent != (ROOT / 'output/validation').resolve():
        raise ValueError('Use a direct owned validation workspace')
    marker = json.loads((work / '.zjcs-validation.json').read_text('utf-8-sig'))
    if Path(marker['repository']).resolve() != ROOT.resolve():
        raise ValueError('Validation workspace belongs to another repository')
    proof_path = work / 'current-profession-skill-icons-source-manifest.json'
    proof = json.loads(proof_path.read_bytes())
    if proof['manifest'] != '911_156995' or identity(Path(__file__).resolve()) != proof['generator']:
        raise ValueError('Restage profession icons with the current source and generator')
    if identity(RECEIPT) != proof['previous_receipt']:
        raise ValueError('The profession icon receipt changed after staging')
    previous = json.loads(RECEIPT.read_bytes())
    checkout_endings = []
    for row in previous['outputs']:
        path = ROOT / row['path']
        raw = path.read_bytes()
        actual = identity(path)
        if actual != row:
            if sha(raw.replace(b'\r\n', b'\n')) != row['sha256']:
                raise ValueError('Preserve a changed previous profession icon output: ' + row['path'])
            checkout_endings.append(dict(recorded=row, checked_out=actual, comparison='Exact LF/CRLF checkout equivalence'))
    for row in [proof['code_receipt'], proof['assembly'], *proof['constructors'], *proof['dependencies']]:
        if identity(ROOT / row['path']) != row:
            raise ValueError('A profession icon conversion dependency changed: ' + row['path'])
    fixture_path = work / 'profession-skill-icons-fixture.json'
    if sha(fixture_path.read_bytes()) != proof['fixture_sha256']:
        raise ValueError('The original profession icon fixture changed')
    for row in proof['outputs']:
        target, staged = ROOT / row['path'], ROOT / row['staged']
        if sha(staged.read_bytes()) != row['sha256'] or (sha(target.read_bytes()) if target.is_file() else None) != row['previous_sha256']:
            raise ValueError('An installed or staged profession icon changed: ' + row['path'])
    staged_names = {row['path'] for row in proof['outputs']}
    retained = [row for row in previous['outputs'] if row['path'] not in staged_names]
    if any(not row['path'].endswith('.meta') for row in retained):
        raise ValueError('Review obsolete profession icon payloads before activation')
    # Snapshot every old output before installing any new payload.
    snapshots = [preserve(ROOT / row['path']) for row in previous['outputs']]
    previous_receipt = preserve(RECEIPT)
    names = [row['path'] for row in proof['outputs']]
    attrs = subprocess.run(['git', 'check-attr', '--stdin', 'filter', 'text'], input='\n'.join(names) + '\n', capture_output=True, text=True, encoding='utf-8', check=True).stdout
    binary = set()
    for line in attrs.splitlines():
        name, attribute, value = line.rsplit(': ', 2)
        if attribute == 'filter' and value == 'lfs' or attribute == 'text' and value == 'unset':
            binary.add(name)
    installed = []
    for row in retained:
        target = ROOT / row['path']
        raw = target.read_bytes().replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')
        if target.read_bytes() != raw:
            target.write_bytes(raw)
        installed.append(identity(target))
    for row in proof['outputs']:
        target = ROOT / row['path']
        raw = (ROOT / row['staged']).read_bytes()
        if row['path'] not in binary:
            raw = raw.replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.is_file() or target.read_bytes() != raw:
            target.write_bytes(raw)
        installed.append(identity(target))
    by_path = {row['path']: row for row in installed}
    for row in [*proof['sprites'], *proof['textures']]:
        row['sha256'] = by_path[row['path']]['sha256']
    proof['outputs'] = installed
    proof['replacement'] = dict(previous_receipt=previous_receipt, previous_outputs=snapshots, checkout_endings=checkout_endings)
    proof.pop('previous_receipt')
    proof['catalog'] = identity(ROOT / 'unity/Assets/Game/Resources/ProfessionUI/skill-images.asset')
    proof['config'] = identity(ROOT / 'unity/Assets/Game/Resources/ProfessionUI/info-config.json')
    proof['validation'] = identity(ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionSkillInfoValidation.cs')
    proof['operation'] = 'Fully decode the five selected tables, retain original profession/skill/skin/equipment order, and resolve every requested Sprite and complete atlas/mip chain. Preserve prior asset GUIDs and unchanged prefab bindings; prefab implementations retain their separate source receipts.'
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Installed current profession icons:', len(proof['sprites']), 'Sprites;', len(proof['textures']), 'complete textures.', flush=True)
    return proof


def refresh_validation(work):
    work = work.resolve()
    if work.parent != (ROOT / 'output/validation').resolve() or Path(json.loads((work / '.zjcs-validation.json').read_text('utf-8-sig'))['repository']).resolve() != ROOT.resolve():
        raise ValueError('Use an owned validation workspace')
    proof = json.loads(RECEIPT.read_bytes())
    for row in proof['outputs']:
        if identity(ROOT / row['path']) != row:
            raise ValueError('An installed profession icon output changed: ' + row['path'])
    fixture_path = work / 'profession-skill-icons-fixture.json'
    fixture = json.loads(fixture_path.read_bytes())
    if sha(fixture_path.read_bytes()) != proof['fixture_sha256']:
        raise ValueError('The profession icon fixture changed')
    with Apk.current() as apk:
        if apk.version != proof['manifest']:
            raise ValueError('The profession icon source changed')
        proof['validation_context'] = validation_context(apk, fixture)
    fixture_path.write_text(json.dumps(fixture, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    proof['fixture_sha256'] = sha(fixture_path.read_bytes())
    proof['validation'] = identity(ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionSkillInfoValidation.cs')
    proof['generator'] = identity(Path(__file__).resolve())
    proof['dependencies'] = [identity(ROOT / 'scripts' / name) for name in DEPENDENCIES]
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Recovered current source palette and installed font validation context.', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--activate', action='store_true')
    parser.add_argument('--refresh-validation', action='store_true')
    args = parser.parse_args()
    (refresh_validation if args.refresh_validation else activate if args.activate else stage)(args.work)
