"""Bind current battle quality frames to verified shared native atlas Sprites."""
import argparse
import json
from pathlib import Path
import re
import subprocess

import yaml
from official_apk import Apk, ROOT
from prepare_current_creation_ui_sprites import CurrentUiSprites
from prepare_current_combat_skill_icons import document, identity, owned, sha, HEADER
from prepare_unity_profession_reward_sprites import Dumper
from refresh_current_character_meshes import preserve


SOURCE = ROOT / 'unity/Assets/Official/CharacterCreation/TrialUI/native-sprites-source-manifest.json'
RECEIPT = ROOT / 'assets/reference/battle-hud/skill-quality-source-manifest.json'
CATALOG = ROOT / 'unity/Assets/Game/Resources/Combat/OfficialBattleSkillQualityCatalog.asset'
LEGACY = ROOT / 'unity/Assets/Official/Combat/SkillQualities/Sprites'
ICONS = ROOT / 'assets/reference/battle-animation/skill-icons-source-manifest.json'
NAMES = ('White', 'Blue', 'Purple', 'Orange', 'Gold', 'Red')
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillQualityGeometry.cs'
VALIDATION = ROOT / 'unity/Assets/Game/Editor/Combat/OfficialBattleSkillQualityAcceptance.cs'


def matching_native(row, fresh):
    """Accept only exact native payloads or a proven text line-ending conversion."""
    path = ROOT / 'unity' / row['path']
    result = []
    for suffix, key in (('', 'sha256'), ('.meta', 'meta_sha256')):
        actual = Path(str(path) + suffix)
        prepared = ROOT / (fresh['path'] + suffix)
        raw, expected = actual.read_bytes(), prepared.read_bytes()
        if sha(expected) != fresh[key] or row['native'][key] != fresh[key]:
            raise ValueError('Current native conversion differs from the canonical source: ' + str(actual))
        if raw.replace(b'\r\n', b'\n') != expected.replace(b'\r\n', b'\n'):
            raise ValueError('Canonical native payload changed: ' + str(actual))
        result.append(identity(actual))
    if row['native']['guid'] != fresh['guid']:
        raise ValueError('The canonical Sprite/Texture GUID changed')
    return result


def stage(work):
    work = owned(work)
    previous = json.loads(RECEIPT.read_bytes())
    if previous['manifest'] == '911_156995':
        verify()
    canonical = json.loads(SOURCE.read_bytes())
    if canonical['manifest'] != '911_156995':
        raise ValueError('Migrate the current shared quality atlas first')
    sprites = {row['native']['expected']['name']: row for row in canonical['sprites']}
    textures = {row['native']['identity']: row for row in canonical['textures']}
    dependencies, bindings, inputs = {}, [], []
    with Apk.current() as apk:
        if apk.version != canonical['manifest']:
            raise ValueError('Selected battle quality source differs')
        code = apk.source.prepared_path / 'official-code'
        language = code / 'common-source/Common/LanguageUtils.cs'
        quality = code / 'common-source/Common/Quality.cs'
        if not re.search(r'public static string GetItemQualityImage\(Quality quality\)\s*\{\s*return \$"UI/ItemQuality/ImgQuality_\{quality\}";\s*\}', language.read_text('utf-8-sig')):
            raise ValueError('Review changed official quality image address rule')
        enum = quality.read_text('utf-8-sig')
        enum_names = [name.strip() for name in enum.split('{', 1)[1].split('}', 1)[0].split(',') if name.strip()]
        if enum_names != ['None', *NAMES, 'Rainbow']:
            raise ValueError('Review changed official quality enum')
        converter = CurrentUiSprites(apk, work / 'native')
        objects = {obj.read().m_Name: obj for obj in apk.objects('defaultpackage_assets_image_ui_itemquality.bundle') if obj.type.name == 'Sprite'}
        for value, name in enumerate(NAMES, 1):
            name = 'ImgQuality_' + name
            obj = objects[name]
            native = converter.sprite(obj)
            canonical_sprite = sprites[name]
            for row in matching_native(canonical_sprite, native):
                dependencies[row['path']] = row
            texture = converter.textures[native['texture']]
            for row in matching_native(textures[native['texture']], texture):
                dependencies[row['path']] = row
            if native['source'] != canonical_sprite['native']['source'] or native['atlas'] != canonical_sprite['native']['atlas']:
                raise ValueError('Current Sprite/atlas provenance differs: ' + name)
            fields = texture['original_fields']
            rect = native['atlas']['render_data']['textureRect']
            offset = native['atlas']['render_data']['textureRectOffset']
            packed = native['atlas']['render_data']
            if packed['settingsRaw'] >> 2 & 15 or packed['alphaTexture']['m_PathID']:
                raise ValueError('Migrate rotated or split-alpha quality geometry first')
            size = native['expected']['rect']
            binding = dict(quality=value, sprite=dict(fileID=21300000, guid=native['guid'], type=2),
                sourceSize=dict(x=size['width'], y=size['height']), trimOffset=offset,
                trimSize=dict(x=rect['width'], y=rect['height']),
                uvMin=dict(x=rect['x']/fields['m_Width'], y=rect['y']/fields['m_Height']),
                uvMax=dict(x=(rect['x']+rect['width'])/fields['m_Width'], y=(rect['y']+rect['height'])/fields['m_Height']))
            bindings.append(binding)
            expected = {key: val for key, val in native['expected'].items() if key != 'path'}
            inputs.append(dict(quality=value, name=name, sprite=identity(ROOT / 'unity' / canonical_sprite['path']),
                texture_path='unity/' + textures[native['texture']]['path'], source=native['source'],
                object_sha256=native['object_sha256'], atlas=native['atlas'], expected=expected,
                texture=dict(source=texture['source'], object_sha256=texture['object_sha256'],
                    original_fields=fields, mips=texture['mips'], pixels_sha256=texture['pixels_sha256'])))
        unsupported = dict(quality=7, source_name='ImgQuality_Rainbow', present_in_quality_bundle='ImgQuality_Rainbow' in objects,
            bundle_sprite_names=sorted(objects), behavior='Keep the existing explicit unsupported branch; never infer Rainbow from Multicolour.')
        if unsupported['present_in_quality_bundle']:
            raise ValueError('A current Rainbow Sprite requires its own reviewed binding')
        sources = [identity(path) for path in (language, quality, code.parent/'code-manifest.json', code/'managed/decoded/Console.dll')]
    catalog = document(CATALOG)
    catalog['MonoBehaviour']['qualities'] = bindings
    raw = (HEADER + yaml.dump(catalog, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)).replace('\r\n', '\n').replace('\n', '\r\n').encode('utf-8')
    staged = work / 'catalog.asset'; staged.write_bytes(raw)
    retired, guids = [], []
    for path in sorted(LEGACY.glob('ImgQuality_*.png')):
        if sha(path.read_bytes()) != previous['outputs'].get(path.relative_to(ROOT).as_posix()):
            raise ValueError('Preserve a modified legacy quality PNG: ' + str(path))
        meta = Path(str(path) + '.meta')
        for target in (path, meta):
            baseline = subprocess.check_output(['git', 'cat-file', '--filters', 'HEAD:' + target.relative_to(ROOT).as_posix()])
            if baseline.replace(b'\r\n', b'\n') != target.read_bytes().replace(b'\r\n', b'\n'):
                raise ValueError('Preserve an uncommitted old quality payload: ' + str(target))
            retired.append(identity(target))
        guids.append(re.search(rb'(?m)^guid: ([a-f0-9]{32})\r?$', meta.read_bytes())[1].decode())
    if retired and len(retired) != 12:
        raise ValueError('Review partial legacy quality retirement')
    if guids:
        patterns = work / 'legacy-guids.txt'; patterns.write_text(''.join('guid: ' + guid + '\n' for guid in guids), encoding='utf-8')
        found = subprocess.run(['rg', '-l', '-F', '-f', str(patterns), 'unity/Assets', '-g', '!*.meta', '-g', '!*.texture2d.asset', '-g', '!*.texture.asset'], cwd=ROOT, capture_output=True, encoding='utf-8')
        if found.returncode not in (0, 1) or any(Path(name).as_posix() != CATALOG.relative_to(ROOT).as_posix() for name in found.stdout.splitlines()):
            raise ValueError('Retain another legacy quality consumer before retirement: ' + found.stdout + found.stderr)
    proof = dict(manifest=canonical['manifest'], canonical=identity(SOURCE), dependencies=list(dependencies.values()),
        source_code=sources, sprites=inputs, bindings=bindings, unsupported=unsupported,
        output=dict(path=CATALOG.relative_to(ROOT).as_posix(), staged=staged.relative_to(ROOT).as_posix(), sha256=sha(raw), previous_sha256=identity(CATALOG)['sha256']),
        retired=retired, previous_receipt=identity(RECEIPT), icons_receipt=identity(ICONS),
        generator=identity(Path(__file__).resolve()), runtime=identity(RUNTIME), validation=identity(VALIDATION),
        converters=[identity(ROOT/'scripts'/name) for name in ('prepare_current_creation_ui_sprites.py', 'prepare_current_creation_skin_icons.py', 'prepare_unity_map12_team_panel_sprites.py', 'prepare_unity_profession_reward_sprites.py')])
    (work/'current-battle-qualities.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('Staged six current native quality frames and', len(retired), 'legacy file retirements.', flush=True)
    return proof


def activate(work):
    work = owned(work); proof = json.loads((work/'current-battle-qualities.json').read_bytes())
    for row in [proof['canonical'], proof['previous_receipt'], proof['icons_receipt'], proof['generator'], proof['runtime'], proof['validation'], *proof['dependencies'], *proof['source_code'], *proof['converters'], *proof['retired']]:
        if identity(ROOT/row['path']) != row:
            raise ValueError('A staged quality input changed: ' + row['path'])
    output = proof['output']
    if identity(CATALOG)['sha256'] != output['previous_sha256'] or sha((ROOT/output['staged']).read_bytes()) != output['sha256']:
        raise ValueError('The installed or staged quality catalog changed')
    snapshots = [preserve(ROOT/row['path']) for row in proof['retired']]
    snapshots.extend(preserve(path) for path in (CATALOG, RECEIPT, ICONS))
    CATALOG.write_bytes((ROOT/output['staged']).read_bytes())
    for row in proof['retired']:
        target = (ROOT/row['path']).resolve()
        if not target.is_relative_to(LEGACY.resolve()) or identity(target) != row:
            raise ValueError('A legacy quality retirement target changed or escaped its folder')
        target.unlink()
    icons = json.loads(ICONS.read_bytes()); icons['quality_frame_catalog'] = identity(CATALOG)
    ICONS.write_text(json.dumps(icons, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    proof['replacement'] = dict(previous_files=snapshots)
    proof['outputs'] = [identity(CATALOG), identity(Path(str(CATALOG)+'.meta'))]
    proof['quality_items_source'] = identity(ICONS)
    proof['operation'] = 'Resolve original enum image names to current byte-verified native Sprites. Share the complete decoded atlas and retain original Simple Image padding, floating packed rectangle UVs, color and quad order. Retire six old cropped PNGs only after their sole consumer has been rebound.'
    for key in ('output', 'retired', 'previous_receipt', 'icons_receipt'):
        proof.pop(key)
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('Installed six current battle quality frames.', flush=True)


def verify():
    from official_source import OfficialSource
    proof = json.loads(RECEIPT.read_bytes())
    if proof['manifest'] != OfficialSource().version:
        raise ValueError('Installed battle quality source differs')
    for row in [proof['canonical'], proof['generator'], proof['runtime'], proof['validation'], proof['quality_items_source'], *proof['dependencies'], *proof['source_code'], *proof['converters'], *proof['outputs']]:
        if identity(ROOT/row['path']) != row:
            raise ValueError('A current battle quality dependency changed: ' + row['path'])
    if document(CATALOG)['MonoBehaviour']['qualities'] != proof['bindings'] or list(LEGACY.glob('ImgQuality_*.png')):
        raise ValueError('The current quality bindings or legacy retirement differ')
    print('Current native battle quality frames verified.', flush=True)
    return proof


def main(argv=None, *, install=False):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path)
    parser.add_argument('--activate', action='store_true')
    parser.add_argument('--verify', '--check', action='store_true')
    args = parser.parse_args(argv)
    if args.verify or install and RECEIPT.is_file() and json.loads(RECEIPT.read_bytes())['manifest'] == '911_156995':
        verify(); return 0
    if args.work is None:
        from validation_workspace import validation_directory
        args.work = validation_directory()
    if args.activate:
        activate(args.work)
    else:
        stage(args.work)
        if install: activate(args.work)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
