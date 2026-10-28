"""Restore the remaining original application code used by tree performances."""
from pathlib import Path
import json
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_attachment_runtime import CODE, record, method
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_map13_prop_parts import meta

NS = 'Zjcs.Official.World.Map13TreePlayables'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/World/Map13TreePlayables'
RECEIPT = ROOT / 'assets/reference/official-map/map13-tree-playable-runtime-source-manifest.json'


def build():
    selected = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assemblies = [CODE / 'managed/decoded' / name for name in ['Assembly-CSharp.dll', 'GameFramework.Runtime.dll']]
    for assembly in assemblies:
        assert sha(assembly.read_bytes()) == selected['assemblies'][assembly.name]['decoded_sha256']
    inputs, outputs = [], []

    def emit(name, text, imports=''):
        text = re.sub(r'(?m)^using (?:Common|GameFramework|Framework(?:\.[\w.]+)?);\n', '', text)
        text = text.replace('namespace GameFramework;', '')
        start = re.search(r'(?m)^(?:\[RequireComponent|\[MyPlayableApply|public )', text).start()
        text = ('// Migrated original tree-playable application source; see source receipt.\n' + imports
                + text[:start] + '\nnamespace ' + NS + '\n{\n' + text[start:] + '\n}\n')
        p = DEST / (name + '.cs'); write(p, ('\n'.join(l.rstrip() for l in text.splitlines()) + '\n').encode())
        meta(p, 'MonoImporter'); outputs.extend([p, Path(str(p) + '.meta')])

    imports = ('using Object = UnityEngine.Object;\nusing Zjcs.Official.StoryPlayables;\n'
               'using Zjcs.Official.StoryAttachment;\nusing Zjcs.Official.StoryAnimation;\n'
               'using Zjcs.Official.StoryFollowBone;\n')
    for name in ['ApplyAnimatorTriggerByAwardItemCount', 'ApplyByBone', 'AttachAgentGoType']:
        p = CODE / 'client-source' / (name + '.cs'); inputs.append(p)
        text, _ = logging_adapter(p.read_text(encoding='utf-8-sig'))
        text = re.sub(r'\bMyStageResultAwardItem\b', 'Map13AwardCountBinding', text)
        emit(name, text, imports)
    p = CODE / 'gameframework-source/GameFramework/MyPlayableMaterialPropertyBlock.cs'; inputs.append(p)
    emit('MyPlayableMaterialPropertyBlock', p.read_text(encoding='utf-8-sig'), imports)

    p = CODE / 'client-source/MyStageResultAwardItem.cs'; inputs.append(p)
    query = method(p.read_text(encoding='utf-8-sig'), 'public long GetStatusItemCount(')
    query = query.replace('Component.GetComponent<WorldStageComponent>()', 'Stage')
    query = query.replace('Component.TryGetAwardItemInfo', 'Awards.TryGetAwardItemInfo')
    emit('Map13AwardCountBinding', '''using UnityEngine;
using Zjcs.Official.World.Map13EntityStateViews;
// Original status-count query only. No icon/resource loading, award execution
// or state transition is provided by this explicit local-data binding.
public sealed class Map13AwardCountBinding : MonoBehaviour
{
    public Map13WorldStageQuery Stage;
    public Map13AwardItemQuery Awards;
    private Map13WorldStageQuery mWorldStageComponent;
''' + query + '\n}\n')
    asm = DEST / (NS + '.asmdef')
    write(asm, encode(dict(name=NS, rootNamespace=NS, references=[
        'Zjcs.Official.StoryPlayables', 'Zjcs.Official.StoryAttachment',
        'Zjcs.Official.StoryAnimation', 'Zjcs.Official.StoryFollowBone',
        'Zjcs.Official.World.Map13EntityStateViews'], autoReferenced=True)))
    meta(asm, 'AssemblyDefinitionImporter'); meta(DEST, folder=True)
    outputs.extend([asm, Path(str(asm) + '.meta'), Path(str(DEST) + '.meta')])
    for name in ['map14-story-playable-runtime-source-manifest.json', 'map14-story-attachment-runtime-source-manifest.json',
                 'map14-story-animator-runtime-source-manifest.json', 'map14-story-follow-bone-runtime-source-manifest.json',
                 'map13-entity-state-views-source-manifest.json']:
        p = RECEIPT.with_name(name)
        if not p.is_file():
            raise ValueError('Verified original runtime receipt is absent: ' + name)
        inputs.append(p)
        for entry in json.loads(p.read_bytes())['outputs']:
            q = ROOT / entry['path']; assert sha(q.read_bytes()) == entry['sha256'], q
            inputs.append(q)
    write(RECEIPT, encode(dict(manifest='871_156598', mapClassId=13,
        method='Copy complete ApplyAnimatorTriggerByAwardItemCount, ApplyByBone, AttachAgentGoType and MyPlayableMaterialPropertyBlock application code. Copy original MyStageResultAwardItem.GetStatusItemCount on an explicit stage/award read binding. Preserve original lifecycle, trigger lookup, source/target/bone choice, property-block curve equations and order; adapt namespaces/diagnostics only.',
        coverage='Source application components only. Original tree performance prefab/particle binding, complete entity lifecycle, map input/save and Player still require integration.',
        assemblies=[record(p) for p in assemblies], inputs=[record(p) for p in sorted(set(inputs))],
        outputs=[record(p) for p in outputs], tools=[record(Path(__file__).resolve())])))
    print('Migrated original tree playable application code:', len(outputs), 'outputs')


if __name__ == '__main__':
    build()
