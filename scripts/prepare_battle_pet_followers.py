"""Recover original pet follower fields, application methods and movement graph."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from UnityPy.classes import PPtr
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_battle_status_orbit_assets import build as build_graphs, SCRIPTS


CODE = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Official/BattleSkillPlayables'
RECEIPT = ROOT / 'assets/reference/battle-animation/pet-followers-source-manifest.json'
DATA = ROOT / 'unity/Assets/Game/Resources/Combat/pet-followers.json'


def replace_once(text, before, after):
    if text.count(before) != 1:
        raise ValueError('Review changed official follower source: ' + before)
    return text.replace(before, after)


def port_follower():
    source = CODE / 'GridTransformFollower.cs'
    text = source.read_text('utf-8-sig')
    text = re.sub(r'^\s*//IL_.*\n', '\n', text, flags=re.M)
    text = re.sub(r'^\s*\[Rename\(.*\)\]\n', '\n', text, flags=re.M)
    for line in ('using System.Runtime.CompilerServices;\n', 'using Common;\n',
                 'using Framework.DependencyInjection;\n', 'using GameFramework;\n'):
        text = text.replace(line, '')
    start = text.index('\t\tpublic Vector3 Pos\n')
    end = text.index('\n\t\tpublic bool Valid', start)
    text = text[:start] + '\t\tpublic Vector3 Pos { get; private set; }\n' + text[end:]
    text = replace_once(text, 'private MyEntity mFollowedTargetEntity;',
                        'private MyTransform mFollowedTargetTransform;')
    text = text.replace('GridTransformComponent', 'ECGridTransformData')
    # Keep the original private serialized names. The adapter injects their
    # verified prefab values before Awake, then supplies the original grid view.
    text = replace_once(text, '\t[Inject]\n\tprivate IMyPlayableManager MyPlayableManager { get; set; }',
                        '\tpublic IMyPlayableManager MyPlayableManager { private get; set; }')
    text = replace_once(text, 'public void Init(Transform followTarget)',
                        'public void Init(Transform followTarget, ECGridTransformData grid)')
    text = replace_once(text, 'mFollowedTargetEntity = ((Component)followTarget).GetComponent<MyEntity>();',
                        'mFollowedTargetTransform = followTarget.GetComponent<MyTransform>();')
    text = replace_once(text, 'mECGridTransformData = mFollowedTargetEntity.Entity.GetComponent<ECGridTransformData>();',
                        'mECGridTransformData = grid;')
    text = text.replace('mECGridTransformData.PosUpdatedEvent += (IGridTransformPosUpdatedEventReceiver)this;',
                        'mECGridTransformData.AddPosUpdatedReceiver(this);')
    text = text.replace('mECGridTransformData.PosUpdatedEvent -= (IGridTransformPosUpdatedEventReceiver)this;',
                        'mECGridTransformData.RemovePosUpdatedReceiver(this);')
    text = text.replace('mFollowedTargetEntity.myTransform.OnScaleUpdated += MyTransform_OnScaleUpdated;',
                        'if (mFollowedTargetTransform) mFollowedTargetTransform.OnScaleUpdated += MyTransform_OnScaleUpdated;')
    text = text.replace('mFollowedTargetEntity.myTransform.OnScaleUpdated -= MyTransform_OnScaleUpdated;',
                        'mFollowedTargetTransform.OnScaleUpdated -= MyTransform_OnScaleUpdated;')
    text = text.replace('mFollowedTargetEntity', 'mFollowedTargetTransform')
    text = text.replace('transform.GridGround.GridToPos(', 'transform.GridToPosition(')
    text = text.replace('mECGridTransformData.GridGround.GridToPos(', 'mECGridTransformData.GridToPosition(')
    text = text.replace('mFollowedTargetTransform.myGridTransform.GridGround.GridToPos(mFollowedTargetTransform.myGridTransform.Pos)',
                        'mECGridTransformData.GridToPosition(mECGridTransformData.Pos)')
    text = replace_once(text, 'private void Update()', 'public void Tick(float deltaTime)')
    text = text.replace('Time.deltaTime', 'deltaTime')
    text = text.replace('mMoveQuene.TryDequeue(ref movingData2)', 'mMoveQuene.TryDequeue(out movingData2)')
    text = replace_once(text, 'public void Release()\n\t{',
                        'public void Release()\n\t{\n\t\tif (mMoveQuene == null) return; // Unity teardown may already have destroyed this follower.')
    for value in ('mStartPos', 'mEndPos', 'val', 'val2', 'GetPosition()'):
        text = text.replace('Vector2.op_Implicit(' + value + ')', '(Vector2)' + value)
    seam = '''
    // Explicit local configuration replaces serialized prefab injection only.
    public void Configure(float moveGridTime, Transform model, GameObject movePlayable,
        float startDelay, int teleportDistance, bool followPosZ, bool useLastMovingData)
    {
        mMoveGridTime = moveGridTime; mModelTransform = model; mMoveAnimPlayable = movePlayable;
        mMyTransform = transform.GetComponent<MyTransform>();
        mStartMovingDelay = startDelay; mTeleportDistance = teleportDistance;
        mFollowPosZ = followPosZ; mUseLastMovingData = useLastMovingData;
    }

    public void NotifyTargetScaleUpdated()
    {
        if (mMoveState == EState.Stop) SetFlipX(mFollowTarget.localScale.x < 0f);
    }
'''
    text = text.rstrip()[:-1] + seam + '}\n'
    header = ('// Original GridTransformFollower algorithm; local grid/configuration/time injection only.\n'
              'using Object = UnityEngine.Object;\n'
              'using Zjcs.Official.EntityLifecycle;\n'
              'using Zjcs.Official.EntityViews;\n'
              'using Zjcs.Official.EntityTransformViews;\n'
              'using Zjcs.Official.StoryPlayables;\n'
              'using Zjcs.Official.StoryPlayableManager;\n'
              'using Zjcs.Official.World.NativeTerrain.Common;\n')
    at = text.index('public class GridTransformFollower')
    text = header + text[:at] + 'namespace Zjcs.Official.BattleSkillPlayables\n{\n' + text[at:] + '}\n'
    if any(token in text for token in ('return field;', '[Inject]', 'mFollowedTargetEntity', 'GridGround')):
        raise ValueError('Unadapted original pet follower dependency')
    target = RUNTIME / source.name
    write_changed(target, '\n'.join(line.rstrip() for line in text.splitlines()) + '\n')
    meta(target)
    return dict(original=identity(source), migrated=identity(target), meta=identity(Path(str(target) + '.meta')))


def port_role_pet():
    source = CODE / 'MyFightRolePet.cs'
    text = source.read_text('utf-8-sig')
    text = text.replace('using Common;\n', '')
    text = text.replace('MyComponent<FightRolePetComponent>', 'MonoBehaviour')
    text = text.replace('private MyEntity mOwnerEntity;', 'private Transform mOwnerEntity;\n\tprivate bool bound;')
    text = replace_once(text, 'public override void OnStart(bool newEntity)',
        'public void Bind(GridTransformFollower follower, MyFightSkillAgent skillAgent,\n'
        '        MyFightSkillAgent ownerSkillAgent, Transform owner, ECGridTransformData grid)')
    text = replace_once(text, 'base.OnStart(newEntity);',
        'mGridTransformFollower = follower; mSkillAgent = skillAgent;\n'
        '\t\tmOwnerEntity = owner; mOwnerSkillAgent = ownerSkillAgent; bound = true;')
    text = replace_once(text, 'mOwnerEntity = myContainer.GetEntity(Component.OwnerEntityId);', '')
    text = replace_once(text, 'mOwnerSkillAgent = ((Component)mOwnerEntity).GetComponent<MyFightSkillAgent>();', '')
    text = replace_once(text, 'mGridTransformFollower.Init(((Component)mOwnerEntity).transform);',
                        'mGridTransformFollower.Init(mOwnerEntity, grid);')
    text = replace_once(text, 'public override void OnEnd(MySerializable userData)\n\t{',
                        'public void Release()\n\t{\n\t\tif (!bound) return;\n\t\tbound = false;')
    text = replace_once(text, 'base.OnEnd(userData);', '')
    text = text.rstrip()[:-1] + '\n\tprivate void OnDestroy() { Release(); }\n}\n'
    header = ('// Original MyFightRolePet subscriptions, nested-cast counter and follower callbacks.\n'
              'using Object = UnityEngine.Object;\nusing Zjcs.Official.EntityTransformViews;\n')
    at = text.index('public class MyFightRolePet')
    text = header + text[:at] + 'namespace Zjcs.Official.BattleSkillPlayables\n{\n' + text[at:] + '}\n'
    target = RUNTIME / source.name
    write_changed(target, '\n'.join(line.rstrip() for line in text.splitlines()) + '\n')
    meta(target)
    return dict(original=identity(source), migrated=identity(target), meta=identity(Path(str(target) + '.meta')))


def build():
    apk, schemas = Apk.current(), CurrentSchemas()
    inventory = ROOT / 'unity/Assets/Official/Combat/NativeMonsters/inventory.json'
    selected = json.loads(inventory.read_bytes())
    models = [row for row in selected['models'] if 9200 < row['entityId'] < 9300]
    rules_path = ROOT / 'unity/Assets/Game/Resources/Combat/pet-battle-rules.json'
    rules = json.loads(rules_path.read_bytes())
    pet_classes = {row['FightEntityClassId']: row['ClassId'] for row in rules['tables']['pet']}
    assert apk.version == selected['manifest'] == '884_156692' and len(models) == 21
    rows, originals, graphs = [], [], {}
    try:
        for model in models:
            entity = model['entityId']
            objects = apk.objects(f'prefabs_ecentity_fight_fightpet_{entity}')
            component, = [o for o in objects if o.type.name == 'MonoBehaviour'
                          and o.read(check_read=False).m_Script.read().m_ClassName == 'GridTransformFollower']
            fields = schemas.read(component)
            renderer = PPtr(**fields['mModelTransform'], assetsfile=component.assets_file).deref()
            assert renderer.read().m_GameObject.read().m_Name == model['transforms'][-1]['name']
            playable = PPtr(**fields['mMoveAnimPlayable'], assetsfile=component.assets_file).deref()
            name = playable.read().m_Name
            # The actual container binding, not a name-derived path.
            bundle = apk.provenance(playable)['bundle']
            path, = [p.removeprefix('Assets/') for p, ptr in apk.container(bundle)
                     if ptr.path_id == playable.path_id]
            assert name not in graphs or graphs[name] == path
            graphs[name] = path
            rows.append(dict(entityId=entity, petClassId=pet_classes[entity], moveGridTime=fields['mMoveGridTime'],
                startDelay=fields['mStartMovingDelay'], teleportDistance=fields['mTeleportDistance'],
                followPosZ=bool(fields['mFollowPosZ']), useLastMovingData=bool(fields['mUseLastMovingData']),
                modelPath='/'.join(v['name'] for v in model['transforms'][1:]), movePlayable=name))
            originals.append(dict(entityId=entity, source=apk.provenance(component),
                object_sha256=hashlib.sha256(component.get_raw_data()).hexdigest(), fields=fields,
                model_transform=identity_object(apk, renderer), move_playable=identity_object(apk, playable)))
    finally:
        apk.archive.close()
    port = port_follower()
    role_port = port_role_pet()
    mappings = dict(SCRIPTS)
    mappings.update({205640859: ('StoryAnimation', 'MyPlayableAnimator'),
        -822603460: ('StoryPlayables', 'ApplyBySource'),
        -194148604: ('StoryPlayables', 'MyPlayableApplyObj'),
        2012015779: ('StoryAttachment', 'MyPlayableAttachToBone')})
    build_graphs(DEST=ROOT / 'unity/Assets/Official/Combat/PetFollowers/Resources/Combat/PetFollowers',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/pet-follower-graphs.json', RECEIPT=RECEIPT,
        ROOTS={}, graph_roots=graphs, family='PetFollowers', shader_prefix='PetFollower_', MAPPED=mappings,
        generator=Path(__file__), limits=['Original childhood follower configuration, movement graph and shared scene adapter. '
            'Does not infer entityization transfer effects, missing passive support timing or server entity creation rules. '
            'Windows Player publication is tracked in the owning battle feature page.'])
    write_changed(DATA, json.dumps(dict(manifest=selected['manifest'], models=rows), ensure_ascii=False, indent=2) + '\n')
    meta(DATA)
    proof = json.loads(RECEIPT.read_bytes())
    proof.update(original_followers=originals, component_schemas=schemas.records,
        selection=identity(inventory), pet_class_rules=identity(rules_path), follower_port=port, role_pet_port=role_port)
    proof['position_conversion_source'] = identity(CODE / 'MyContainer.cs')
    proof['runtime_bindings'] = [identity(ROOT / path) for path in (
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattlePetFollowerCatalog.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattlePetFollowerView.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleLiveMonsterModelView.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleEntityPositionContainer.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.Pets.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillGraphPlayback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillEntityPlayback.cs')]
    proof['validation_sources'] = [identity(ROOT / path) for path in (
        'unity/Assets/Game/Editor/OfficialBattlePetFollowerAcceptance.cs',
        'unity/Assets/Game/Editor/OfficialBattlePetHostAcceptance.cs',
        'scripts/fixtures/battle_skill_graph_dispatch_harness.cs',
        'scripts/test_battle_skill_graph_dispatch.ps1')]
    proof['outputs'].extend(identity(p) for p in (DATA, Path(str(DATA) + '.meta')))
    write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Recovered', len(rows), 'original pet followers and', len(graphs), 'movement graphs')


def identity_object(apk, obj):
    return dict(source=apk.provenance(obj), object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--port-only', action='store_true')
    arguments = parser.parse_args()
    print(port_follower(), port_role_pet()) if arguments.port_only else build()
