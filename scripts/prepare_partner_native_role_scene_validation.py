"""Isolate the shared scene's original partner movement and role view bindings."""
import json
from official_apk import ROOT
from prepare_partner_native_full_skill_graph_validation import main as prepare
from validation_workspace import validation_directory


def main():
    roots = ['unity/Assets/Game/Resources/Combat/OfficialBattleModelCatalog.asset']
    roots += [path.relative_to(ROOT).as_posix() for path in
        (ROOT / 'unity/Assets/Official/Combat/CharacterDeath/Resources').rglob('*.prefab')]
    roots += [path.relative_to(ROOT).as_posix() for path in
        (ROOT / 'unity/Assets/Official/Combat/MovementEffects/Resources').rglob('*.prefab')]
    roots += [path.relative_to(ROOT).as_posix() for path in
        (ROOT / 'unity/Assets/Official/Combat/PartnerHpBars/Resources').rglob('*')
        if path.is_file() and path.suffix != '.meta']
    prepare(extra_roots=roots, extra_editors=[
        'unity/Assets/Game/Editor/PartnerBattlePlayables/OfficialPartnerNativeRoleSceneProbe.cs'])
    definition = validation_directory()/'native-battle-unity/Assets/Editor/Validation.asmdef'
    data = json.loads(definition.read_bytes())
    data['references'] = sorted(set(data['references']) | {
        'Zjcs.Official.BattleUnitUI', 'Zjcs.Official.World.Map13EntityRendering'})
    definition.write_text(json.dumps(data, indent=2)+'\n', encoding='utf-8')


if __name__ == '__main__':
    main()
