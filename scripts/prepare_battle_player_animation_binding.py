"""Recover the current original 3100 application-controller and model chain."""
import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / '.tools/python-packages'))
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from prepare_unity_profession_character_resources import meta
from UnityPy.classes import PPtr


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def build():
    apk, schemas = Apk.current(), CurrentSchemas()
    assert apk.version == '884_156692'
    originals = []
    try:
        objects = apk.objects('prefabs_ecentity_fight_fightcharacter_3100')
        def component(name):
            return next(obj for obj in objects if obj.type.name == 'MonoBehaviour'
                        and obj.read(check_read=False).m_Script.read().m_ClassName == name)
        controller = component('AnimationController')
        appearance = component('PlayerAnimatorAppearance')
        fields, appearance_fields = schemas.read(controller), schemas.read(appearance)
        animator = PPtr(**fields['myAnimator'], assetsfile=controller.assets_file).deref()
        assert appearance_fields['AnimController']['m_PathID'] == controller.path_id
        assert appearance_fields['Animator'] == dict(m_FileID=0, m_PathID=0)
        assert appearance_fields['m_GameObject']['m_PathID'] == animator.read().m_GameObject.path_id
        node = next(ref.component.deref() for ref in animator.read().m_GameObject.read().m_Component
                    if ref.component.deref().type.name == 'Transform')
        chain, nodes = [], []
        while node:
            data = node.read(); name = data.m_GameObject.read().m_Name
            chain.append(dict(name=name, fields=node.read_typetree(), gameObject=data.m_GameObject.read_typetree()))
            nodes.append(node)
            node = data.m_Father.deref() if data.m_Father.path_id else None
        chain.reverse()
        assert [row['name'] for row in chain] == ['3100', '3100_view', 'body', 'PlayerModel_Game']
        assert all(row['gameObject']['m_IsActive'] for row in chain)
        assert controller.read(check_read=False).m_GameObject.read().m_Name == chain[0]['name']
        for obj in (controller, appearance, animator, *nodes,
                    *(node.read().m_GameObject.deref() for node in nodes)):
            originals.append(dict(source=apk.provenance(obj), sha256=digest(obj.get_raw_data())))
        row = dict(entityId=3100, animatorPath='/'.join(value['name'] for value in chain[1:]),
                   fields=fields, appearance=appearance_fields, animator=animator.read_typetree(), transforms=chain)
    finally:
        apk.archive.close()
    target = ROOT / 'unity/Assets/Game/Resources/Combat/player-animation-binding.json'
    proof_path = ROOT / 'assets/reference/battle-animation/player-animation-binding-source-manifest.json'
    payload = (json.dumps(dict(manifest=apk.version, player=row), ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    if target.exists() and target.read_bytes() != payload:
        previous = json.loads(proof_path.read_bytes())
        assert digest(target.read_bytes()) == previous['outputs'][target.relative_to(ROOT).as_posix()]
    target.write_bytes(payload)
    metadata = Path(str(target) + '.meta')
    if not metadata.exists():
        meta(target)
        metadata.write_bytes(metadata.read_bytes().replace(b'\r\n', b'\n'))
    application = ROOT / '.tools/source-packages/current' / apk.version / 'official-code/client-source/AnimationController.cs'
    bindings = [
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeBattlePlayerActor.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattlePlayerModelClipValidation.PlayerActor.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattlePlayerModelClipValidation.cs',
    ]
    proof = dict(manifest=apk.version, originals=originals, schemas=schemas.records,
                 application=dict(path=application.relative_to(ROOT).as_posix(), sha256=digest(application.read_bytes())),
                 outputs={path.relative_to(ROOT).as_posix(): digest(path.read_bytes()) for path in (target, metadata)},
                 generator_sha256=digest(Path(__file__).read_bytes()),
                 bindings={path + suffix: digest((ROOT / (path + suffix)).read_bytes())
                           for path in bindings for suffix in ('', '.meta')},
                 limits=['Restores only the original 3100 model/controller branch; the owning battle view still supplies ECS, HUD, shadow, player services and role events.',
                         'No claim of all skill graphs, interrupted transitions, full original entity prefab, input/save workflow or Windows Player delivery.'],
                 operation='Decode current original 3100 root application controller, its exact model Animator and PlayerAnimatorAppearance cross-reference, and complete four-node transform chain. No third-party animation implementation or new art payload is read.')
    proof_path.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
    print('Installed current original 3100 player animation binding.')


if __name__ == '__main__':
    build()
