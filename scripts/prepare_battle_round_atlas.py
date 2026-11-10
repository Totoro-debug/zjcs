"""Port the selected client's dynamic portrait atlas without changing its algorithms."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/DynamicTexture2DAtlas'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/BattleRoundUI/DynamicTexture2DAtlas'
RECEIPT = ROOT / 'assets/reference/battle-hud/round-atlas-source-manifest.json'
NAMES = ('AddTexture2DResult', 'BoxInfo', 'DynamicTexture2DAtlasUtils', 'ETC2GridDynamicAtlas',
         'FallBackGridDynamicAtlas', 'GridBoxContainer', 'GridSpriteImage', 'GridSpriteInfo',
         'GridSpriteRawImage', 'IGridDynamicAtlas')


def sha(data):
    return hashlib.sha256(data).hexdigest()


def metadata(path, folder=False):
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        value = hashlib.md5(('zjcs:round-atlas:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        meta.write_text('fileFormatVersion: 2\nguid: ' + value + ('\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
            if folder else '\n'), encoding='utf-8')


def port(text):
    text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
    text = re.sub(r'^using (?:Framework\.Logging(?:\.InterpolatedStringHandler)?|UnityEngine.Device);\n', '', text, flags=re.M)
    text = text.replace('namespace DynamicTexture2DAtlas;', 'namespace Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas\n{\nusing Object = UnityEngine.Object;')
    # ILSpy's C# 14 field-backed properties are plain automatic properties here.
    pattern = r'((?:public|private) \w+ \w+)\s*\{\s*\[CompilerGenerated\]\s*get\s*\{\s*return field;\s*\}\s*(?:\[CompilerGenerated\]\s*(private set)\s*\{\s*field = value;\s*\}\s*)?\}'
    text = re.sub(pattern, lambda m: m[1] + ' { get;' + (' private set;' if m[2] else '') + ' }', text)
    text = text.replace('\t\tbase._002Ector();\n', '')
    text = text.replace('.Remove(uniqueKey, ref ', '.Remove(uniqueKey, out ')
    # Calls on an explicitly cast this were ILSpy's rendering of base dispatch.
    text = text.replace('((RawImage)this).OnPopulateMesh(vh);', 'base.OnPopulateMesh(vh);')
    text = text.replace('((Graphic)this).OnDestroy();', 'base.OnDestroy();')
    text = text.replace('Color32.op_Implicit(((Graphic)this).color)', '(Color32)color')
    text = re.sub(r'Vector4.op_Implicit\((gridSpriteInfo\.\w+)\)', r'(Vector4)\1', text)
    text = re.sub(r'new Vector3\((num\w*), (num\w*)\)', r'new Vector3(\1, \2, 0)', text)
    text = text.replace('GLoggerErrorInterpolatedStringHandler', 'RoundAtlasDiagnostic')
    text = text.replace('GLoggerWarnInterpolatedStringHandler', 'RoundAtlasDiagnostic')
    if re.search(r'\bfield\b|_002Ector|op_Implicit|Framework\.', text):
        raise ValueError('Unadapted decompiler syntax/dependency')
    return '// Official client 884_156692; deterministic C# / logging adaptation only.\n' + text.rstrip() + '\n}\n'


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    metadata(DEST, True)
    inputs, outputs = {}, {}
    for name in NAMES:
        source = SOURCE / (name + '.cs')
        target = DEST / (name + '.cs')
        text = port(source.read_text(encoding='utf-8-sig'))
        target.write_text(text, encoding='utf-8', newline='\n')
        metadata(target)
        inputs[source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
    for name in ('FightDynamicTextureETC2', 'HeadCharacterPortraitETC2Scene', 'GameRoundPortraitETC2SceneHandler'):
        source = SOURCE.parent / (name + '.cs')
        inputs[source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
    native_root = SOURCE.parents[2] / 'native-conversion'
    manifest = native_root / 'native-export-all-source-manifest.json'
    source_manifest = json.loads(manifest.read_bytes())
    if source_manifest['manifest'] != '884_156692':
        raise ValueError('Review changed original native export version')
    key = 'ExportedProject/Assets/ScriptableConfig/Modules/Fight/GamePlayManagerConfig.asset'
    config = native_root / 'native-export-all' / key
    expected, = [row['sha256'] for row in source_manifest['outputs'] if row['path'] == key]
    if sha(config.read_bytes()) != expected:
        raise ValueError('Changed original GamePlayManagerConfig export')
    flags = {name: int(re.search(r'^  ' + name + r': (\d+)$', config.read_text(encoding='utf-8-sig'), re.M)[1])
             for name in ('EnablePlayerPortraitETC2', 'EnablePlayerPortraitETC2_Scene')}
    if flags != dict(EnablePlayerPortraitETC2=1, EnablePlayerPortraitETC2_Scene=1):
        raise ValueError('Review changed original portrait path selection')
    for source in (manifest, config):
        inputs[source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
    for target in DEST.glob('*.cs'):
        metadata(target)
    for target in sorted(DEST.iterdir()):
        if target.is_file(): outputs[target.relative_to(ROOT).as_posix()] = sha(target.read_bytes())
    outputs[Path(str(DEST) + '.meta').relative_to(ROOT).as_posix()] = sha(Path(str(DEST) + '.meta').read_bytes())
    RECEIPT.write_text(json.dumps(dict(manifest='884_156692', inputs=inputs, outputs=outputs,
        generator_sha256=sha(Path(__file__).read_bytes()), original_portrait_flags=flags,
        adaptations=['Namespace isolation and UnityEngine.Object alias.',
            'Use actual UnityEngine.SystemInfo on Windows rather than the editor device simulation facade.',
            'Remove IL diagnostic comments; automatic properties and implicit conversions for Unity C#.',
            'Dictionary.Remove out parameter and base RawImage/Graphic dispatch decompiler repairs.',
            'Local Unity logging adapter retains original diagnostic messages and call sites.'],
        runtime_ports={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in (
            ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneTimelineView.cs',
            ROOT / 'unity/Assets/Game/Editor/BattleRoundUI/OfficialBattleRoundSceneAcceptance.cs',
            ROOT / 'unity/Assets/Game/Editor/BattleRoundUI/Zjcs.Editor.BattleRoundUI.asmdef')},
        scope='Original factory, ETC2 and fallback atlases, grid allocation/extension, sprite/UV ownership and UI consumers. The existing public timeline routes readable full 100x120 player captures through the original atlas; static sprite branches are retained. Native head request/handler and complete native card migration remain pending. No UI asset fields are changed.'),
        ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Ported', len(NAMES), 'original dynamic portrait atlas classes.')


if __name__ == '__main__':
    main()
