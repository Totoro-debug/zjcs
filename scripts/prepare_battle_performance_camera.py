"""Recover original performance cameras from the selected-version FightStage prefabs."""
import json
import re
from pathlib import Path

import yaml

from inspect_battle_skill_graph_bindings import EXPORT_RECEIPT, ROOT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed

EXPORT = (ROOT / '.tools/source-packages/current/884_156692/native-conversion/'
          'native-export-all/ExportedProject/Assets')
DATA = ROOT / 'unity/Assets/Game/Resources/Combat/performance-camera-bindings.json'
RECEIPT = ROOT / 'assets/reference/battle-scenes/performance-camera-source-manifest.json'


def documents(path):
    return {int(match[1]): yaml.load(match[2], Loader=yaml.CSafeLoader)
            for match in re.finditer(r'(?ms)^--- !u!\d+ &(\d+)\n(.*?)(?=^--- !u!|\Z)',
                                     path.read_text(encoding='utf-8-sig'))}


def main():
    proof = json.loads(EXPORT_RECEIPT.read_text(encoding='utf-8'))
    verified = {row['path']: row['sha256'] for row in proof['outputs']}
    records, sources = [], []
    for path in sorted((EXPORT / 'Prefabs/EcEntity/Fight/FightStage').glob('*/game_*.prefab')):
        source = identity(path)
        if source['sha256'] != verified[path.relative_to(EXPORT.parents[1]).as_posix()]:
            raise ValueError(f'Original battle stage hash differs: {path}')
        docs = documents(path)
        controllers = [item['MonoBehaviour'] for item in docs.values()
                       if 'mPerformanceCamera' in item.get('MonoBehaviour', {})]
        if not controllers:
            continue
        if len(controllers) != 1:
            raise ValueError(f'Expected one original GameCameraController: {path}')
        binding = controllers[0]['mPerformanceCamera']
        if binding['FramingTransposerProxy']['fileID'] or binding['RecomposerProxy']['fileID']:
            raise ValueError(f'Performance camera has an additional original proxy: {path}')
        camera = docs[binding['VirtualCamera']['fileID']]['MonoBehaviour']
        offset = docs[binding['CameraOffset']['fileID']]['MonoBehaviour']
        if camera['m_Script']['fileID'] != -1223560802 or offset['m_Script']['fileID'] != 1589288150:
            raise ValueError(f'Unexpected original Cinemachine component: {path}')
        if camera['m_Follow']['fileID'] or camera['m_LookAt']['fileID']:
            raise ValueError(f'Performance camera has an additional target binding: {path}')
        go = docs[camera['m_GameObject']['fileID']]['GameObject']
        if len(go['m_Component']) != 3:
            raise ValueError(f'Performance camera has an additional original component: {path}')
        pipeline = docs[camera['m_ComponentOwner']['fileID']]['Transform']
        pipeline_go = docs[pipeline['m_GameObject']['fileID']]['GameObject']
        scripts = [docs[item['component']['fileID']]['MonoBehaviour']['m_Script']['fileID']
                   for item in pipeline_go['m_Component']
                   if 'MonoBehaviour' in docs[item['component']['fileID']]]
        if scripts != [246809616] or pipeline['m_Children']:
            raise ValueError(f'Performance camera has an additional pipeline: {path}')
        transforms = {key: value['Transform'] for key, value in docs.items() if 'Transform' in value}
        transform = next(transforms[item['component']['fileID']] for item in go['m_Component']
                         if item['component']['fileID'] in transforms)
        hierarchy = []
        while transform['m_Father']['fileID']:
            node = docs[transform['m_GameObject']['fileID']]['GameObject']
            hierarchy.append(dict(name=node['m_Name'], active=bool(node['m_IsActive']),
                position=transform['m_LocalPosition'], rotation=transform['m_LocalRotation'],
                scale=transform['m_LocalScale']))
            transform = transforms[transform['m_Father']['fileID']]
        hierarchy.reverse()
        clean = lambda value: {key: item for key, item in value.items()
            if not key.startswith(('m_Object', 'm_Corresponding', 'm_Prefab', 'm_Editor'))
            and key not in ('m_GameObject', 'm_Script', 'm_Name', 'm_ComponentOwner', 'm_Follow', 'm_LookAt')}
        camera = clean(camera)
        camera['m_Transitions']['m_InheritPosition'] = bool(camera['m_Transitions']['m_InheritPosition'])
        offset = clean(offset)
        offset['m_PreserveComposition'] = bool(offset['m_PreserveComposition'])
        records.append(dict(sceneName=path.stem, transforms=hierarchy,
            cameraJson=json.dumps(camera, separators=(',', ':')),
            offsetJson=json.dumps(offset, separators=(',', ':'))))
        sources.append(source)
    if not records:
        raise ValueError('No original performance camera was extracted.')
    write_changed(DATA, json.dumps(dict(manifest='884_156692', cameras=records),
                                  ensure_ascii=False, indent=2) + '\n')
    meta(DATA)
    code = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source'
    receipt = dict(manifest='884_156692', generator=identity(Path(__file__)),
        native_export_manifest=identity(EXPORT_RECEIPT), inputs=sources,
        code_inputs=[identity(code / (name + '.cs')) for name in
            ('GameCameraController', 'FightCamera', 'MyPlayableFightCameraPerformance',
             'InputOperationModule', 'InputOperationLayer')],
        outputs=[identity(DATA), identity(DATA.with_name(DATA.name + '.meta'))],
        operation='Recover the original inactive performance virtual camera hierarchy, full lens and '
                  'transition fields, priority and CameraOffset. Require the original empty pipeline '
                  'and absence of framing/recomposer/follow/look-at bindings; do not invent defaults.')
    write_changed(RECEIPT, json.dumps(receipt, ensure_ascii=False, indent=2) + '\n')
    print('Original performance cameras:', len(records))


if __name__ == '__main__':
    main()
