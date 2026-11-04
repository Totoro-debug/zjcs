"""Stage current resource catalogs alongside the shared original minimap fixture."""
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_mainline_minimap_offline_validation import main
from validation_workspace import validation_directory


if __name__ == '__main__':
    # The shared fixture already stages all resource-point assemblies through
    # the historical catalog. Each current catalog owns its native dependency
    # closure, so extending the fixture only requires these independent assets.
    main()
    project = validation_directory() / 'minimap-unity'
    source = ROOT / 'unity'
    for map_id in (10, 11, 12, 13, 14, 16):
        path = source / f'Assets/Official/World/Map/{map_id}/NativeMiniMapResourcesCurrent'
        shutil.copytree(path, project / path.relative_to(source), dirs_exist_ok=True)
        metadata = Path(str(path) + '.meta')
        shutil.copy2(metadata, project / metadata.relative_to(source))
    print('Staged six current original resource-point catalogs')
