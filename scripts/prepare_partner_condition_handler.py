"""Migrate original condition subscription dispatch and selected type projections."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_partner_gift_configuration import method, normalize
from prepare_partner_input_dialog import metadata


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692': raise ValueError('Review selected condition subscription source')
    inputs, methods = {}, {}

    def source(name):
        path = ROOT / '.tools/source-packages/current' / version / 'official-code/common-source/Common' / (name + '.cs')
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        return path.read_text('utf-8-sig').replace('\r\n', '\n')

    def selected(name, signature):
        body = method(source(name), signature)
        methods.setdefault(name, {})[signature] = hashlib.sha256(body.encode()).hexdigest()
        return body

    template = ROOT / 'scripts/templates/partner_conditions/PartnerConditionHandler.cs'
    inputs[template.relative_to(ROOT).as_posix()] = sha(template)
    text = template.read_text('utf8')
    original = source('ConditionHandler')
    excluded = re.search(r'\tpublic readonly HashSet<ConditionType> Excluded =.*?\n\t};', original, re.S)[0]
    fields = [excluded]
    for name in ('mConditionActionMap', 'mIsInvoking', 'mPendingRemoveMap', 'mPendingAddMap'):
        fields.append(re.findall(r'^\tprivate [^\n]*\b' + name + r'[^\n]*;', original, re.M)[0])
    text = text.replace('/*FIELDS*/', '\n\n'.join(fields))
    bodies = []
    signatures = ['private bool IsExcluded(ConditionType conditionType)',
        'public void Register(ICondition condition, Action action)',
        'private void Register(ConditionType conditionType, Action action)',
        'public void Deregister(ICondition condition, Action action)',
        'private void Deregister(ConditionType conditionType, Action action)',
        'private void AddToPendingMap(ConditionType conditionType, Action action, Dictionary<ConditionType, Dictionary<Action, int>> map)',
        'public void DeregisterConditionType(ConditionType conditionType)',
        'private void InvokeConditionAction(ConditionType conditionType)', 'private void PostInvoke()']
    for signature in signatures:
        body = selected('ConditionHandler', signature)
        body = body.replace('Parsers.Instance.ConditionInfoParser', 'new ConditionInfoParser()')
        body = body.replace('mServerHoursConditionHandler?.OnConditionMapChange();', 'onConditionMapChange?.Invoke();')
        if 'InvokeConditionAction(' in signature:
            body, count = re.subn(r'\t{4}GLoggerErrorInterpolatedStringHandler message =.*?\n\t{4}Log.Error\([^\n]*\);',
                '\t\t\t\treportFailure(exception, conditionType);', body, flags=re.S)
            if count != 1: raise ValueError('Review original callback failure boundary')
        bodies.append(body)
    text = text.replace('/*HANDLER_METHODS*/', '\n\n'.join(bodies))
    text = text.replace('/*PARSER_METHODS*/', '\n\n'.join(selected('ConditionInfoParser', signature) for signature in (
        'public void Expand(ICondition condition, List<ICondition> result)', 'public void Distinct(List<ICondition> list)')))
    player_methods = []
    for name in ('MPlayer_OnItemCountUpdate', 'MPlayer_OnNpcFriendshipLevelUpdate', 'MPlayer_OnAddNewNpc',
            'MPlayer_OnSubRankUpdate', 'MPlayer_OnLevelUpdate', 'MPlayer_OnWorldMapProgressUpdate',
            'MPlayer_OnMapStatusUpdate', 'OnWorldMapActived'):
        signature, = re.findall(r'^\t(private void ' + name + r'\([^\n]+)', original, re.M)
        body = selected('ConditionHandler', signature).replace('private void', 'public void', 1)
        for old, new in [('PlayerItemDataWrap', 'object'), ('AwardSourceData', 'object'), ('NpcMeetType', 'int'),
                ('SubRank obj', 'int obj'), ('MapRegionStatus', 'int')]: body = body.replace(old, new)
        player_methods.append(body)
    text = text.replace('/*PLAYER_METHODS*/', '\n\n'.join(player_methods))
    rules = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
    inputs[rules.relative_to(ROOT).as_posix()] = sha(rules)
    tables = json.loads(rules.read_bytes())['tables']
    rows = {row['ClassId']: row['Condition'] for row in tables['conditions']}
    seen, names = set(), set()

    def visit(node):
        if isinstance(node, dict):
            name = node.get('_type', '')
            if name.endswith('Condition'):
                names.add(name)
                for key in node.get('Ids') or []:
                    if key not in seen:
                        seen.add(key)
                        visit(rows[key])
            for child in node.values(): visit(child)
        elif isinstance(node, list):
            for child in node: visit(child)

    for row in tables['npc']:
        for field in ('CanMeetCondition', 'CanShowCondition', 'WaitMeetCondition'): visit(row[field])
    for row in tables['npc_profile']: visit(row['UnlockCondition'])
    talent_ids = {row['UnlockCondition'] for row in tables['npc_talent_tree_npc_map']}
    talent_ids.update(row[field] for row in tables['npc_talent_item']
        for field in ('TalentTreeUnlockConditionId', 'TransformGiftUnlockConditionId'))
    for key in sorted(talent_ids):
        seen.add(key)
        visit(rows[key])
    visit([row['Condition'] for row in tables['function_limit']
        if row['Type'] in (65, 136, 1011) and row['Key0'] == 0 and row['Key1'] == 0])
    type_pairs = {}
    for name in sorted(names):
        raw = source(name)
        type_name, = re.findall(r'public override ConditionType Type => ConditionType\.(\w+);', raw)
        type_pairs[name] = type_name
    text = text.replace('/*CONDITION_TYPES*/', '\n'.join('{ "' + name + '", ConditionType.' + enum + ' },' for name, enum in type_pairs.items()))
    # Reuse the complete selected enum already migrated into the pure partner rules.
    enum = source('ConditionType').split('namespace Common;\n', 1)[1].strip()
    existing = (ROOT / 'unity/Assets/Game/Runtime/Official/Partners/PartnerConditionRules.cs').read_text('utf8')
    if normalize(enum).strip() not in normalize(existing): raise ValueError('Selected condition enum differs')
    if any(token in text for token in ('/*', 'Parsers.', 'GLogger', 'Log.Error', 'mServerHoursConditionHandler')):
        raise ValueError('Unadapted subscription boundary')
    target = ROOT / 'unity/Assets/Game/Runtime/Official/Partners/PartnerConditionHandler.cs'
    target.write_text(normalize('// Original subscription methods; see partner-condition-handler-source-manifest.json.\n' + text), 'utf8', newline='\n')
    meta = metadata(target)
    meta.write_text(normalize(meta.read_text('utf8')), 'utf8', newline='\n')
    local = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerTipDotConditions.cs'
    local_meta = metadata(local)
    local_meta.write_text(normalize(local_meta.read_text('utf8')), 'utf8', newline='\n')
    inputs['unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerTipDotPlayer.cs'] = sha(ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerTipDotPlayer.cs')
    helpers = ['scripts/fixtures/partner_condition_handler_validation.cs', 'scripts/prepare_partner_condition_handler_validation.py']
    receipt = dict(manifest=version, inputs=inputs, methods=methods, generator_sha256=sha(Path(__file__)),
        condition_types=type_pairs, condition_ids=sorted(seen),
        outputs={path.relative_to(ROOT).as_posix(): sha(path) for path in (target, meta, local, local_meta)},
        validation_helpers={name: sha(ROOT / name) for name in helpers},
        operation=['Original type-based subscription reference counts, excluded types, recursive expansion and stable type deduplication.',
            'Original callback snapshot, per-callback failure boundary and pending additions before pending removals.',
            'Original item/NPC/friendship/level/rank/map event dispatch order. Player/config/logging/hour-listener dependencies are explicit ports.',
            'Saved NPC, friendship and item events subscribe once per owner and unsubscribe on disposal; predicates remain in the existing rules.',
            'This subscription boundary does not start conversations, mutate saves, activate talent nodes or poll predicates.',
            'Formal graph/window ownership, other player/map event sources and server-hour scheduling remain pending.'])
    (ROOT / 'assets/reference/partner-condition-handler-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Migrated original condition dispatcher:', len(type_pairs), 'types,', len(seen), 'referenced conditions')


if __name__ == '__main__': main()
