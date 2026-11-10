"""Port original battle portrait requests and handlers; keep serialized UI fields."""
from pathlib import Path
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/BattleRoundUI'
NAMES = ('HeadCharacterPortrait', 'HeadCharacterPortraitETC2', 'HeadCharacterPortraitETC2Scene',
         'IFightDynamicTextureETC2', 'IGameRoundPortraitHandler', 'GameRoundPortraitNormalHandler',
         'GameRoundPortraitETC2Handler', 'GameRoundPortraitETC2SceneHandler')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    inputs, outputs = {}, {}
    for name in NAMES:
        source = SOURCE / (name + '.cs')
        text = source.read_text(encoding='utf-8-sig')
        text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
        text = re.sub(r'^using (?:Common|DynamicTexture2DAtlas|Framework\.[\w.]+);\n', '', text, flags=re.M)
        text = text.replace('AppearanceData appearanceData', 'object appearanceData')
        text = text.replace('Profession profession', 'int profession')
        text = text.replace('PlayerPortraitSettings.EPortraitType', 'ProfessionPortraitKind')
        text = text.replace('CharacterPortraitUtils.GetLongHashCode', 'ProfessionPortraitRequestKey.GetLongHashCode')
        text = text.replace('appearanceData, sex, profession, needPersist', 'appearanceData, (int)sex, profession, needPersist')
        text = text.replace('CircleSpriteRenderer', 'WorldCircleSpriteRenderer')
        text = text.replace('\t[Inject]\n', '')
        injection = 'private IFightDynamicTextureETC2 mFightDynamicTextureETC2 { get; set; }'
        if injection in text:
            text = text.replace(injection, injection + '\n\n\tpublic void BindAtlas(IFightDynamicTextureETC2 atlas) => mFightDynamicTextureETC2 = atlas ?? throw new System.ArgumentNullException(nameof(atlas));')
        text = text.replace('GLoggerWarnInterpolatedStringHandler', 'RoundAtlasDiagnostic')
        header = ('// Official client 884_156692; original request/handler control flow.\n'
                  'using Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.WorldUI;\n'
                  'using Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas;\n')
        text = header + 'namespace Zjcs.Official.BattleRoundUI\n{\nusing Object = UnityEngine.Object;\n' + text + '\n}\n'
        target = DEST / (name + '.cs')
        target.write_text(text, encoding='utf-8', newline='\n')
        meta = Path(str(target) + '.meta')
        if not meta.exists():
            guid = hashlib.md5(('zjcs:round-portrait:' + name).encode()).hexdigest()
            meta.write_text('fileFormatVersion: 2\nguid: ' + guid + '\n', encoding='utf-8')
        inputs[source.relative_to(ROOT).as_posix()] = sha(source)
        for p in (target, meta): outputs[p.relative_to(ROOT).as_posix()] = sha(p)
    receipt = ROOT / 'assets/reference/battle-hud/round-portrait-source-manifest.json'
    shared = ('CharacterPortraitBase', 'CharacterPortraitRequestor', 'CharacterPortraitCache',
              'CharacterPortraitTexture2D', 'ProfessionPortraitRequestKey')
    for name in shared:
        p = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI' / (name + '.cs')
        inputs[p.relative_to(ROOT).as_posix()] = sha(p)
    runtime = [DEST / 'Zjcs.Official.BattleRoundUI.asmdef']
    editor = ROOT / 'unity/Assets/Game/Editor/BattleRoundUI'
    runtime += [editor / name for name in ('OfficialBattlePortraitRequestAcceptance.cs',
                'OfficialBattlePortraitRequestAcceptance.cs.meta', 'OfficialBattleRoundSceneAcceptance.cs',
                'Zjcs.Editor.BattleRoundUI.asmdef')]
    receipt.write_text(json.dumps(dict(manifest='884_156692', inputs=inputs, outputs=outputs,
        runtime_ports={p.relative_to(ROOT).as_posix(): sha(p) for p in runtime},
        generator_sha256=sha(Path(__file__)), adaptations=[
            'Namespace isolation; reuse existing official portrait base/requestor/cache and request-key implementation.',
            'Reuse existing Sex/portrait-kind enums; profession integer and opaque appearance as in the existing offline portrait interface.',
            'Explicit BindAtlas replaces framework injection without changing serialized fields or event order.',
            'Reuse the migrated original circle renderer and diagnostic adapter; remove IL comments.'],
        scope='Original head request, scene/UGUI atlas notification and three portrait handlers. Full native card bindings and player-handler configuration selection are pending. No UI asset edits.'),
        ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('Ported', len(NAMES), 'original portrait classes.')


if __name__ == '__main__':
    main()
