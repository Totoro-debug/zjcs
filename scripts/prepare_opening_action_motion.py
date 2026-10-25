"""Extract the opening record's skill movement prefabs, curves and EC info from APK 854."""
import hashlib
import json
import math
from pathlib import Path

from official_apk import Apk, ROOT
from official_ec_codec import Formats
from official_manifest import read_asset_manifest
from official_unity_schema import ScriptSchemas
from official_shader_programs import gles_programs
from prepare_official_combat_entities import iter_rows
from prepare_character_creation_opening import json_bytes, write_with_meta, REFERENCE
from UnityPy.classes.PPtr import PPtr


def main():
    apk, schemas, formats = Apk(), ScriptSchemas(), Formats()
    identities = (11532, 11833, 15301)
    configs, inputs = {}, {}

    def record(obj):
        inputs[obj.assets_file.name + ':' + str(obj.path_id)] = dict(apk.provenance(obj),
            sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())
        return obj

    def fields(obj):
        return schemas.read(record(obj))

    def pointer(value, owner):
        return record(PPtr(**value, assetsfile=owner.assets_file).deref())

    for _, rows in iter_rows(apk):
        for parent, identity, components in rows:
            if identity not in identities:
                continue
            configs[identity] = {typ.rsplit('.', 1)[-1]: formats.component(typ, data)
                                 for typ, data in components if typ.rsplit('.', 1)[-1]
                                 in ('FightActionSkillMoveComponent', 'ActionComponent')}
    # Keep the exact config_ec container inputs beside the prefab object receipts.
    for _, ptr in apk.container('config_ec'):
        obj = ptr.deref()
        if obj.type.name == 'TextAsset': record(obj)
    assets = [row for name in apk.archive.namelist()
              if name.startswith('assets/yoo/') and name.endswith('_854_156383.v3.bytes')
              for row in read_asset_manifest(apk.archive.read(name))]
    motions = []
    for identity in identities:
        location, = [row for row in assets if row['path'].endswith('/' + str(identity) + '.prefab')]
        obj = next(obj for obj in apk.objects(location['bundle']) if obj.type.name == 'MonoBehaviour'
                   and obj.read(check_read=False).m_Script.read().m_ClassName == 'MyFightActionSkillMove')
        move = fields(obj)
        sequence_go = pointer(move['myPlayablePrefab'], obj).read()
        sequence_obj = next(c.component.deref() for c in sequence_go.m_Component
            if c.component.type.name == 'MonoBehaviour'
            and c.component.deref().read(check_read=False).m_Script.read().m_ClassName == 'MyPlayableSequence')
        sequence = fields(sequence_obj)
        event, = sequence['myPlayables']
        track_obj = pointer(event['myPlayable'], sequence_obj)
        track = fields(track_obj)
        curve = fields(pointer(track['myTrack'], track_obj))
        if event['myTime'] != 0 or not track['myFixDuration'] or track['myUseTrackRotation'] \
                or track['myUseTrackScale'] or curve['myLoopType'] != 0:
            raise ValueError('Opening movement adapter requires the recorded fixed-duration curve binding')
        motions.append(dict(classId=identity, info=configs[identity], playable=track, curve=curve))
    # JSON has no Infinity literal; preserve the stepped tangent as a string for Unity's curve reader.
    def finite(value):
        if isinstance(value, float) and math.isinf(value):
            return 'Infinity' if value > 0 else '-Infinity'
        if isinstance(value, dict): return {k: finite(v) for k, v in value.items()}
        if isinstance(value, list): return [finite(v) for v in value]
        return value
    run_bundle = 'defaultpackage_assets_prefabs_ecentity_fight_fightskill_move_15300.bundle'
    skill_obj = next(obj for obj in apk.objects(run_bundle) if obj.type.name == 'MonoBehaviour'
                   and obj.read(check_read=False).m_Script.read().m_ClassName == 'MyFightSkill')
    skill_fields = fields(skill_obj)
    run_go = pointer(skill_fields['myPlayablePrefab'], skill_obj).read()
    sequence_obj = next(c.component.deref() for c in run_go.m_Component if c.component.type.name == 'MonoBehaviour'
                       and c.component.deref().read(check_read=False).m_Script.read().m_ClassName == 'MyPlayableSequence')
    run_events = fields(sequence_obj)['myPlayables']
    run_event = next(event for event in run_events if pointer(event['myPlayable'], sequence_obj)
                     .read(check_read=False).m_Script.read().m_ClassName == 'MyPlayableAnimator')
    run_obj = pointer(run_event['myPlayable'], sequence_obj)
    run_fields = fields(run_obj)
    if run_fields['myTriggerParam'] != 'run': raise ValueError('Official move skill trigger changed')
    animation_events = [dict(classId=15300, kind='animator', eventTime=run_event['myTime'], eventSpeed=run_fields['mySpeed'],
        source_event=dict(playable='MyPlayableAnimator', fields=run_fields))]
    action_obj = next(obj for obj in apk.objects('defaultpackage_assets_prefabs_ecentity_fight_fightaction_20001.bundle')
                      if obj.type.name == 'MonoBehaviour'
                      and obj.read(check_read=False).m_Script.read().m_ClassName == 'MyAction')
    if fields(action_obj)['myEffect']['m_PathID'] != 0:
        raise ValueError('Generic skill action now contains an animation effect')
    output = write_with_meta(ROOT / 'unity/Assets/StreamingAssets/Opening/action-motion.json',
                             json_bytes(finite(dict(manifest='854_156383', motions=motions, animations=animation_events))))
    hit_sources = []
    for identity in (3481, 3803):
        bundle = f'defaultpackage_assets_prefabs_ecentity_fight_fightmonster_{identity}.bundle'
        for obj in apk.objects(bundle):
            if obj.type.name != 'MonoBehaviour' or obj.read(check_read=False).m_Script.read().m_ClassName != 'MeshMaterialProperty':
                continue
            binding = fields(obj)
            renderer = pointer(binding['MeshRenderer'], obj).read()
            materials = []
            for material_pointer in renderer.m_Materials:
                material_obj = record(material_pointer.deref())
                material = material_obj.read()
                shader_obj = record(material.m_Shader.deref())
                shader = shader_obj.read()
                programs = gles_programs(shader, with_keywords=True)
                hit_program = next((p for p in programs if p['keywords'] == ['_HITEFFECT']), None)
                materials.append(dict(name=material.m_Name, shader=shader.m_ParsedForm.m_Name,
                    hit_program_sha256=hashlib.sha256(hit_program['source'].encode()).hexdigest() if hit_program else None,
                    hit_properties=hit_program is not None))
            hit_sources.append(dict(classId=identity, binding=binding, materials=materials))
    receipt = json.loads(REFERENCE.read_text(encoding='utf8'))
    source_dir = ROOT / '.tools/source-packages/official-code'
    source_files = ['client-source/MyFightActionSkillMove.cs', 'client-source/MyPlayableTrack.cs',
        'client-source/MyTrack.cs', 'client-source/MyCurveTrack.cs', 'client-source/MyGridTransform.cs',
        'client-source/MyFightSkill.cs', 'client-source/MyAction.cs', 'client-source/MyPlayableOverrideAnimator.cs',
        'client-source/MyPlayableAnimator.cs', 'client-source/MeshMaterialProperty.cs',
        'client-source/MyPlayableMaterialProperty.cs', 'client-source/Spine.Extension/CustomRenderExistingMesh.cs',
        'client-source/AppearanceSystem/AppearanceRendererLite.cs', 'common-source/Common/FightActionSkillMoveComponent.cs']
    receipt['action_motion'] = dict(inputs=inputs, hit_material_sources=hit_sources,
        generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        official_code=[dict(path=(source_dir / name).relative_to(ROOT).as_posix(),
            sha256=hashlib.sha256((source_dir / name).read_bytes()).hexdigest()) for name in source_files],
        operation='Original skill playable animation triggers and MyCurveTrack curves, per-segment timers and '
                  'grid reset; Action 20001 has no myEffect and must not replace skill-specific animation. '
                  'Hit tint follows SkeletonEffect-2D GLES alpha clipping, alpha-scaled blend and premultiplied output; '
                  'native stencil-only materials and particle children remain separate')
    receipt['action_motion']['unity_outputs'] = [dict(path=path, sha256=hashlib.sha256((ROOT/path).read_bytes()).hexdigest())
        for path in ('unity/Assets/Game/Resources/Opening/OpeningSpineHit.shader',
            'unity/Assets/Game/Runtime/Presentation/Opening/OpeningUiEffectDissolve.shader',
            'unity/Assets/Game/Runtime/Presentation/Opening/OpeningUiEffectDistort.shader',
            'unity/Assets/Game/Runtime/Presentation/Opening/OpeningUiEffectSequeue.shader',
            'unity/Assets/Game/Runtime/Presentation/Opening/OpeningUiParticle.shader',)]
    receipt['outputs'] = [row for row in receipt['outputs'] if row['path'] != output['path']] + [output]
    REFERENCE.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf8', newline='\n')
    print('Extracted', len(motions), 'official opening movement bindings')


if __name__ == '__main__':
    main()
