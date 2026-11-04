"""Isolate the shared scene's original partner movement and role view bindings."""
from official_apk import ROOT
from prepare_partner_native_full_skill_graph_validation import main as prepare


def main():
    roots = ['unity/Assets/Game/Resources/Combat/OfficialBattleModelCatalog.asset']
    roots += [path.relative_to(ROOT).as_posix() for path in
        (ROOT / 'unity/Assets/Official/Combat/CharacterDeath/Resources').rglob('*.prefab')]
    roots += [path.relative_to(ROOT).as_posix() for path in
        (ROOT / 'unity/Assets/Official/Combat/MovementEffects/Resources').rglob('*.prefab')]
    prepare(extra_roots=roots, extra_editors=[
        'unity/Assets/Game/Editor/PartnerBattlePlayables/OfficialPartnerNativeRoleSceneProbe.cs'])


if __name__ == '__main__':
    main()
