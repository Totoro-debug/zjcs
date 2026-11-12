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
         'EffectUIType', 'DamageCustomActionUI', 'ElementConfrontationUI', 'DamageAutoSizeTextBack',
         'FightDamageSettings', 'ElementDamageColor', 'CharacterDamageUIPoolPresent', 'ICustomDamageUIFilter',
         'CustomDamageUITypeFilter', 'CharacterDamageUIManager')
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


def replace_method(text, name, replacement):
    match = re.search(r'^\s*(?:private|protected override) [^\n]*\b' + name + r'\([^\n]*\)\s*\{', text, re.M)
    if not match: raise ValueError('Original damage manager method missing: ' + name)
    start, cursor, depth = match.start(), match.end(), 1
    while depth:
        depth += (text[cursor] == '{') - (text[cursor] == '}')
        cursor += 1
    return text[:start] + '\n\t' + replacement + '\n' + text[cursor:]


def manager(text):
    text = re.sub(r'^\s*private DisposableBag \w+;[^\S\n]*\n', '\n', text, flags=re.M)
    text = re.sub(r'^\s*\[Inject\]\s*private [^\n]+ \w+ \{ get; set; \}[^\S\n]*\n', '\n', text, flags=re.M)
    text = text.replace('public void Init()', 'public CharacterDamageUIContext Context { private get; set; }\n\n\tpublic void Init()')
    text = text.replace('FightDamageSettings.Value', 'Context.Settings')
    text = text.replace('mFightRoot.DamageFloatsRoot', 'Context.DamageFloatsRoot')
    text = text.replace('Parsers.Instance.GameSettingsParser.Get().ECIsUseGameObjectPool', 'Context.UseGameObjectPool')
    text = text.replace('mFightRoot.Container.GetEntity(', 'Context.GetEntity(')
    text = text.replace('(Object)(object)mFightRoot.Fight == (Object)null', '!Context.StageType.HasValue')
    text = text.replace('StageType stageType = mFightRoot.Fight.Component.StageInfo.Type;', 'int stageType = Context.StageType.Value;')
    text = text.replace('FightSettings.Value.FightWarmupSettings', 'Context.WarmupSettings.FightWarmupSettings')
    text = re.sub(r'\bFightWarmupSetting\b', 'CharacterDamageUIStageWarmup', text)
    text = re.sub(r'\bFightDamageEffectUIWarmupSetting\b', 'CharacterDamageUIPresetCount', text)
    text = text.replace('\t\tmEffectUIDisposableBag = default;\n', '')
    text = text.replace('Disposable.AddTo<AssetOperationHandle<GameObject>>(ResourceLoader.LoadAssetByGuid<GameObject>(characterDamageUIPoolPresent.EffectUIPrefabProxy.AssetGuid), ref mEffectUIDisposableBag).Result',
                        'Context.LoadPrefab(characterDamageUIPoolPresent.EffectUIPrefabProxy.AssetGuid)')
    text = text.replace('mEffectUIDisposableBag.Dispose();', 'Context.ReleasePrefabs();')
    text = replace_method(text, 'OnEnableAndInited', '''protected override void OnEnableAndInited()
    {
        base.OnEnableAndInited();
        Context.OnCharacterAdd += MyGame_OnCharacterAdd;
        Context.OnCharacterRemove += MyGame_OnCharacterRemove;
        Context.OnSkillReplay += OnSkillReplay;
        foreach (CharacterDamageUIRole character in Context.Characters) MyGame_OnCharacterAdd(character);
    }''')
    text = replace_method(text, 'OnDisableAndInited', '''protected override void OnDisableAndInited()
    {
        Context.OnSkillReplay -= OnSkillReplay;
        Context.OnCharacterAdd -= MyGame_OnCharacterAdd;
        Context.OnCharacterRemove -= MyGame_OnCharacterRemove;
        foreach (CharacterDamageUIRole character in Context.Characters) MyGame_OnCharacterRemove(character);
        base.OnDisableAndInited();
    }''')
    for name, op in [('MyGame_OnCharacterAdd', '+='), ('MyGame_OnCharacterRemove', '-=')]:
        text = replace_method(text, name, '''private void %s(CharacterDamageUIRole character)
    {
        MyFightRoleFight component = character.Fight;
        if (component != null) component.OnDamage %s MyBattle_OnDamage;
        character.OnSkillCostHp %s OnSkillCostHp;
        character.OnDisperseStatus %s OnDisperseStatus;
        character.OnStatusDamage %s FightStatusAgent_OnStatusDamage;
        character.OnStatusTriggerCustomAction %s FightStatusAgent_OnStatusTriggerCustomAction;
    }''' % (name, op, op, op, op, op))
    text = replace_method(text, 'OnSkillReplay', '''private void OnSkillReplay(CharacterDamageUISkillReplay replay)
    {
        if (replay.IsExtra) return;
        MyEntity entity = Context.GetEntity(replay.SkillId);
        if (entity != null)
        {
            MyFightSkill skill = entity.GetComponent<MyFightSkill>();
            if (CanShowSkillReplay(skill)) ShowResetSkillCDUI(replay.CharacterId);
        }
        else
        {
            Context.GetSkillPrefabAsync(replay.SkillId, prefabSkill =>
            {
                if (prefabSkill && prefabSkill.ShowSkillReplay) ShowResetSkillCDUI(replay.CharacterId);
            });
        }
    }''')
    text = text.replace('!((Object)(object)mFightRoot?.Container == (Object)null)', 'Context != null')
    text = text.replace('LanguageUtils.GetEnumKey(customAction)', '"EStatusCustomAction." + customAction')
    text = text.replace('FightRoleFightComponent', 'CharacterDamageUIRole')
    text = text.replace('battle.Entity.Id', 'battle.Id').replace('comp.Entity.Id', 'comp.Id')
    text = text.replace('component.SkillElemntType', 'Context.GetSkillElementType(dmg)')
    text = text.replace('UIUtils.Instantiate(value, damageFloatsRoot)',
                        'StoryPlayableInstantiation.Instantiate(damageFloatsRoot, value, damageFloatsRoot, false)')
    # Only replace generated structured logging blocks; preserve the error branch.
    text = re.sub(r'\t\t\tILogger skill = FightLog.Skill;.*?GZLoggerExtensions.Error\([^\n]+;',
        '\t\t\tDebug.LogError("CreateEffectUIPool;effectUIPrefab != null, i= " + i);', text, count=1, flags=re.S)
    text = re.sub(r'\t\t\t\tILogger skill = FightLog.Skill;.*?GZLoggerExtensions.Error\([^\n]+;',
        '\t\t\t\tDebug.LogError("[Battle] CharacterUIManager EffectUIType(" + effectUIType + ") is not support type!");', text, count=1, flags=re.S)
    text = text.replace('\t\tbool flag = default;\n', '')
    text = re.sub(r'^using (?:R3|ZLogger|Microsoft.Extensions.Logging|GFrameCore.Extensions.ZLogger);\s*\n', '', text, flags=re.M)
    if re.search(r'\b(?:mFightRoot|DisposableBag|FightLog|ZLogger|FightRoleFightComponent|FightStatusAgentComponent)\b', text):
        raise ValueError('Unadapted original damage manager dependency')
    return text


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
    inputs.extend(identity(CODE / 'client-source' / (name + '.cs'))
                  for name in ('FightWarmupSetting', 'FightDamageEffectUIWarmupSetting'))
    for name in NAMES + ('EStatusCustomAction',):
        path = CODE / ('common-source/Common' if name == 'EStatusCustomAction' else 'client-source') / (name + '.cs')
        inputs.append(identity(path))
        text = path.read_text(encoding='utf-8-sig')
        if name == 'CharacterDamageUIManager': text = manager(text)
        text = text.replace('[ScriptableObjectConfig]\n', '')
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
        imports = {
            'CharacterDamageUIManager': ('EntityLoading', 'StoryPlayableManager', 'BattleSkillPlayables', 'ProfessionUI'),
            'ElementDamageColor': ('BattleSkillPlayables',),
            'CharacterDamageUIPoolPresent': ('ProfessionUI',),
            'ICustomDamageUIFilter': ('BattleSkillPlayables',),
            'CustomDamageUITypeFilter': ('BattleSkillPlayables',),
        }
        usings.extend('using Zjcs.Official.' + value + ';' for value in imports.get(name, ()))
        text = re.sub(r'^using .*;\s*\n', '', text, flags=re.M)
        target = DEST / (name + '.cs')
        write_changed(target, '\n'.join(usings) + '\n' + HEADER + text.strip() + '\n}\n')
        meta(target)
        outputs.extend((identity(target), identity(Path(str(target) + '.meta'))))
    bindings = DEST / 'CharacterDamageUIContext.cs'
    meta(bindings)
    outputs.extend((identity(bindings), identity(Path(str(bindings) + '.meta'))))
    assembly = DEST / 'Zjcs.Official.BattleDamageUI.asmdef'
    write_changed(assembly, json.dumps(dict(name='Zjcs.Official.BattleDamageUI', autoReferenced=False,
        references=['Unity.TextMeshPro', 'Zjcs.Official.EntityLifecycle', 'Zjcs.Official.StoryPlayables',
                    'Zjcs.Official.StoryBubbles', 'Zjcs.Official.WorldUI', 'Zjcs.Official.World.NativeLabelUI',
                    'Zjcs.Official.BattleSkillPlayables', 'Zjcs.Official.EntityLoading', 'Zjcs.Official.ProfessionUI',
                    'Zjcs.Official.StoryPlayableManager']), indent=2) + '\n')
    meta(assembly)
    outputs.extend((identity(assembly), identity(Path(str(assembly) + '.meta'))))
    receipt = ROOT / 'assets/reference/battle-hud/damage-ui-runtime-source-manifest.json'
    receipt.write_text(json.dumps(dict(manifest='884_156692', inputs=inputs, outputs=outputs,
        generator=identity(Path(__file__)),
        adaptations=['Namespace and existing original entity/pool/language types; remove IL comments and repair explicit Unity bool operators.',
            'Reuse original Chinese battle-number formatter; original LRU cache omitted.',
            'Inject element threshold as a local property; original branches and order retained.',
            'Original manager presentation, stacking, type/color priority and pool lifecycle retained; local fight/resource/notification context replaces absent framework root and R3 subscriptions.',
            'Original stage-specific warmup lookup retained over consumed native fields; context supplies stage identity, consumed skill element and asynchronous prefab replay check; no server result is inferred.'],
        scope='Complete original serialized UI behaviours and manager; formal battle context migrated separately.'),
        ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


if __name__ == '__main__':
    main()
