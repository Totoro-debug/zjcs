"""Prepare the native-particle integration scene for status playable components."""
import os
import json
from pathlib import Path
import shutil

from prepare_battle_movement_effect_validation import prepare, ROOT


def build(work):
    project = prepare(work)
    # The shared combat assembly also contains native shader implementations;
    # keep their assets alongside the metadata copied by the base helper.
    for path in (ROOT / 'unity/Assets/Game/Runtime/Official/Combat').rglob('*'):
        if path.is_file() and path.suffix in ('.shader', '.hlsl', '.cginc'):
            for source in (path, Path(str(path) + '.meta')):
                if not source.is_file(): continue
                target = project / source.relative_to(ROOT / 'unity')
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, target)
    for folder in ('Runtime/Official/StoryPlayableManager', 'Runtime/Official/BattleStatusPlayables',
                   'Runtime/Official/BattleGridItemPlayables', 'Runtime/Official/StorySounds',
                   'Editor/BattleStatusPlayables'):
        source = ROOT / 'unity/Assets/Game' / folder
        for path in [Path(str(source) + '.meta'), *source.rglob('*')]:
            if not path.is_file():
                continue
            target = project / path.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
    # The audio leaf consumes only the real event bridge and reference asset.
    # Keep these production sources in their actual assembly identity without
    # pulling unrelated profession UI dependencies into this particle fixture.
    audio = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'
    for name in ('OfficialWwiseEvent.cs', 'WwiseEventReference.cs'):
        for path in (audio / name, audio / (name + '.meta')):
            target = project / path.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
    (project / audio.relative_to(ROOT / 'unity') / 'Zjcs.Official.ProfessionUI.asmdef').write_text(
        json.dumps(dict(name='Zjcs.Official.ProfessionUI', references=[])), encoding='utf-8')
    source = ROOT / 'unity/Assets/Official/Combat/StatusOrbits'
    if source.is_dir():
        paths = [Path(str(source) + '.meta'), *source.rglob('*')]
        data = ROOT / 'unity/Assets/Game/Resources/Combat/status-orbits.json'
        paths += [data, Path(str(data) + '.meta')]
        for shader in (ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/Shaders').glob('StatusOrbit_*.shader'):
            paths += [shader, Path(str(shader) + '.meta')]
        for path in paths:
            if not path.is_file(): continue
            target = project / path.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
    source = ROOT / 'unity/Assets/Official/Combat/StatusBurns'
    paths = [Path(str(source) + '.meta'), *source.rglob('*')]
    data = ROOT / 'unity/Assets/Game/Resources/Combat/status-burns.json'
    paths += [data, Path(str(data) + '.meta')]
    for shader in (ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/Shaders').glob('StatusBurn_*.shader'):
        paths += [shader, Path(str(shader) + '.meta')]
    for path in paths:
        if not path.is_file(): continue
        target = project / path.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)
    editor = project / 'Assets/Game/Editor/BattleStatusPlayables'
    shutil.copy2(ROOT / 'scripts/fixtures/battle_grid_item_harness.cs', editor / 'BattleGridItemHarness.cs')
    (editor / 'BurningGridValidation.cs').write_text('''using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Zjcs.Combat.Official;
namespace Zjcs.Editor
{
    public static class BurningGridValidation
    {
        private static string Read(string name) => File.ReadAllText(Path.Combine(
            Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"), "unity/Assets/Official/Combat/" + name + ".json"));
        private static string Hash(string text)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
        public static void Run()
        {
            string skills = Read("skill-rules"), battle = Read("battle-rules"), rules = Read("rules");
            var catalog = FightOfficialCombatCatalog.Load(skills, Hash(skills), "854_156383", battle, Hash(battle), "854_156383");
            var items = FightOfficialSkillItemCatalog.Load(rules, Hash(rules), "854_156383");
            var formula = catalog.CreateDamageFormula(rules, Hash(rules), "854_156383");
            var checkpoint = BattleGridItemHarness.Run(catalog, items, formula);
            string json = JsonUtility.ToJson(checkpoint);
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            File.WriteAllText(Path.Combine(output, "grid-before.json"), json);
            var restored = JsonUtility.FromJson<FightBattleCheckpoint>(json);
            File.WriteAllText(Path.Combine(output, "grid-roundtrip.json"), JsonUtility.ToJson(restored));
            try { BattleGridItemHarness.Restore(restored, catalog, items, formula); }
            catch { File.WriteAllText(Path.Combine(output, "grid-failed.json"), JsonUtility.ToJson(restored)); throw; }
            BattleGridItemHarness.CreatorDeath(JsonUtility.FromJson<FightBattleCheckpoint>(json), catalog, items, formula);
            Debug.Log("Burning grid actual Unity compilation and JsonUtility checkpoint expiry/death passed.");
            OfficialBattleStatusPlayableAcceptance.Run();
        }
    }
}
''', encoding='utf-8')
    return project


if __name__ == '__main__':
    print(build(Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()))
