"""Port the exact condition methods reached by selected original conversations."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_partner_gift_configuration import method, without_logging
from validation_workspace import validation_directory


def sha(raw): return hashlib.sha256(raw).hexdigest()


def build(work, install=False):
    if not (work / '.zjcs-validation.json').is_file(): raise ValueError('Owned workspace required')
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    inputs, methods = {}, {}

    def source(name):
        relative = 'common-source/Common/' + name + '.cs'
        path = ROOT / '.tools/source-packages/official-code' / relative
        raw = path.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code' / relative).read_bytes():
            raise ValueError('Selected conversation condition source differs: ' + name)
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        return raw.decode('utf-8-sig').replace('\r\n', '\n')

    def selected(name, signature, diagnostics=0):
        body = method(source(name), signature)
        methods[name + '.' + signature] = sha(body.encode())
        return without_logging(body, diagnostics) if diagnostics else body

    template = ROOT / 'scripts/templates/partner_dialogue/PartnerConversationConditionRules.cs'
    inputs[template.relative_to(ROOT).as_posix()] = sha(template.read_bytes())
    text = template.read_text('utf8')
    specs = {
        'CompositeCondition': [('public List<ICondition> GetConditions()', 1)],
        'CompositeAndCondition': [], 'CompositeOrCondition': [],
        'LogicalNotCondition': [('private ICondition GetCondition()', 1)],
        'NpcFriendshipLevelCondition': [('private long GetValue(DataHub dataHub)', 1)],
        'NpcDialogueEndCondition': [('private int GetValue(DataHub dataHub)', 1)],
        'WorldMapProgressCondition': [('public long GetValue(DataHub dataHub)', 1), ('public int CodeProgress', 0)],
        'StoryCountCondition': [],
        'MapRegionGoddessCondition': [('private bool IsGoddessActive(DataHub dataHub)', 0)]
    }
    classes = []
    for name, extras in specs.items():
        raw = source(name)
        fields = re.findall(r'^\tpublic (?:int|string|bool|List<int>|HashSet<int>) \w+(?: = [^;]+)?;', raw, re.M)
        members = fields + ['public ' + name + '(PartnerConversationConditionRules configuration) : base(configuration) { }']
        if name != 'CompositeCondition':
            for signature in ('public override bool IsMet(DataHub dataHub)',
                'public override (long current, long target) GetProgress(DataHub dataHub)'):
                members.append(selected(name, signature, int(name == 'StoryCountCondition' and 'bool IsMet' in signature)))
        members += [selected(name, signature, count) for signature, count in extras]
        if name in ('WorldMapProgressCondition', 'MapRegionGoddessCondition'):
            members.append('public int CodeRegion => Region;')
        if name == 'WorldMapProgressCondition': members.append('private int _CodeProgress;')
        body = '\n\n'.join(members)
        body = body.replace('Parsers.Instance.ConditionInfoParser.GetCondition', 'configuration.GetCondition')
        body = body.replace('PlayerNpcDialogueDataWrap', 'PartnerConversationRecord')
        if name == 'WorldMapProgressCondition':
            body, count = re.subn(r'num = \(\(CodeRegion >= 0\)[^\n]+;',
                'num = player.CalculateWorldMapProgress(mapClassId, CodeRegion);', body)
            if count != 1: raise ValueError('Inspect original world progress calculation boundary')
        if name == 'MapRegionGoddessCondition':
            original = 'int mapRegionGoddessId = MapRegionEntityInfoManager.Instance.GetMapRegionGoddessId(MapClassId, CodeRegion);'
            if body.count(original) != 1: raise ValueError('Inspect original goddess identity query')
            body = body.replace(original, '').replace(
                'player.IsMapEntityActived(MapClassId, MapWorldActiveableEntityType.Goddess, mapRegionGoddessId)',
                'player.IsRegionGoddessActive(MapClassId, CodeRegion)')
        parent = 'CompositeCondition' if name in ('CompositeAndCondition', 'CompositeOrCondition') else 'ICondition'
        modifier = 'abstract' if name == 'CompositeCondition' else 'sealed'
        classes.append('private ' + modifier + ' class ' + name + ' : ' + parent + '\n{\n' + body + '\n}')
    text = text.replace('/*CONDITIONS*/', '\n\n'.join(classes))
    text = text.replace('/*COMPARE*/', selected('Formula', 'public static bool Compare<T>(T lhs, string op, T rhs) where T : IComparable<T>, IEquatable<T>'))
    text = text.replace('/*INT2FLOAT*/', selected('Formula', 'public static float Int2Float(int val)'))
    if any(token in text for token in ('/*', 'Parsers.', 'GLogger', 'MapRegionEntityInfoManager.', 'Formula.Instance.')):
        raise ValueError('Unadapted original conversation condition boundary')
    tables_path = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
    nodes_path = ROOT / 'unity/Assets/Official/Partners/partner-dialogue-rules.json'
    for path in (tables_path, nodes_path): inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    tables = json.loads(tables_path.read_bytes())['tables']
    nodes = json.loads(nodes_path.read_bytes())['nodes']
    rows = {row['ClassId']: row['Condition'] for row in tables['conditions']}
    needed, types = set(), set()

    def visit(node, ancestors=()):
        if node is None: return
        types.add(node['_type'])
        ids = [node['Id']] if node['_type'] == 'LogicalNotCondition' else node.get('Ids') or []
        for identity in ids:
            if identity in ancestors: raise ValueError('Cyclic original conversation condition')
            if identity not in needed:
                needed.add(identity); visit(rows[identity], ancestors + (identity,))

    for node in nodes:
        visit(node['UnlockCondition']); visit(node['ChainSequenceCondition'])
    supported = (set(specs) - {'CompositeCondition'}) | {'NoneCondition', 'LevelCondition', 'MapRegionActivedCondition'}
    if types != supported or len(needed) != 208: raise ValueError('Review selected conversation condition closure')
    for path in (ROOT / 'unity/Assets/Game/Runtime/Official/Partners/PartnerConditionRules.cs',
        ROOT / 'unity/Assets/Game/Runtime/Official/Partners/PartnerConversationRules.cs'):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    output = ('\n'.join(line.rstrip() for line in text.expandtabs(4).splitlines()) + '\n').encode('utf8')
    target = ROOT / 'unity/Assets/Game/Runtime/Official/Partners/PartnerConversationConditionRules.cs'
    meta = ('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs-partner-conversation-conditions').hexdigest() +
        '\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n'
        '  icon: {fileID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
    receipt = dict(manifest=version, input_sha256=inputs, original_method_sha256=methods,
        closure=dict(condition_ids=sorted(needed), types=sorted(types)), generator_sha256=sha(Path(__file__).read_bytes()),
        installed_output_sha256={target.relative_to(ROOT).as_posix(): sha(output),
            target.relative_to(ROOT).as_posix() + '.meta': sha(meta)},
        adaptations=['Reuse already migrated original None/Level/MapRegionActived leaf conditions.',
            'Original composite, negation, friendship, ended-dialogue, story, goddess and world-progress boolean methods.',
            'Original world-progress float conversion and truncation; its raw map calculation is an explicit actual-map service port.',
            'Goddess ID lookup and activation query use an explicit actual-map service port.',
            'Typed player/configuration ports; diagnostic logs omitted without changing return or short-circuit branches.'])
    receipt['validation_source_sha256'] = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in (
        ROOT / 'scripts/test_partner_conversation_conditions.py',
        ROOT / 'scripts/fixtures/partner_conversation_conditions_harness.cs')}
    proof = work / 'partner-conversation-conditions-source-manifest.json'
    proof.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    (work / target.name).write_bytes(output)
    if install:
        previous_path = ROOT / 'assets/reference' / proof.name
        previous = json.loads(previous_path.read_bytes()) if previous_path.exists() else {}
        planned = [(target, output), (Path(str(target) + '.meta'), meta)]
        for path, raw in planned:
            if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get('installed_output_sha256', {}).get(path.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve changed conversation condition output: ' + str(path))
        for path, raw in planned: path.write_bytes(raw)
        previous_path.write_bytes(proof.read_bytes())
    print('Prepared original conversation conditions:', len(types), 'types;', len(needed), 'referenced IDs', flush=True)
    return receipt


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    build(validation_directory(), parser.parse_args().install)
