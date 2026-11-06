"""Record the common formal map host's original-world integration."""
import json
from pathlib import Path

from official_apk import ROOT
from record_unity_map14_native_host import record


def main():
    directory = ROOT / 'assets/reference/official-map'
    source = ROOT / '.tools/source-packages/current/884_156692/official-code/common-source/Common'
    outputs = [ROOT / 'unity/Assets/Game/Runtime/Presentation/World/Mainline' / name for name in (
        'OfficialMainlineMapRuntime.SharedWorld.cs', 'OfficialMainlineMapRuntime.SharedWorld.cs.meta',
        'OfficialMainlineMapRuntime.NativeMap14.cs')]
    outputs += [ROOT / 'unity/Assets/Game/Editor/Map13Mainline' / name for name in (
        'OfficialMap13MainlinePlayAcceptance.cs', 'OfficialMap13MainlinePlayAcceptance.SharedWorlds.cs',
        'OfficialMap13MainlinePlayAcceptance.SharedWorlds.cs.meta')]
    outputs += [ROOT / 'unity/Assets/Game/Runtime/Presentation/World/OfficialMap10UiCatalog.cs']
    receipt = dict(
        format=1,
        inputs=[record(source / (name + '.cs')) for name in ('PlayerMap', 'MapPartInfoBase')],
        dependencies=[record(directory / name) for name in (
            'mainline-shared-world-data-source-manifest.json',
            'mainline-shared-client-configuration-source-manifest.json',
            'mainline-shared-map-configuration-source-manifest.json',
            'map14-shared-ec-runtime-source-manifest.json')],
        tools=[record(Path(__file__).resolve(), 'LF')],
        outputs=[record(path, 'LF') for path in outputs],
        bindings=[dict(path='unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.cs',
                       startup='InitializeView -> InitializeMainlineSharedWorld',
                       shutdown='DisposeRuntime -> DisposeMainlineSharedWorld')],
        operation='Move original client-world loading, saved-player projection, entity/region/character synchronization and disposal from the Map14 terrain branch into the common formal map host. Select original data with the host map identity and preserve the local private/public container split. Query the unchanged original RegionStatusSupported and MapDefaultRegionStatus for single-status maps, as consumed by original PlayerMap. Terrain14 borrows the running common world. Dispose original scopes on failed/aborted startup and after map presentation closes. Rebuild transient UI sprite/panel lookups after Unity Play reload retains the parsed payload. No new gameplay formula, art or original serialized hierarchy is generated.',
        source_versions=dict(host='offline integration', official_consumer='884_156692',
                             retained_shared_IL='871_156598; see dependency receipts; not whole-assembly equivalence'),
        verification_entry='Zjcs.Editor.Map13Mainline.OfficialMap13MainlinePlayAcceptance.RunSharedWorlds',
        verification_scope=['actual formal Maps10-15 startup', 'original player/container/LoID identity',
                            'saved placed-entity stage and grid projection', 'original supported/default region status',
                            'simulated map pointer movement', 'cold save reopen', 'interrupted startup cleanup'],
        limitations=['Map16 has the common integration path but its formal runtime entry remains unfinished and disabled.',
                     'Full original character prefab, native minimap root/HUD and Windows Player publication remain unfinished.',
                     'Generated exploration entities remain owned by the existing exploration layer; this change does not add their original-container spawn executor.'])
    target = directory / 'mainline-shared-world-host-source-manifest.json'
    target.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Recorded common formal shared-world host:', len(outputs), 'code/metadata outputs', flush=True)


if __name__ == '__main__':
    main()
