"""Extract original initial/profession skin settings and record their local projection."""
import hashlib
import json
from pathlib import Path
import re
import uuid

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_unity_profession_native_inputs import CACHE
from prepare_unity_profession_photographer import record


def build():
    apk = Apk.current()
    if apk.version != '871_156598': raise ValueError('Unexpected profession skin-default version')
    code = ROOT / '.tools/source-packages/current/871_156598/official-code'
    code_manifest = code.parent/'code-manifest.json'
    manifest = json.loads(code_manifest.read_bytes())
    assembly = code/'managed/decoded/Console.dll'
    if manifest['manifest'] != apk.version or record(assembly)['sha256'] != manifest['assemblies']['Console.dll']['decoded_sha256']:
        raise ValueError('Official appearance-default application assembly differs')
    decoder = Constructors(code)
    mapping = {m[2]: m[1] for m in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', decoder.source('Tables'))}
    decoded, inputs = {}, []
    for name in ('game_settings', 'equip_type'):
        obj = apk.asset('config_luban', '/' + name + '.g.bytes')
        raw = obj.read().m_Script.encode('utf-8', 'surrogateescape')
        decoded[name] = decoder.table(raw, mapping[name])
        inputs.append(dict(name=name, table_type=mapping[name], source=apk.provenance(obj),
                           payload_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
                           sha256=hashlib.sha256(raw).hexdigest(), consumed_bytes=len(raw)))
    cached = CACHE / 'typed/skin-default-tables.json'
    cached.write_text(json.dumps(decoded, ensure_ascii=False, separators=(',', ':'))+'\n', encoding='utf-8', newline='\n')
    payload = {name: decoded['game_settings'][name] for name in
               ('InitialWeaponSwordSkinId', 'InitialWeaponWandSkinId', 'ProfessionSkinTypeList')}
    payload['EquipTypes'] = decoded['equip_type']
    dest = ROOT / 'unity/Assets/Game/Resources/ProfessionUI/skin-defaults.json'
    dest.write_text(json.dumps(payload, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')
    files = [dest, ROOT/'unity/Assets/Game/Runtime/Official/ProfessionUI/ProfessionAppearanceSkinDefaults.cs',
             ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionSkinDefaultsValidation.cs']
    for path in files:
        meta = path.with_name(path.name+'.meta')
        if not meta.exists():
            guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:871_156598:'+path.relative_to(ROOT).as_posix()).hex
            importer = 'TextScriptImporter' if path.suffix == '.json' else 'MonoImporter'
            extra = '' if path.suffix == '.json' else '  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n'
            meta.write_text(f'fileFormatVersion: 2\nguid: {guid}\n{importer}:\n  externalObjects: {{}}\n'+extra+'  userData:\n  assetBundleName:\n  assetBundleVariant:\n', encoding='utf-8', newline='\n')
    sources = []
    for name, source in sorted(decoder.sources.items()):
        if not source: continue
        path = code/'common-source/Common'/(name+'.cs')
        if not path.exists() or name in ('Pos2d','Vector2','Vector3'):
            path = code/'common-source/Common.LubanGen'/(name+'.cs')
        sources.append(record(path))
    app = code/'common-source/Common'
    result = dict(format=1, manifest=apk.version, code_manifest=record(code_manifest), assembly=record(assembly), tables=inputs, constructor_sources=sources,
        interpreter=record(ROOT/'scripts/official_tables.py'), cache=record(cached),
        application=[record(app/(name+'.cs')) for name in ('AppearanceDataEx','EquipTypeInfoParser','ItemType')],
        prerequisites=[record(ROOT/'assets/reference/profession-ui'/(name+'-source-manifest.json')) for name in ('local-appearance','appearance-resolver')],
        outputs=[record(p) for f in files for p in (f,f.with_name(f.name+'.meta'))],
        adapter=record(ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionAppearanceBindings.cs'),
        regression=record(ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionLocalAppearanceValidation.cs'),
        generator=record(Path(__file__)),
        operation='Fully decode current official game_settings and all nine equip_type rows. Package three original settings and complete equip rows in original order. Port application default-skin methods with explicit model/parser/event context, preserving validation, removals, duplicate equip positions and dirty-event ordering.',
        adaptation='The existing local wardrobe omitted non-base profession skin categories. Only on its read-only nonempty appearance snapshot, derive defaults by the original methods and append missing non-BaseSkin entries. Never overwrite saved values or mutate/persist the selected player. Empty wardrobes remain empty; raw protocol projection and equipment absence/explicit-zero remain distinct.',
        limitations=['Main original profession prefab, weapon graphics, physical constraints, full simulated-input/save flow and Windows release remain incomplete.'],
        validated_scope='Pending actual Play Mode source-default and local appearance regression checks.')
    proof = ROOT/'assets/reference/profession-ui/skin-defaults-source-manifest.json'
    proof.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Extracted original skin defaults:', payload['InitialWeaponSwordSkinId'],payload['InitialWeaponWandSkinId'],len(payload['EquipTypes']),'equip rows')
    return result


if __name__ == '__main__': build()
