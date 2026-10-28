"""Migrate the game's AttachmentFollower, adapting its animation data boundary.

Only the game's own client-source class is read, never Spine runtime source.
The native prop solver supplies live bone/slot/attachment values.
"""
import json
import re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_prop_hierarchies import encode, sha, write
from prepare_unity_map13_prop_parts import meta
from prepare_unity_story_playable_runtime import logging_adapter

SOURCE = ROOT / '.tools/source-packages/current/871_156598/official-code/client-source/AttachmentFollower.cs'
OUTPUT = ROOT / 'unity/Assets/Game/Runtime/Official/World/Map13Attachment'
RECEIPT = ROOT / 'assets/reference/official-map/map13-native-attachment-follower-source-manifest.json'


def build():
    text = SOURCE.read_text(encoding='utf-8-sig').replace('\r\n', '\n')
    text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
    text = re.sub(r'^using (?:Framework[^;]*|Spine(?:\.Unity)?);\n', '', text, flags=re.M)
    text, diagnostics = logging_adapter(text)
    text = text.replace(' : MonoBehaviour, ISkeletonFollower', ' : MonoBehaviour')
    text = text.replace('SkeletonGraphic.MeshGenerator.settings.zSpacing', 'SkeletonGraphic.zSpacing')
    text = re.sub(r'Object\.op_Implicit\(\(Object\)\(object\)(\w+)\)', r'(\1 != null)', text)
    # These aliases supply only application-facing data from our solver. The
    # original Graphic field remains null in all 26 selected world bindings.
    aliases = '\n'.join('using '+name+' = Zjcs.Offline.World.NativeMap13Skeletons.'+target+';' for name,target in [
        ('SkeletonRenderer','NativeMap13PropMecanim'),('SkeletonGraphic','NativeMap13PropMecanim'),
        ('Skeleton','NativeMap13ApplicationPose'),('Bone','NativeMap13ApplicationBone'),
        ('Slot','NativeMap13ApplicationSlot'),('Attachment','NativeMap13Attachment')])
    start = text.index('[AddComponentMenu(')
    text = '// Migrated original game application code; see map13-native-attachment-follower source receipt.\nusing Object = UnityEngine.Object;\n'+aliases+'\n'+text[:start]+'namespace Zjcs.Official.World.Map13Attachment\n{\n'+text[start:]+'\n}\n'
    target = OUTPUT / 'AttachmentFollower.cs'; write(target, text.encode());meta(target,'MonoImporter')
    asmdef = OUTPUT / 'Zjcs.Official.World.Map13Attachment.asmdef'
    write(asmdef,encode(dict(name='Zjcs.Official.World.Map13Attachment',rootNamespace='Zjcs.Official.World.Map13Attachment',references=['Zjcs.Offline.World.NativeMap13Skeletons'],autoReferenced=False)))
    meta(asmdef,'AssemblyDefinitionImporter');meta(OUTPUT,folder=True)
    pose = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap13Skeletons/NativeMap13ApplicationPose.cs';meta(pose,'MonoImporter')
    outputs=[target,Path(str(target)+'.meta'),asmdef,Path(str(asmdef)+'.meta'),Path(str(OUTPUT)+'.meta'),pose,Path(str(pose)+'.meta')]
    inputs=[SOURCE,Path(__file__).resolve(),ROOT/'scripts/prepare_unity_story_playable_runtime.py',ROOT/'unity/Assets/Official/World/Map/13/Source/native-prop-hierarchies.json']
    hierarchy=json.loads(inputs[-1].read_bytes())
    selected=[o for o in hierarchy['objects'] if o['type']=='AttachmentFollower']
    for record in selected:
        if record['fields']['SkeletonGraphic']['m_PathID']:
            raise ValueError('World follower unexpectedly references a UI Graphic')
    indexed={o['key']:o for o in hierarchy['objects']}
    nodes=[]
    for root in hierarchy['roots']:
        if not any(o['key'] in root['gameObjects'] for o in hierarchy['objects'] if o['type']=='GameObject' and o['cab'].lower()=='cab-6585e135989a91ebeae613b4264b0589'):continue
        for identity in root['gameObjects']:
            go=indexed[identity]
            tf,=[indexed[go['cab'].lower()+':'+str(r['component']['m_PathID'])] for r in go['fields']['m_Component'] if indexed[go['cab'].lower()+':'+str(r['component']['m_PathID'])]['type']=='Transform']
            parent=tf['fields']['m_Father']['m_PathID']
            nodes.append(dict(key=identity,transformKey=tf['key'],parentTransformKey=go['cab'].lower()+':'+str(parent) if parent else '',name=go['fields']['m_Name'],layer=go['fields']['m_Layer'],active=go['fields']['m_IsActive'],pose=tf['fields']))
    bindings=[]
    for record in selected:
        fields=record['fields']
        bindings.append(dict(key=record['key'],gameObject=record['cab'].lower()+':'+str(fields['m_GameObject']['m_PathID']),renderer=record['cab'].lower()+':'+str(fields['SkeletonRenderer']['m_PathID']) if fields['SkeletonRenderer']['m_PathID'] else '',fields=fields))
    recipe=ROOT/'unity/Assets/Official/World/Map/13/Source/native-attachment-followers.json'
    write(recipe,encode(dict(manifest='871_156598',mapClassId=13,followers=bindings,nodes=nodes)))
    meta(recipe,'TextScriptImporter');outputs += [recipe,Path(str(recipe)+'.meta')]
    write(RECEIPT,encode(dict(manifest='871_156598',mapClassId=13,
        method='Original game AttachmentFollower methods, branches, lifecycle and draw-order cache. Namespace/logging/ILSpy repairs and explicit live native geometry data aliases only. No animation SDK implementation.',
        diagnostics=diagnostics,inputs=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p.read_bytes())) for p in inputs],outputs=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p.read_bytes())) for p in outputs],
        boundary='Only unconstrained native prop skeletons. The original UI SkeletonGraphic field must be null; UI and constrained NPC animation need their own bindings.')))
    print('Original application AttachmentFollower migrated with',diagnostics,'diagnostic adaptations.')


if __name__=='__main__':build()
