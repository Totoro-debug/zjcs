"""Copy original legacy entity visibility without inventing shared visibility rules."""
import json
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_attachment_runtime import CODE, record
from prepare_unity_story_sound_runtime import runtime_meta

NS = 'Zjcs.Official.EntityRootVisibility'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/EntityRootVisibility'
RECEIPT = ROOT / 'assets/reference/official-map/map14-entity-root-visibility-source-manifest.json'


def build():
    manifest = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assemblies = [CODE / 'managed/decoded' / name for name in ('Assembly-CSharp.dll', 'Console.dll')]
    for path in assemblies:
        if sha(path.read_bytes()) != manifest['assemblies'][path.name]['decoded_sha256']:
            raise ValueError('Original entity visibility assembly changed')
    dependency = ROOT / 'assets/reference/official-map/map13-entity-visibility-views-source-manifest.json'
    receipt = json.loads(dependency.read_bytes())
    if receipt['manifest'] != '871_156598':
        raise ValueError('Reused original visibility version differs')
    dependencies = []
    for row in receipt['outputs']:
        path = ROOT / row['path']
        if path.suffix == '.cs':
            if sha(path.read_bytes()) != row['sha256']:
                raise ValueError('Reused visibility source changed: ' + str(path))
            dependencies.append(record(path))
    inputs, outputs = [], []
    for name in ('MyVisibility', 'MyVisibilityGrid'):
        path = CODE / 'client-source' / (name + '.cs')
        inputs.append(path)
        text = path.read_text(encoding='utf-8-sig')
        text = re.sub(r'(?m)^using (?:Common|Framework.DependencyInjection);\n', '', text)
        text = text.replace('EcComponentInfoEmpty', 'object')
        text = text.replace(' where T : VisibilityComponent', '').replace(' where TInfo : EcComponentInfo', '')
        text = text.replace('VisibilityGridComponentInfo', 'object').replace('VisibilityGridComponent', 'Map13VisibilityData')
        text = re.sub(r'\bVisibilityComponent\b', 'Map13VisibilityData', text)
        text = re.sub(r'\bMySerializable\b', 'object', text)
        text = text.replace('Component.', '((Map13VisibilityData)(object)Component).')
        text = text.replace('\t[Inject]\n', '').replace('private IEntityClientVisibilityHandlerFactory', 'public IEntityClientVisibilityHandlerFactory')
        imports = ('// Complete original legacy entity visibility; see Map14 source receipt.\n'
                   'using Object = UnityEngine.Object;\n'
                   'using Zjcs.Official.EntityLifecycle;\nusing Zjcs.Official.EntityViews;\n'
                   'using Zjcs.Official.StorySourceFade;\n'
                   'using Zjcs.Official.World.Map13EntityVisibilityViews;\n'
                   'using IMyVisibility = Zjcs.Official.EntityLifecycle.IMyVisibility;\n')
        first = text.index('public class ')
        text = imports + text[:first] + 'namespace ' + NS + '\n{\n' + text[first:] + '\n}\n'
        target = DEST / path.name
        write(target, text.encode('utf-8')); runtime_meta(target)
        outputs += [target, target.with_suffix('.cs.meta')]
    asmdef = DEST / (NS + '.asmdef')
    write(asmdef, encode(dict(name=NS, rootNamespace=NS, autoReferenced=False,
          references=['Zjcs.Official.EntityLifecycle', 'Zjcs.Official.EntityViews',
                      'Zjcs.Official.StorySourceFade', 'Zjcs.Official.StoryPlayables',
                      'Zjcs.Official.World.Map13EntityVisibilityViews',
                      'Zjcs.Official.World.Map13EntityEvents'])))
    runtime_meta(asmdef); runtime_meta(DEST)
    outputs += [asmdef, asmdef.with_suffix('.asmdef.meta'), DEST.with_suffix('.meta')]
    write(RECEIPT, encode(dict(manifest='871_156598', mapClassId=14,
        assemblies=[record(p) for p in assemblies], inputs=[record(p) for p in inputs],
        dependencies=dependencies+[record(dependency),record(ROOT / 'assets/reference/official-map/map14-entity-lifecycle-source-manifest.json')],
        tools=[record(ROOT / 'scripts/prepare_unity_entity_root_visibility.py')], outputs=[record(p) for p in outputs],
        operation='Copy complete original MyVisibility generic classes and MyVisibilityGrid. Reuse same-version original visibility flags, update delegates, handler interfaces/factory and EC view. Bind consumed visibility state through the existing shared-state surface; generic source visibility field access casts that required surface, with no state rules supplied. Preserve EC-view bypass, start flag, fade event gates, clear/create/init/subscribe ordering, all-handler visibility check, source request rebuild, changed-only visibility/fade/event publication and end teardown. Dependency injection becomes explicit host property.',
        limitations=['Formal shared visibility state/handlers, native root prefab/input/save and Player remain required.'])))
    print('Complete original legacy MyVisibility and MyVisibilityGrid migrated.')


if __name__ == '__main__':
    build()
