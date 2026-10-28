"""Copy the official entity playable-support view and its handler contracts.

Concrete state handlers are migrated with their real entity dependencies;
this module does not infer world-stage or periodic-interaction behavior.
"""
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_attachment_runtime import CODE, record
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_story_sound_runtime import runtime_meta

NAMESPACE = 'Zjcs.Official.EntityViewPlayables'
OUTPUT = ROOT / 'unity/Assets/Game/Runtime/Official/EntityViewPlayables'
RECEIPT = ROOT / 'assets/reference/official-map/map14-entity-playable-views-source-manifest.json'


def build():
    manifest = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assembly = CODE / 'managed/decoded/Assembly-CSharp.dll'
    if sha(assembly.read_bytes()) != manifest['assemblies'][assembly.name]['decoded_sha256']:
        raise ValueError('Selected original application assembly changed')
    names = ('ECPlayableSupporterView', 'IECPlayableSupporter',
             'ECPlayableBaseHandler', 'ECPlayableBaseHandlerData')
    sources = [CODE / 'client-source' / (name + '.cs') for name in names]
    outputs, adaptations = [], []
    for path in sources:
        text = path.read_text(encoding='utf-8-sig')
        text = re.sub(r'(?m)^using (?:Common|GameFramework|Framework(?:\.[\w.]+)?);\n', '', text)
        text = re.sub(r'(?m)^\s*\[Inject\]\n', '\n', text)
        text = text.replace('private IMyPlayableManager MyPlayableManager { get; set; }',
                            'public IMyPlayableManager MyPlayableManager { get; set; }')
        text = text.replace('private IServiceProvider ServiceProvider { get; set; }',
                            'public IServiceProvider ServiceProvider { get; set; }')
        # Map only the entity type. The public property name EcEntity is part
        # of the original handler contract and must remain unchanged.
        text = text.replace('EcEntity EcEntity', 'ECViewEntityData EcEntity')
        text, diagnostics = logging_adapter(text)
        text = re.sub(r'(?m)^\s*//IL_[^\n]*\n', '', text)
        declaration = re.search(r'(?m)^(?:\[Serializable\]|public (?:abstract )?(?:class|interface))', text)
        if not declaration:
            raise ValueError('Original playable view declaration missing: ' + path.name)
        text = ('// Migrated official application source; see entity playable view receipt.\n'
                'using Object = UnityEngine.Object;\n'
                'using Zjcs.Official.EntityViews;\n'
                'using Zjcs.Official.StoryPlayables;\n'
                'using Zjcs.Official.StorySourceFade;\n'
                'using Zjcs.Official.StoryPlayableManager;\n' + text[:declaration.start()] +
                'namespace ' + NAMESPACE + '\n{\n' + text[declaration.start():] + '\n}\n')
        target = OUTPUT / path.name
        write(target, text.encode()); runtime_meta(target)
        outputs += [target, Path(str(target) + '.meta')]
        adaptations.append(dict(source=path.relative_to(CODE).as_posix(), diagnosticBlocks=diagnostics))
    asmdef = OUTPUT / (NAMESPACE + '.asmdef')
    write(asmdef, encode(dict(name=NAMESPACE, rootNamespace=NAMESPACE, autoReferenced=True,
          references=['Zjcs.Official.EntityViews', 'Zjcs.Official.StoryPlayables',
                      'Zjcs.Official.StorySourceFade', 'Zjcs.Official.StoryPlayableManager'])))
    runtime_meta(asmdef); runtime_meta(OUTPUT)
    outputs += [asmdef, Path(str(asmdef) + '.meta'), Path(str(OUTPUT) + '.meta')]
    write(RECEIPT, encode(dict(manifest='871_156598', mapClassId=14,
        assemblies=[record(assembly)], inputs=[record(p) for p in sources],
        dependencies=[record(ROOT / 'assets/reference/official-map' / name) for name in
                      ('map14-entity-view-runtime-source-manifest.json',
                       'map14-story-playable-manager-source-manifest.json',
                       'map14-story-source-fade-runtime-source-manifest.json')],
        tools=[record(Path(__file__)), record(ROOT / 'scripts/prepare_unity_story_playable_runtime.py'),
               record(ROOT / 'scripts/prepare_unity_story_sound_runtime.py')],
        outputs=[record(p) for p in outputs], adaptations=adaptations,
        operation='Copy original entity playable-support view, supporter interface and base handler/data. Preserve fade/visibility gates, source-parent binding, new/static state, appear-to-loop transition, handler traversal/null diagnostics, callback subscription/unsubscription, immediate stop ordering and release. Only namespace, entity type consumption boundary, logging and explicit DI properties are adapted.',
        limitations=['Concrete world-stage/periodic/state handlers, native managed-reference prefab binding, formal entity/map/input/save and Windows Player integration remain required.'])))
    print('Original entity playable view migrated:', len(sources), 'source files.')


if __name__ == '__main__':
    build()
