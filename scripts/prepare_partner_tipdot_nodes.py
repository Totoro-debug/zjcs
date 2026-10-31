"""Port the original partner red-dot graph nodes without global online services."""
import hashlib
import json
from pathlib import Path
import re

from prepare_partner_roster_ui import method
from prepare_partner_ui_helpers import meta

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Roster/TipDots'


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    inputs, outputs = {}, {}
    DEST.mkdir(parents=True, exist_ok=True)
    meta(DEST, folder=True)
    function_path = ROOT / '.tools/source-packages/official-code/common-source/Common/FunctionType.cs'
    function_raw = function_path.read_bytes()
    if function_raw != (ROOT / '.tools/source-packages/current' / version / 'official-code/common-source/Common/FunctionType.cs').read_bytes():
        raise ValueError('Official function enum differs from selected source')
    inputs[function_path.relative_to(ROOT).as_posix()] = hashlib.sha256(function_raw).hexdigest()
    address_list = re.search(r'\bNPCAddressList\s*=\s*(\d+)', function_raw.decode('utf-8-sig'))[1]

    def source(name):
        path = ROOT / '.tools/source-packages/official-code/client-source' / (name + '.cs')
        raw = path.read_bytes()
        selected = ROOT / '.tools/source-packages/current' / version / 'official-code/client-source' / path.name
        if raw != selected.read_bytes():
            raise ValueError('Official node differs from selected source: ' + name)
        inputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(raw).hexdigest()
        return raw.decode('utf-8-sig').replace('\r\n', '\n')

    def write(name, text):
        text = re.sub(r'^using (Common|MessagePack|GFrameCore[\w.]*|XNode);\n', '', text, flags=re.M)
        text = re.sub(r'^\s*\[(?:IgnoreMember|DisplayNodeJump|MenuPath|CreateNodeMenu|NodeWidth)[^\n]*\n', '', text, flags=re.M)
        text = text.replace(', IFillable<TipDotNodeBase>', '')
        text = text.replace('CloneableHelper.ThrowException(source == null, GetType().Name);',
                            'throw new System.ArgumentException(GetType().Name, nameof(source));')
        text = text.replace('CloneableHelper.Fill<TipDotGraphNodeSelector, TipDotGraphNodeSelector>(ref mNodeSelector, obj.mNodeSelector);',
                            'mNodeSelector = obj.mNodeSelector?.Clone();')
        text = text.replace('PlayerItemDataWrap', 'PartnerTipDotItem').replace('AwardSourceData', 'object')
        text = text.replace('PlayerNpcDataWrap', 'PartnerTipDotNpcRecord')
        text = re.sub(r'\bNpcInfo\b', 'PartnerTipDotNpc', text)
        text = re.sub(r'\bNpcProfileInfo\b', 'PartnerTipDotProfile', text)
        text = text.replace('NpcInfoParser npcInfoParser = Parsers.Instance.NpcInfoParser;', '')
        text = text.replace('npcInfoParser.Get(', 'Services.GetNpc(')
        text = text.replace('ICondition', 'PartnerTipDotCondition')
        text = text.replace('MeManager.MainPlayer', 'Services.Player')
        text = text.replace('Parsers.Instance.NpcInfoParser.GetNpcs(NpcType.MapExploreBoss)', 'Services.MapExploreNpcs()')
        text = text.replace('Parsers.Instance.NpcInfoParser.Get(', 'Services.GetNpc(')
        text = text.replace('Parsers.Instance.NpcProfileInfoParser.GetNpcProfileInfos(', 'Services.GetNpcProfiles(')
        text = text.replace('Parsers.Instance.NpcProfileInfoParser.Get(', 'Services.GetNpcProfile(')
        text = text.replace('NpcHelper.GetNpcMeetStatus(', 'Services.GetNpcMeetStatus(')
        text = text.replace('NpcTalentTreeNpcMapInfoParser.TalentGraph', 'PartnerTipDotTalentGraph')
        text = text.replace('NpcTalentTreeNpcMapInfoParser.TalentNodeInfo', 'PartnerTipDotTalentNode')
        text = text.replace('Parsers.Instance.NpcTalentTreeNpcMapInfoParser.GetTalentGraphByNpcClassId(', 'Services.GetTalentGraph(')
        text = text.replace('NpcTalentItemInfo', 'PartnerTipDotTalentItem')
        text = text.replace('Parsers.Instance.PartnerTipDotTalentItemParser.Get(', 'Services.GetTalentItem(')
        text = text.replace('Parsers.Instance.PartnerTipDotTalentItemParser.IsTalentTreeFunctionLimit(', 'Services.IsTalentTreeFunctionLimit(')
        text = text.replace('Parsers.Instance.PartnerTipDotTalentItemParser.IsTransformFunctionLimit(', 'Services.IsTransformFunctionLimit(')
        text = text.replace('NpcTalentTreeUIUtils.GetPointState(', 'Services.GetTalentPointState(')
        text = text.replace('NpcTalentTreeUIUtils.EPointState.CanActive', 'PartnerTipDotTalentPointState.CanActive')
        text = text.replace('NpcTalentTreeUIUtils.IsGiftCanTransformTalentPoint(', 'Services.IsGiftCanTransformTalentPoint(')
        text = text.replace('ConditionHandler.Instance.Register(', 'Services.RegisterCondition(')
        text = text.replace('ConditionHandler.Instance.Deregister(', 'Services.DeregisterCondition(')
        text = text.replace('Player mainPlayer', 'IPartnerTipDotPlayer mainPlayer')
        text = text.replace('FunctionType.NPCAddressList', address_list)
        # Keep each original event subscription, check and clone branch. Services
        # is injected per runtime clone, never serialized into the official graph.
        if 'Services.' in text:
            marker = re.search(r'public (?:abstract )?class \w+[^\n]*\n\{', text)
            declaration = text[marker.start():marker.end()]
            text = text[:marker.start()] + declaration.replace('\n{', ', IPartnerTipDotNode\n{') + text[marker.end():]
            marker = re.search(r'public (?:abstract )?class \w+[^\n]*\n\{', text)
            text = text[:marker.end()] + '\n\tpublic IPartnerTipDotServices Services { get; set; }\n' + text[marker.end():]
        text = '// Ported from active official client; see partner-tipdot-source-manifest.json.\n' + \
            'namespace Zjcs.Official.PartnersUI\n{\nusing Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.Partners;\n' + text + '\n}\n'
        path = DEST / (name + '.cs')
        path.write_text('\n'.join(line.rstrip() for line in text.split('\n')), 'utf8')
        meta(path)
        outputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()

    write('TipDotOutLinkNode', source('TipDotOutLinkNode'))
    text = source('TipDotDynamicLinkNodeBase')
    text = text.replace('ITipDotNodeManager nodeManager', 'IPartnerDynamicTipDotNodeManager nodeManager')
    text = text.replace('= NodeManager;', '= (IPartnerDynamicTipDotNodeManager)NodeManager;')
    write('TipDotDynamicLinkNodeBase', text)
    text = source('TipDotGraphDynamicLinkNode')
    text = text.replace('AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly assembly) => assembly.GetName().Name == "Assembly-CSharp").GetTypes()',
                        'typeof(TipDotDynamicLinkNodeBase).Assembly.GetTypes()')
    write('TipDotGraphDynamicLinkNode', text)
    text = source('TipDotNodeFactory')
    text = re.sub(r'^using (Framework[\w.]*|GFrameCore[\w.]*|Microsoft[\w.]*|ZLogger);\n', '', text, flags=re.M)
    start = text.index('\t[Inject]')
    end = text.index('\tpublic List<TipDotNodeBase> ProductNodes', start)
    text = text[:start] + '''    private readonly Func<string, TipDotNodeGraph> graphLookup;
    private readonly Action<TipDotNodeBase> injectNode;
    public TipDotNodeFactory(Func<string, TipDotNodeGraph> graphLookup, Action<TipDotNodeBase> injectNode)
    {
        this.graphLookup = graphLookup ?? throw new ArgumentNullException(nameof(graphLookup));
        this.injectNode = injectNode ?? throw new ArgumentNullException(nameof(injectNode));
    }

''' + text[end:]
    text = text.replace(' : ITipDotNodeFactory', '')
    text = method(text, 'public List<TipDotNodeBase> ProductNodes(string graphName, INodeInstanceInfo instanceInfo)',
                  '''public List<TipDotNodeBase> ProductNodes(string graphName, INodeInstanceInfo instanceInfo)
    {
        TipDotNodeGraph nodeGraph = graphLookup(graphName);
        if (nodeGraph == null)
        {
            UnityEngine.Debug.LogError("Original tip-dot graph is not registered: " + graphName);
            return null;
        }
        return ProductNodes(nodeGraph, instanceInfo);
    }''')
    text = text.replace('(Node item)', '(ProfessionGraphAssetNode item)')
    start = text.index('\t\t\t\tGLoggerErrorInterpolatedStringHandler')
    end = text.index('\n\t\t\t}', start)
    text = text[:start] + '\t\t\t\tUnityEngine.Debug.LogError("Clone Node Failed! nodeKey: " + tipDotNodeKey);' + text[end:]
    text = text.replace('item2.Deconstruct(ref tipDotGraphNode, ref tipDotNodeBase2);',
                        'tipDotGraphNode = item2.Key;\n\t\t\ttipDotNodeBase2 = item2.Value;')
    text = text.replace('ServiceProviderExtensions.Inject(ServiceProvider, (object)tipDotNodeBase3);', 'injectNode(tipDotNodeBase3);')
    text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
    text = text.replace('using UnityEngine;', 'using UnityEngine;\nusing Object = UnityEngine.Object;')
    write('TipDotNodeFactory', text)
    for name in ('NpcNodeInstanceInfo', 'NpcProfileNodeInstanceInfo'):
        text = source(name)
        # Debug descriptions do not resolve through a global online player/parser.
        text = method(text, 'public override string GetInstanceDesc()',
                      'public override string GetInstanceDesc() { return ' +
                      ('NpcId.ToString()' if name == 'NpcNodeInstanceInfo' else '$"{NpcId} 档案Id: {NpcProfileId}"') + '; }')
        write(name, text)

    text = source('TipDotLinkNpcProfileNode')
    text = text.replace('NodeManager.ConnectNodes(', '((IPartnerDynamicTipDotNodeManager)NodeManager).ConnectNodes(')
    write('TipDotLinkNpcProfileNode', text)
    for name in ('TipDotNpcMeetStatusConditionNode', 'TipDotNpcProfileCanUnlockConditionNode',
                 'TipDotNpcTalentTreeGiftCanActivePoint', 'TipDotNpcTalentTreeGiftCanTransToTalentPoint'):
        write(name, source(name))
    text = source('TipDotNpcCanSendGiftConditionNode')
    text = text.replace('NpcFriendshipLevelInfoParser npcFriendshipLevelInfoParser = Parsers.Instance.NpcFriendshipLevelInfoParser;',
                        'IPartnerTipDotServices npcFriendshipLevelInfoParser = Services;')
    text = text.replace('NpcGiftInfoParser npcGiftInfoParser = Parsers.Instance.NpcGiftInfoParser;',
                        'IPartnerTipDotServices npcGiftInfoParser = Services;')
    write('TipDotNpcCanSendGiftConditionNode', text)

    for name in ('TipNpcAchievementHasTipConditionNode', 'TipStarGodNpcAchievementHasTipConditionNode'):
        text = source(name)
        text = re.sub(r'^using Framework[^\n]*\n', '', text, flags=re.M)
        text = text.replace('AchievementDataKey>', 'ProfessionTipDotAchievementKey>')
        text = re.sub(r'AchievementDataKey(?:StarGod)?NpcFriendshipLevel (\w+) = new AchievementDataKey(?:StarGod)?NpcFriendshipLevel\s*\{\s*NpcClassId = ([^\n]+)\s*\};',
                      lambda match: 'ProfessionTipDotAchievementKey ' + match[1] + ' = Services.NpcFriendshipKey(' + match[2].strip() + ', ' +
                      ('true' if 'StarGod' in name else 'false') + ');', text)
        text = text.replace('ClonerResolver.Instance.GetClonerWithVerify<List<AchievementTypeAndParamItem>>().Fill(ref AchievementTypeAndParamList, obj.AchievementTypeAndParamList);',
                            'AchievementTypeAndParamList = obj.AchievementTypeAndParamList?.ConvertAll(value => value?.Clone());')
        if 'StarGod' in name:
            text = method(text, 'public override void Init()', '''public override void Init()
    {
        base.Init();
        mAchievementDataKeyDict.Clear();
        if (!(NodeKey.InstanceInfo is NpcNodeInstanceInfo instanceInfo))
        {
            UnityEngine.Debug.LogError("TipStarGodNpcAchievementHasTipConditionNode: NpcNodeInstanceInfo required");
            return;
        }
        var npcInfo = Services.GetNpc(instanceInfo.NpcId);
        if (npcInfo == null)
            UnityEngine.Debug.LogError("TipStarGodNpcAchievementHasTipConditionNode: missing NPC " + instanceInfo.NpcId);
        else if (npcInfo.IsStarGodNpc)
        {
            var key = Services.NpcFriendshipKey(instanceInfo.NpcId, true);
            mAchievementDataKeyDict.Add(instanceInfo.NpcId, key);
            AddDataKey(key);
        }
    }''')
        write(name, text)

    source('NpcTalentTreeUIUtils')
    contract = DEST / 'PartnerTipDotServices.cs'
    receipt = dict(manifest=version, inputs=inputs, outputs=outputs,
                   adapters={contract.relative_to(ROOT).as_posix(): hashlib.sha256(contract.read_bytes()).hexdigest()},
                   generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                   adaptations=['Per-clone player/parser/condition service injection; no global online singleton.',
                                'Reuse existing official profession red-dot core and achievement node.',
                                'Dynamic node manager extension preserves existing consumers.',
                                'Editor type selection uses the migrated assembly; debug instance descriptions use IDs.'])
    path = ROOT / 'assets/reference/partner-tipdot-source-manifest.json'
    path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8')
    print('Ported official partner red-dot nodes:', len(outputs))


if __name__ == '__main__':
    main()
