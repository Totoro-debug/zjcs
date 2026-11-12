"""Stage real native damage prefabs, scripts, shaders and a Play Mode fixture."""
import json
import os
from pathlib import Path
import shutil

from prepare_battle_movement_effect_validation import prepare, ROOT
from validation_assemblies import stage_runtime_modules


def main():
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    project = prepare(work)
    selected = stage_runtime_modules(project, ROOT / 'unity/Assets/Game/Runtime',
        ['Zjcs.Official.BattleDamageUI', 'Zjcs.Official.BattleRoundUI', 'Zjcs.Official.EntityLoading'])
    shutil.copytree(ROOT / 'unity/Assets/Spine', project / 'Assets/Spine', dirs_exist_ok=True)
    shutil.copytree(ROOT / 'unity/Assets/TextMesh Pro', project / 'Assets/TextMesh Pro', dirs_exist_ok=True)
    source = ROOT / 'unity/Assets/Official/Combat/DamageUI'
    shutil.copytree(source, project / source.relative_to(ROOT / 'unity'), dirs_exist_ok=True)
    # All converted resources are owned by this module, with external script
    # references supplied by the dependency closure above and Unity packages.
    shutil.rmtree(project / 'Assets/Game/Editor/BattleMovementEffects')
    editor = project / 'Assets/Editor'
    editor.mkdir(exist_ok=True)
    shutil.copy2(ROOT / 'scripts/fixtures/battle_damage_ui_native_validation.cs', editor / 'DamageUiNativeValidation.cs')
    shutil.copy2(ROOT / 'scripts/fixtures/battle_damage_ui_manager_checks.cs', editor / 'DamageUiManagerChecks.cs')
    (editor / 'DamageUiValidation.asmdef').write_text(json.dumps(dict(
        name='DamageUiValidation', includePlatforms=['Editor'], references=sorted(selected | {'Unity.TextMeshPro', 'Unity.ugui'})), indent=2), encoding='utf-8')
    print('Prepared native damage UI:', len(selected), 'assemblies;', project, flush=True)


if __name__ == '__main__':
    main()
