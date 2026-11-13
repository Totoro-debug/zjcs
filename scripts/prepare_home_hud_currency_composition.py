"""Compose the actual currency pool/windows into the complete original HomeUI."""
import argparse,hashlib,json,shutil
from pathlib import Path
from official_apk import ROOT
from prepare_home_auto_increase_runtime import CODE,metadata
from validation_workspace import validation_directory

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def build():
 inputs={};outputs={}
 def read(relative):
  path=ROOT/relative;inputs[relative]=sha(path);return json.loads(path.read_bytes()) if path.suffix=='.json' else path.read_bytes()
 for name in ('home-top-bar-native','home-hud-native','home-hud-frame-native','home-currency-windows-native','home-currency-windows-runtime','home-currency-actions-runtime'):
  proof=read('assets/reference/'+name+'-source-manifest.json');assert proof['manifest']=='884_156692'
  for group in ('installed_output_sha256','reused_output_sha256'):
   for relative,digest in proof.get(group,{}).items():assert sha(ROOT/relative)==digest,relative
 for relative in ('client-source/TopBarManager.cs','client-source/TopBarHandler.cs','client-source/TopBarItem.cs','client-source/MePlayerItemUI.cs','common-source/Common/LanguageUtils.cs','framework-firstpass-source/Framework.UI/WindowContainer.cs'):
  read((CODE/relative).relative_to(ROOT).as_posix())
 for relative in ('unity/Assets/Game/Runtime/Official/HomeHudUI/HomeTopBarManager.cs','unity/Assets/Game/Runtime/Offline/HomeHudUI/OfflineHomeHud.cs','unity/Assets/Game/Runtime/Offline/HomeUI/OfflineHomeCurrencyPlayer.cs','unity/Assets/Game/Runtime/Offline/HomeCurrencyUI/OfflineHomeCurrencyActions.cs','scripts/fixtures/home_hud_currency_validation.cs','scripts/fixtures/home_hud_bindings_native_validation.cs','scripts/prepare_home_hud_bindings.py','scripts/prepare_home_task_trace_runtime.py'):
  read(relative)
 for name in ('OfflineHomeTopBar.cs','OfflineHomeTopBarPools.cs'):
  path=ROOT/'unity/Assets/Game/Runtime/Offline/HomeUI'/name
  meta=metadata(path)
  for output in (path,meta):outputs[output.relative_to(ROOT).as_posix()]=sha(output)
 path=ROOT/'unity/Assets/Game/Runtime/Offline/HomeCurrencyUI/OfflineHomeCurrencyTopBarHost.cs'
 for output in (path,metadata(path)):outputs[output.relative_to(ROOT).as_posix()]=sha(output)
 proof={'manifest':'884_156692','generator_sha256':sha(Path(__file__)),'inputs':inputs,'outputs':outputs,
  'scope':'Original top-bar manager/pools can bind the existing handler in complete HomeUI without instantiating another parent. Actual profile, selected native currency actions/windows, original HomeUI show/hide and shared pool composition are verified separately from the still pending formal HomeFeatureRuntime.'}
 (ROOT/'assets/reference/home-hud-currency-composition-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
 return proof

def stage(project,proof):
 from prepare_home_task_trace_runtime import stage as stage_tasks
 from prepare_home_hud_bindings import build as build_hud,stage as stage_hud
 assert project.resolve().is_relative_to(validation_directory().resolve())
 stage_tasks(project);stage_hud(project,build_hud())
 actions=json.loads((ROOT/'assets/reference/home-currency-actions-runtime-source-manifest.json').read_bytes())
 for relative,digest in {**actions['outputs'],**proof['outputs']}.items():
  assert sha(ROOT/relative)==digest
  target=project/(ROOT/relative).relative_to(ROOT/'unity');target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/relative,target)
 folder=project/'Assets/HomeCurrencyWindowsNativeValidation'
 shutil.copy2(ROOT/'scripts/fixtures/home_hud_currency_validation.cs',folder/'HomeHudCurrencyValidation.cs')
 path=folder/'HomeCurrencyWindowsNativeValidation.cs';text=path.read_text('utf8');marker='        yield return ExerciseComposedServices();';assert text.count(marker)==1
 path.write_text(text.replace(marker,marker+'\n        yield return ExerciseHomeHudCurrency();'),'utf8',newline='\n')
 path=folder/'HomeCurrencyWindowsNativeValidation.asmdef';definition=json.loads(path.read_bytes())
 definition['references']=list(dict.fromkeys(definition['references']+json.loads((ROOT/'unity/Assets/Game/Runtime/Offline/HomeHudUI/Zjcs.Offline.HomeHudUI.asmdef').read_bytes())['references']+['Zjcs.Offline.HomeHudUI']))
 path.write_text(json.dumps(definition,indent=2)+'\n','utf8',newline='\n')
 print('Staged actual profile currency composition in complete original HomeUI',flush=True)

if __name__=='__main__':
 parser=argparse.ArgumentParser(__doc__);parser.add_argument('--project',type=Path);args=parser.parse_args();proof=build()
 if args.project:stage(args.project,proof)
