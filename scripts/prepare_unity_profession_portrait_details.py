"""Copy the original portrait hash/detail helpers across the opaque data boundary."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CODE = ROOT / '.tools/source-packages/current/871_156598/official-code'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def method(text, name):
    start = text.rindex('\tpublic static ', 0, text.index(' ' + name + '('))
    opening = text.index('{', start)
    depth = 1
    end = opening + 1
    while depth:
        if text[end] == '{': depth += 1
        if text[end] == '}': depth -= 1
        end += 1
    return text[start:end]


def build():
    source = CODE / 'client-source/CharacterPortraitUtils.cs'
    text = source.read_text(encoding='utf-8')
    names = ['GetAppearanceHashCode', 'GetParam', 'GetDetailRequestData', 'IsVaildCacha']
    methods = []
    for name in names:
        part = method(text, name)
        part = part.replace('this AppearanceData appearanceData', 'this object appearanceData')
        part = part.replace('ICharacterPortraitSkinHashProvider hashProvider)', 'ICharacterPortraitSkinHashProvider hashProvider, IProfessionPortraitAppearanceReader reader)')
        part = part.replace('appearanceData.ModifyTimeStamp', 'reader.GetModifyTimeStamp(appearanceData)')
        part = part.replace('KeyValuePair<SkinType, SkinData>', 'KeyValuePair<SkinType, object>').replace('appearanceData.SkinMap', 'reader.GetSkins(appearanceData)')
        part = part.replace('SkinData value = item.Value;', 'object value = item.Value;').replace('GetSkinHash(value.SkinId)', 'GetSkinHash(reader.GetSkinId(value))')
        part = part.replace('ICharacterPortraitSkinHashProvider provider, out ', 'ICharacterPortraitSkinHashProvider provider, IProfessionPortraitAppearanceReader reader, out ')
        part = part.replace('AppearanceData appearanceData = ', 'object appearanceData = ')
        part = part.replace('GetAppearanceHashCode(param, provider)', 'GetAppearanceHashCode(param, provider, reader)')
        methods.append(part)
    data_source = CODE / 'common-source/Common/AppearanceData.cs'
    skin_source = CODE / 'common-source/Common/SkinData.cs'
    for path, required in [(data_source, ['public long ModifyTimeStamp;', 'public Dictionary<SkinType, SkinData> SkinMap']),
                           (skin_source, ['public int SkinId;'])]:
        original = path.read_text(encoding='utf-8')
        if not all(value in original for value in required):
            raise ValueError('Original portrait field boundary changed: ' + str(path))
    target = RUNTIME / 'CharacterPortraitDetails.cs'
    target.write_text('namespace Zjcs.Official.ProfessionUI\n{\nusing System.Collections.Generic;\npublic static class CharacterPortraitDetails\n{\n' + '\n\n'.join(methods) + '\n}\n}\n', encoding='utf-8', newline='\n')
    reader = RUNTIME / 'ProfessionPortraitAppearanceReader.cs'
    for path in (target, reader):
        path.with_name(path.name + '.meta').write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs-profession-ui-' + path.stem).encode()).hexdigest() + '\n', encoding='utf-8', newline='\n')
    proof = dict(format=1, manifest='871_156598', source=record(source), methods=names,
        appearance_field_sources=[record(data_source), record(skin_source)],
        runtime=[dict(record(p), meta=record(p.with_name(p.name + '.meta'))) for p in (target, reader)],
        generator=record(Path(__file__)),
        validation=record(ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionPortraitManagerValidation.cs'),
        portrait_settings=record(ROOT / 'assets/reference/profession-ui/portrait-settings-source-manifest.json'),
        operation='Copy four original helpers. Retain ModifyTimeStamp XOR skin hashes only when the original weight is greater than zero; do not multiply by the weight or include excluded zero-weight skins. Keep original request/version/hash data and cache hash-only validity. Read the original public AppearanceData/SkinData field schema through an explicit read-only adapter across the existing opaque payload boundary. No appearance clone or mutation is performed by the adapter.',
        limitations=['Full appearance models/builders, production studio and profession page remain unbound.'])
    (ROOT / 'assets/reference/profession-ui/portrait-details-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Ported four original portrait detail/hash helper methods.')


if __name__ == '__main__':
    build()
