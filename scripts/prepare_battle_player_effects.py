"""Restore the current original Renderer appearance effects and their full closure."""
import hashlib
import json
import re
from pathlib import Path

from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_battle_scene_appearance_runtime import identity, write_changed
from prepare_battle_status_orbit_assets import build
from prepare_unity_profession_character_resources import asset, guid, meta
from validation_workspace import validation_directory

RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI'
OFFICIAL = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'
DEST = ROOT / 'unity/Assets/Official/Combat/PlayerEffects/Resources/Combat/PlayerEffects'
SCENE = ROOT / 'unity/Assets/Official/Combat/SceneCharacterAppearance/Resources/Combat/SceneAppearance'


def main():
    work = validation_directory()
    apk, schemas = Apk.current(), CurrentSchemas()
    assert apk.version == '884_156692'
    table = json.loads((SCENE / 'appearance-tables.json').read_bytes())
    effects = {r['SkinName']: r for r in table['Spines'] if r['HasEffect']}
    manifest = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
    available = {Path(r['path']).stem: r for r in manifest
        if '/PlayerEffect/Renderer/' in r['path'] and r['path'].endswith('.prefab')}
    roots = {name: row['path'].removeprefix('Assets/') for name, row in available.items() if name in effects}
    integrated = sorted(set(effects) - set(roots))
    assert len(effects) == 36 and len(roots) == 32
    assert integrated == ['pWeapon_100_G_141', 'pWeapon_100_G_142', 'pWeapon_200_G_141', 'pWeapon_200_G_142']
    # AppearanceRendererPartHandler checks GetPartConfig BEFORE HasEffect.
    config = (SCENE / 'AppearanceConfig.asset').read_text(encoding='utf-8-sig')
    part_config = config.split('  mPartConfigs:', 1)[1].split('  mAppearanceOverrideConfigs:', 1)[0]
    for name in integrated:
        assert re.search(r'\bSkinType: ' + str(effects[name]['SkinType']) + r'\b', part_config)
        assert any(r['path'].endswith('/' + name + '_Integrated.prefab') for r in manifest)
    source_directory = DEST.parents[2] / 'Sources'
    source_directory.mkdir(parents=True, exist_ok=True)
    meta(source_directory, folder=True)
    outputs, sources, cases = [Path(str(source_directory) + '.meta')], [], []
    for name, relative in sorted(roots.items()):
        row = available[name]
        go = apk.asset(row['bundle'], row['path'])
        nodes = []

        def record(obj):
            result = dict(source=apk.provenance(obj), payload_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())
            sources.append(result)
            return result

        def visit(obj, parent):
            value = obj.read()
            components, children = [], []
            for pointer in value.m_Component:
                component = pointer.component.deref()
                kind = component.type.name
                if kind == 'MonoBehaviour':
                    script = component.read(check_read=False).m_Script.read()
                    kind = (script.m_Namespace + '.' if script.m_Namespace else '') + script.m_ClassName
                    fields = schemas.read(component)
                else:
                    fields = component.read_typetree()
                components.append(dict(type=kind, fields=fields, **record(component)))
                if kind == 'Transform': children.extend(component.read().m_Children)
            nodes.append(dict(name=value.m_Name, parent=parent,
                game_object=dict(fields=obj.read_typetree(), **record(obj)), components=components))
            for child in children: visit(child.read().m_GameObject.deref(), str(obj.path_id))

        visit(go, None)
        source = source_directory / (name + '.json')
        write_changed(source, json.dumps(dict(path=row['path'], guid=row['guid'], nodes=nodes),
            ensure_ascii=False, separators=(',', ':')) + '\n')
        meta(source)
        outputs.extend((source, Path(str(source) + '.meta')))
        node_indices = {n['game_object']['source']['path_id']: i for i, n in enumerate(nodes)}
        kinds = [c['type'] for n in nodes for c in n['components']]
        cases.append(dict(name=name, skin_type=effects[name]['SkinType'], nodes=[dict(name=n['name'],
            parent=node_indices.get(n['parent'], -1), active=bool(n['game_object']['fields']['m_IsActive']),
            transform=next(c['fields'] for c in n['components'] if c['type'] == 'Transform')) for n in nodes],
            particles=kinds.count('ParticleSystem'), followers=kinds.count('SkeletonRendererFollower'),
            backers=kinds.count('AppearanceSystem.SpineExt.SkeletonRendererAttachmentBacker')))
    apk.archive.close()
    write_changed(work / 'player-effect-cases.json', json.dumps(dict(cases=cases), ensure_ascii=False) + '\n')
    mapped = {}
    for file_id, name, namespace, source, target_namespace, target in [
        (1513771027, 'SkeletonRendererFollower', '', 'SkeletonRendererFollower.cs',
            'Zjcs.Official.ProfessionUI', OFFICIAL / 'SkeletonRendererFollower.cs'),
        (-1965046926, 'SkeletonRendererAttachmentBacker', 'AppearanceSystem.SpineExt',
            'AppearanceSystem.SpineExt/SkeletonRendererAttachmentBacker.cs',
            'Zjcs.Offline.ProfessionUI', RUNTIME / 'SkeletonRendererAttachmentBacker.cs')]:
        mapped[file_id] = dict(name=name, namespace=namespace, source=source,
            target_namespace=target_namespace, runtime=target.relative_to(ROOT).as_posix())
    conversion = ROOT / 'assets/reference/battle-animation/player-effect-assets-source-manifest.json'
    data = ROOT / 'unity/Assets/Game/Resources/Combat/PlayerEffects/native-prefabs.json'
    data.parent.mkdir(parents=True, exist_ok=True)
    meta(data.parent, folder=True)
    outputs.append(Path(str(data.parent) + '.meta'))
    build(DEST=DEST, DATA=data,
        RECEIPT=conversion, ROOTS={}, MAPPED={}, family='PlayerEffects', shader_prefix='PlayerEffect_',
        graph_roots=roots, source_scripts=mapped, runtime_receipts=['scene-appearance-runtime-source-manifest.json'],
        generator=Path(__file__),
        operation='Complete original Renderer appearance effects. Preserve every original local object and component, particle configuration, transforms and internal references. Remap the two original application component types to existing follower and independent attachment geometry bindings; convert original textures and shaders through verified Windows caches.',
        limits=['Formal battle player factory and Windows Player integration remain separate.'])
    entries = []
    for name, relative in sorted(roots.items()):
        target = DEST / relative
        chunks = re.split(r'(?=^--- !u!)', target.read_text(encoding='utf-8-sig'), flags=re.M)
        root, = [c for c in chunks if c.startswith('--- !u!1 &')
            and re.search(r'(?m)^  m_Name: ' + re.escape(name) + '$', c)]
        root_id = int(re.search(r'^--- !u!1 &(\d+)$', root, re.M)[1])
        entries.append(dict(Path='Appearance/PlayerEffect/Renderer/' + name, AssetGuid=available[name]['guid'],
            Asset=dict(fileID=root_id, guid=guid(target), type=3)))
    catalog = DEST / 'catalog.asset'
    asset(catalog, 'Original Renderer appearance effects', RUNTIME / 'OfflineProfessionImageCatalog.cs', dict(Entries=entries))
    outputs.extend((catalog, Path(str(catalog) + '.meta')))
    runtime = [RUNTIME / 'SkeletonRendererAttachmentBacker.cs', RUNTIME / 'ProfessionNativeSceneRenderer.cs',
        RUNTIME / 'ProfessionNativeAttachment.cs', RUNTIME / 'OfflineProfessionAppearancePartBindings.cs',
        OFFICIAL / 'AppearanceRendererPartHandler.cs', OFFICIAL / 'SkeletonRendererFollower.cs']
    proof = dict(manifest='884_156692', source_table=identity(SCENE / 'appearance-tables.json'),
        source_config=identity(SCENE / 'AppearanceConfig.asset'), sources=sources,
        schemas=schemas.records, required_effect_skins=sorted(effects), integrated_part_skins=integrated,
        unused_renderer_prefabs=sorted(set(available) - set(roots)), entries=entries,
        asset_conversion=identity(conversion), outputs=[identity(p) for p in outputs],
        generator=identity(Path(__file__)), runtime=[identity(p) for p in runtime],
        original_mesh_bake=identity(ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/SpineMeshBakeUtils.cs'),
        validation=[identity(ROOT / ('unity/Assets/Game/Editor/Combat/AnimatorPlayback/' + name + '.cs')) for name in (
            'OfficialBattlePlayerModelClipValidation', 'OfficialBattlePlayerModelClipValidation.PlayerActor',
            'OfficialBattlePlayerModelClipValidation.PlayerEffects')], binding='Combat/PlayerEffects/catalog',
        limits=['Production player factory and Windows Player remain unverified.'])
    write_changed(ROOT / 'assets/reference/battle-animation/player-effects-source-manifest.json',
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Original Renderer appearance effects:', len(entries), flush=True)


if __name__ == '__main__': main()
