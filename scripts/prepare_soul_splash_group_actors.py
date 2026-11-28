"""Install the current original group targets required by Soul Splash combat."""
from pathlib import Path

from official_apk import Apk, ROOT
from official_map_codec import unpack
from official_source import OfficialSource
from prepare_battle_skill_actor import build


def main():
    source = OfficialSource()
    cache = source.prepared_path / 'soul-splash-group-actors'
    locations = {}
    with Apk.current() as apk:
        for path, obj in apk.container('config_ec'):
            name = Path(path).name
            if not name.startswith('FightMonster_'):
                continue
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            try:
                rows = unpack(raw)[0]
            except Exception:
                rows = unpack(raw[2:])[0]
            for _, entity, _ in rows:
                if entity in (3205, 3401):
                    assert str(entity) not in locations, 'Duplicate current group actor'
                    locations[str(entity)] = 'Assets/Config/EC/' + name
    assert set(locations) == {'3205', '3401'}, 'Current group actor EC locations are missing'
    for entity in (3205, 3401):
        build(entity, cache=cache,
              destination=ROOT / f'unity/Assets/Official/Combat/SoulSplashGroupActor{entity}',
              resource_root=f'Combat/NativeMonsters/SkillActors/SoulSplashGroup{entity}',
              receipt_prefix=f'soul-splash-group-{entity}', generator=Path(__file__),
              verify_inherited_properties=False, reuse_verified_shader=False,
              reuse_verified_playable_shader=False,
              lifecycle_names={3401: {'mDiePlayable': 'playable_die_2'}},
              entity_paths={str(entity): locations[str(entity)]})


if __name__ == '__main__':
    main()
