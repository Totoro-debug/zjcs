"""Restore every original status prefab in the two initial / four first-promotion closure."""
import json
import re
from pathlib import Path
from official_apk import ROOT
from prepare_battle_cast_assets import EVENT_ASSET
from prepare_battle_grid_entity_assets import script_id
from prepare_battle_status_orbit_assets import build
from prepare_battle_scene_appearance_runtime import identity, write_changed
from prepare_first_promotion_status_runtime import main as prepare_runtime, EMPTY


def main():
    prepare_runtime()
    scope_path = ROOT / 'assets/reference/battle-rules/first-promotion-source-manifest.json'
    scope = json.loads(scope_path.read_bytes())
    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, value in json.loads((ROOT / 'unity/Assets/Official/Combat' / name).read_bytes())['entities'].items():
            entities.setdefault(int(key), {}).update(value)
    statuses = [key for key in scope['configured_entity_ids'] if 'FightStatusComponent' in entities[key]]
    assert len(statuses) == 83
    embedded = {10712: '10710/10712', 10772: '10770/10772', 11142: '11140/11142',
                11582: '11580/11582', 11632: '11630/11632', 24191: '24192/24191/24191'}
    roots = {key: 'Prefabs/EcEntity/Fight/FightSkill/' + embedded.get(key, f'{key}/{key}') + '.prefab'
             for key in statuses}
    names = set(EMPTY) | set('''ApplyByBone ApplyBySource ApplyByStatusStackCount
ApplyEnableOnPlay ApplyOnStop ApplyOnStopFadeOut AutoStopOnSourceDie MyAction MyEffectFadeOut
MyEntity MyFightGameplayTag MyFightStatus MyFightStatusDamageReduceAbsolute MyFightStatusRoundStart
MyFightStatusShieldPersistent MyPlayableAnimator MyPlayableAttachToBone MyPlayableFollowBone
MyPlayableFollowSpinePart MyPlayableOrbitMovement MyPlayableOverrideAnimator MyPlayableSequence
MyPlayableSound MyStatusAutoRemove MyStatusEnd SkeletonRendererFollower'''.split())
    runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
    paths = list(runtime.rglob('*.cs'))
    mapped = {}
    for name in sorted(names):
        matches = [p for p in paths if p.stem == name]
        assert len(matches) == 1, ('Review original script binding', name, matches)
        path, = matches
        code = ROOT / '.tools/source-packages/current/884_156692/official-code'
        sources = [p for p in (code / 'client-source' / (name + '.cs'),
                   code / 'game-framework-source/GameFramework' / (name + '.cs')) if p.is_file()]
        assert len(sources) == 1, ('Review original source identity', name, sources)
        namespace = re.search(r'(?m)^namespace ([\w.]+)', sources[0].read_text(encoding='utf-8-sig'))
        mapped[script_id(name, namespace[1] if namespace else '')] = (path.parent.relative_to(runtime).as_posix(), name)
    receipt = ROOT / 'assets/reference/battle-animation/first-promotion-status-assets-source-manifest.json'
    build(DEST=ROOT / 'unity/Assets/Official/Combat/FirstPromotionStatuses/Resources/Combat/FirstPromotionStatuses',
          DATA=ROOT / 'unity/Assets/Game/Resources/Combat/first-promotion-statuses.json',
          RECEIPT=receipt, ROOTS={}, graph_roots=roots, MAPPED=mapped,
          family='FirstPromotionStatuses', shader_prefix='FirstPromotionStatus_',
          reference_scripts={-210918009: dict(name='WwiseEventReference', native_asset=EVENT_ASSET,
              runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/WwiseEventReference.cs',
              receipt='assets/reference/profession-ui/audio-source-manifest.json')},
          runtime_receipts=['status-view-runtime-source-manifest.json',
                            'status-round-start-view-source-manifest.json',
                            'first-promotion-status-runtime-source-manifest.json'],
          generator=Path(__file__),
          limits=['Full original status hierarchy and reference closure are retained. '
                  'Animation and component event playback require the formal battle host and actual validation.'])
    result = json.loads(receipt.read_bytes())
    result['skill_scope'] = identity(scope_path)
    result['embedded_status_paths'] = embedded
    write_changed(receipt, json.dumps(result, ensure_ascii=False, indent=2) + '\n')


if __name__ == '__main__':
    main()
