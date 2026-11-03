"""Port original read-only talent predicates consumed by partner tip graphs."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_partner_gift_configuration import method, normalize, without_logging
from prepare_partner_input_dialog import metadata


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692': raise ValueError('Review selected talent source version')
    inputs, methods = {}, {}

    def source(relative):
        path = ROOT / '.tools/source-packages/current' / version / 'official-code' / relative
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        return path.read_text('utf-8-sig').replace('\r\n', '\n')

    def selected(relative, signature, diagnostics=0):
        value = method(source(relative), signature)
        methods.setdefault(relative, {})[signature] = hashlib.sha256(value.encode()).hexdigest()
        return without_logging(value, diagnostics) if diagnostics else value

    common = 'common-source/Common/'
    template = ROOT / 'scripts/templates/partner_talent/PartnerTalentRules.cs'
    inputs[template.relative_to(ROOT).as_posix()] = sha(template)
    text = template.read_text('utf8')
    graph_source = source(common + 'NpcTalentTreeNpcMapInfoParser.cs')
    graph = graph_source[graph_source.index('\tpublic class TalentGraph\n'):graph_source.index('\n\tprivate Dictionary<int, TalentGraph>')]
    methods.setdefault(common + 'NpcTalentTreeNpcMapInfoParser.cs', {})['TalentGraph and TalentNodeInfo'] = hashlib.sha256(graph.encode()).hexdigest()
    graph = without_logging(graph.replace('\n\t', '\n').lstrip('\t'), 2)
    graph = graph.replace('public class TalentGraph\n{', 'public class TalentGraph\n{\nprivate readonly PartnerTalentRules configuration;')
    graph = graph.replace('public TalentGraph(int npcId)', 'public TalentGraph(PartnerTalentRules configuration, int npcId)').replace('mNpcId = npcId;', 'this.configuration = configuration;\n mNpcId = npcId;')
    graph = graph.replace('public class TalentNodeInfo\n{', 'public class TalentNodeInfo\n{\ninternal PartnerTalentRules configuration;')
    graph = graph.replace('AddNode(new TalentNodeInfo\n\t\t{', 'AddNode(new TalentNodeInfo\n\t\t{\n configuration = configuration,')
    graph = graph.replace('NpcTalentItemInfoParser itemInfoParser', 'PartnerTalentRules itemInfoParser').replace('public PropType ShowPropType;', 'public int ShowPropType;')
    graph = graph.replace('ICondition.DataHub', 'DataHub')
    text = text.replace('/*GRAPH*/', graph)
    text = text.replace('/*NODE_ENUM*/', source(common + 'ENodeType.cs').split('namespace Common;\n', 1)[1])
    ui = source('client-source/NpcTalentTreeUIUtils.cs')
    enum = re.search(r'\tpublic enum EPointState\n\t\{.*?\n\t\}', ui, re.S)[0]
    text = text.replace('/*POINT_ENUM*/', enum)
    bridges = re.search(r'\tpublic static readonly HashSet<ENodeType> sBridgePoints.*?\n\t};', source(common + 'NpcTalentTreeNpcMapInfo.cs'), re.S)[0]
    text = text.replace('/*BRIDGE_POINTS*/', bridges)
    player = []
    for signature in ['public long GetNpcTalentAllPointCount(int npcClassId)', 'public long GetNpcTalentNeedRemainingTalentPoint(int npcClassId)', 'public IReadOnlyCollection<int> GetNpcTalentTreeLockedNodeIds(int npcClassId)']:
        player.append(selected(common + 'Player.cs', signature))
    player.append(selected(common + 'PlayerItemManagerExt.cs', 'public static bool HasItems(this Player player, Dictionary<int, long> items)').replace('public static bool HasItems(this Player player,', 'public bool HasItems(').replace('player.GetItemAmount', 'GetItemAmount'))
    text = text.replace('/*PLAYER_METHODS*/', '\n'.join(player))
    classes = []
    for name, fields, signatures in [
        ('NoneCondition', '', [('public override bool IsMet(DataHub dataHub)', 0)]),
        ('NpcFriendshipLevelCondition', 'public int NpcClassId, FriendshipLevel; public bool IsReverse;', [('public override bool IsMet(DataHub dataHub)', 0), ('public override (long current, long target) GetProgress(DataHub dataHub)', 0), ('private long GetValue(DataHub dataHub)', 1)]),
        ('NpcFriendshipMaxLevelCondition', 'public int NpcClassId; public bool IsReverse;', [('public override bool IsMet(DataHub dataHub)', 0), ('public override (long current, long target) GetProgress(DataHub dataHub)', 0), ('private bool IsMaxLevel(Player player)', 0), ('private bool GetValue(DataHub dataHub)', 1)]),
        ('NpcTalentTreeNodePreNodeActiveCountCondition', 'public int Count;', [('public override bool IsMet(DataHub dataHub)', 0), ('public override (long current, long target) GetProgress(DataHub dataHub)', 0), ('protected long GetValue(DataHub dataHub)', 3)])]:
        members = [selected(common + name + '.cs', signature, diagnostics) for signature, diagnostics in signatures]
        classes.append('private class ' + name + ' : ICondition {\n' + fields + '\npublic ' + name + '(PartnerTalentRules configuration) : base(configuration) {}\n' + '\n'.join(members) + '\n}')
    text = text.replace('/*CONDITIONS*/', '\n'.join(classes))
    bodies = []
    for signature in ['public bool IsTransformFunctionLimit(Player player, int npcId)', 'public bool IsTalentTreeFunctionLimit(Player player, int npcId)']:
        body = selected(common + 'NpcTalentItemInfoParser.cs', signature).replace('ICondition.DataHub', 'DataHub')
        bodies.append(body)
    for signature in ['private static (long TalentPoint, long TalentExp) TransExpToTalentPoint(long exp)', 'public static long TransformGiftToTalentExp(this Player player, int npcClassId, IReadOnlyDictionary<int, long> gifts)', 'public static (long TalentPoint, long TalentExp) TransformGiftToTalentPoint(this Player player, int npcClassId, IReadOnlyDictionary<int, long> gifts)', 'public static bool IsNpcTalentPointCanActive(this Player player, int npcClassId, int nodeId, out bool isItemNotEnough)']:
        body = selected(common + 'PlayerNpcTalentTreeExt.cs', signature).replace('private static', 'private').replace('public static', 'private').replace('this Player player', 'Player player')
        body = body.replace('Quality quality', 'int quality').replace('<Quality, long>', '<int, long>').replace('IReadOnlyDictionary<Quality, long>', 'IReadOnlyDictionary<int, long>')
        if 'IsNpcTalentPointCanActive(' in body:
            body = body.replace('Parsers.Instance.NpcTalentTreeNpcMapInfoParser.GetTalentGraphByNpcClassId(', 'GetTalentGraphByNpcClassId(')
        bodies.append(body)
    body = selected('client-source/NpcTalentTreeUIUtils.cs', 'public static bool IsGiftCanTransformTalentPoint(Player player, int npcId)')
    bodies.append(body.replace('public static', 'private').replace('ItemType.NpcGift', '328').replace('PlayerItemDataWrap value', 'Gift value'))
    body = selected('client-source/NpcTalentTreeUIUtils.cs', 'public static EPointState GetPointState(NpcTalentTreeNpcMapInfoParser.TalentNodeInfo nodeInfo, IReadOnlyCollection<int> activatedSet)')
    bodies.append(body.replace('public static EPointState GetPointState(NpcTalentTreeNpcMapInfoParser.TalentNodeInfo', 'private EPointState GetPointState(Player player, TalentNodeInfo').replace('MeManager.MainPlayer', 'player'))
    text = text.replace('/*METHODS*/', '\n'.join(bodies))
    # Original static parser slots become this immutable selected configuration.
    replacements = {
        'Parsers.Instance.NpcTalentItemInfoParser.Get(': 'configuration.Get(',
        'Parsers.Instance.NpcTalentTreeNpcMapInfoParser.GetTalentGraphByNpcClassId(': 'configuration.GetTalentGraphByNpcClassId(',
        'Parsers.Instance.NpcFriendshipLevelInfoParser.GetMaxLevel(': 'configuration.maximumFriendshipLevel(',
        'Parsers.Instance.ConditionInfoParser.GetCondition(': 'GetCondition(',
        'Parsers.Instance.ItemInfoParser.Get(': 'giftItems[',
        'Parsers.GameSettings.NpcTalentTreeSetting': 'settings',
        'CollectionExtensions.GetValueOrDefault': 'ValueOrDefault',
    }
    # Item lookup uses an indexer instead of the original parser Get.
    text = text.replace('Parsers.Instance.ItemInfoParser.Get(key).Quality', 'giftItems[key].Quality')
    for old, new in replacements.items(): text = text.replace(old, new)
    text = text.replace('NpcTalentTreeNpcMapInfoParser.TalentGraph', 'TalentGraph')
    if any(token in text for token in ('/*', 'Parsers.', 'GLogger', 'Log.Error', 'MeManager', 'CollectionExtensions.')):
        raise ValueError('Unbound original talent implementation: ' + repr([line for line in text.splitlines() if any(token in line for token in ('/*', 'Parsers.', 'GLogger', 'Log.Error', 'MeManager', 'CollectionExtensions.'))]))
    output = ROOT / 'unity/Assets/Game/Runtime/Official/Partners/PartnerTalentRules.cs'
    output.write_text(normalize('// Original talent predicates; see partner-talent-source-manifest.json.\n' + text), 'utf8', newline='\n')
    meta = metadata(output)
    for path in (meta,): path.write_text('\n'.join(line.rstrip() for line in path.read_text('utf8').splitlines()) + '\n', 'utf8', newline='\n')
    rules = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
    inputs[rules.relative_to(ROOT).as_posix()] = sha(rules)
    tables = json.loads(rules.read_bytes())['tables']
    if len(tables['npc_talent_item']) != 3 or len(tables['npc_talent_tree_npc_map']) != 252: raise ValueError('Review original talent table closure')
    for relative in ('official-source.json', 'assets/reference/partner-gift-configuration-source-manifest.json',
            'unity/Assets/Game/Runtime/Official/Partners/PartnerConditionRules.cs'):
        inputs[relative] = sha(ROOT / relative)
    result = dict(manifest=version, inputs=inputs, methods=methods,
        outputs={path.relative_to(ROOT).as_posix(): sha(path) for path in (output, meta)}, generator_sha256=sha(Path(__file__)),
        validation_helpers={relative: sha(ROOT / relative) for relative in ('scripts/fixtures/partner_talent_validation.cs', 'scripts/prepare_partner_talent_validation.py')},
        operation=['Port original game-owned talent graph, node conditions, point-state and gift transformation predicates without a framework implementation.',
            'Preserve original node/edge construction order, bridge traversal, costs, friendship limits, quantity casts and quotient/remainder.',
            'Replace parser/player slots with explicit selected configuration and live read-only player interfaces. No invented activation or conversion transaction.',
            'Reuse the existing selected partner tables. No new or changed art, prefab, layout, save mutation or full asset snapshot.'])
    receipt = ROOT / 'assets/reference/partner-talent-source-manifest.json'
    receipt.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Ported original talent predicates: 3 NPCs, 252 nodes')


if __name__ == '__main__': main()
