"""Recover each native partner's original application AnimationController binding."""
import argparse
import hashlib
import json
from pathlib import Path

from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from UnityPy.classes import PPtr


def build(work):
    work = work.resolve()
    if work.parent != ROOT / 'output/validation' or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Use an owned partner skill graph validation workspace')
    inventory = ROOT / 'unity/Assets/Official/Combat/NativePartners/catalog.json'
    selected = json.loads(inventory.read_bytes())
    apk, schemas = Apk.current(), CurrentSchemas()
    if apk.version != selected['manifest'] or apk.version != '884_156692':
        raise ValueError('Review original partner animation binding versions')
    rows, objects = [], []

    def key(obj):
        return str(obj.assets_file.name) + ':' + str(obj.path_id)

    try:
        assets = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
        for partner in selected['fighters']:
            entity = partner['entityId']
            candidates = [row for row in assets if row['path'].endswith('/' + str(entity) + '.prefab')]
            if len(candidates) != 1:
                raise ValueError('Original partner entity is ambiguous: ' + str(entity))
            root = apk.asset(candidates[0]['bundle'], '/' + str(entity) + '.prefab')
            components = [ref.component.deref() for ref in root.read().m_Component]
            transform, = [obj for obj in components if obj.type.name == 'Transform']
            controller, = [obj for obj in components if obj.type.name == 'MonoBehaviour'
                and obj.read(check_read=False).m_Script.read().m_ClassName == 'AnimationController']
            fields = schemas.read(controller)
            animator = PPtr(**fields['myAnimator'], assetsfile=controller.assets_file).deref()
            animator_transform, = [ref.component.deref() for ref in animator.read().m_GameObject.read().m_Component
                if ref.component.deref().type.name == 'Transform']
            if key(transform) != partner['transforms'][0]['sourceKey'] or key(animator_transform) != partner['transforms'][-1]['sourceKey']:
                raise ValueError('Original animation controller points to a different partner hierarchy')
            rows.append(dict(npcId=partner['npcId'], entityId=entity, rootKey=key(transform),
                animatorKey=key(animator_transform), componentKey=key(controller), fields=fields))
            for obj in (controller, animator):
                objects.append(dict(source=apk.provenance(obj),
                    object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest()))
            print(partner['npcId'], 'original animation controller bound', flush=True)
    finally:
        apk.archive.close()
    target = work / 'partner-native-animation-bindings.json'
    write_changed(target, json.dumps(dict(manifest=apk.version, partners=rows), ensure_ascii=False, separators=(',', ':')) + '\n')
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    proof = dict(manifest=apk.version, selection=identity(inventory), objects=objects,
        component_schemas=schemas.records, output=identity(target), generator=identity(Path(__file__).resolve()),
        application_sources=[identity(code / 'client-source/AnimationController.cs')])
    write_changed(work / 'partner-native-animation-bindings-source-manifest.json', json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    return proof


def install(work, proof):
    target = ROOT / 'unity/Assets/Official/Combat/NativePartnerAssets/Resources/Combat/NativePartnerAssets/animation-bindings.json'
    receipt = ROOT / 'assets/reference/partner-native-animation-bindings-source-manifest.json'
    raw = (work / 'partner-native-animation-bindings.json').read_bytes()
    if target.exists() and target.read_bytes() != raw and (not receipt.exists() or
            hashlib.sha256(target.read_bytes()).hexdigest() != json.loads(receipt.read_bytes())['output']['sha256']):
        raise ValueError('Preserve the existing original partner animation binding until provenance matches')
    target.write_bytes(raw)
    binding = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/PartnerNativeBattleAnimationBinding.cs'
    for path in (target, binding):
        metadata = Path(str(path) + '.meta')
        if not metadata.exists():
            meta(path)
        write_changed(metadata, '\n'.join(line.rstrip() for line in metadata.read_text('utf8').splitlines()) + '\n')
    proof['output'] = identity(target)
    proof['outputs'] = [identity(path) for path in (Path(str(target) + '.meta'), binding, Path(str(binding) + '.meta'))]
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    proof['bindings'] = [identity(path) for path in (runtime / 'Official/StoryAnimation/AnimationController.cs',
        runtime / 'Offline/ProfessionUI/Zjcs.Offline.ProfessionUI.asmdef')]
    proof['decoding_inputs'] = [identity(ROOT / 'scripts' / name) for name in
        ('current_component_schemas.py', 'official_metadata.py', 'official_unity_schema.py', 'official_apk.py', 'official_manifest.py')]
    write_changed(receipt, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--workspace', required=True, type=Path)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    receipt = build(args.workspace)
    if args.install:
        install(args.workspace, receipt)
