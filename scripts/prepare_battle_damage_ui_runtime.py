"""Port the complete original serialized world-damage UI behaviours."""
import json
import re
from hashlib import sha256
from pathlib import Path

from official_apk import ROOT
from prepare_battle_hit_view_runtime import clean
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed

CODE = ROOT / '.tools/source-packages/current/884_156692/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/BattleDamageUI'
NAMES = ('CharacterDamageUI', 'DamageNumberUI', 'DamageColor', 'DamageFloatSize',
         'EffectUIType', 'DamageCustomActionUI', 'ElementConfrontationUI', 'DamageAutoSizeTextBack')
HEADER = '''// Complete original behaviour; see battle-hud/damage-ui-runtime-source-manifest.json.
using System;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using AttachToObjUI = Zjcs.Official.StoryBubbles.AttachToObjUI;
namespace Zjcs.Official.BattleDamageUI
{
'''


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    if json.loads((ROOT / 'official-source.json').read_bytes())['active'] != '884_156692':
        raise ValueError('Review selected original damage UI version')
    manifest = CODE.parent / 'code-manifest.json'
    proof = json.loads(manifest.read_bytes())
    if proof['manifest'] != '884_156692': raise ValueError('Review original UI assemblies')
    inputs, outputs = [identity(manifest)], []
    for name in ('Assembly-CSharp.dll', 'Console.dll'):
        assembly_path = CODE / 'managed/decoded' / name
        if sha256(assembly_path.read_bytes()).hexdigest() != proof['assemblies'][name]['decoded_sha256']:
            raise ValueError('Changed original UI assembly: ' + name)
        inputs.append(identity(assembly_path))
    for name in NAMES + ('EStatusCustomAction',):
        path = CODE / ('common-source/Common' if name == 'EStatusCustomAction' else 'client-source') / (name + '.cs')
        inputs.append(identity(path))
        text = path.read_text(encoding='utf-8-sig')
        text = re.sub(r'^using (?:Common|Framework(?:\.DependencyInjection)?);\s*\n', '', text, flags=re.M)
        text = re.sub(r'^namespace Common;\s*\n', '', text, flags=re.M)
        text = re.sub(r'\[Inject\]\s*private IScriptableObjectConfig<FightSettings> mSettings \{ get; set; \}',
                      'public float ElementConfrontThreshold { get; set; }', text)
        text = text.replace('mSettings.Value.ElementConfrontThreshold', 'ElementConfrontThreshold')
        text = re.sub(r'(mTipUi|mTrueDamageText)\.SetColor\(([^;]+)\);', r'\1.Color = \2;', text)
        text = text.replace('BattleUIUtils.GetBattleNumberString(damageValue)',
            'Zjcs.Official.World.NativeLabelUI.Framework.NumberFormatHelper.FormatBattleByFourWidth('
            'Math.Abs(damageValue), new string[] { "", "万", "亿", "兆", "京" })')
        # Preserve attributes at the type declaration, and keep namespace imports
        # such as IEnumerator/TMP_Text that are still used by the complete body.
        text = clean(text)
        text = re.sub(r'Object\.op_Implicit\(\(Object\)\(object\)(\w+)\)', r'\1', text)
        usings = [value for value in re.findall(r'^using [^\r\n]+;', text, re.M)
                  if value not in ('using System;', 'using UnityEngine;')]
        text = re.sub(r'^using .*;\s*\n', '', text, flags=re.M)
        target = DEST / (name + '.cs')
        write_changed(target, '\n'.join(usings) + '\n' + HEADER + text.strip() + '\n}\n')
        meta(target)
        outputs.extend((identity(target), identity(Path(str(target) + '.meta'))))
    assembly = DEST / 'Zjcs.Official.BattleDamageUI.asmdef'
    write_changed(assembly, json.dumps(dict(name='Zjcs.Official.BattleDamageUI', autoReferenced=False,
        references=['Unity.TextMeshPro', 'Zjcs.Official.EntityLifecycle', 'Zjcs.Official.StoryPlayables',
                    'Zjcs.Official.StoryBubbles', 'Zjcs.Official.WorldUI', 'Zjcs.Official.World.NativeLabelUI']), indent=2) + '\n')
    meta(assembly)
    outputs.extend((identity(assembly), identity(Path(str(assembly) + '.meta'))))
    receipt = ROOT / 'assets/reference/battle-hud/damage-ui-runtime-source-manifest.json'
    receipt.write_text(json.dumps(dict(manifest='884_156692', inputs=inputs, outputs=outputs,
        generator=identity(Path(__file__)),
        adaptations=['Namespace and existing original entity/pool/language types; remove IL comments and repair explicit Unity bool operators.',
            'Reuse original Chinese battle-number formatter; original LRU cache omitted.',
            'Inject element threshold as a local property; original branches and order retained.'],
        scope='Complete original serialized UI behaviours; manager/event host migrated separately.'),
        ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


if __name__ == '__main__':
    main()
