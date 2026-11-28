"""Bind current native skill Sprites to battle and character consumers."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

import yaml

from official_apk import Apk, ROOT
from prepare_battle_scene_player_appearance import CurrentConstructors
from prepare_unity_profession_reward_sprites import Dumper
from conversion_history import preserve


SOURCE = ROOT / 'assets/reference/profession-ui/skill-icons-source-manifest.json'
RECEIPT = ROOT / 'assets/reference/battle-animation/skill-icons-source-manifest.json'
COMBAT = ROOT / 'unity/Assets/Game/Resources/Combat/OfficialCombatSkillIconCatalog.asset'
CHARACTER = ROOT / 'unity/Assets/Game/Resources/Character/OfficialCharacterAssetCatalog.asset'
EXTRAS = ROOT / 'unity/Assets/Game/Resources/Character/SkillSkinIcons'
OLD_EXTRAS = ROOT / 'assets/reference/character-appearance/skill-skin-icons.json'
HEADER = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))


def owned(work):
    work = work.resolve()
    if work.parent != (ROOT / 'output/validation').resolve() or Path(json.loads((work / '.zjcs-validation.json').read_text('utf-8-sig'))['repository']).resolve() != ROOT.resolve():
        raise ValueError('Use an owned validation workspace')
    return work


def document(path):
    text = path.read_text('utf-8-sig')
    return yaml.safe_load('MonoBehaviour:' + text.split('MonoBehaviour:', 1)[1])


def stage(work):
    work = owned(work)
    if RECEIPT.is_file():
        installed = json.loads(RECEIPT.read_bytes())
        for row in installed['outputs']:
            if identity(ROOT / row['path']) != row:
                raise ValueError('Preserve a changed installed skill icon output: ' + row['path'])
    primary = json.loads(SOURCE.read_bytes())
    if primary['manifest'] != '911_156995':
        raise ValueError('Migrate the selected profession icon closure first')
    for row in primary['outputs']:
        if identity(ROOT / row['path']) != row:
            raise ValueError('A canonical profession icon changed: ' + row['path'])
    sprites = {row['identity']: row for row in primary['sprites']}
    by_icon = {}
    for row in primary['bindings']:
        sprite = sprites[row['sprite_identity']]
        if row['icon'] in by_icon and by_icon[row['icon']]['guid'] != sprite['guid']:
            raise ValueError('Canonical image address is ambiguous')
        by_icon[row['icon']] = sprite
    with Apk.current() as apk:
        if apk.version != primary['manifest']:
            raise ValueError('The selected battle icon source differs')
        code = apk.source.prepared_path / 'official-code'
        decoder = CurrentConstructors(code)
        mapping = {m[2].rsplit('/', 1)[-1]: m[1] for m in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', decoder.source('Tables'))}
        tables, inputs = {}, []
        for name in ('item', 'game_settings', 'skill_skin', 'skill'):
            obj = apk.asset('config_luban', '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf-8', 'surrogateescape')
            tables[name] = json.loads(json.dumps(decoder.table(raw, mapping[name]), ensure_ascii=False))
            inputs.append(dict(name=name, source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()), sha256=sha(raw), bytes=len(raw)))
        types = {int(key) for key in tables['game_settings']['SkillTypes']}
        skin_ids = {row['ClassId'] for row in tables['skill_skin']}
        items = [row for row in tables['item'] if row['ItemType'] in types or row['ClassId'] in skin_ids]
        entries, bindings = [], []
        for item in items:
            native = by_icon.get(item['RawIcon'])
            if native is None:
                raise ValueError('Convert an additional current skill icon first: ' + item['RawIcon'])
            entries.append(dict(itemId=item['ClassId'], sprite=dict(fileID=21300000, guid=native['guid'], type=2)))
            bindings.append(dict(item_id=item['ClassId'], raw_icon=item['RawIcon'], sprite=identity(ROOT / native['path']), guid=native['guid']))
        combat = document(COMBAT)
        previous_ids = {row['itemId'] for row in combat['MonoBehaviour']['icons']}
        if not previous_ids <= {row['itemId'] for row in entries}:
            raise ValueError('An installed battle icon item was removed from the selected source')
        combat['MonoBehaviour']['icons'] = entries
        character = document(CHARACTER)
        by_name = {row['expected']['name']: row for row in primary['sprites']}
        rebound = []
        for row in character['MonoBehaviour']['sprites']:
            match = re.match(r'^sprites/(skill_\d+)(?:-|\.)', row['sourcePath'])
            if match is None:
                continue
            native = by_name.get(match[1])
            if native is None:
                raise ValueError('Convert an installed character skill Sprite first: ' + row['sourcePath'])
            row['sprite'] = dict(fileID=21300000, guid=native['guid'], type=2)
            rebound.append(dict(source_path=row['sourcePath'], sprite=native['path'], guid=native['guid']))
        payloads = {COMBAT: (HEADER + yaml.dump(combat, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)).encode('utf-8'), CHARACTER: (HEADER + yaml.dump(character, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)).encode('utf-8')}
        quality_ids = {row['ClassId'] for row in tables['skill']} | skin_ids
        quality_rows = [dict(itemId=row['ClassId'], quality=row['Quality']) for row in tables['item'] if row['ClassId'] in quality_ids]
        if len(quality_rows) != len(quality_ids):
            raise ValueError('A selected skill quality item definition is absent or duplicated')
        quality_path = ROOT / 'unity/Assets/Game/Resources/Combat/battle-skill-qualities.json'
        quality_raw = (json.dumps(dict(manifest=apk.version, items=quality_rows), separators=(',', ':')) + '\r\n').encode('utf-8')
        payloads[quality_path] = quality_raw
        payloads[quality_path.with_name('battle-skill-qualities-source-manifest.json')] = (json.dumps(dict(manifest=apk.version, output_sha256=sha(quality_raw))) + '\r\n').encode('utf-8')
        retired, aliases = [], []
        item_rows = {row['ClassId']: row for row in tables['item']}
        names = {item_rows[row['ClassId']]['RawIcon'].rsplit('/', 1)[-1] for row in tables['skill_skin'] if row['SkinType'] != 0}
        old_pngs = {path.stem: path for path in EXTRAS.glob('skill_*.png')}
        if old_pngs.keys() - names:
            raise ValueError('Review removed official skill icon resources before retirement')
        for name in sorted(names):
            previous = old_pngs.get(name)
            if previous is not None:
                meta = previous.with_name(previous.name + '.meta')
                for path in (previous, meta):
                    baseline = subprocess.check_output(['git', 'cat-file', '--filters', 'HEAD:' + path.relative_to(ROOT).as_posix()])
                    same = path.read_bytes() == baseline if path.suffix == '.png' else path.read_bytes().replace(b'\r\n', b'\n') == baseline.replace(b'\r\n', b'\n')
                    if not same:
                        raise ValueError('Preserve an uncommitted old skill icon: ' + str(path))
                guid = re.search(rb'(?m)^guid: ([a-f0-9]{32})\r?$', meta.read_bytes())[1].decode()
                retired.extend((identity(previous), identity(meta)))
            else:
                current_meta = EXTRAS / (name + '.asset.meta')
                guid = re.search(rb'(?m)^guid: ([a-f0-9]{32})\r?$', current_meta.read_bytes())[1].decode() if current_meta.is_file() else hashlib.md5(('zjcs:current-skill-resource:' + name).encode('utf-8')).hexdigest()
            native = by_name.get(name)
            if native is None:
                raise ValueError('Recover the selected skill appearance icon first: ' + name)
            target = EXTRAS / (name + '.asset')
            payloads[target] = (ROOT / native['path']).read_bytes()
            payloads[target.with_name(target.name + '.meta')] = ('fileFormatVersion: 2\nguid: ' + guid + '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 21300000\n').encode('utf-8')
            aliases.append(dict(name=name, path=target.relative_to(ROOT).as_posix(), guid=guid, canonical=native['path']))
        # Retained GUIDs may represent only a Sprite; Texture2D references require a separate migration.
        wanted = work / 'previous-skill-icon-guids.txt'
        wanted.write_text(''.join('guid: ' + row['guid'] + '\n' for row in aliases), encoding='utf-8')
        found = subprocess.run(['rg', '-l', '-F', '-f', str(wanted), 'unity/Assets', '-g', '!*.meta', '-g', '!*.texture2d.asset', '-g', '!*.texture.asset'], cwd=ROOT, capture_output=True, encoding='utf-8')
        if found.returncode not in (0, 1):
            raise ValueError(found.stderr)
        for name in found.stdout.splitlines():
            text = (ROOT / name).read_text('utf-8-sig')
            for row in aliases:
                for pointer in re.findall(r'\{[^{}]*guid: ' + row['guid'] + r'[^{}]*\}', text):
                    if not re.search(r'fileID: 21300000[, }]', pointer):
                        raise ValueError('Retain an old skill icon Texture2D consumer first: ' + name)
        outputs = []
        for target, raw in payloads.items():
            raw = raw.replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')
            path = work / 'assets' / target.relative_to(ROOT)
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
            outputs.append(dict(path=target.relative_to(ROOT).as_posix(), staged=path.relative_to(ROOT).as_posix(), sha256=sha(raw), previous_sha256=sha(target.read_bytes()) if target.is_file() else None))
        proof = dict(manifest=apk.version, canonical=identity(SOURCE), tables=inputs, constructors=[identity(path) for path in decoder.paths.values()], bindings=bindings, character_bindings=rebound, aliases=aliases, quality_items=quality_rows, retired=retired, previous_receipt=identity(RECEIPT) if RECEIPT.is_file() else None, previous_alias_receipt=identity(OLD_EXTRAS) if OLD_EXTRAS.is_file() else None, outputs=outputs, generator=identity(Path(__file__).resolve()), decoder=identity(ROOT / 'scripts/prepare_battle_scene_player_appearance.py'))
        (work / 'current-combat-skill-icons-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print('Staged current skill icons:', len(bindings), 'battle items;', len(rebound), 'character bindings;', len(aliases), 'original resource paths.', flush=True)
        return proof


def activate(work):
    work = owned(work)
    proof = json.loads((work / 'current-combat-skill-icons-source-manifest.json').read_bytes())
    if (identity(RECEIPT) if RECEIPT.is_file() else None) != proof.get('previous_receipt'):
        raise ValueError('The installed skill icon receipt changed after staging')
    for row in [proof['canonical'], proof['generator'], proof['decoder'], *proof['constructors'], *proof['retired']] + ([proof['previous_alias_receipt']] if proof['previous_alias_receipt'] else []):
        if identity(ROOT / row['path']) != row:
            raise ValueError('A current skill icon input changed: ' + row['path'])
    for row in proof['outputs']:
        target = ROOT / row['path']
        if sha((ROOT / row['staged']).read_bytes()) != row['sha256'] or (sha(target.read_bytes()) if target.is_file() else None) != row['previous_sha256']:
            raise ValueError('A staged or installed skill icon changed: ' + row['path'])
    snapshots = [preserve(ROOT / row['path']) for row in proof['retired']]
    snapshots += [preserve(ROOT / row['path']) for row in proof['outputs'] if row['previous_sha256'] is not None]
    alias_receipt = preserve(OLD_EXTRAS) if OLD_EXTRAS.is_file() else None
    previous_receipt = preserve(RECEIPT) if RECEIPT.is_file() else None
    for row in proof['outputs']:
        target = ROOT / row['path']
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes((ROOT / row['staged']).read_bytes())
    for row in proof['retired']:
        target = (ROOT / row['path']).resolve()
        if not target.is_relative_to(EXTRAS.resolve()) or sha(target.read_bytes()) != row['sha256']:
            raise ValueError('Retirement target changed or escaped the owned skill icon folder')
        target.unlink()
    proof['outputs'] = [identity(ROOT / row['path']) for row in proof['outputs']]
    proof['replacement'] = dict(previous_outputs=snapshots, previous_alias_receipt=alias_receipt, previous_receipt=previous_receipt)
    proof.pop('retired')
    proof.pop('previous_alias_receipt')
    proof.pop('previous_receipt', None)
    proof['runtime'] = identity(ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialCombatSkillIconCatalog.cs')
    proof['operation'] = 'Resolve every current skill item and skill-skin item to the verified canonical native Sprite; retain source order, character source paths and resource names. Reuse complete canonical atlas textures and replace the old cropped PNG resource carriers with native Sprite data while preserving their GUIDs.'
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    if OLD_EXTRAS.is_file():
        OLD_EXTRAS.write_text(json.dumps(dict(manifest=proof['manifest'], source=identity(RECEIPT), sprites=proof['aliases']), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Installed current battle and character skill icon bindings.', flush=True)


def verify():
    from official_source import OfficialSource
    proof = json.loads(RECEIPT.read_bytes())
    if proof['manifest'] != OfficialSource().version or identity(SOURCE) != proof['canonical']:
        raise ValueError('The canonical skill icon source changed')
    for row in [*proof['outputs'], proof['runtime'], proof['generator'], *proof.get('runtimes', []), *proof.get('entry_points', []), *proof.get('validation', [])]:
        if identity(ROOT / row['path']) != row:
            raise ValueError('A current skill icon output or generator changed: ' + row['path'])
    for row in proof['replacement']['previous_outputs']:
        if row['path'].endswith('.png') and (ROOT / row['path']).is_file():
            raise ValueError('A retired skill PNG was restored: ' + row['path'])
    print('Current skill icon bindings verified:', len(proof['bindings']), 'items.', flush=True)
    return proof


def main(argv=None, *, install=False):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path)
    parser.add_argument('--activate', action='store_true')
    parser.add_argument('--verify', '--check', action='store_true')
    args = parser.parse_args(argv)
    if args.verify:
        verify()
        return 0
    if install and RECEIPT.is_file():
        verify()
        return 0
    if args.work is None:
        from validation_workspace import validation_directory
        args.work = validation_directory()
    if args.activate:
        activate(args.work)
    else:
        stage(args.work)
        if install:
            activate(args.work)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
