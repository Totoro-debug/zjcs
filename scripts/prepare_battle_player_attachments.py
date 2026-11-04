"""Recover original 3100 attachment registries and their Transform branches.

Reads serialized prefab data and field schemas, never animation SDK algorithms.
"""
import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / '.tools/python-packages'))
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from prepare_unity_profession_character_resources import meta
from UnityPy.classes import PPtr


def build():
    apk, schemas = Apk.current(), CurrentSchemas()
    assert apk.version == '884_156692'
    objects, nodes = [], []

    def key(obj):
        return str(obj.assets_file.name) + ':' + str(obj.path_id)

    def identity(obj):
        objects.append(dict(source=apk.provenance(obj), sha256=hashlib.sha256(obj.get_raw_data()).hexdigest()))

    def references(owner, value, path='', result=None):
        if result is None:
            result = {}
        if isinstance(value, dict):
            if set(value) == {'m_FileID', 'm_PathID'}:
                if value['m_PathID']:
                    target = PPtr(**value, assetsfile=owner.assets_file).deref()
                    result[path] = dict(sourceKey=key(target), type=target.type.name)
            else:
                for name, child in value.items():
                    references(owner, child, path + '/' + name, result)
        elif isinstance(value, list):
            for index, child in enumerate(value):
                references(owner, child, path + '/' + str(index), result)
        return result

    def walk(transform, parent, path, sibling=0):
        data = transform.read(); go = data.m_GameObject.deref(); game = go.read()
        row = dict(sourceKey=key(transform), gameObjectKey=key(go), parentKey=parent, path=path,
                   siblingIndex=sibling, name=game.m_Name, active=game.m_IsActive, layer=game.m_Layer,
                   transformType=transform.type.name, fields=transform.read_typetree(), components=[])
        nodes.append(row); identity(transform); identity(go)
        for ref in game.m_Component:
            component = ref.component.deref()
            if component.type.name != 'MonoBehaviour':
                continue
            name = component.read(check_read=False).m_Script.read().m_ClassName
            if name not in ('AttachAgent', 'AttachmentFollower', 'BoneFollower', 'SkeletonMecanim'):
                continue
            entry = dict(type=name, sourceKey=key(component))
            row['components'].append(entry)
            if name != 'SkeletonMecanim':
                fields = schemas.read(component)
                entry.update(fields=fields, references=references(component, fields))
            identity(component)
        for index, child in enumerate(data.m_Children):
            item = child.deref(); name = item.read().m_GameObject.read().m_Name
            walk(item, key(transform), path + ('/' if path else '') + name, index)

    try:
        available = apk.objects('prefabs_ecentity_fight_fightcharacter_3100')
        agent = next(obj for obj in available if obj.type.name == 'MonoBehaviour'
                     and obj.read(check_read=False).m_Script.read().m_ClassName == 'AttachAgent')
        go = agent.read(check_read=False).m_GameObject.read()
        transform = next(ref.component.deref() for ref in go.m_Component if ref.component.deref().type.name == 'Transform')
        assert go.m_Name == '3100'
        walk(transform, '', '')
    finally:
        apk.archive.close()
    target = ROOT / 'unity/Assets/Game/Resources/Combat/player-attachments.json'
    receipt = ROOT / 'assets/reference/battle-animation/player-attachments-source-manifest.json'
    raw = (json.dumps(dict(manifest=apk.version, entityId=3100, nodes=nodes), ensure_ascii=False, indent=2) + '\n').encode()
    if target.exists() and target.read_bytes() != raw:
        assert hashlib.sha256(target.read_bytes()).hexdigest() == json.loads(receipt.read_bytes())['output']['sha256']
    target.write_bytes(raw)
    metadata = Path(str(target) + '.meta')
    if not metadata.exists():
        meta(target)
        metadata.write_bytes(metadata.read_bytes().replace(b'\r\n', b'\n'))
    proof = dict(manifest=apk.version, originals=objects, schemas=schemas.records,
                 output=dict(path=target.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(raw).hexdigest()),
                 metadata=dict(path=metadata.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(metadata.read_bytes()).hexdigest()),
                 generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                 bindings={path + suffix: hashlib.sha256((ROOT / (path + suffix)).read_bytes()).hexdigest()
                           for path in (
                               'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeBattlePlayerAttachments.cs',
                               'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativePlayerBoneFollower.cs',
                               'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeBattlePlayerActor.cs',
                               'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattlePlayerModelClipValidation.PlayerAttachments.cs',
                               'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattlePlayerModelClipValidation.PlayerBoneFollowers.cs',
                               'unity/Assets/Game/Editor/Combat/AnimatorPlayback/Zjcs.Editor.Combat.AnimatorPlayback.asmdef',
                               'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattlePlayerModelClipValidation.PlayerActor.cs',
                               'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattlePlayerModelClipValidation.cs',
                           ) for suffix in ('', '.meta')},
                 application={name: hashlib.sha256((ROOT / '.tools/source-packages/current' / apk.version
                                                    / 'official-code/client-source' / (name + '.cs')).read_bytes()).hexdigest()
                              for name in ('AttachAgent', 'AttachmentFollower', 'AttachPosRotCfg')},
                 dependencies={name: hashlib.sha256((ROOT / 'assets/reference/battle-animation'
                                                    / (name + '-source-manifest.json')).read_bytes()).hexdigest()
                               for name in ('player-animation-binding', 'player-scene-bindings')},
                 limits=['Only serialized hierarchy and attachment application data; no third-party animation implementation is read.',
                         'The two direct-child bone followers use independent geometry checked against public API outputs; no SDK algorithm source is read or copied.',
                         'The player native skeleton has unit global scale. Other follower parent relationships, flags and axis orientations are outside this adapter.',
                         'Other original ECS, UI, rendering and effect components are not instantiated by the attachment binding.'])
    receipt.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2) + '\n').encode())
    print('Recovered original 3100 attachment hierarchy:', len(nodes), 'nodes.')


if __name__ == '__main__':
    build()
