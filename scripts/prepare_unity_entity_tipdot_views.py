"""Migrate original map entity tip-dot view, linker and ownership gate."""
import json
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_attachment_runtime import CODE, record
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_story_sound_runtime import runtime_meta
from prepare_unity_entity_transform_views import method

NAMESPACE = 'Zjcs.Official.EntityTipDotViews'
OUTPUT = ROOT / 'unity/Assets/Game/Runtime/Official/EntityTipDotViews'
RECEIPT = ROOT / 'assets/reference/official-map/map14-entity-tipdot-views-source-manifest.json'

BOUNDARIES = '''using System;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.EntityTipDotViews
{
    public abstract class ECEntityTipDotSourceData
    {
        public abstract EntityOwnershipData Ownership { get; }
        public abstract ECViewEntityData Entity { get; }
    }
    public sealed class EntityTipDotExtendInfo
    {
        public bool InteractionUnique;
        public string ReddotGraphName;
    }
    public sealed class EntityTipDotMapInfo { public int MapGroupId; }
    public interface IEntityTipDotQueries
    {
        bool IsLeader { get; }
        long MainPlayerId { get; }
        ECViewEntityData GetEntity(EcEntityIdentifier id);
        int GetContainerClassId(ECViewEntityData entity);
        Pos2d GetPosition(ECViewEntityData entity);
        string GetEntityTag(ECViewEntityData entity);
        EntityTipDotExtendInfo GetExtendInfo(int classId);
        EntityTipDotMapInfo GetMapInfo(int mapClassId);
        string GetReddotGraphName(ECViewEntityData entity, int groupId, int mapClassId, string tag);
    }
    public abstract class EntityTipDotPrefabLease : IDisposable
    {
        public abstract GameObject Result { get; }
        public abstract void Dispose();
    }
    public interface IEntityTipDotPrefabLoader
    {
        EntityTipDotPrefabLease LoadAssetByGuid(string guid);
        GameObject Instantiate(GameObject prefab, Transform parent);
    }
    public static class EntityTipDotServices
    {
        public static IEntityTipDotQueries Queries { get; set; }
        public static IEntityTipDotQueries Require => Queries ??
            throw new InvalidOperationException("Original entity tip-dot queries are not bound.");
    }
}
'''


def build():
    proof = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assemblies = [CODE / 'managed/decoded' / name for name in ('Assembly-CSharp.dll', 'Console.dll')]
    for path in assemblies:
        if sha(path.read_bytes()) != proof['assemblies'][path.name]['decoded_sha256']:
            raise ValueError('Selected original entity tip-dot assembly changed')
    sources = [CODE / 'client-source' / (name + '.cs') for name in
               ('ECTipdotLinkerView', 'MapEntityTipDotNodeLinker', 'MapEntityNodeInstanceInfo')]
    sources += [CODE / 'common-source/Common' / (name + '.cs') for name in ('EntityOwnershipData', 'EntityOwnershipType')]
    ownership = CODE / 'client-source/MapMyEntityUtils.cs'
    units = [(path.name, path.read_text(encoding='utf-8-sig')) for path in sources]
    units += [('EntityTipDotOwnership.cs', 'public static class EntityTipDotOwnership\n{\n' +
               method(ownership.read_text(encoding='utf-8-sig'), 'public static bool EntityShowTipForMe(EntitySourceComponent entitySource)') + '\n}\n')]
    outputs, adaptations = [], []
    for name, text in units:
        text = re.sub(r'(?m)^using (?:Common|MessagePack|Framework(?:\.[\w.]+)?);\n', '', text)
        text = re.sub(r'(?m)^namespace Common;\n', '', text)
        text = re.sub(r'(?m)^\s*\[(?:Inject|MessagePackObject\([^\n]*\)|Key\(\d+\))\]\n', '\n', text)
        text, blocks = logging_adapter(text)
        text = re.sub(r'\bEcEntity\b', 'ECViewEntityData', text)
        text = re.sub(r'\bEntitySourceComponent\b', 'ECEntityTipDotSourceData', text)
        text = re.sub(r'\bEntityExtendParameterInfo\b', 'EntityTipDotExtendInfo', text)
        text = re.sub(r'\bMapInfo\b', 'EntityTipDotMapInfo', text)
        text = re.sub(r'\bMultiLanguageText\b', 'WorldMultiLanguageText', text)
        text = text.replace('StateSwitcher<ETipDotLevelType>', 'WorldStateSwitcher<ETipDotLevelType>')
        text = text.replace('ITipDotSystemController', 'IProfessionTipDotSystemController')
        text = re.sub(r'\bTipDotSystem\b(?= (?:TipDotSystem|tipDotSystem))', 'IProfessionTipDotSystem', text)
        text = text.replace('IResourceLoader', 'IEntityTipDotPrefabLoader')
        text = text.replace('AssetOperationHandle<GameObject>', 'EntityTipDotPrefabLease')
        text = text.replace('ResourceLoader.LoadAssetByGuid<GameObject>(', 'ResourceLoader.LoadAssetByGuid(')
        text = text.replace('UIUtils.Instantiate(', 'ResourceLoader.Instantiate(')
        text = text.replace('MeManager.Instance.IsLeader', 'EntityTipDotServices.Require.IsLeader')
        text = text.replace('MapMyEntityUtils.EntityShowTipForMe(', 'EntityTipDotOwnership.EntityShowTipForMe(')
        text = text.replace('MeManager.MainPlayer.Id', 'EntityTipDotServices.Require.MainPlayerId')
        text = text.replace('Parsers.Instance.EntityExtendParameterInfoParser.Get(', 'EntityTipDotServices.Require.GetExtendInfo(')
        text = text.replace('MeManager.Instance.MapContainer?.GetEntity(', 'EntityTipDotServices.Require.GetEntity(')
        text = text.replace('ecEntity.Container.ClassId', 'EntityTipDotServices.Require.GetContainerClassId(ecEntity)')
        text = text.replace('MapInfoManager.Instance.GetMapInfo(', 'EntityTipDotServices.Require.GetMapInfo(')
        text = text.replace('MapUtils.GetMapEntityReddotGraphName(', 'EntityTipDotServices.Require.GetReddotGraphName(')
        text = text.replace('ecEntity.GetEntityTag()', 'EntityTipDotServices.Require.GetEntityTag(ecEntity)')
        text = text.replace('ent.GridTransform.Pos', 'EntityTipDotServices.Require.GetPosition(ent)')
        text = text.replace(' : MySerializable', '').replace('public override ushort GetOpCode()', 'public ushort GetOpCode()')
        for declaration in ('IEntityTipDotPrefabLoader ResourceLoader', 'IProfessionTipDotSystemController Controller',
                            'IProfessionTipDotSystem TipDotSystem'):
            text = text.replace('private ' + declaration + ' { get; set; }', 'public ' + declaration + ' { get; set; }')
        start = re.search(r'(?m)^public (?:static )?(?:class|enum)', text)
        if not start:
            raise ValueError('Original entity tip-dot declaration missing: ' + name)
        text = ('// Migrated original map tip-dot application code; see entity tip-dot receipt.\n'
                'using Object = UnityEngine.Object;\nusing Zjcs.Official.EntityViews;\n'
                'using Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.WorldUI;\nusing Zjcs.Official.StoryPlayables;\n'
                'using Zjcs.Official.World.NativeTerrain.Common;\n' + text[:start.start()] +
                'namespace ' + NAMESPACE + '\n{\n' + text[start.start():] + '\n}\n')
        target = OUTPUT / name
        write(target, text.encode()); runtime_meta(target)
        outputs += [target, target.with_suffix(target.suffix + '.meta')]
        adaptations.append(dict(output=name, diagnosticBlocks=blocks))
    target = OUTPUT / 'EntityTipDotBindings.cs'
    write(target, BOUNDARIES.encode()); runtime_meta(target)
    outputs += [target, target.with_suffix(target.suffix + '.meta')]
    asmdef = OUTPUT / (NAMESPACE + '.asmdef')
    write(asmdef, encode(dict(name=NAMESPACE, rootNamespace=NAMESPACE, autoReferenced=True,
          references=['Zjcs.Official.EntityViews', 'Zjcs.Official.StoryPlayables', 'Zjcs.Official.ProfessionUI',
                      'Zjcs.Official.WorldUI', 'Zjcs.Official.World.NativeTerrain'])))
    runtime_meta(asmdef); runtime_meta(OUTPUT)
    outputs += [asmdef, asmdef.with_suffix(asmdef.suffix + '.meta'), OUTPUT.with_suffix('.meta')]
    dependencies = [ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI' / name for name in
                    ('BaseNodeInstanceInfo.cs', 'TipDotNodeKey.cs', 'TipDotStateInfo.cs', 'IProfessionTipDotSystem.cs', 'AssetGuidProxy.cs')]
    dependencies += [ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldMultiLanguageText.cs']
    write(RECEIPT, encode(dict(manifest='871_156598', mapClassId=14,
        assemblies=[record(p) for p in assemblies], inputs=[record(p) for p in sources + [ownership]],
        tools=[record(ROOT / 'scripts/prepare_unity_entity_tipdot_views.py'),
               record(ROOT / 'scripts/prepare_unity_entity_transform_views.py'),
               record(ROOT / 'scripts/prepare_unity_story_playable_runtime.py'),
               record(ROOT / 'scripts/prepare_unity_story_sound_runtime.py')],
        dependencies=[record(p) for p in dependencies], outputs=[record(p) for p in outputs], adaptations=adaptations,
        operation='Copy complete original ECTipdotLinkerView, MapEntityTipDotNodeLinker, MapEntityNodeInstanceInfo and ownership data/enum; select complete EntityShowTipForMe ownership gate. Preserve leader/ownership short-circuit order, null/map/graph gates, class-and-position instance identity, link/unlink and show subscriptions, number overflow strictly above 99, lazy prefab load/instantiate/release, original field and callback order. Reuse original ported tip-dot contracts, key/state and localization component. Replace singleton entity/map/config queries and GUID resource instantiation with explicit required hosts; ownership opcode remains 40340.',
        limitations=['Concrete tip-dot graph computation, original map/config/entity query and resource hosts, native entity prefab bindings, formal map/input/save and Windows Player remain required.'])))
    print('Original entity tip-dot view/linker/ownership sources migrated:', len(units))


if __name__ == '__main__':
    build()
