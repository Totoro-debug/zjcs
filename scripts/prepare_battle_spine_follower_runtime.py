"""Port original application followers over the independent native pose interfaces."""
import json
import re
import struct
from pathlib import Path
from Crypto.Hash import MD4

from official_apk import ROOT
from prepare_battle_status_playable_runtime import CODE, identity, write
from prepare_battle_status_material_runtime import method_fingerprints
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_story_sound_runtime import runtime_meta

OUTPUT = ROOT/'unity/Assets/Game/Runtime/Official/BattleSpinePlayables'
RECEIPT = ROOT/'assets/reference/battle-animation/spine-follower-runtime-source-manifest.json'
NAMES = ('AttachmentFollower', 'SkeletonAttachmentActiveTracker', 'MyPlayableFollowSpinePart')


def change(text, old, new):
    assert old in text, old
    return text.replace(old, new)


def adapt(original, name):
    text, diagnostics = logging_adapter(original)
    text = re.sub(r'(?m)^using (?:GameFramework|Framework[\w.]*|Spine(?:\.Unity)?|NaughtyAttributes);\n', '', text)
    text = re.sub(r'(?m)^\s*\[(?:ShowNonSerializedField|Button\([^\n]*)\]\n', '', text)
    text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
    text = re.sub(r'Object\.op_Implicit\(\(Object\)\(object\)(\w+)\)', r'(\1 != null)', text)
    if name == 'AttachmentFollower':
        text = change(text, ' : MonoBehaviour, ISkeletonFollower', ' : MonoBehaviour')
        text = change(text, 'target.GetComponent<SkeletonRenderer>()', '(Component)target.GetComponent<IProfessionFollowerRenderer>()')
        text = change(text, 'target.GetComponent<SkeletonGraphic>()', '(Component)target.GetComponent<IProfessionFollowerGraphic>()')
        text = change(text, 'val.GetAttachment(SlotName, AttachmentName)', 'val.GetAttachment(mSlot.Index, AttachmentName)')
        text = change(text, 'HandleRebuildGraphic(SkeletonGraphic _)', 'HandleRebuildGraphic(IProfessionFollowerAnimation _)')
        text = change(text, 'HandleRebuildRenderer(SkeletonRenderer skeletonRenderer)', 'HandleRebuildRenderer(IProfessionFollowerAnimation skeletonRenderer)')
        text = change(text, 'SkeletonRenderer.OnRebuild', 'RendererPort.OnAnimationRebuild')
        text = change(text, 'SkeletonGraphic.OnRebuild', 'GraphicPort.OnAnimationRebuild')
        text = change(text, 'SkeletonRenderer.updateWhenInvisible', 'RendererPort.UpdateWhenInvisible')
        text = change(text, 'SkeletonRenderer.UpdateMode', 'RendererPort.UpdateMode')
        text = change(text, 'SkeletonRenderer.Initialize(false, false)', '((IProfessionAppearanceRendererLite)SkeletonRenderer).Initialize(false, false)')
        text = change(text, 'return SkeletonRenderer.valid;', 'return RendererPort.Skeleton != null;')
        text = change(text, 'SkeletonGraphic.Initialize(false)', '((IProfessionAppearanceUIGraphic)SkeletonGraphic).Initialize(false)')
        text = change(text, 'return SkeletonGraphic.IsValid;', 'return GraphicPort.Skeleton != null;')
        text = change(text, 'SkeletonRenderer.Skeleton', 'RendererPort.Skeleton')
        text = change(text, 'SkeletonGraphic.Skeleton', 'GraphicPort.Skeleton')
        text = change(text, 'SkeletonRenderer.zSpacing', 'RendererPort.ZSpacing')
        text = change(text, 'SkeletonGraphic.MeshGenerator.settings.zSpacing', 'GraphicPort.ZSpacing')
        text = change(text, 'mSkeleton.DrawOrder.Items', 'mSkeleton.DrawOrder')
        text = change(text, '\tpublic SkeletonGraphic SkeletonGraphic;', '\tpublic SkeletonGraphic SkeletonGraphic;\n\n'
            '\tprivate IProfessionFollowerRenderer RendererPort => (IProfessionFollowerRenderer)SkeletonRenderer;\n'
            '\tprivate IProfessionFollowerGraphic GraphicPort => (IProfessionFollowerGraphic)SkeletonGraphic;')
    elif name == 'SkeletonAttachmentActiveTracker':
        text = change(text, 'SkeletonRenderer.skeleton', '((IProfessionFollowerRenderer)SkeletonRenderer).Skeleton')
        text = change(text, 'skeleton.GetAttachment(SlotName, AttachmentName)', 'skeleton.GetAttachment(mSlot.Index, AttachmentName)')
        assert 'mSlot.A != 0f' in text
        text = re.sub(r'\bmSlot\.A\b', '((IProfessionFollowerSlotAlpha)mSlot).Alpha', text)
    else:
        text = change(text, 'typeof(SkeletonRenderer)', 'typeof(IProfessionFollowerRenderer)')
        text = change(text, '(SkeletonRenderer)(object)((val is SkeletonRenderer) ? val : null)',
            '(Component)((val is IProfessionFollowerRenderer) ? val : null)')
    assert not any(word in text for word in ('using Spine', 'Spine.Unity.', 'OnRebuild', 'GLogger', 'ShowNonSerializedField', '[Button'))
    start = re.search(r'(?m)^(?:\[|public class)', text).start()
    aliases = ('using SkeletonRenderer = UnityEngine.Component;\nusing SkeletonGraphic = UnityEngine.Component;\n'
        'using Skeleton = Zjcs.Official.ProfessionUI.IProfessionFollowerSkeleton;\n'
        'using Bone = Zjcs.Official.ProfessionUI.IProfessionFollowerBone;\n'
        'using Slot = Zjcs.Official.ProfessionUI.IProfessionFollowerSlot;\n'
        'using Attachment = System.Object;\n')
    text = ('// Migrated current official application source; see spine follower runtime receipt.\n'
        'using Object = UnityEngine.Object;\nusing Zjcs.Official.StoryPlayables;\n'
        'using Zjcs.Official.ProfessionUI;\n'+aliases+text[:start]
        +'namespace Zjcs.Official.BattleSpinePlayables\n{\n'+text[start:]+'\n}\n')
    return text, diagnostics


def build():
    manifest = json.loads((CODE.parent/'code-manifest.json').read_bytes())
    assembly = CODE/'managed/decoded/Assembly-CSharp.dll'
    assert manifest['manifest'] == '884_156692'
    assert identity(assembly)['sha256'] == manifest['assemblies'][assembly.name]['decoded_sha256']
    originals, outputs = [], []
    bone_source = CODE/'client-source/ApplyByBone.cs'
    bone_target = ROOT/'unity/Assets/Game/Runtime/Official/World/Map13TreePlayables/ApplyByBone.cs'
    bone_methods = method_fingerprints(logging_adapter(bone_source.read_text(encoding='utf-8-sig'))[0])
    assert bone_methods == method_fingerprints(bone_target.read_text(encoding='utf-8-sig'))
    enum_source = CODE/'client-source/AttachAgentGoType.cs'
    enum_target = bone_target.parent/'AttachAgentGoType.cs'
    enum_body = lambda p: re.sub(r'\s+', '', re.search(r'public enum AttachAgentGoType\s*\{([^}]+)\}',
        p.read_text(encoding='utf-8-sig'))[1])
    assert enum_body(enum_source) == enum_body(enum_target)
    for name in NAMES:
        source = CODE/'client-source'/(name+'.cs')
        original = source.read_text(encoding='utf-8-sig')
        text, diagnostics = adapt(original, name)
        before, after = method_fingerprints(original), method_fingerprints(text)
        method_name = lambda signature: re.search(r'(\w+)\(', signature)[1]
        assert [method_name(k) for k in before] == [method_name(k) for k in after], name
        target = OUTPUT/(name+'.cs');write(target, text.encode());runtime_meta(target)
        originals.append(dict(original=identity(source), migrated=identity(target),
            original_mono_script_file_id=struct.unpack('<i', MD4.new(('s\0\0\0'+name).encode()).digest()[:4])[0],
            diagnostics_adapted=diagnostics, original_method_bodies=before, adapted_method_bodies=after,
            unchanged_methods=[method_name(k) for k in before if k in after and before[k] == after[k]]))
        outputs += [target, Path(str(target)+'.meta')]
    asmdef = OUTPUT/'Zjcs.Official.BattleSpinePlayables.asmdef'
    write(asmdef, (json.dumps(dict(name='Zjcs.Official.BattleSpinePlayables', references=[
        'Zjcs.Official.StoryPlayables','Zjcs.Official.ProfessionUI']),indent=2)+'\n').encode())
    runtime_meta(asmdef);runtime_meta(OUTPUT)
    outputs += [asmdef, Path(str(asmdef)+'.meta'), Path(str(OUTPUT)+'.meta')]
    write(RECEIPT, (json.dumps(dict(manifest=manifest['manifest'],
        code_extraction=identity(CODE.parent/'code-manifest.json'), assembly=identity(assembly),
        originals=originals, generator=identity(Path(__file__)), outputs=[identity(p) for p in outputs],
        reused_apply_by_bone=dict(original=identity(bone_source), migrated=identity(bone_target),
            verified_current_methods=bone_methods, adaptation='Diagnostic logging only; all six current method bodies compared after the logging adapter.'),
        reused_attachment_origin_enum=dict(original=identity(enum_source), migrated=identity(enum_target),
            verified_values=enum_body(enum_source)),
        verification_tools=[identity(ROOT/p) for p in (
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.SpineFollowers.cs',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.ScenePlayerAppearance.cs',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/Zjcs.Editor.Combat.AnimatorPlayback.asmdef')],
        reused_dependencies=[identity(ROOT/p) for p in (
            'unity/Assets/Game/Runtime/Official/ProfessionUI/ProfessionFollowerBindings.cs',
            'unity/Assets/Game/Runtime/Official/ProfessionUI/BaseSkeletonFollower.cs',
            'unity/Assets/Game/Runtime/Official/ProfessionUI/SkeletonRendererFollower.cs',
            'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeAppearanceSkeleton.cs')],
        adaptation='Original application classes, serialized field names, method order and branches retained. SDK data types use existing independent native follower interfaces; renderer/graphic fields remain serialized Components. Rebuild events, initialization, validity (an initialized native pose exists), attachment lookup by slot index, draw-order arrays and live slot alpha cross explicit native boundaries. No animation SDK code is copied.',
        limits=['Runtime components require native renderer/graphic endpoints with the declared initialization interfaces. Native graph bindings and actual actor integration require separate verification.']),
        ensure_ascii=False,indent=2)+'\n').encode())
    print('Original spine application followers migrated:',len(originals),'methods:',sum(len(x['original_method_bodies']) for x in originals))


if __name__ == '__main__':build()
