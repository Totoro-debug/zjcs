"""Prepare an owned graphics project using the production level-up owner/catalog."""
import json
import re
import shutil
from pathlib import Path
from official_apk import ROOT
from prepare_home_level_up_validation import prepare as prepare_native
from validation_assemblies import stage_runtime_modules
from validation_workspace import validation_directory


def prepare(work):
    project = prepare_native(work)
    stage_runtime_modules(project, ROOT/'unity/Assets/Game/Runtime', [
        'Zjcs.Offline.HomeLevelUpUI', 'Zjcs.Official.HomeLevelUpUI', 'Zjcs.Offline.ProfessionUI',
        'Zjcs.Offline.PartnersUI', 'Zjcs.Official.World.NativeText', 'Coffee.UIParticle'])
    by_guid = {}
    for meta in (ROOT/'unity/Assets').rglob('*.meta'):
        path = Path(str(meta)[:-5])
        if path.is_file():
            match = re.search(rb'(?m)^guid: ([0-9a-f]{32})',meta.read_bytes())
            if match: by_guid[match[1]] = path
    pending = [ROOT/'unity'/relative for relative in (
        'Assets/Game/Resources/Home/HomeLevelUpAssets.asset', 'Assets/Game/Resources/Home/HomeLevelUpImages.asset',
        'Assets/Game/Resources/Home/level-up-properties.json',
        'Assets/StreamingAssets/PartnerNpcShared/Config/Binary/level_prop.g.bytes')]
    visited = set()
    while pending:
        source = pending.pop()
        if source in visited or source.suffix == '.cs': continue
        visited.add(source); raw = source.read_bytes()
        target = project/source.relative_to(ROOT/'unity'); target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(source,target); shutil.copy2(Path(str(source)+'.meta'),Path(str(target)+'.meta'))
        if raw.startswith(b'%YAML'):
            pending.extend(by_guid[key] for key in set(re.findall(rb'guid: ([0-9a-f]{32})',raw)) if key in by_guid)
    shutil.copy2(ROOT/'scripts/fixtures/home_level_up_windows_validation.cs',project/'Assets/Editor/HomeLevelUpNativeValidation.cs')
    path = project/'Assets/Editor/Validation.asmdef'; definition=json.loads(path.read_bytes())
    definition['references'] += ['Zjcs.Offline.HomeLevelUpUI','Zjcs.Offline.Progression']
    path.write_text(json.dumps(definition,indent=2)+'\n','utf8',newline='\n')
    print('Staged production level-up resource catalog and window owner:', project,flush=True)


if __name__ == '__main__': prepare(validation_directory())
