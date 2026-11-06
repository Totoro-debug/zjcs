"""Extract exact second-phase particle subtrees from the verified entity prefab."""
import re


def particle_subtrees(raw):
    raw = raw.replace('\r\n', '\n')
    blocks = list(re.finditer(r'(?ms)^--- !u!(\d+) &(\d+)[^\n]*\n.*?(?=^--- !u!|\Z)', raw))
    records = {int(m[2]): (int(m[1]), m[0]) for m in blocks}

    def reference(block, field):
        found = re.search(r'(?m)^  ' + field + r': \{fileID: (\d+)\}', block)
        assert found, field
        return int(found[1])

    transforms = {key: block for key, (kind, block) in records.items() if kind == 4}
    paths = {}

    def path(key):
        if key not in paths:
            block = transforms[key]
            name = re.search(r'(?m)^  m_Name: (.+)$', records[reference(block, 'm_GameObject')][1])[1]
            parent = reference(block, 'm_Father')
            paths[key] = (path(parent) + '/' if parent in transforms else '') + name
        return paths[key]

    roots = [key for key in transforms if re.search(
        r'/Stage2/.+/Eff_Npc_ChaosMonarch_(?:LeftWings02(?:_1)?|RightWings02(?:_1)?|Skill00(?:_0[1-4])?)$', path(key))]
    assert len(roots) == 9, roots
    kept_transforms = set()
    for key in transforms:
        if not any(path(key) == path(root) or path(key).startswith(path(root) + '/') for root in roots):
            continue
        while key in transforms and key not in kept_transforms:
            kept_transforms.add(key)
            key = reference(transforms[key], 'm_Father')
    kept_go = {reference(transforms[key], 'm_GameObject') for key in kept_transforms}
    selected = {key: (kind, block) for key, (kind, block) in records.items()
        if (kind == 1 and key in kept_go) or (kind in (4, 198, 199)
            and reference(block, 'm_GameObject') in kept_go)}
    assert sum(kind == 198 for kind, _ in selected.values()) == 33
    assert sum(kind == 199 for kind, _ in selected.values()) == 39

    def trim_list(block, field):
        pattern = r'(?ms)^  ' + field + r':\n(?:  -[^\n]*\n)*'
        match = re.search(pattern, block)
        if not match:
            assert re.search(r'(?m)^  ' + field + r': \[\]$', block), field
            return block
        lines = match[0].splitlines(keepends=True)
        kept = [line for line in lines[1:]
            if int(re.search(r'fileID: (\d+)', line)[1]) in selected]
        replacement = lines[0] + ''.join(kept) if kept else '  ' + field + ': []\n'
        return block[:match.start()] + replacement + block[match.end():]

    output = [raw[:blocks[0].start()]]
    for key, (kind, block) in selected.items():
        if kind == 1:
            block = trim_list(block, 'm_Component')
        elif kind == 4:
            block = trim_list(block, 'm_Children')
        # External GUID references are retained for the native dependency resolver.
        # Every nonzero local reference must still resolve inside this exact graph.
        for found in re.finditer(r'\{fileID: (\d+)\}', block):
            assert int(found[1]) == 0 or int(found[1]) in selected, (key, found[0])
        output.append(block)
    return ''.join(output), dict(
        root_paths=[path(key).split('/', 1)[1] for key in roots],
        source_object_ids=sorted(selected),
        particle_systems=33, particle_renderers=39,
        operation='Retain original second-phase particle objects, transforms and ancestors; '
            'remove component/child references outside the selected particle graph. '
            'Original fields and external native asset references are preserved.')
