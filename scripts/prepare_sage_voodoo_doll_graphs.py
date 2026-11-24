"""Restore the original Mapping Substitute link playable and its dependencies."""
import hashlib
from pathlib import Path

from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_source import OfficialSource
from prepare_battle_status_orbit_assets import MAPPED, build
from UnityPy.classes import PPtr


def main():
    source = OfficialSource()
    cache = ROOT / '.tools/source-packages/current' / source.version / 'sage-voodoo-doll'
    shader = verified_noise_mask_binding(cache)
    family = 'SageVoodooDollSkillGraphs'
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat' / family / 'Resources/Combat' / family,
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/sage-voodoo-doll-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/sage-voodoo-doll-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [12200, 10140]),
            'hits': ('hit_entity_id', None, [12201, 12203, 10141]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', []),
        },
        family=family, cache_name='sage-voodoo-doll', generator=Path(__file__), external_assets=shader,
    )
    status_family = 'SageVoodooDollStatusGraphs'
    build(
        DEST=ROOT / 'unity/Assets/Official/Combat' / status_family / 'Resources/Combat' / status_family,
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/sage-voodoo-doll-current-status-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/sage-voodoo-doll-current-status-graphs-source-manifest.json',
        ROOTS={}, graph_roots={entity: f'Prefabs/EcEntity/Fight/FightSkill/12200/{entity}.prefab'
            for entity in (12202, 12204)},
        MAPPED={**MAPPED, -1481699058: ('BattleStatusPlayables', 'MyFightStatus'),
            -550836577: ('BattleGridItemEntities', 'MyFightStatusProp'),
            -109006141: ('EntityActions', 'MyAction'), 932710152: ('BattleGridItemEntities', 'MyStatusEnd'),
            1200213464: ('EntityLifecycle', 'MyEntity')},
        family=status_family, shader_prefix=status_family + '_', generator=Path(__file__),
        native_manifest=source.version, native_cache=cache, native_export_name='native-export', external_assets=shader,
    )
    schemas = CurrentSchemas(cache / 'inputs/Managed')
    with Apk.current() as apk:
        component, = [obj for obj in apk.objects('defaultpackage_assets_prefabs_ecentity_fight_fightother_4600.bundle')
            if obj.type.name == 'MonoBehaviour'
            and obj.read(check_read=False).m_Script.read().m_ClassName == 'MyFightRoleVoodooDoll']
        fields = schemas.read(component)
        playable = PPtr(**fields['Playable'], assetsfile=component.assets_file).deref()
        if playable.read().m_Name != 'playable_Link':
            raise ValueError('Bind the original substitute link graph')
        original = dict(source=apk.provenance(playable),
            object_sha256=hashlib.sha256(playable.get_raw_data()).hexdigest())
    build(
        DEST=ROOT / 'unity/Assets/Official/Combat/SageVoodooDollGraphs/Resources/Combat/SageVoodooDollGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/sage-voodoo-doll-current-link-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/sage-voodoo-doll-current-link-graphs-source-manifest.json',
        ROOTS={}, graph_roots={'link': 'Prefabs/EcEntity/Fight/FightSkill/12200/playable_Link.prefab'},
        graph_originals={'link': original}, MAPPED={**MAPPED,
            2012015779: ('StoryAttachment', 'MyPlayableAttachToBone'),
            -741389653: ('BattleTrackPlayables', 'MyPlayableTrack'),
            -424924989: ('BattleSkillPlayables', 'LinkEffect')},
        family='SageVoodooDollGraphs', shader_prefix='SageVoodooDollGraph_',
        generator=Path(__file__), native_manifest=source.version, native_cache=cache,
        native_export_name='native-export', external_assets=shader,
        limits=['The original role view plays this graph from the substitute to its linked role until the substitute ends.'],
    )


if __name__ == '__main__':
    main()
