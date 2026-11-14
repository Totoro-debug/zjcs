"""Stage the selected configuration in the native real-player menu validation."""
import json
from pathlib import Path

from official_apk import ROOT
from prepare_home_function_menu_player_validation import prepare as prepare_player, replace
from prepare_home_function_menu_configuration import build
from validation_workspace import validation_directory


def prepare():
    prepare_player()
    build()
    project=validation_directory()/'unity'
    previous=project/'Assets/HomeFunctionMenuPlayerValidation'
    folder=project/'Assets/HomeFunctionMenuConfigurationValidation'
    folder.mkdir(exist_ok=True)
    for name in ('HomeFunctionMenuPlayerRunner.cs','ControlledServices.cs','RealPlayerChecks.cs'):
        source=(previous/name).read_text('utf8').replace('HomeFunctionMenuPlayerRunner','HomeFunctionMenuConfigurationRunner')
        source=source.replace('HomeFunctionMenuPlayerValidation','HomeFunctionMenuConfigurationValidation')
        if name.endswith('Runner.cs'):
            source=replace(source,'        var cameraObject = new GameObject(',
                '        CheckConfiguration();\n        var cameraObject = new GameObject(')
            source=source.replace('HOME_FUNCTION_MENU_PLAYER_PASSED','HOME_FUNCTION_MENU_CONFIGURED_PLAYER_PASSED')
        elif name=='RealPlayerChecks.cs':
            source=replace(source,'host=new MenuServices{LocalModel=player};',
                'host=new MenuServices{LocalModel=player,LocalConfiguration=new Zjcs.Offline.HomeFunctionMenuUI.OfflineHomeFunctionMenuConfiguration()};')
        else:
            source=replace(source,'public GameObject Menu;',
                'public Zjcs.Offline.HomeFunctionMenuUI.OfflineHomeFunctionMenuConfiguration LocalConfiguration; public GameObject Menu;')
            source=replace(source,'public bool CheckWikiDisabled()=>true;',
                'public bool CheckWikiDisabled()=>LocalConfiguration==null;')
            source=replace(source,'public HomeMenuWikiInfo GetWikiInfo(string type)=>throw new Exception("disabled Wiki must short circuit");',
                'public HomeMenuWikiInfo GetWikiInfo(string type)=>LocalConfiguration.GetWikiInfo(type);')
            source=replace(source,'public IReadOnlyCollection<int> DisplayServerTimeRegionList=>Array.Empty<int>();',
                'public IReadOnlyCollection<int> DisplayServerTimeRegionList=>LocalConfiguration?.DisplayServerTimeRegionList??Array.Empty<int>();')
        (folder/name.replace('HomeFunctionMenuPlayer','HomeFunctionMenuConfiguration')).write_text(source,'utf8',newline='\n')
    fixture=ROOT/'scripts/fixtures/home_function_menu_configuration_validation.cs'
    (folder/'ConfigurationChecks.cs').write_text('#if UNITY_EDITOR\n'+fixture.read_text('utf8')+'\n#endif\n','utf8',newline='\n')
    assembly=json.loads((previous/'Zjcs.HomeFunctionMenuPlayerValidation.asmdef').read_bytes())
    assembly['name']='Zjcs.HomeFunctionMenuConfigurationValidation'
    (folder/'Zjcs.HomeFunctionMenuConfigurationValidation.asmdef').write_text(json.dumps(assembly,indent=2)+'\n','utf8',newline='\n')
    print('Staged original menu with selected configuration and real personal conditions')


if __name__=='__main__':
    prepare()
