"""Stage selected furniture integration and a native pointer-driven repair check."""
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_home_conditions_validation import main as prepare_conditions
from validation_workspace import validation_directory


def main():
    prepare_conditions()
    work=validation_directory()
    project=work/'unity'
    paths=(
        'Assets/Game/Runtime/Offline/Home/OfficialHomeRules.cs',
        'Assets/Game/Runtime/Offline/Home/OfflineHomeProgression.cs',
        'Assets/Game/Runtime/Offline/HomeUI/OfflineCurrentHomeFurniture.cs',
        'Assets/Game/Runtime/Offline/HomeUI/OfflineCurrentHomeFurniture.cs.meta',
        'Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs',
        'Assets/Game/Runtime/Presentation/Home/Zjcs.Presentation.Home.asmdef',
    )
    for relative in paths:
        shutil.copy2(ROOT/'unity'/relative,project/relative)
    editor=project/'Assets/Editor/HomeFurniture'
    editor.mkdir(exist_ok=True)
    shutil.copy2(ROOT/'scripts/fixtures/home_furniture_validation.cs',editor/'HomeFurnitureValidation.cs')
    shutil.copy2(ROOT/'scripts/fixtures/home_furniture_native_state.cs',editor/'HomeFurnitureNativeState.cs')
    shutil.copy2(ROOT/'scripts/fixtures/home_furniture_host_validation.cs',editor/'HomeFurnitureHostValidation.cs')
    references=['Zjcs.Official.HomeConditions','Zjcs.Official.Partners','Zjcs.Offline.Home',
        'Zjcs.Offline.HomeUI','Zjcs.Offline.Progression','Zjcs.Offline.State','Zjcs.Editor',
        'Zjcs.Presentation.Home','Zjcs.Offline.CharacterCreation',
        'Zjcs.Official.HomeUI','Zjcs.Offline.ProfessionUI','Zjcs.Official.ProfessionUI',
        'Zjcs.Official.World.NativeLabelUI',
        'Zjcs.Official.WorldUI','Zjcs.Official.World.NativeText','Zjcs.Offline.PartnersUI',
        'Zjcs.Official.PartnerFriendshipDialogUI','Zjcs.Presentation.World',
        'Unity.TextMeshPro','Unity.RenderPipelines.Core.Runtime','Unity.RenderPipelines.Universal.Runtime',
        'spine-unity','spine-csharp']
    (editor/'HomeFurnitureValidation.asmdef').write_text(json.dumps(dict(name='HomeFurnitureValidation',
        includePlatforms=['Editor'],references=references),indent=2)+'\n','utf8',newline='\n')
    # Reuse the already-verified native fixture's canvas, raycasts, captures and
    # animation waits. Replace its response stub with the real durable service.
    source=(ROOT/'scripts/fixtures/home_repair_native_validation.cs').read_text('utf8')
    source=source.replace('HomeRepairNativeValidation','HomeFurnitureNativeValidation').replace('HomeRepairNativeRunner','HomeFurnitureNativeRunner')
    replacements={
        'EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);':
            'Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");\n        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);',
        'private bool hasCost;':'private HomeFurnitureNativeState state;',
        'public bool HasRepairCost(EFurniture furniture) => hasCost;':'public bool HasRepairCost(EFurniture furniture) => state.HasCost((int)furniture);',
        'public void Repair(EFurniture furniture, Action onSuccess) { repairs++; onSuccess(); }':
            'public void Repair(EFurniture furniture, Action onSuccess) { state.Repair((int)furniture); repairs++; onSuccess(); }',
        'AudioListener.volume = 0;':'AudioListener.volume = 0; HomeFurnitureValidation.Check(); state = new HomeFurnitureNativeState();',
        'events = new GameObject("InternalSimulatedPointer", typeof(EventSystem)).GetComponent<EventSystem>();':
            'Require(Object.FindObjectsOfType<EventSystem>().Length == 0, "isolated window fixture owns the only event system");\n        events = new GameObject("InternalSimulatedPointer", typeof(EventSystem)).GetComponent<EventSystem>();',
        'Click(repair.RepairBtn);\n        Require(messages == 1 && repairs == 0 && activeKey == "repair", "insufficient cost preserves repair window");\n        hasCost = true;':
            'Require(state.CanRepairBed, "current official bed repair condition before pointer input");',
        'Require(repairs == 1 && activeKey == "repair_notify", "success opens original notify before closing repair");':
            'Require(repairs == 1 && activeKey == "repair_notify", "success opens original notify before closing repair"); state.VerifyReopen();',
        'var source = Resources.Load<Font>("HomeValidationFont");':
            'var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Official/World/Map/13/NativeText/FZY4K.ttf");',
        'repair-bed.png':'furniture-current-bed.png',
        'repair-notify-bed.png':'furniture-current-notify.png',
        'HOME_REPAIR_NATIVE_PASSED: native bindings, simulated pointer, insufficient cost, successful response ordering, notify, mask and popup clips; isolated window scope.':
            'HOME_FURNITURE_NATIVE_PASSED: current conditions, real repair transaction, disk reopen, simulated pointer and native repair/notify animation; isolated window scope.',
    }
    for before,after in replacements.items():
        if before not in source:raise ValueError('Native furniture validation source drift: '+before[:80])
        source=source.replace(before,after)
    (editor/'HomeFurnitureNativeValidation.cs').write_text(source,'utf8',newline='\n')
    print('Staged current furniture and native transaction validation')


if __name__=='__main__':main()
