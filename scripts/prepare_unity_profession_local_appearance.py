"""Record the local profile boundary for the original profession appearance code."""
import hashlib
import json
from pathlib import Path
import subprocess

from prepare_unity_profession_photographer import ROOT, CODE, record


def schema_sections():
    path = ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs'
    committed = subprocess.check_output(['git', 'show', 'HEAD:' + path.relative_to(ROOT).as_posix()], cwd=ROOT).decode('utf-8').replace('\r\n', '\n')
    work = path.read_text(encoding='utf-8')
    sections = []
    for name in ('OfflinePlayerProgress', 'OfflineWardrobeProgress', 'OfflineAppearanceSkin', 'OfflineAppearanceDye', 'OfflineAppearanceWearTime'):
        def extract(text):
            start = text.index('public sealed class ' + name + '\n')
            brace = text.index('{', start)
            end, depth = brace + 1, 1
            while depth:
                depth += (text[end] == '{') - (text[end] == '}')
                end += 1
            return text[start:end]
        source = extract(work)
        if source != extract(committed):
            raise ValueError('Uncommitted local appearance schema change: ' + name)
        sections.append(dict(name=name, utf8_lf_sha256=hashlib.sha256(source.encode()).hexdigest()))
    return dict(path=path.relative_to(ROOT).as_posix(), sections=sections)


def build():
    code_manifest = CODE.parent.parent / 'code-manifest.json'
    manifest = json.loads(code_manifest.read_bytes())
    if manifest['manifest'] != '871_156598':
        raise ValueError('Local profession appearance source version changed')
    assemblies = []
    for name in ('Assembly-CSharp.dll', 'Console.dll'):
        path = CODE.parent / 'managed/decoded' / name
        if record(path)['sha256'] != manifest['assemblies'][name]['decoded_sha256']:
            raise ValueError('Local appearance source assembly changed: ' + name)
        assemblies.append(record(path))
    common = CODE.parent / 'common-source/Common'
    inputs = [CODE / 'AppearanceSystem.Runtime/AppearanceUtils.cs', CODE / 'ProfessionUtils.cs',
              CODE / 'PlayerAppearanceInfo.cs', common / 'AppearanceData.cs', common / 'SkinData.cs',
              common / 'SkinDyeData.cs', common / 'ItemType.cs', common / 'DataWrapExtension.cs',
              common / 'ProfessionBaseInfoParser.cs', common / 'PlayerAppearanceDataWrap.cs']
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionAppearanceBindings.cs'
    validation = ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionLocalAppearanceValidation.cs'
    result = dict(format=1, manifest=manifest['manifest'], code_manifest=record(code_manifest),
        assemblies=assemblies, sources=[record(p) for p in inputs],
        configuration=record(ROOT / 'assets/reference/profession-ui/appearance-resolver-source-manifest.json'),
        application=record(ROOT / 'assets/reference/profession-ui/player-appearance-source-manifest.json'),
        local_schema=schema_sections(),
        local_equipment_writer=record(ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterProgressionService.cs'),
        runtime=record(runtime), runtime_meta=record(runtime.with_name(runtime.name + '.meta')),
        assembly=record(runtime.parent / 'Zjcs.Offline.ProfessionUI.asmdef'),
        validation=record(validation), validation_meta=record(validation.with_name(validation.name + '.meta')),
        generator=record(Path(__file__)),
        binding='Explicit selected player ID, local profile accessor, current item-type lookup, original AppearanceConfig '
                'and AppearanceAnimNameCombineRule; no historic equipment catalog, global auto-install, network or user-save write.',
        scope='Project local base skins, equipment SkinType->EquipPos, appended item IDs, UTC wear times, HSV dyes, '
              'title and appearance level into the migrated original model; original base-profession/animation-ID methods '
              'use explicit local initial/equipped weapon context. Deep clone all ten protocol fields before the original preview.',
        adaptations=['The owning session explicitly refreshes after a successful local transaction; snapshot replacement precedes event dispatch.',
            'Local ModifyTimeStamp is an in-memory strictly increasing UTC-tick portrait-cache revision, not a server timestamp or saved progression rule.',
            'FullBodyAppearanceId and LimitItemExpSet have no local save counterpart; their protocol defaults remain unchanged. Expiry rules are not inferred.',
            'The protocol clone is an independent deep-copy adapter replacing network wrapper serialization; preserves fields, nulls and dictionary order.',
            'Unavailable current item types and other player IDs are reported explicitly; no historic table or remote player is substituted.'],
        limitations=['Not installed in the actual profession window yet. The window owner must bind current item data and transaction events.',
            'Character capture endpoint in the data validation is a fixture; native actor constraints/graphic/parts are still incomplete.',
            'No full native page, mouse flow, Windows Player or release validation is claimed.'],
        validated_scope='Pending Unity compilation and actual Play Mode local-data/preview validation.')
    dest = ROOT / 'assets/reference/profession-ui/local-appearance-source-manifest.json'
    dest.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Recorded selected local player appearance and original preview boundary')
    return result


if __name__ == '__main__':
    build()
