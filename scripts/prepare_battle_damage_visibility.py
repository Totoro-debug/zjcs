"""Extract original damage-visibility switches and role-local zero-damage filters."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas

OUTPUT = 'unity/Assets/Game/Resources/Combat/damage-visibility.json'
RECEIPT = 'assets/reference/battle-hud/damage-visibility-source-manifest.json'
FILTER_FIELDS = ['IsCrit', 'IsBlock', 'IsDodge', 'IsBlinding', 'IsImmunity',
                 'IsBreakResilience', 'IsTrueDamage']


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes((json.dumps(value, ensure_ascii=False, indent=2)+'\n').encode())


def refresh_runtime():
    path=ROOT/RECEIPT
    receipt=json.loads(path.read_bytes())
    names=[
        'unity/Assets/Game/Runtime/Official/Combat/CharacterDamageUIManager.cs',
        'unity/Assets/Game/Runtime/Official/Combat/CustomDamageUITypeFilter.cs',
        'unity/Assets/Game/Runtime/Official/Combat/FightBattleExecutor.cs',
        'unity/Assets/Game/Runtime/Official/Combat/FightBattleStatistics.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleDamageVisibilityCatalog.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleDamageColors.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.cs',
        'unity/Assets/Game/Editor/Combat/OfficialBattleDamageVisibilityAcceptance.cs',
        'unity/Assets/Game/Editor/Combat/OfficialBattleHudAssetTools.cs',
        'unity/Assets/Game/Editor/OfficialMap10BattlePlayModeAcceptance.cs',
        'scripts/test_battle_damage_display.ps1']
    receipt['runtime']=[dict(path=n,sha256=sha(ROOT/n)) for n in names]
    receipt['output_sha256']=sha(ROOT/OUTPUT)
    receipt['generator_sha256']=sha(__file__)
    receipt['effect_type_sources']=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p))
        for p in [ROOT/'.tools/source-packages/official-code/client-source/EffectUIType.cs',
                  ROOT/'.tools/source-packages/current/871_156598/official-code/client-source/EffectUIType.cs']]
    receipt['legacy_records']='Absent DamageEntityId remains zero and uses original MyFightDamage defaults (hide=false, showZero=true). Do not confuse the runtime damage serial or hit entity identity with the missing damage prefab identity. Positive unbound prefab IDs fail explicitly.'
    save(path,receipt)
    # These existing receipts also pin the touched shared presentation/executor code.
    for name in ['battle-hud/damage-display-source-manifest.json',
                 'battle-hud/element-feedback-source-manifest.json',
                 'battle-hud/world-damage-source-manifest.json',
                 'battle-rules/element-confrontation-source-manifest.json']:
        path=ROOT/'assets/reference'/name
        data=json.loads(path.read_bytes())
        for row in data.get('runtime',[]):
            if row['path'] in names:row['sha256']=sha(ROOT/row['path'])
        if name=='battle-hud/damage-display-source-manifest.json':
            data['generator_sha256']=sha(ROOT/'scripts/prepare_battle_damage_display.py')
            data['remaining']='Status-specific display distinctions and complete per-event timing remain incomplete. See damage-visibility-source-manifest.json for restored zero/hide filters and element-feedback-source-manifest.json for element labels and animations.'
        if name=='battle-hud/element-feedback-source-manifest.json':
            data['generator_sha256']=sha(ROOT/'scripts/prepare_battle_element_feedback.py')
            data['remaining']='Floating-text kinds, merge/source rules, timing dependencies, audio and general TMPEffect branches remain incomplete. Original zero/hide filters are recorded separately in damage-visibility-source-manifest.json.'
        path.write_bytes((json.dumps(data,ensure_ascii=(name in ['battle-hud/element-feedback-source-manifest.json','battle-hud/world-damage-source-manifest.json']),indent=2)+'\n').encode())


def build():
    apk, schemas = Apk(manifest='854_156383'), ScriptSchemas()
    damage, roles, inputs, scanned = {}, [], {}, []

    def record(obj):
        key = obj.assets_file.name+':'+str(obj.path_id)
        inputs[key] = dict(apk.provenance(obj), raw_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())

    bundles = [b for b in apk.index if re.search(
        r'_fight(?:skill|monster|npc|character|pet)_[^/]+\.bundle$', b)]
    for index, bundle in enumerate(bundles):
        objects = apk.objects(bundle)
        scanned.append(dict(bundle=bundle, bundle_hash=apk.index[bundle]))
        root = None
        role = re.search(r'_fight(monster|npc|character|pet)_([^/]+)\.bundle$', bundle)
        if role:
            roots = [p.deref() for path,p in apk.container(bundle)
                     if path.endswith('.prefab') and p.type.name == 'GameObject']
            if len(roots) != 1:
                raise ValueError('Expected one original role prefab: '+bundle)
            root = roots[0]
            record(root)
            filters = []
        for obj in objects:
            if obj.type.name != 'MonoBehaviour':
                continue
            native = obj.read(check_read=False)
            script = native.m_Script.deref()
            kind = script.read().m_ClassName
            if kind not in ('MyFightDamage', 'CustomDamageUITypeFilter'):
                continue
            cfg = schemas.read(obj)
            record(obj); record(script)
            owner = native.m_GameObject.deref()
            record(owner)
            if kind == 'MyFightDamage':
                identity = owner.read().m_Name
                if not identity.isdigit():
                    raise ValueError('Damage prefab lost numeric identity: '+identity)
                row = dict(damage_entity_id=int(identity), hide=bool(cfg['myHideDamage']),
                           show_zero=bool(cfg['myShowZeroDamage']))
                if int(identity) in damage and damage[int(identity)] != row:
                    raise ValueError('Conflicting damage display flags: '+identity)
                damage[int(identity)] = row
            elif role:
                if owner.path_id != root.path_id or owner.assets_file.name != root.assets_file.name:
                    raise ValueError('Filter is not on role root; binding needs investigation: '+bundle)
                filters.append({field: bool(cfg[field]) for field in FILTER_FIELDS})
            else:
                raise ValueError('Filter outside role prefab: '+bundle)
        if role:
            if len(filters) > 1:
                raise ValueError('Multiple role filters require original component order: '+bundle)
            roles.append(dict(kind=role[1], entity=role[2], has_filter=bool(filters),
                              filter=filters[0] if filters else None))
        if index % 250 == 0:
            print('Original damage visibility', index, '/', len(bundles), flush=True)
    references = set()
    configs = []
    for name in ('battle-rules', 'skill-rules'):
        path = ROOT/('unity/Assets/Official/Combat/'+name+'.json')
        for components in json.loads(path.read_bytes())['entities'].values():
            for kind, cfg in components.items():
                if kind.startswith('FightHit') and isinstance(cfg, dict) and cfg.get('DamageId', 0)>0:
                    references.add(cfg['DamageId'])
        configs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path)))
    if references-set(damage):
        raise ValueError('Referenced original damage display flags are missing: '+str(sorted(references-set(damage))))
    payload = dict(manifest='854_156383', damage=sorted(damage.values(), key=lambda r:r['damage_entity_id']),
                   roles=roles)
    save(ROOT/OUTPUT, payload)
    official = []
    for version, directory in [('854_156383', ROOT/'.tools/source-packages/official-code'),
        ('871_156598', ROOT/'.tools/source-packages/current/871_156598/official-code')]:
        official.append(dict(manifest=version, files=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p))
            for p in [directory/'client-source'/name for name in (
                'MyFightDamage.cs', 'CharacterDamageUIManager.cs', 'CustomDamageUITypeFilter.cs')]]))
    receipt = dict(manifest='854_156383', official_code=official, scanned=scanned,
        inputs=list(inputs.values()), output_path=OUTPUT, output_sha256=sha(ROOT/OUTPUT),
        generator_sha256=sha(__file__), damage_count=len(damage), role_count=len(roles),
        hidden_count=sum(r['hide'] for r in damage.values()),
        suppress_zero_count=sum(not r['show_zero'] for r in damage.values()),
        config_inputs=configs, referenced_damage_count=len(references), unbound_references=[],
        filtered_roles=[r for r in roles if r['filter'] is not None],
        operation='Read actual MyFightDamage flags and root CustomDamageUITypeFilter fields from the pinned official native prefabs with strict managed schemas. No filter is inferred from combat stats or character type.')
    save(ROOT/RECEIPT, receipt)
    refresh_runtime()
    print('Extracted',len(damage),'damage flags;',len(roles),'roles; filters=',receipt['filtered_roles'],flush=True)


if __name__ == '__main__':
    if sys.argv[1:]==['--refresh-runtime']:refresh_runtime()
    elif sys.argv[1:]:raise SystemExit('Use no arguments to extract, or --refresh-runtime after code-only changes.')
    else:build()
