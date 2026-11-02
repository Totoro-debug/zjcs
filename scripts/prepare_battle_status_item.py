"""Restore the original world status prefab, distinct from menu StatusUI."""
import hashlib
import json
import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / '.tools/source-packages/current/884_156692/native-conversion'
EXPORT = CACHE / 'native-export-all'
SOURCE = EXPORT / 'ExportedProject/Assets/Prefabs/Fight/Status/BattleStatusItem.prefab'
TARGET = ROOT / 'unity/Assets/Official/Combat/Status/Resources/Combat/Status/BattleStatusItem.prefab'
RECEIPT = ROOT / 'assets/reference/battle-hud/status-item-source-manifest.json'


def digest(raw): return hashlib.sha256(raw).hexdigest()


def identity(path): return {'path': path.relative_to(ROOT).as_posix(), 'sha256': digest(path.read_bytes())}


def guid(path):
    return re.search(r'^guid: ([a-f0-9]{32})', Path(str(path) + '.meta').read_text(encoding='utf-8-sig'), re.M)[1]


def meta(path, folder=False):
    p = Path(str(path) + '.meta')
    if not p.exists():
        value = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/status-item/' + path.relative_to(ROOT).as_posix()).hex
        p.write_text('fileFormatVersion: 2\nguid: ' + value + ('\nfolderAsset: yes\n' if folder else '\n'), encoding='utf-8')


def main():
    proof = json.loads((CACHE / 'native-export-all-source-manifest.json').read_bytes())
    assert proof['manifest'] == '884_156692'
    expected = {row['path']: row['sha256'] for row in proof['outputs']}
    assert digest(SOURCE.read_bytes()) == expected[SOURCE.relative_to(EXPORT).as_posix()]
    font = EXPORT / 'ExportedProject/Assets/TextMesh Pro/Fonts/FZY4K.ttf'
    assert digest(font.read_bytes()) == expected[font.relative_to(EXPORT).as_posix()]
    assert digest(font.read_bytes()) == '90023872dffe6a58503de655bd0631bf3334ce3b6b72cc75a6625b685c94c386'
    text = SOURCE.read_text(encoding='utf-8-sig')
    item = ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/FightStatusItem.cs'
    effect = ROOT / 'unity/Assets/Game/Runtime/Official/World/NativeText/TMPEffect.cs'
    tmp = next((ROOT / 'unity/Library/PackageCache').glob('com.unity.textmeshpro@*/Scripts/Runtime/TextMeshPro.cs'))
    # The installed native FZY4K asset uses this identical official TTF and the
    # existing Windows TMP shader/material port, already shared by world tips.
    shared = ROOT / 'unity/Assets/Official/Combat/ElementFeedback/Resources/Combat/ElementFeedback'
    font_asset = shared / 'Font_993fe9f48bba80bc.asset'
    material = shared / 'Material_57b7f2adeb457a8f.mat'
    source_font = EXPORT / 'ExportedProject/Assets/TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic_0.asset'
    source_material = EXPORT / 'ExportedProject/Assets/TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF Material_2.mat'
    for path in (source_font, source_material):
        assert digest(path.read_bytes()) == expected[path.relative_to(EXPORT).as_posix()]
    def face(path):
        return re.search(r'(?ms)^  m_FaceInfo:\n.*?(?=^  [^ ])', path.read_text(encoding='utf-8-sig'))[0]
    assert face(source_font) == face(font_asset), 'Installed official font face metrics differ'
    def floats(path):
        return {key: float(value) for key, value in re.findall(r'^\s*(?:- )?(_\w+): ([-\d.eE]+)$',
            path.read_text(encoding='utf-8-sig'), re.M)}
    original_values = floats(source_material)
    assert all(original_values[key] == value for key, value in floats(material).items())
    background = ROOT / 'unity/Assets/Official/Combat/HUD/Sprites/BG_Abnormal-56ca57608c.png'
    bindings = {-10113449: guid(item), -806885394: guid(tmp), 1301585197: guid(effect)}
    # Remove only the language wrapper for literal "{0}". Keep the original TMP
    # component, outline effect, four nodes, rects, renderers and sorting values.
    text = re.sub(r'(?ms)^--- !u!114 &114372920208702427\n.*?(?=^--- !u!|\Z)', '', text)
    text = text.replace('  - component: {fileID: 114372920208702427}\n', '')
    text = text.replace('mStackCountText: {fileID: 114372920208702427}', 'mStackCountText: {fileID: 114589667821646929}')
    def script(match):
        key = int(match[1]); assert key in bindings, key
        return 'm_Script: {fileID: 11500000, guid: ' + bindings[key] + ', type: 3}'
    text = re.sub(r'm_Script: \{fileID: (-?\d+), guid: [a-f0-9]{32}, type: 3\}', script, text)
    for old, new in [('59f4a7347d13ee84897ef251bdf2b26d', guid(font_asset)),
                     ('bd38b875bd3c62f45b6217d057f638d0', guid(material)),
                     ('a4c59a0874d21924d836b2b70c10c62e', guid(background))]: text = text.replace(old, new)
    # PNG sprites are TextureImporter subassets (type 3); native .asset/.mat
    # references retain type 2. The converter's source PNG pointer uses type 2.
    text = text.replace('guid: ' + guid(background) + ', type: 2}', 'guid: ' + guid(background) + ', type: 3}')
    for p in reversed(list(TARGET.parent.parents)):
        if p != ROOT / 'unity/Assets/Official/Combat' and ROOT / 'unity/Assets/Official/Combat' not in p.parents: continue
        p.mkdir(exist_ok=True); meta(p, True)
    TARGET.parent.mkdir(exist_ok=True); meta(TARGET.parent, True)
    TARGET.write_text(text, encoding='utf-8'); meta(TARGET)
    inputs = [SOURCE, font, source_font, source_material, item, effect, font_asset, material, background,
              ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/FightStatusItem.cs',
              ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/FightStatusItemBase.cs',
              ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/FightStatusController.cs']
    inputs += [Path(str(p)+'.meta') for p in (item, effect, tmp, font_asset, material, background)]
    runtime = [ROOT / name for name in (
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleUnitWorldHudView.cs',
        'unity/Assets/Game/Editor/Combat/OfficialBattleStatusCounterAcceptance.cs',
        'unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.RoundStartup.cs',
        'unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.StatusIcons.cs')]
    RECEIPT.write_text(json.dumps({'manifest': '884_156692', 'inputs': [identity(p) for p in inputs],
        'converter_receipt': identity(CACHE / 'native-export-all-source-manifest.json'),
        'generator': identity(Path(__file__)), 'outputs': [identity(TARGET), identity(Path(str(TARGET)+'.meta'))],
        'runtime': [identity(p) for p in runtime],
        'adaptation': 'Original native prefab; explicit sprite overload and literal count formatter. Original builtin sprite material, sorting, TMP parameters and TMPEffect retained. Bind identical official FZY4K TTF through the installed native world-tip font/material port; no alternate font or artwork. Historical 854 unit-bar cell/layout binding remains separately pinned and unchanged.'}, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


if __name__ == '__main__': main()
