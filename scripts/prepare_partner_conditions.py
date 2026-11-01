"""Migrate selected official condition bodies used by the current partner roster."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_partner_gift_configuration import method, normalize, without_logging
from validation_workspace import validation_directory


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def build(work):
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    inputs, methods = {}, {}

    def source(relative):
        path = ROOT / '.tools/source-packages/official-code' / relative
        raw = path.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code' / relative).read_bytes():
            raise ValueError('Selected condition source differs: ' + relative)
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        return raw.decode('utf-8-sig').replace('\r\n', '\n')

    def selected(name, signature, diagnostics=0):
        relative = 'common-source/' + name
        body = method(source(relative), signature)
        methods.setdefault(relative, {})[signature] = sha(body.encode())
        return without_logging(body, diagnostics) if diagnostics else body

    template = ROOT / 'scripts/templates/partner_conditions/PartnerConditionRules.cs'
    inputs[template.relative_to(ROOT).as_posix()] = sha(template.read_bytes())
    text = template.read_text('utf8')
    rules_path = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
    inputs[rules_path.relative_to(ROOT).as_posix()] = sha(rules_path.read_bytes())
    tables = json.loads(rules_path.read_bytes())['tables']
    condition_rows = {row['ClassId']: row['Condition'] for row in tables['conditions']}
    needed, condition_types = set(), set()

    def visit(node, visiting=()):
        condition_types.add(node['_type'])
        for class_id in node.get('Ids') or []:
            if class_id in visiting:
                raise ValueError('Cyclic selected partner condition: ' + str(class_id))
            if class_id not in needed:
                needed.add(class_id)
                visit(condition_rows[class_id], visiting + (class_id,))

    for row in tables['npc']:
        for field in ('CanMeetCondition', 'CanShowCondition', 'WaitMeetCondition'):
            visit(row[field])
    for row in tables['function_limit']:
        if row['Type'] == 1011 and row['Key0'] == 0 and row['Key1'] == 0:
            visit(row['Condition'])
    supported = {'NoneCondition', 'AlwaysCondition', 'LevelCondition', 'HasItemCondition', 'MapUnlockCondition',
                 'MapRegionActivedCondition', 'HasManualMeetNpcCondition', 'CompositeAndCondition', 'CompositeOrCondition'}
    if condition_types != supported or len(needed) != 72 or len(tables['npc']) != 148:
        raise ValueError('Selected partner condition closure changed')
    base = source('common-source/Common/ICondition.cs')
    start = base.index('\tpublic class DataHub\n')
    end = base.index('\n\t}\n', start) + len('\n\t}')
    hub = base[start:end]
    methods.setdefault('common-source/Common/ICondition.cs', {})['DataHub'] = sha(hub.encode())
    hub = without_logging(hub.replace('GLoggerWarnInterpolatedStringHandler', 'GLoggerErrorInterpolatedStringHandler')
                          .replace('Log.Warn(', 'Log.Error('), 1)
    text = text.replace('/*DATA_HUB*/', hub)
    for name, marker in [('ConditionDataType', 'CONDITION_DATA_TYPE'), ('ConditionType', 'CONDITION_TYPE'), ('NpcType', 'NPC_TYPE')]:
        enum = source('common-source/Common/' + name + '.cs').split('namespace Common;\n', 1)[1].strip()
        text = text.replace('/*' + marker + '*/', enum)
    classes = []
    fields = {'LevelCondition': ['Level', 'Op'], 'HasItemCondition': ['ClassId', 'Count', 'Op'],
              'MapUnlockCondition': ['MapClassId'], 'MapRegionActivedCondition': ['MapClassId', 'Region'],
              'CompositeCondition': ['Ids']}
    extra = {'LevelCondition': [('private int GetLevel(DataHub dataHub)', 1)],
             'MapRegionActivedCondition': [('private int GetValue(DataHub dataHub)', 1)],
             'HasManualMeetNpcCondition': [('private bool CheckMeet(DataHub hub)', 1)],
             'CompositeCondition': [('public List<ICondition> GetConditions()', 1)]}
    for name in ('NoneCondition', 'AlwaysCondition', 'LevelCondition', 'HasItemCondition', 'MapUnlockCondition',
                 'MapRegionActivedCondition', 'HasManualMeetNpcCondition', 'CompositeCondition', 'CompositeAndCondition', 'CompositeOrCondition'):
        raw = source('common-source/Common/' + name + '.cs')
        parent = 'CompositeCondition' if name in ('CompositeAndCondition', 'CompositeOrCondition') else 'ICondition'
        declaration = 'private ' + ('abstract' if name == 'CompositeCondition' else 'sealed') + ' class ' + name + ' : ' + parent
        declarations = re.findall(r'^\tpublic (?:int|string|List<int>) (\w+)(?: = [^;]+)?;', raw, re.M)
        if declarations != fields.get(name, []):
            raise ValueError('Selected condition fields changed: ' + name)
        members = re.findall(r'^\tpublic (?:int|string|List<int>) \w+(?: = [^;]+)?;', raw, re.M)
        members.append('public ' + name + '(PartnerConditionRules configuration) : base(configuration) { }')
        members += re.findall(r'\tpublic override (?:ConditionType Type|bool IsComposite) => [^;]+;', raw)
        if name != 'CompositeCondition':
            members.append(selected('Common/' + name + '.cs', 'public override bool IsMet(DataHub dataHub)', int(name == 'HasItemCondition')))
        for signature, diagnostics in extra.get(name, []):
            members.append(selected('Common/' + name + '.cs', signature, diagnostics))
        body = '\n\n'.join(members).replace('Parsers.Instance.ConditionInfoParser', 'configuration.conditions').replace('Parsers.Instance.NpcInfoParser', 'configuration.npcs')
        classes.append(declaration + '\n{\n' + body + '\n}')
    text = text.replace('/*CONDITIONS*/', '\n\n'.join(classes))
    text = text.replace('/*GET_CONDITION*/', selected('Common/ConditionInfoParser.cs', 'public ICondition GetCondition(int id)'))
    text = text.replace('/*GET_NPCS*/', selected('Common/NpcInfoParser.cs', 'public IEnumerable<NpcInfo> GetNpcs(NpcType ignoreType)'))
    text = text.replace('/*GET_MEET_ITEM*/', selected('Common/NpcInfo.cs', 'public HasItemCondition GetHasItemCanMeetCondition()'))
    text = text.replace('/*COMPARE*/', selected('Common/Formula.cs', 'public static bool Compare<T>(T lhs, string op, T rhs) where T : IComparable<T>, IEquatable<T>'))
    status = selected('NpcHelper.cs', 'public static NpcMeetStatus GetNpcMeetStatus(Player player, int npcClassId)', 1)
    text = text.replace('/*MEET_STATUS*/', status.replace('public static', 'private').replace('Parsers.Instance.NpcInfoParser', 'npcs'))
    if any(token in text for token in ('/*', 'Parsers.', 'GLogger', 'Log.Error', 'Log.Warn')):
        raise ValueError('Unbound selected partner condition implementation')
    stage = work / 'partner-conditions'
    stage.mkdir(exist_ok=True)
    output = normalize('// Original condition methods; see partner-condition-source-manifest.json.\n' + text).encode()
    target = stage / 'PartnerConditionRules.cs'
    target.write_bytes(output)
    metadata = ('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:partner-current-condition-rules').hexdigest()
        + '\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {fileID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
    (stage / 'PartnerConditionRules.cs.meta').write_bytes(metadata)
    bindings = ['unity/Assets/Game/Runtime/Offline/Progression/' + name for name in (
        'OfficialPartnerAssetCatalog.cs', 'OfficialCharacterAssetCatalog.cs', 'OfficialCharacterCatalog.Partners.cs',
        'OfflineCharacterProgressionService.cs', 'OfflineCharacterProgressionService.Partners.cs')]
    bindings.append('unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerRosterUiServices.cs')
    validation = ['scripts/fixtures/' + name for name in ('partner_condition_validation.cs',
        'partner_roster_transaction_validation.cs', 'partner_roster_native_validation.cs', 'partner_roster_ui_validation.cs')]
    validation.append('scripts/prepare_partner_conditions_validation.py')
    proof = dict(manifest=version, source_sha256=inputs, methods=methods, generator_sha256=sha(Path(__file__).read_bytes()),
                 closure=dict(npc_count=148, condition_ids=sorted(needed), types=sorted(condition_types), collection_function=1011),
                 bindings_sha256={name: sha((ROOT / name).read_bytes()) for name in bindings},
                 validation_sha256={name: sha((ROOT / name).read_bytes()) for name in validation},
                 outputs={target.name: sha(output), target.name + '.meta': sha(metadata)}, adaptations=[
                     'Original selected IsMet/helper/compare/data-hub methods, condition type identities and iteration order.',
                     'Diagnostic logging omitted; return values, null entries, short circuit and default comparison retained.',
                     'Typed local player/configuration ports; no network or server implementations invented.',
                     'Only NPC meet/show/wait and collection-unlock configuration closure is instantiated.',
                     'Current partner metadata/items/levels are scoped separately from other historical character tables.',
                     'Local BaseLevel uses the existing base-level projection bounded by official rank level limits.',
                     'Existing offline meet transaction consumes signal items including tracked units and saves before UI notification.'])
    (work / 'partner-condition-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared selected partner condition bodies:', sum(len(values) for values in methods.values()))
    return proof


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    proof = build(work)
    if args.install:
        prior_path = ROOT / 'assets/reference/partner-condition-source-manifest.json'
        previous = json.loads(prior_path.read_bytes()).get('installed_outputs', {}) if prior_path.is_file() else {}
        installed = {}
        for relative, expected in proof['outputs'].items():
            source = work / 'partner-conditions' / relative
            target = ROOT / 'unity/Assets/Game/Runtime/Official/Partners' / relative
            key = target.relative_to(ROOT).as_posix()
            if target.is_file() and sha(target.read_bytes()) not in (expected, previous.get(key)):
                raise ValueError('Preserve an edited partner condition implementation: ' + key)
            target.write_bytes(source.read_bytes())
            installed[key] = expected
        proof['installed_outputs'] = installed
        prior_path.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__':
    main()
