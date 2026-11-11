"""Restore the complete original airborne animation graph without changing its timing."""
import json
from pathlib import Path

from official_apk import ROOT
from prepare_battle_status_orbit_assets import build
from prepare_battle_air_combo_runtime import main as prepare_runtime
from prepare_battle_grid_entity_assets import script_id


def main():
    prepare_runtime()
    proof = json.loads((ROOT / 'assets/reference/battle-rules/air-combo-source-manifest.json').read_bytes())
    source, = [obj for prefab in proof['original_visuals']['prefabs'] if prefab['id'] == 24269
        for obj in prefab['objects']
        if obj['type'] == 'GameObject' and obj['fields']['m_Name'] == 'playable_AirCombo']
    original = {prefab['id']: next(obj for obj in prefab['objects']
        if obj['type'] == 'GameObject' and obj['fields']['m_Name'] == str(prefab['id']))
        for prefab in proof['original_visuals']['prefabs']}
    original[-24269] = source
    scripts = {script_id(name): (folder, name) for folder, names in (
        ('EntityLifecycle', ('MyEntity',)), ('EntityActions', ('MyAction',)),
        ('BattleStatusPlayables', ('MyFightStatus',)),
        ('BattleGridItemEntities', ('MyFightStatusDamageRemove', 'MyFightStatusDamageSkill', 'MyStatusAutoRemove')))
        for name in names}
    scripts.update({-253978515: ('StoryPlayables', 'MyPlayableSequence'),
        205640859: ('StoryAnimation', 'MyPlayableAnimator'),
        -822603460: ('StoryPlayables', 'ApplyBySource')})
    build(DEST=ROOT / 'unity/Assets/Official/Combat/AirCombo/Resources/Combat/AirCombo',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/air-combo.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/air-combo-assets-source-manifest.json',
        ROOTS={}, graph_roots={-24269: 'GameObject/playable_AirCombo.prefab',
            **{key: f'Prefabs/EcEntity/Fight/FightSkill/{key}/{key}.prefab' for key in (24269, 24270)}},
        graph_originals={key: dict(source=obj['source'], object_sha256=obj['payload_sha256'])
            for key, obj in original.items()}, MAPPED=scripts,
        family='AirCombo', shader_prefix='AirCombo_', generator=Path(__file__),
        runtime_receipts=('status-view-runtime-source-manifest.json', 'air-combo-runtime-source-manifest.json',
            '../official-map/map14-story-animator-runtime-source-manifest.json'),
        operation='Restore both complete APK-verified 24269/24270 status prefabs and playable_AirCombo with their original local objects, '
            'sequence order, zero-delay animator leaf, floating trigger and 1.5-second graph duration. '
            'Remap only migrated script GUIDs and resource paths. The 0.7/0.8-second status clock is separate.',
        limits=['Complete native original status prefabs and animation graph resources. The runtime binding '
            'and its actual battle Play Mode/Windows Player verification are maintained in docs/features/skills.md.'])


if __name__ == '__main__':
    main()
