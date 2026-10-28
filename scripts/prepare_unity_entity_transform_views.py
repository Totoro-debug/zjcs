"""Copy original entity Animator, attachment, transform and grid view classes."""
import json
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_attachment_runtime import CODE, record
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_story_sound_runtime import runtime_meta

NAMESPACE = 'Zjcs.Official.EntityTransformViews'
OUTPUT = ROOT / 'unity/Assets/Game/Runtime/Official/EntityTransformViews'
RECEIPT = ROOT / 'assets/reference/official-map/map14-entity-transform-views-source-manifest.json'

BOUNDARIES = '''using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.EntityTransformViews
{
    // Consumed shared state/event/query surfaces; concrete shared grid rules
    // and event publication must be supplied by the original local entity.
    public abstract class ECGridTransformInfo
    {
        public abstract Vector2 RealCenter { get; }
        public abstract List<Pos2d> GetBodyRange();
    }
    public abstract class ECGridTransformData
    {
        public abstract Pos2d Pos { get; }
        public abstract Direction Direction { get; }
        public abstract bool FlipX { get; }
        public abstract ECGridTransformInfo Info { get; }
        public abstract Vector3 GridToPosition(Pos2d grid);
        public abstract Pos2d PositionToGrid(Vector3 pos);
        public abstract void AddPosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver);
        public abstract void RemovePosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver);
    }
    public abstract class ECTransformData
    {
        public abstract Vector3 Position { get; }
        public abstract Quaternion Rotation { get; }
        public abstract Vector3 Scale { get; }
    }
    public abstract class ECViewMoveAgent : MonoBehaviour
    {
        public abstract bool IsMoving { get; }
        public abstract void Stop();
    }
}
'''


def method(source, signature):
    start = source.index(signature)
    opening = source.index('{', start)
    count = 1
    end = opening + 1
    while count:
        count += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


def build():
    proof = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assemblies = [CODE / 'managed/decoded' / name for name in ('Assembly-CSharp.dll', 'Console.dll')]
    for path in assemblies:
        if sha(path.read_bytes()) != proof['assemblies'][path.name]['decoded_sha256']:
            raise ValueError('Selected original entity transform assembly changed')
    sources = [CODE / 'client-source' / (name + '.cs') for name in
               ('ECAnimatorView', 'ECAttachAgentView', 'ECTransformView', 'ECGridTransformView', 'ZSetUtils')]
    sources += [CODE / 'common-source/Common/IGridTransformPosUpdatedEventReceiver.cs']
    enum_source = CODE / 'common-source/Common/EnumUtils.cs'
    outputs = []
    adapted = []
    units = [(path.name, path.read_text(encoding='utf-8-sig')) for path in sources]
    units += [('EntityDirectionRotation.cs', 'using UnityEngine;\npublic static class EntityDirectionRotation\n{\n' +
               method(enum_source.read_text(encoding='utf-8-sig'), 'public static Quaternion DirToRotation(this Direction dir)') + '\n}\n')]
    for name, text in units:
        text = re.sub(r'(?m)^using (?:Common|Framework(?:\.[\w.]+)?);\n', '', text)
        text = re.sub(r'(?m)^namespace Common;\n', '', text)
        text, blocks = logging_adapter(text)
        text = re.sub(r'\bMyEntity\b', 'ECViewEntityBinding', text)
        text = re.sub(r'\bGridTransformComponent\b', 'ECGridTransformData', text)
        text = re.sub(r'\bTransformComponent\b', 'ECTransformData', text)
        text = re.sub(r'\bMoveAgent\b', 'ECViewMoveAgent', text)
        text = text.replace('mGridTransformComponent.PosUpdatedEvent += (IGridTransformPosUpdatedEventReceiver)this;',
                            'mGridTransformComponent.AddPosUpdatedReceiver(this);')
        text = text.replace('mGridTransformComponent.PosUpdatedEvent -= (IGridTransformPosUpdatedEventReceiver)this;',
                            'mGridTransformComponent.RemovePosUpdatedReceiver(this);')
        text = re.sub(r'(?m)^\s*//IL_[^\n]*\n', '', text)
        declaration = re.search(r'(?m)^public (?:static )?(?:class|interface)', text)
        if not declaration:
            raise ValueError('Original entity transform declaration missing: ' + name)
        text = ('// Migrated complete original application class; see entity transform receipt.\n'
                'using Object = UnityEngine.Object;\nusing Zjcs.Official.EntityViews;\n'
                'using Zjcs.Official.World.NativeTerrain.Common;\n'
                'using AttachAgent = Zjcs.Official.StoryAttachment.AttachAgent;\n'
                'using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;\n' +
                text[:declaration.start()] + 'namespace ' + NAMESPACE + '\n{\n' +
                text[declaration.start():] + '\n}\n')
        target = OUTPUT / name
        write(target, text.encode()); runtime_meta(target)
        outputs += [target, target.with_suffix(target.suffix + '.meta')]
        adapted.append(dict(output=name, diagnosticBlocks=blocks))
    target = OUTPUT / 'EntityTransformBindings.cs'
    write(target, BOUNDARIES.encode()); runtime_meta(target)
    outputs += [target, target.with_suffix(target.suffix + '.meta')]
    asmdef = OUTPUT / (NAMESPACE + '.asmdef')
    write(asmdef, encode(dict(name=NAMESPACE, rootNamespace=NAMESPACE, autoReferenced=True,
          references=['Zjcs.Official.EntityViews', 'Zjcs.Official.StoryAttachment',
                      'Zjcs.Official.StoryAnimation', 'Zjcs.Official.World.NativeTerrain'])))
    runtime_meta(asmdef); runtime_meta(OUTPUT)
    outputs += [asmdef, asmdef.with_suffix(asmdef.suffix + '.meta'), OUTPUT.with_suffix('.meta')]
    write(RECEIPT, encode(dict(manifest='871_156598', mapClassId=14,
        assemblies=[record(p) for p in assemblies], inputs=[record(p) for p in sources + [enum_source]],
        tools=[record(ROOT / 'scripts/prepare_unity_entity_transform_views.py'),
               record(ROOT / 'scripts/prepare_unity_story_playable_runtime.py'),
               record(ROOT / 'scripts/prepare_unity_story_sound_runtime.py')],
        dependencies=[record(ROOT / 'unity/Assets/Game/Runtime/Official/World/NativeTerrain/Common/Pos2d.cs'),
                      record(ROOT / 'unity/Assets/Game/Runtime/Official/World/NativeTerrain/Common/Direction.cs'),
                      record(ROOT / 'unity/Assets/Game/Runtime/Official/StoryAnimation/AnimationController.cs'),
                      record(ROOT / 'unity/Assets/Game/Runtime/Official/StoryAttachment/AttachAgent.cs')],
        outputs=[record(p) for p in outputs], adaptations=adapted,
        operation='Copy complete ECAnimatorView, ECAttachAgentView, ECTransformView, ECGridTransformView, ZSetUtils and grid position receiver; select complete original EnumUtils.DirToRotation. Reuse migrated original Pos2d/Direction, Animator controller and attachment registry. Preserve order-zero Animator/attachment initialization, registry clear/add order, parent-entity transforms, position/rotation/scale notifications, grid subscription, moving-agent stop before position and callback, original Z/offset/center formula, nullable horizontal flip, scale magnitude and body-range fallback. Keep the original unassigned optional ECTransformView field rather than inventing discovery. Shared data, conversion queries, movement and event publication remain explicit required host inputs.',
        limitations=['Concrete shared grid/entity rules and publication, native managed-reference display prefabs, formal map/input/save and Windows Player remain required.'])))
    print('Original entity transform/Animator/attachment sources migrated:', len(units))


if __name__ == '__main__':
    build()
