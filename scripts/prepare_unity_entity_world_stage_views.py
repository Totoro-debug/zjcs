"""Copy original world-stage views/providers and their playable handler.

Shared EC state queries and replacement/buffered-event publication are explicit
required host inputs. This module preserves client consumption; it does not
infer shared/server state transitions, operations or rewards.
"""
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_attachment_runtime import CODE, record
from prepare_unity_story_sound_runtime import runtime_meta

NAMESPACE = 'Zjcs.Official.EntityWorldStages'
OUTPUT = ROOT / 'unity/Assets/Game/Runtime/Official/EntityWorldStages'
RECEIPT = ROOT / 'assets/reference/official-map/map14-entity-world-stage-views-source-manifest.json'

BOUNDARIES = '''using System;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World;
namespace Zjcs.Official.EntityWorldStages
{
    // Consumed shared-component state/query surface. A formal local entity
    // must bind the original rules; this abstract type implements no rules.
    public abstract class ECWorldStageStateData
    {
        public abstract int State { get; }
        public abstract OfficialStageStateInfo GetCurStateInfo();
        public abstract int GetStateComponentIndex(StageComponentType type, int subStateIndex = 0);
    }
    public interface IECWorldStageReplacementSource
    {
        void AddReplaceReceiver(IMyEntityReplaceEventReceiver receiver);
        void RemoveReplaceReceiver(IMyEntityReplaceEventReceiver receiver);
    }
}
'''


def build():
    proof = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assemblies = [CODE / 'managed/decoded' / name for name in ('Assembly-CSharp.dll', 'Console.dll')]
    for path in assemblies:
        if sha(path.read_bytes()) != proof['assemblies'][path.name]['decoded_sha256']:
            raise ValueError('Selected original world-stage assembly changed')
    names = ('ECWorldStageView', 'WorldStageStateCfg', 'IECWorldStageStateCfgBaseProvider',
             'ECWorldStageCfgNormalProvider', 'ECWorldStageStateCfgBaseProviderItemData',
             'IECWorldStageInteractionOperation', 'ECWorldStageInteractionOperationItemData',
             'EntityWorldStageUpdateEventMessage', 'IEcEntityBufferEventHandleModule',
             'IEcEntityBufferEventContext', 'ECPlayableWorldStageHandler')
    sources = [CODE / 'client-source' / (name + '.cs') for name in names]
    sources += [CODE / 'client-source/Common/IMyEntityReplaceEventReceiver.cs',
                CODE / 'common-source/Common/StageComponentType.cs']
    legacy = CODE / 'client-source/MyWorldStage.cs'
    outputs = []
    for path in sources:
        text = path.read_text(encoding='utf-8-sig')
        text = re.sub(r'(?m)^using (?:Common|GameFramework|Framework(?:\.[\w.]+)?|Microsoft.Extensions.DependencyInjection);\n', '', text)
        text = re.sub(r'(?m)^namespace Common;\n', '', text)
        text = re.sub(r'(?m)^\s*\[(?:Inject|Rename\([^\n]*\))\]\n', '\n', text)
        text = re.sub(r'\bMyEntity\b', 'ECViewEntityBinding', text)
        text = re.sub(r'\bEcEntity\b', 'ECViewEntityData', text)
        text = re.sub(r'\bWorldStageComponent\b', 'ECWorldStageStateData', text)
        text = re.sub(r'\bMySerializable\b', 'object', text)
        text = re.sub(r'\bStageStateInfo\b', 'OfficialStageStateInfo', text)
        text = text.replace('StateSwitcher<int>', 'WorldStateSwitcher<int>')
        text = text.replace('MyWorldStage.StageStateCfg', 'LegacyWorldStageStateCfg')
        text = text.replace('IMapRootManagerProxy', 'IECViewMapRootLifetime')
        text = text.replace('private IEcEntityBufferEventHandleModule EcEntityBufferEventHandleModule { get; set; }',
                            'public IEcEntityBufferEventHandleModule EcEntityBufferEventHandleModule { get; set; }')
        text = text.replace('mMyEntity.ReplaceEvent += (IMyEntityReplaceEventReceiver)this;',
                            '((IECWorldStageReplacementSource)mMyEntity).AddReplaceReceiver(this);')
        text = text.replace('mMyEntity.ReplaceEvent -= (IMyEntityReplaceEventReceiver)this;',
                            '((IECWorldStageReplacementSource)mMyEntity).RemoveReplaceReceiver(this);')
        text = text.replace('ServiceProviderServiceExtensions.GetService<IECViewMapRootLifetime>(ServiceProvider)',
                            '(IECViewMapRootLifetime)ServiceProvider.GetService(typeof(IECViewMapRootLifetime))')
        declaration = re.search(r'(?m)^(?:\[Serializable\]|public (?:abstract )?(?:class|interface|struct|enum))', text)
        if not declaration:
            raise ValueError('Original world-stage declaration missing: ' + path.name)
        text = ('// Migrated official client source; see entity world-stage receipt.\n'
                'using Object = UnityEngine.Object;\nusing Zjcs.Official.EntityViews;\n'
                'using Zjcs.Official.EntityViewPlayables;\nusing Zjcs.Official.StoryPlayables;\n'
                'using Zjcs.Official.World;\nusing Zjcs.Official.WorldUI;\n' + text[:declaration.start()] +
                'namespace ' + NAMESPACE + '\n{\n' + text[declaration.start():] + '\n}\n')
        target = OUTPUT / path.name; write(target, text.encode()); runtime_meta(target)
        outputs += [target, Path(str(target) + '.meta')]
    raw = legacy.read_text(encoding='utf-8-sig')
    found = re.search(r'\t\[Serializable\]\n\tpublic class StageStateCfg\n\t\{.*?\n\t\}', raw, re.S)
    if not found:
        raise ValueError('Original legacy state configuration declaration missing')
    text = ('// Original MyWorldStage.StageStateCfg declaration only.\nusing System;\nusing UnityEngine;\n'
            'namespace ' + NAMESPACE + '\n{\n' + found[0].replace('class StageStateCfg', 'class LegacyWorldStageStateCfg') + '\n}\n')
    for name, content in (('LegacyWorldStageStateCfg.cs', text), ('ECWorldStageStateData.cs', BOUNDARIES)):
        target = OUTPUT / name; write(target, content.encode()); runtime_meta(target)
        outputs += [target, Path(str(target) + '.meta')]
    asmdef = OUTPUT / (NAMESPACE + '.asmdef')
    write(asmdef, encode(dict(name=NAMESPACE, rootNamespace=NAMESPACE, autoReferenced=True,
          references=['Zjcs.Official.EntityViews', 'Zjcs.Official.EntityViewPlayables',
                      'Zjcs.Official.StoryPlayables', 'Zjcs.Official.World', 'Zjcs.Official.WorldUI'])))
    runtime_meta(asmdef); runtime_meta(OUTPUT)
    outputs += [asmdef, Path(str(asmdef) + '.meta'), Path(str(OUTPUT) + '.meta')]
    write(RECEIPT, encode(dict(manifest='871_156598', mapClassId=14,
        assemblies=[record(p) for p in assemblies], inputs=[record(p) for p in sources + [legacy]],
        tools=[record(Path(__file__)), record(ROOT / 'scripts/prepare_unity_story_sound_runtime.py')],
        dependencies=[record(ROOT / 'unity/Assets/Game/Runtime/Official/World/OfficialStageStateInfo.cs'),
                      record(ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldStateSwitcher.cs')],
        outputs=[record(p) for p in outputs],
        operation='Copy complete original ECWorldStageView, normal configuration provider, operation/provider/event contracts, original StageComponentType values, legacy configuration declaration and ECPlayableWorldStageHandler. Preserve replacement/init/subscription and disposal/unsubscribe/provider-release order, switcher-before-event, configuration lookups, legacy projection, start/loop transition, forced visibility, callback detach-before-stop and map-destroy stop/detach branches. Shared-component state queries and entity replacement/event publication remain required host surfaces; opaque MySerializable event payload is forwarded as object without interpreting or changing it.',
        limitations=['Shared state queries, replacement and buffered-event publication are required host inputs. Concrete interaction operations, original managed-reference entity prefabs, formal map/input/save and Windows Player remain required. Focused Play Mode fixtures do not establish complete project compilation or formal gameplay integration.'])))
    print('Original entity world-stage sources migrated:', len(sources), 'plus legacy declaration.')


if __name__ == '__main__':
    build()
