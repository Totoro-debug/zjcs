"""Assemble runtime code and implicit inputs for isolated Unity validation projects."""
import json
from pathlib import Path
import re
import shutil


def stage_runtime_modules(project, runtime, roots, *, allowed_external=None, excluded=()):
    """Copy a dependency closure and return its runtime assembly names.

    None preserves fixtures that delegate external dependencies to their packages.
    An allowlist rejects unknown externals before any files are copied. Exclusions
    take precedence over runtime definitions (used by the startup package fixture).
    Asset/prefab roots, Unity packages and Editor fixtures remain with the caller.
    """
    modules, selected, external = resolve_runtime_modules(runtime, roots, excluded=excluded)
    if allowed_external is not None and external - set(allowed_external):
        raise ValueError("Unresolved validation assemblies: " + repr(sorted(external - set(allowed_external))))
    copy_modules(project, runtime, modules, selected)
    return selected


def resolve_runtime_modules(runtime, roots, *, excluded=()):
    """Resolve code roots; callers with external payloads retain their own adapter."""
    modules = {json.loads(path.read_bytes())["name"]: path
               for path in runtime.rglob("*.asmdef")}
    selected, external, pending = set(), set(), list(roots)
    while pending:
        name = pending.pop()
        if name in excluded or name in selected:
            continue
        if name not in modules:
            external.add(name)
            continue
        selected.add(name)
        pending.extend(json.loads(modules[name].read_bytes()).get("references", []))
    return modules, selected, external


def copy_modules(project, runtime, modules, selected):
    module_guids = {re.search(r'(?m)^guid: ([0-9a-f]{32})', Path(str(path) + '.meta').read_text('utf8'))[1]: name
                    for name, path in modules.items()}
    folders = [modules[name].parent for name in selected]
    for reference in runtime.rglob('*.asmref'):
        target = json.loads(reference.read_bytes())['reference']
        target = module_guids.get(target[5:]) if target.startswith('GUID:') else target
        if target in selected:
            folders.append(reference.parent)
    for folder in folders:
        for source in folder.rglob('*'):
            if not source.is_file() or source.suffix not in ('.cs', '.meta', '.asmdef', '.asmref'):
                continue
            parent = source.parent
            while parent != folder and not list(parent.glob('*.asmdef')) and not list(parent.glob('*.asmref')):
                parent = parent.parent
            if parent != folder:
                continue
            if source.suffix == '.meta' and Path(str(source)[:-5]).suffix not in ('.cs', '.asmdef', '.asmref'):
                continue
            target = project / 'Assets/Game/Runtime' / source.relative_to(runtime)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
    # These assemblies load their local inputs at runtime; asmdef references
    # alone do not include the native plug-in or Resources configuration.
    inputs = []
    if 'Zjcs.Official.SharedSerialization' in selected:
        inputs.append('Assets/Official/World/Map/14/SharedSerialization/MessagePack.dll')
    if 'Zjcs.Offline.Progression' in selected:
        inputs.append('Assets/Game/Resources/Home/item-auto-increase.json')
        inputs.append('Assets/Game/Resources/Map14/world-alone-preparation.json')
        inputs.append('Assets/Game/Resources/Map16/world-alone-configuration.json')
    for relative in inputs:
        for suffix in ('', '.meta'):
            source = runtime.parents[2] / (relative + suffix)
            target = project / (relative + suffix)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
