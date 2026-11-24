"""Install an original skill actor through the common native monster pipeline."""
import json
from pathlib import Path

from current_battle_native_skill_graphs import verified_noise_mask_binding
from official_apk import Apk, ROOT
from official_source import OfficialSource
from prepare_battle_character_shadows import main as build_shadows
from prepare_battle_live_monster_assets import build as install_model
from prepare_battle_live_monsters import build as extract_model, entity_prefab_family
from prepare_battle_monster_animation_controllers import build as build_controllers
from prepare_battle_monster_animator_bindings import build as build_states
from prepare_battle_native_monster_appearance import build as build_appearance
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_battle_summon_role_views import main as build_role_views
from prepare_partner_native_battle_attachments import build as build_attachments
from validation_workspace import validation_directory
from verify_current_summon_properties import verify as verify_summon_properties


def build(entity, *, cache, destination, resource_root, receipt_prefix, generator,
          lifecycle_names=None, combat_tables=None):
    work = validation_directory()
    source = OfficialSource()
    managed = cache / 'inputs/Managed'
    resources = destination / 'Resources' / resource_root
    receipts = ROOT / 'assets/reference/battle-animation'

    def receipt(kind):
        return receipts / (receipt_prefix + '-current-' + kind + '-source-manifest.json')

    summon_properties = verify_summon_properties([entity])
    template_record = json.loads((receipts / 'live-monsters-source-manifest.json').read_bytes())['serializer_template_source']
    template = ROOT / template_record['path']
    if identity(template)['sha256'] != template_record['sha256']:
        raise ValueError('Verified monster texture serializer template differs')
    raw = template.read_text(encoding='utf-8')
    if raw.count('--- !u!28 &2800000\n') != 1 or '  m_TextureFormat: 4\n' not in raw:
        raise ValueError('Bind the original monster RGBA texture serializer')
    (work / 'monster-texture-template.asset').write_text(raw.replace('--- !u!28 &2800000\n', '--- !u!28 &1\n'),
        encoding='utf-8', newline='\n')
    extract_model([entity], conversion_cache=cache)
    model_receipt = receipt('actor')
    install_model(destination=destination, receipt_path=model_receipt, resource_path=resource_root + '/catalog',
        presentation_resource_root=resource_root, reuse_verified_shader=True)
    appearance_receipt = receipt('appearance')
    appearance = build_appearance([entity], destination=destination, native_root=destination,
        model_receipt=model_receipt, catalog_path='Resources/' + resource_root + '/appearance.asset',
        managed_directory=managed, texture_directory='textures')
    write_changed(appearance_receipt, json.dumps(appearance, ensure_ascii=False, indent=2) + '\n')
    inventory = destination / 'inventory.json'
    states_receipt = receipt('animator-bindings')
    build_states(root=destination, receipt_path=model_receipt, target=resources / 'animator-bindings.json',
        output_receipt=states_receipt)
    controllers_receipt = receipt('animation-controllers')
    build_controllers(inventory=inventory, receipt=model_receipt, target=resources / 'animation-controllers.json',
        proof_path=controllers_receipt, managed_directory=managed)
    row, = build_attachments(work, monsters=True, inventory=inventory, managed_directory=managed)
    attachments = resources / 'attachments.json'
    write_changed(attachments, (work / 'monster-native-attachments.json').read_text('utf-8'))
    meta(attachments)
    proof = json.loads((work / 'monster-native-attachments-source-manifest.json').read_bytes())
    proof['output'] = identity(attachments)
    attachments_receipt = receipt('attachments')
    write_changed(attachments_receipt, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    nodes = {node['sourceKey']: node for node in row['nodes']}
    shadow, = [node for node in nodes.values() if node['name'] == 'ShadowBoneFollower']
    path = []
    while shadow is not None:
        path.insert(0, shadow['name'])
        shadow = nodes.get(shadow['parentKey'])
    with Apk.current() as apk:
        family = entity_prefab_family(apk, entity)
    role = dict(prefab=str(entity), parts=['/'.join(path)],
        sourcePrefab=f'Prefabs/EcEntity/Fight/{dict(fightmonster="FightMonster", fightpet="FightPet", fightother="FightOther")[family]}/{entity}.prefab')
    shadow_receipt = receipt('shadow')
    build_shadows(destination=resources / 'Shadows', data=resources / 'shadow-bindings.json', receipt=shadow_receipt,
        role_catalog=dict(manifest=source.version, roles=[role]), catalog_source=attachments,
        resource_root=resource_root + '/Shadows', generator=generator, conversion_cache=cache)
    role_receipt = receipt('role-views')
    build_role_views(entities_to_prepare=[entity], base=resources, receipt=role_receipt,
        resource_root=resource_root, conversion_cache=cache, external_assets=verified_noise_mask_binding(cache),
        lifecycle_names=lifecycle_names)
    role_proof = json.loads(role_receipt.read_bytes())
    additional = (model_receipt, appearance_receipt, states_receipt, controllers_receipt, attachments_receipt, shadow_receipt)
    outputs = {row['path']: row for row in role_proof['outputs']}
    outputs[proof['output']['path']] = proof['output']
    attachment_meta = identity(Path(str(attachments) + '.meta'))
    outputs[attachment_meta['path']] = attachment_meta
    for selected in (model_receipt, appearance_receipt, states_receipt, controllers_receipt):
        for path, expected in json.loads(selected.read_bytes())['outputs'].items():
            row = identity(ROOT / path)
            if row['sha256'] != expected:
                raise ValueError('Original skill actor output differs: ' + path)
            outputs[path] = row
    for row in json.loads(shadow_receipt.read_bytes())['outputs']:
        if row != identity(ROOT / row['path']):
            raise ValueError('Original skill actor shadow output differs: ' + row['path'])
        outputs[row['path']] = row
    role_proof.update(outputs=list(outputs.values()), native_actor_receipts=[identity(path) for path in additional],
        native_actor_installer=identity(generator), common_actor_installer=identity(Path(__file__)),
        summon_properties=summon_properties, combat_table_sources=combat_tables or [])
    write_changed(role_receipt, json.dumps(role_proof, ensure_ascii=False, indent=2) + '\n')
    return role_receipt
