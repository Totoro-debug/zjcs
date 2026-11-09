"""Resolve full native battle graph sound references against selected original banks.

This records original sources; it does not replace Wwise actions or DSP with PCM.
"""
from collections import Counter
import io
import importlib.metadata
import json
import os
from pathlib import Path
import re

from map14_audio_source import AudioSource, sha
from prepare_battle_scene_appearance_runtime import write_changed


ROOT = Path(__file__).resolve().parents[1]
GRAPH = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
EVENT_ROOT = 'unity/Assets/Official/Combat/SkillGraphRuntime/Resources/Combat/SkillGraphs/Wwise/ScriptableObjects/Event/'
RECEIPT = ROOT / 'assets/reference/battle-animation/native-sound-sources-source-manifest.json'
MINIMUM = 'wwisebnk_assets_wwisedata_android_minimum_pck.rawfile'
LIBRARY = 'lib/arm64-v8a/libAkSoundEngine.so'


def identity(path):
    raw = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=len(raw), sha256=sha(raw))


def native_version(raw):
    """Read the actual ARM64 exported constant getters, including branch veneers."""
    from capstone import Cs, CS_ARCH_ARM64, CS_MODE_ARM
    from elftools.elf.elffile import ELFFile

    elf = ELFFile(io.BytesIO(raw))
    if elf['e_machine'] != 'EM_AARCH64':
        raise ValueError('Inspect changed original Wwise architecture')
    symbols = {s.name: s for s in elf.get_section_by_name('.dynsym').iter_symbols()}
    decoder = Cs(CS_ARCH_ARM64, CS_MODE_ARM)
    proofs = []

    def code(address, length):
        for segment in elf.iter_segments():
            if segment['p_vaddr'] <= address and address + length <= segment['p_vaddr'] + segment['p_filesz']:
                offset = segment['p_offset'] + address - segment['p_vaddr']
                return raw[offset:offset + length]
        raise ValueError('Original Wwise getter is outside file-backed ELF segments')

    def constant(name):
        address = symbols[name]['st_value']
        visited, steps, result = set(), [], None
        for _ in range(12):
            if address in visited:
                raise ValueError('Original Wwise version getter contains a branch cycle')
            visited.add(address)
            instruction = next(decoder.disasm(code(address, 4), address), None)
            if instruction is None:
                raise ValueError('Original Wwise getter could not be disassembled')
            mnemonic, operands = instruction.mnemonic, instruction.op_str
            steps.append(dict(address=address, bytes=bytes(instruction.bytes).hex(),
                              mnemonic=mnemonic, operands=operands))
            if mnemonic == 'b' and operands.startswith('#'):
                address = int(operands[1:], 0)
                continue
            if mnemonic == 'mov' and re.fullmatch(r'w0, #(?:0x[0-9a-f]+|\d+)', operands):
                result = int(operands.split('#')[1], 0)
            elif mnemonic == 'movk' and result is not None:
                match = re.fullmatch(r'w0, #(0x[0-9a-f]+|\d+)(?:, lsl #(\d+))?', operands)
                if match is None:
                    raise ValueError('Inspect changed original Wwise version constant')
                shift = int(match[2] or 0)
                result = (result & ~(0xffff << shift)) | (int(match[1], 0) << shift)
            elif mnemonic == 'ret' and not operands and result is not None:
                proofs.append(dict(symbol=name, value=result, instructions=steps))
                return result
            else:
                raise ValueError('Inspect non-constant original Wwise version getter: ' + name)
            address += 4
        raise ValueError('Original Wwise version getter exceeded inspection limit')

    major_minor = constant('CSharp_GetMajorMinorVersion')
    subminor_build = constant('CSharp_GetSubminorBuildVersion')
    version = '.'.join(str(x) for x in (major_minor >> 16, major_minor & 65535,
                                       subminor_build >> 16, subminor_build & 65535))
    return dict(version=version, entry=LIBRARY, bytes=len(raw), sha256=sha(raw), getters=proofs)


def event_references():
    graph = json.loads(GRAPH.read_bytes())
    if graph['manifest'] != '884_156692':
        raise ValueError('Inspect changed original battle graph version')
    records = [r for r in graph['outputs'] if r['path'].startswith(EVENT_ROOT) and r['path'].endswith('.asset')]
    if not records:
        raise ValueError('The complete native graph has no original Wwise references')
    references = []
    for record in sorted(records, key=lambda r: r['path']):
        path = ROOT / record['path']
        raw = path.read_bytes()
        if sha(raw) != record['sha256']:
            raise ValueError('Changed native Wwise event output: ' + record['path'])
        text = raw.decode('utf-8')
        fields = {}
        for key in ('objectName', 'id', 'guid'):
            matches = re.findall(r'^  ' + key + r': (.+)$', text, re.M)
            if len(matches) != 1:
                raise ValueError('Inspect changed native Wwise reference fields: ' + record['path'])
            fields[key] = matches[0].strip()
        if fields['guid'].upper() != path.stem.upper():
            raise ValueError('Native Wwise reference GUID does not match its asset identity')
        references.append(dict(id=int(fields['id']), name=fields['objectName'], guid=fields['guid'],
                               source=identity(path)))
    return references


def values(fields, name):
    return [value for key, value, _ in fields if key == name]


def main():
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    if work.parent != (ROOT / 'output/validation').resolve() or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Use an owned validation workspace')
    references = event_references()
    with AudioSource(expected_version='884_156692', work_directory=work) as audio:
        library = native_version(audio.archive.read(LIBRARY))
        library['source'] = dict(origin='apk_zip', entry=LIBRARY, apkSha256=audio.selected.apk_sha256)
        if library['version'] != '2022.1.18.8567':
            raise ValueError('Inspect changed original Wwise version: ' + library['version'])
        required = set()
        names_by_id = {}
        for reference in references:
            event_id, banks = audio.event_banks(reference['name'])
            if event_id != reference['id'] or not banks:
                raise ValueError('Unresolved official event mapping: ' + reference['name'])
            previous = names_by_id.setdefault(event_id, reference['name'])
            if previous != reference['name']:
                raise ValueError('Inspect original Wwise event ID/name collision')
            reference['banks'] = banks
            required.update(banks)
        for name in sorted(required):
            suffix = '_' + name.lower() + '_pck.rawfile'
            matches = [p for p in audio.inventory if p.endswith(suffix) or p.endswith('_zh_cn' + suffix)]
            if len(matches) != 1:
                raise ValueError('Ambiguous official event bank: ' + name)
            audio.objects(matches[0])
        audio.objects(MINIMUM)
        actions = {}
        for reference in references:
            event_id = reference['id']
            _, fields = audio.by_id[event_id]
            if values(fields, 'eHircType') != [4]:
                raise ValueError('Original event reference does not point at a Wwise Event')
            ordered = values(fields, 'ulActionID')
            for action_id in ordered:
                _, action = audio.by_id[action_id]
                if values(action, 'eHircType') != [3]:
                    raise ValueError('Original event action has a different HIRC type')
                if action_id not in actions:
                    # Keep all original properties; do not collapse event actions into a sound ID.
                    actions[action_id] = dict(id=action_id, fields=action,
                        sources=[{k: v for k, v in r.items() if k != 'base64'}
                                 for r in audio.object_payloads[action_id]])
            reference['actions'] = ordered
        types = Counter(values(fields, 'eHircType')[0] for _, fields in audio.by_id.values())
        proof = dict(manifest=audio.selected.version, graph_input=identity(GRAPH),
            native_library=library, events=references,
            actions=[actions[k] for k in sorted(actions)], packages=audio.receipts(),
            bank_object_types={str(k): v for k, v in sorted(types.items())},
            tools=[identity(Path(__file__)), identity(ROOT / 'scripts/map14_audio_source.py')],
            parser_versions={name: importlib.metadata.version(name) for name in ('capstone', 'pyelftools')},
            scope='Original native skill/hit graph event references and their ordered actions; complete mapped bank packages plus Minimum are hash-verified APK-first inputs.',
            limitations=['No Windows native audio runtime has been installed or validated.',
                'This source check does not prove transitive media/RTPC/switch/state closure or reproduce Wwise DSP.',
                'It does not activate the production battle sound host or modify original playable timing.'])
        write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
        print(json.dumps(dict(references=len(references), events=len(names_by_id), banks=len(required),
                              actions=len(actions), objects=sum(types.values()),
                              packages=len(audio.packages), version=library['version'])))


if __name__ == '__main__':
    main()
