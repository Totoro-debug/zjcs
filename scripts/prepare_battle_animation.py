"""Convert original AnimatorController graphs and battle animator playables."""
import hashlib
import json
import re
import struct
import numpy as np
from pathlib import Path
from prepare_map_regions import Regions, ROOT, save, transform as unity_transform
from UnityPy.classes.PPtr import PPtr
from official_battle_spine_component import read_component, player_renderers as read_player_renderers

OUT = ROOT / 'assets/official/battle-animation'


def build():
    e = Regions()
    controllers, skeletons, playables, failures, players = {}, {}, {}, {}, {}
    entities, effects, animation_components, skeleton_animators = {}, {}, {}, {}
    skeleton_renderers, player_renderers, model_bindings, body_attachments = {}, {}, {}, {}

    def model_path(obj):
        go = obj.read(check_read=False).m_GameObject.deref()
        parts = []
        while go:
            parts.insert(0, go.read().m_Name)
            transform = next(c.component.read() for c in go.read().m_Component if c.component.type.name in ['Transform', 'RectTransform'])
            go = transform.m_Father.read().m_GameObject.deref() if transform.m_Father.path_id else None
        return '/' + '/'.join(parts)

    def with_hsv_offset(renderer, part):
        game_object = part.read(check_read=False).m_GameObject.deref()
        for ref in game_object.read(check_read=False).m_Component:
            component = ref.component.deref()
            if component.type.name != 'MonoBehaviour' or e.mono(component)[0] != 'AppearanceRendererHsvOffset':continue
            cfg = e.schemas.read(component);e.input(component)
            renderer['hsv_offset'] = dict(hue=float(cfg.get('Hue',0)),
                saturation=float(cfg.get('Saturation',0.2)),value=float(cfg.get('Value',0.2)))
            break
        return renderer

    def skeleton_binding(part):
        renderer = with_hsv_offset(read_component(part.get_raw_data(), e.mono(part)[0]),part);e.input(part)
        ptr = renderer['skeletonDataAsset']
        if not ptr['m_PathID']: return '', renderer
        data = PPtr(**ptr, assetsfile=part.assets_file).deref()
        return data.assets_file.name + ':' + str(data.path_id) + ':' + renderer['initialSkinName'], renderer

    def clip_data(obj):
        e.input(obj)
        data = obj.read_typetree()
        timing = data.get('m_MuscleClip', {})
        return {'name': data['m_Name'],
                'duration': float(timing.get('m_StopTime', 1)) - float(timing.get('m_StartTime', 0)),
                'loop': bool(timing.get('m_LoopTime', False)), 'sample_rate': float(data.get('m_SampleRate', 30))}

    def animation_component(obj):
        cfg = e.schemas.read(obj)
        e.input(obj)
        animator = pointer(cfg.get('myAnimator'), obj)
        fields = {k: cfg[k] for k in ['InitOnAwake', 'CreateOverrideController', 'NotCreateGraph', 'mResetAnimatorInitedOnDisable']}
        if animator: animation_components[animator.assets_file.name + ':' + str(animator.path_id)] = fields
        return fields

    def pointer(value, owner):
        return PPtr(**value, assetsfile=owner.assets_file).deref() if value and value.get('m_PathID') else None

    def body_attachment(bundle):
        path, pointer = next((path, ptr) for path, ptr in e.apk.container(bundle) if path.endswith('.prefab'))
        root = pointer.deref()
        result = {}

        def visit(obj, parent, parent_path='', transform_chain=()):
            go = obj.read()
            name = go.m_Name.strip()
            current_path = parent_path + '/' + name
            transform_obj = next((part.component.deref() for part in go.m_Component
                                  if part.component.deref().type.name in ['Transform', 'RectTransform']), None)
            if transform_obj is None:
                return
            transform = transform_obj.read()
            world = parent @ unity_transform(transform)
            chain = transform_chain + ((obj, transform_obj),)
            if name == 'body':
                for game_object, local_transform in chain:
                    e.input(game_object)
                    e.input(local_transform)
                result['body'] = dict(path=current_path, matrix=world.tolist())
            for child in transform.m_Children:
                child_obj = child.read().m_GameObject.deref()
                if child_obj.read().m_IsActive:
                    visit(child_obj, world, current_path, chain)

        visit(root, np.eye(4))
        if 'body' not in result:
            raise ValueError('Original AttachAgent body transform missing in ' + path)
        return result

    def controller(obj):
        key = obj.assets_file.name + ':' + str(obj.path_id)
        if key in controllers: return key
        e.input(obj)
        raw = obj.read_typetree()
        if obj.type.name == 'AnimatorOverrideController':
            base = pointer(raw['m_Controller'], obj)
            base_key = controller(base)
            overrides = {}
            for row in raw['m_Clips']:
                original, replacement = pointer(row['m_OriginalClip'], obj), pointer(row['m_OverrideClip'], obj)
                if original and replacement: overrides[original.read().m_Name] = clip_data(replacement)
            controllers[key] = {'base': base_key, 'overrides': overrides, 'source': e.apk.provenance(obj)}
            return key
        clips = []
        for ptr in raw['m_AnimationClips']:
            clip = pointer(ptr, obj)
            if clip is None: clips.append({}); continue
            clips.append(clip_data(clip))
        controllers[key] = {'name': raw['m_Name'], 'graph': raw['m_Controller'],
                            'strings': {str(k): v for k, v in raw['m_TOS']}, 'clips': clips, 'source': e.apk.provenance(obj)}
        return key

    def playable(obj, at=0.0, speed=1.0, chain=()):
        if obj is None: return []
        key = obj.assets_file.name + ':' + str(obj.path_id)
        if key in chain: return []
        chain += (key,)
        e.input(obj)
        if obj.type.name == 'GameObject':
            result = []
            for ptr in obj.read().m_Component:
                part = ptr.component.deref()
                if part.type.name == 'MonoBehaviour' and e.mono(part)[0].startswith('MyPlayable') and e.mono(part)[0] != 'MyPlayableApplyObj':
                    result += playable(part, at, speed, chain)
            return result
        if obj.type.name != 'MonoBehaviour': return []
        kind = e.mono(obj)[0]
        if kind not in ['MyPlayableSequence', 'MyPlayableAnimationControllerPause', 'MyPlayableAnimator', 'MyPlayableOverrideAnimator']: return []
        cfg = e.schemas.read(obj)
        lifecycle = {k: cfg[k] for k in ['myFadeInDuration', 'myFadeOutDuration', 'myEndDestroy', 'DestroyOnRemove']}
        speed *= float(cfg.get('mySpeed', 1))
        if speed <= 0: raise ValueError('Nonpositive original playable speed: ' + key)
        if kind == 'MyPlayableSequence':
            return [v for row in cfg.get('myPlayables', []) for v in playable(pointer(row['myPlayable'], obj), at + float(row['myTime']) / speed, speed, chain)]
        if kind == 'MyPlayableAnimationControllerPause':
            return [{'kind': 'pause', 'time': at, 'speed': speed, 'lifecycle': lifecycle, 'fields': {'myDuration': cfg['Duration'], 'myEndResumeParam': bool(cfg['ResumeOnDestroy'])}, 'source': e.apk.provenance(obj)}]
        if kind == 'MyPlayableAnimator':
            fields = {k: cfg[k] for k in ['myBoolParam', 'myBoolValue', 'myFloatParam', 'myFloatValue', 'IntParam', 'IntValue', 'myTriggerParam', 'PreStateNameAllowList', 'myDuration', 'LayerName', 'LayerWeight', 'myEndResumeParam']}
            return [{'kind': 'animator', 'time': at, 'speed': speed, 'lifecycle': lifecycle, 'fields': fields, 'source': e.apk.provenance(obj)}]
        if kind == 'MyPlayableOverrideAnimator':
            clip = pointer(cfg.get('OverrideClip'), obj)
            if not clip: return []
            e.input(clip)
            # Preserve every authored field apart from object pointers.
            fields = {k: v for k, v in cfg.items() if not isinstance(v, dict) and k not in ['m_Name', 'm_Enabled']}
            return [{'kind': 'override', 'time': at, 'speed': speed, 'animation': clip.read().m_Name, 'clip': clip_data(clip), 'lifecycle': lifecycle, 'fields': fields, 'source': e.apk.provenance(obj)}]
        return []

    bundles = [b for b in e.apk.index if b.startswith('defaultpackage_assets_prefabs_appearance_')]
    for index, bundle in enumerate(bundles):
        try:
            for obj in e.apk.objects(bundle):
                if obj.type.name == 'MonoBehaviour' and e.mono(obj)[0] == 'SkeletonAnimation':
                    skeleton_key, renderer = skeleton_binding(obj)
                    if skeleton_key:
                        model_bindings[model_path(obj)] = dict(skeleton=skeleton_key, renderer=renderer, source=e.apk.provenance(obj))
                if obj.type.name == 'MonoBehaviour' and e.mono(obj)[0] == 'AnimationController':
                    animation_component(obj)
                if obj.type.name != 'Animator': continue
                cfg = obj.read_typetree()
                control = pointer(cfg.get('m_Controller'), obj)
                if not control: continue
                key = controller(control)
                for part in obj.read().m_GameObject.read().m_Component:
                    part = part.component.deref()
                    if part.type.name != 'MonoBehaviour' or e.mono(part)[0] not in ['SkeletonMecanim', 'SkeletonAnimation']: continue
                    raw = part.get_raw_data()
                    renderer = with_hsv_offset(read_component(raw, e.mono(part)[0]),part);e.input(part)
                    file_id, path_id = struct.unpack_from('<iq', raw, 32)
                    if not path_id:
                        player_renderers[key] = renderer
                        continue  # Runtime player skeleton is assigned by appearance.
                    data = PPtr(m_FileID=file_id, m_PathID=path_id, assetsfile=part.assets_file).deref()
                    skin_length = struct.unpack_from('<i', raw, 44)[0]
                    skin = raw[48:48+skin_length].decode('utf8')
                    skeleton_key = data.assets_file.name + ':' + str(data.path_id) + ':' + skin
                    skeletons[skeleton_key] = key
                    skeleton_renderers[skeleton_key] = renderer
                    skeleton_animators[skeleton_key] = obj.assets_file.name + ':' + str(obj.path_id)
                    model_bindings[model_path(part)] = dict(skeleton=skeleton_key, controller=key, animator=obj.assets_file.name + ':' + str(obj.path_id), renderer=renderer, source=e.apk.provenance(part))
        except Exception as error: failures[bundle] = str(error)
        if index % 100 == 0: print('Battle controllers', index, '/', len(bundles), flush=True)
    for path, ptr in e.apk.container('defaultpackage_assets_appearanceassets_character_model_anim.bundle'):
        if ptr.type.name in ['AnimatorController', 'AnimatorOverrideController']:
            players[path] = controller(ptr.deref())
    bundles = [b for b in e.apk.index if re.search(r'_fightskill_\d+\.bundle$', b)]
    for index, bundle in enumerate(bundles):
        try:
            for obj in e.apk.objects(bundle):
                if obj.type.name != 'MonoBehaviour': continue
                kind = e.mono(obj)[0]
                if kind in ['MyFightDamage', 'MyFightStatus'] or kind.startswith('MyFightHit'):
                    cfg = e.schemas.read(obj);e.input(obj)
                    identity = obj.read(check_read=False).m_GameObject.read().m_Name
                    if not identity.isdigit(): raise ValueError('Nested battle entity requires its original numeric GameObject name: ' + identity)
                    rows = {field: playable(pointer(value, obj)) for field, value in cfg.items()
                            if isinstance(value, dict) and value.get('m_PathID') and 'playable' in field.lower()}
                    if kind == 'MyFightDamage': rows['display'] = {k: cfg[k] for k in ['myHideDamage', 'myShowZeroDamage']}
                    if kind == 'MyFightStatus': rows['display'] = {k: cfg[k] for k in ['ShowSpriteAbovePlayer', 'IsIconShowStatusRoundReplaceStack', 'IsIconShowCustomCount']}
                    if any(rows.values()): effects[kind + ':' + identity] = rows
                if kind != 'MyFightSkill': continue
                cfg = e.schemas.read(obj);e.input(obj)
                entity = obj.read(check_read=False).m_GameObject.read().m_Name
                if not entity.isdigit(): entity = re.search(r'_(\d+)\.bundle$', bundle)[1]
                rows = {'default': playable(pointer(cfg.get('myPlayablePrefab'), obj)),
                        'show_replay': bool(cfg['ShowSkillReplay'])}
                for direction, field in enumerate(['UpPlayablePrefab', 'DownPlayablePrefab', 'LeftPlayablePrefab', 'RightPlayablePrefab']):
                    if cfg.get(field, {}).get('m_PathID'): rows[str(direction)] = playable(pointer(cfg[field], obj))
                if any(rows.values()): playables[entity] = rows
        except Exception as error: failures[bundle] = str(error)
        if index % 100 == 0: print('Battle animator playables', index, '/', len(bundles), flush=True)
    output = OUT / 'catalog.json'
    bundles = [b for b in e.apk.index if re.search(r'_fight(?:monster|npc|character|pet)_\d+\.bundle$', b)]
    for index, bundle in enumerate(bundles):
        identity = re.search(r'_(\d+)\.bundle$', bundle)[1]
        try:
            body_attachments[identity] = body_attachment(bundle)
            for obj in e.apk.objects(bundle):
                if obj.type.name == 'Animator':
                    native = obj.read_typetree()
                    control = pointer(native.get('m_Controller'), obj)
                    if control:
                        key = controller(control)
                        for part in obj.read().m_GameObject.read().m_Component:
                            part = part.component.deref()
                            if part.type.name != 'MonoBehaviour' or e.mono(part)[0] != 'SkeletonMecanim': continue
                            raw = part.get_raw_data()
                            renderer = with_hsv_offset(read_component(raw, 'SkeletonMecanim'),part);e.input(part)
                            f, p = struct.unpack_from('<iq', raw, 32)
                            if not p: continue
                            data = PPtr(m_FileID=f, m_PathID=p, assetsfile=part.assets_file).deref()
                            length = struct.unpack_from('<i', raw, 44)[0]
                            skin = raw[48:48+length].decode('utf8')
                            skeleton_key = data.assets_file.name + ':' + str(data.path_id) + ':' + skin
                            skeletons[skeleton_key] = key
                            skeleton_renderers[skeleton_key] = renderer
                            skeleton_animators[skeleton_key] = obj.assets_file.name + ':' + str(obj.path_id)
                    continue
                if obj.type.name != 'MonoBehaviour': continue
                kind = e.mono(obj)[0]
                if kind == 'AnimationController':
                    entities.setdefault(identity, {})['animation_component'] = animation_component(obj)
                    continue
                if kind not in ['MyFightRoleCharacter', 'MyActionAgent', 'MyFightDamage', 'MyFightStatus', 'MyFightHitScope', 'DefaultMoveEffectSupport']: continue
                cfg = e.schemas.read(obj);e.input(obj)
                if kind == 'MyFightRoleCharacter':
                    row = entities.setdefault(identity, {})
                    for field in ['DieOnStartPlayable', 'DiePlayable', 'GeneratePlayable']:
                        row[field] = playable(pointer(cfg.get(field), obj))
                    for field in ['TryDiePlayableOnStart', 'GenerateDelayTime', 'GeneratePlayableNeedDelay']:
                        if field in cfg: row[field] = cfg[field]
                elif kind == 'MyActionAgent':
                    entities.setdefault(identity, {})['actions'] = {
                        str(row['ActionType']): {'start': playable(pointer(row['Playable'], obj)), 'end': playable(pointer(row['EndPlayable'], obj)), 'stop': bool(row['StopOnActionEnd'])}
                        for row in cfg['ActionTypePlayables']}
                elif kind == 'DefaultMoveEffectSupport':
                    entities.setdefault(identity, {})['move'] = playable(pointer(cfg.get('mMoveEffectPlayable'), obj))
                else:
                    rows = {field: playable(pointer(value, obj)) for field, value in cfg.items()
                            if isinstance(value, dict) and value.get('m_PathID') and 'playable' in field.lower()}
                    if any(rows.values()): effects[kind + ':' + identity] = rows
        except Exception as error: failures[bundle] = str(error)
        if index % 500 == 0: print('Entity animator playables', index, '/', len(bundles), flush=True)
    save(output, {'controllers': controllers, 'skeletons': skeletons, 'skills': playables, 'players': players, 'entities': entities, 'effects': effects,
                  'body_attachments': body_attachments,
                  'skeleton_components': {k: animation_components[v] for k, v in skeleton_animators.items() if v in animation_components},
                  'skeleton_renderers': skeleton_renderers, 'player_renderers': player_renderers,
                  'model_bindings': {k: dict(v, component=animation_components.get(v.get('animator'), {})) for k, v in model_bindings.items()},
                  'player_prefabs': read_player_renderers(e.apk,e)})
    source = ROOT / '.tools/source-packages/official-code/client-source'
    names = ['MyPlayableAnimator', 'MyPlayableOverrideAnimator', 'MyPlayableAnimationControllerPause', 'MyPlayableMaterialProperty',
             'MyPlayableFollowSpinePart', 'MyPlayableActiveBone', 'MyPlayableCharacterPartVisibility',
             'MyPlayableModifyPlayerAppearance', 'MyPlayableCameraMove', 'MyPlayableGroundEffect',
             'MyPlayableFightCameraFollowerWeightModifier', 'MyPlayableFightCameraPerformance',
             'MyPlayableAfterimage', 'AfterimageData', 'MyPlayableSpineClone', 'MyPlayableGrapplingLine',
             'MyPlayableFollowBone', 'MyPlayableTrack', 'MyPlayableAttachToBone', 'MyPlayableSpineBoneLookAt',
             'ApplyByBone', 'ApplyOnInit', 'AttachAgent', 'AttachAgentGoType',
             'EFightHitSourceToMeAnchor',
             'MyFightHitBase', 'MyFightHitFixed', 'MyFightHitChained',
             'ApplyByGroundType', 'ApplyOnStopFadeOut', 'ApplyOnStopCheckSourceDie',
             'AttachPosCfg', 'MyEffectFadeOut', 'MyFightDamage',
             'AppearanceRendererHsvOffset', 'AnimationController', 'StateMergeList', 'MyFightSkill', 'MyFightRoleCharacter', 'MyActionAgent']
    code = {n + '.cs': hashlib.sha256((source / (n + '.cs')).read_bytes()).hexdigest() for n in names if (source / (n + '.cs')).exists()}
    for relative in ['MeshMaterialProperty.cs','AppearanceSystem.Runtime/AppearanceMaterialUtils.cs',
                     'PlayableMaterialPropertyProvider.cs','PlayableMaterialBoundingBoxProvider.cs',
                     'PlayableMaterialPropertyStencilProvider.cs']:
        path = source / relative
        if path.exists():code[relative] = hashlib.sha256(path.read_bytes()).hexdigest()
    common_source = ROOT / '.tools/source-packages/official-code/common-source/Common'
    common_code = {name: hashlib.sha256((common_source / name).read_bytes()).hexdigest()
                   for name in ['GridGroundComponentInfo.cs','GroundType.cs','BattleFormulaHandler.cs']
                   if (common_source / name).exists()}
    for name in ['GrapplingLine.cs','GrapplingLineCtrl.cs','GrapplingLineNode.cs',
                 'GrapplingLineSetting.cs','GrapplingLineState.cs','GroundDissolve.cs',
                 'IAfterimageRTParamsProvider.cs','SpineBoneAfterimageRTParamsProvider.cs',
                 'RTRenderer.cs','MeshRendererZSortComparer.cs']:
        path = source / name
        if path.exists():code[name] = hashlib.sha256(path.read_bytes()).hexdigest()
    for path in source.glob('MyPlayable*.cs'):
        code[path.name] = hashlib.sha256(path.read_bytes()).hexdigest()
    for path in source.glob('MyFightHit*.cs'):
        code[path.name] = hashlib.sha256(path.read_bytes()).hexdigest()
    framework = ROOT / '.tools/source-packages/official-code/gameframework-animation-source'
    framework_code = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in framework.glob('*.cs')}
    assembly = ROOT / '.tools/source-packages/official-code/managed/decoded/GameFramework.Runtime.dll'
    if assembly.exists():framework_code['GameFramework.Runtime.dll'] = hashlib.sha256(assembly.read_bytes()).hexdigest()
    save(ROOT / 'assets/reference/battle-animation/source-manifest.json', {
        'manifest': '854_156383', 'inputs': e.source_records, 'schemas': e.schemas.records,
        'code': code, 'common_code': common_code,
        'framework_code': framework_code,
        'framework_symbols': ['GameFramework.MyPlayable.PlayImpl', 'GameFramework.MyPlayableApplyAttribute.GetApplyComponent',
                              'GameFramework.MyPlayableLookAt.PlayImpl', 'GameFramework.MyPlayableLookAt.ProcessFrameImpl',
                              'GameFramework.MyPlayableSequence.InitDataOnPlay', 'GameFramework.MyPlayableSequence.StopImpl'],
        'engine_reference': {'transition_interruption': 'https://docs.unity3d.com/2022.3/Documentation/Manual/class-Transition.html',
                             'scope': 'Original controller transitions all use InterruptionSource=None; AnyState remains eligible while ordinary state transitions wait for a crossfade to finish'},
        'output_sha256': hashlib.sha256(output.read_bytes()).hexdigest(), 'unconverted': failures,
        'operation': 'Original controller constants and internal blend trees, SpineMecanim mix settings, playable sequences, MyFightHitBase SourceToMe anchor and CalcSkillStartPos offsets, chained-hit endpoints, AppearanceRendererHsvOffset base material values, material playable semantics, AttachAgent body transforms, GroundType variant fallback and Slot transforms, ApplyOnStopFadeOut/CheckSourceDie lifecycles, and GrapplingLine Verlet-style links and LineRenderer settings; afterimages, camera effects and ground-collider particles use approved 2D equivalents'})
    print('Converted', len(controllers), 'controllers,', len(skeletons), 'skeleton bindings,', len(playables), 'skills;', len(failures), 'unconverted', flush=True)


if __name__ == '__main__': build()
