"""Migrate the original MyWorldStage used by the four Map14 hero statues.

This copies the complete client class, reusing its already migrated nested
configuration declaration. Only consumed MyComponent members are selected from
the original base; shared state, entity visibility and lookup are required inputs.
"""
import json
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_attachment_runtime import CODE, record
from prepare_unity_story_sound_runtime import runtime_meta

NAMESPACE = 'Zjcs.Official.LegacyWorldStages'
OUTPUT = ROOT / 'unity/Assets/Game/Runtime/Official/LegacyWorldStages'
RECEIPT = ROOT / 'assets/reference/official-map/map14-legacy-world-stage-source-manifest.json'

BOUNDARIES = '''using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.StoryPlayables;
namespace Zjcs.Official.LegacyWorldStages
{
    // Consumed original shared-component properties. No state rules are inferred.
    public abstract class LegacyWorldStageComponentData : ECWorldStageStateData
    {
        public abstract LegacyWorldStageComponentData InteractionRedirectStage { get; }
        public abstract ECViewEntityData Entity { get; }
    }
    public interface ILegacyWorldStageVisibilitySource
    {
        IClientVisibilitySupporter ClientVisibilitySupporter { get; }
    }
    public interface ILegacyWorldStageEntityLookup
    {
        ECViewEntityBinding GetEntity(EcEntityIdentifier id);
    }
}
'''


def build():
    proof = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assemblies = [CODE / 'managed/decoded' / name for name in ('Assembly-CSharp.dll', 'Console.dll')]
    for path in assemblies:
        if sha(path.read_bytes()) != proof['assemblies'][path.name]['decoded_sha256']:
            raise ValueError('Selected original legacy world-stage assembly changed')
    source = CODE / 'client-source/MyWorldStage.cs'
    base = CODE / 'client-source/MyComponent.cs'
    raw = source.read_text(encoding='utf-8-sig')
    nested = re.search(r'\t\[Serializable\]\n\tpublic class StageStateCfg\n\t\{.*?\n\t\}', raw, re.S)
    if not nested:
        raise ValueError('Original legacy configuration declaration changed')
    text = raw[:nested.start()] + raw[nested.end():]
    text = re.sub(r'(?m)^using (?:Common|GameFramework|Framework(?:\.[\w.]+)?);\n', '', text)
    text = re.sub(r'(?m)^\s*\[(?:Inject|Rename\([^\n]*\))\]\n', '\n', text)
    text = text.replace('MyComponent<WorldStageComponent, WorldStageComponentInfo>', 'LegacyWorldStageComponent')
    text = re.sub(r'\bStageStateCfg\b', 'LegacyWorldStageStateCfg', text)
    text = re.sub(r'\bStageStateInfo\b', 'OfficialStageStateInfo', text)
    text = re.sub(r'\bMyEntity\b', 'ECViewEntityBinding', text)
    text = re.sub(r'\bMySerializable\b', 'object', text)
    text = text.replace('StateSwitcher<int>', 'WorldStateSwitcher<int>')
    text = text.replace('IMapRootManagerProxy', 'ILegacyWorldStageEntityLookup')
    text = text.replace('myEntity.ClientVisibilitySupporter',
                        '((ILegacyWorldStageVisibilitySource)myEntity).ClientVisibilitySupporter')
    text = text.replace('myEntity.ReplaceEvent += (IMyEntityReplaceEventReceiver)this;',
                        '((IECWorldStageReplacementSource)myEntity).AddReplaceReceiver(this);')
    text = text.replace('myEntity.ReplaceEvent -= (IMyEntityReplaceEventReceiver)this;',
                        '((IECWorldStageReplacementSource)myEntity).RemoveReplaceReceiver(this);')
    text = text.replace('MapRootManagerProxy.MyContainer.GetEntity(', 'MapRootManagerProxy.GetEntity(')
    text = text.replace('Object.op_Implicit((Object)(object)myEntity)', '(bool)(Object)(object)myEntity')
    for name in ('ILegacyWorldStageEntityLookup MapRootManagerProxy', 'IMyPlayableManager MyPlayableManager',
                 'IEcEntityBufferEventHandleModule EcEntityBufferEventHandleModule'):
        text = text.replace('private ' + name + ' { get; set; }', 'public ' + name + ' { get; set; }')
    declaration = text.index('public class MyWorldStage')
    imports = ('using Object = UnityEngine.Object;\nusing Zjcs.Official.EntityViews;\n'
               'using Zjcs.Official.EntityWorldStages;\nusing Zjcs.Official.StoryPlayables;\n'
               'using Zjcs.Official.StorySourceFade;\nusing Zjcs.Official.StoryPlayableManager;\n'
               'using Zjcs.Official.World;\nusing Zjcs.Official.WorldUI;\n')
    text = ('// Complete original MyWorldStage client class; see legacy world-stage receipt.\n' +
            imports + text[:declaration] + 'namespace ' + NAMESPACE + '\n{\n' + text[declaration:] + '\n}\n')

    # These are the exact consumed base declarations, selected before adapting
    # the entity/shared-component types. Unused logger/container/transform APIs
    # are not introduced as substitute implementations.
    base_raw = base.read_text(encoding='utf-8-sig')
    markers = [
        r'\tprotected MyEntity _myEntity;',
        r'\tpublic MyEntity myEntity\n\t\{.*?\n\t\}',
        r'\tpublic virtual void OnStart\(bool newEntity\)\n\t\{\n\t\}',
        r'\tpublic virtual void OnEnd\(MySerializable userData\)\n\t\{\n\t\}',
        r'\tpublic T Component;',
        r'\tprotected virtual void Awake\(\)\n\t\{\n\t\}',
        r'\tpublic override void Bind\(\)\n\t\{.*?\n\t\}',
    ]
    selected = []
    for pattern in markers:
        found = re.search(pattern, base_raw, re.S)
        if not found:
            raise ValueError('Original consumed MyComponent declaration changed: ' + pattern)
        selected.append(found[0])
    base_text = '\n\n'.join(selected)
    base_text = re.sub(r'\bMyEntity\b', 'ECViewEntityBinding', base_text)
    base_text = re.sub(r'\bMySerializable\b', 'object', base_text)
    base_text = re.sub(r'\bT\b', 'LegacyWorldStageComponentData', base_text)
    base_text = base_text.replace('public override void Bind()', 'public virtual void Bind()')
    base_text = ('// Consumed original MyComponent members, selected mechanically.\nusing UnityEngine;\n' + imports +
                 'namespace ' + NAMESPACE + '\n{\npublic abstract class LegacyWorldStageComponent : UnityEngine.MonoBehaviour\n{\n' +
                 base_text + '\n}\n}\n')
    outputs = []
    for name, content in (('MyWorldStage.cs', text), ('LegacyWorldStageComponent.cs', base_text),
                          ('LegacyWorldStageBindings.cs', BOUNDARIES)):
        target = OUTPUT / name
        write(target, content.encode()); runtime_meta(target)
        outputs += [target, target.with_suffix(target.suffix + '.meta')]
    asmdef = OUTPUT / (NAMESPACE + '.asmdef')
    write(asmdef, encode(dict(name=NAMESPACE, rootNamespace=NAMESPACE, autoReferenced=True,
          references=['Zjcs.Official.EntityViews', 'Zjcs.Official.EntityWorldStages',
                      'Zjcs.Official.StoryPlayables', 'Zjcs.Official.StorySourceFade',
                      'Zjcs.Official.StoryPlayableManager',
                      'Zjcs.Official.World', 'Zjcs.Official.WorldUI'])))
    runtime_meta(asmdef); runtime_meta(OUTPUT)
    outputs += [asmdef, asmdef.with_suffix(asmdef.suffix + '.meta'), OUTPUT.with_suffix('.meta')]
    write(RECEIPT, encode(dict(manifest='871_156598', mapClassId=14,
        assemblies=[record(p) for p in assemblies], inputs=[record(source), record(base)],
        tools=[record(ROOT / 'scripts/prepare_unity_legacy_world_stage_runtime.py'),
               record(ROOT / 'scripts/prepare_unity_story_sound_runtime.py')],
        dependencies=[record(ROOT / 'unity/Assets/Game/Runtime/Official/EntityWorldStages/LegacyWorldStageStateCfg.cs'),
                      record(ROOT / 'assets/reference/official-map/map14-entity-world-stage-views-source-manifest.json')],
        outputs=[record(p) for p in outputs],
        operation='Copy the complete original MyWorldStage, reuse its original nested StageStateCfg declaration, and mechanically select its consumed MyComponent members. Preserve Awake/start/end order, EC-view delegation, visibility/fade gates, replacement, state switcher, appearance and state/start/loop callbacks, asynchronous stop behavior, separate uncontrolled loop, state queries, movement interaction flag and redirect lookup. Replace DI with explicit properties and original entity/shared-state access with required host surfaces; do not invent state transitions or interaction rules.',
        limitations=['Four native hero-statue prefabs, formal entity/shared state, interaction rules, map/input/save and Windows Player remain required. This client component alone is not completed statue gameplay.'])))
    print('Complete original legacy MyWorldStage and consumed base members migrated.')


if __name__ == '__main__':
    build()
