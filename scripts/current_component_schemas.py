"""Strict field schemas from current official assemblies and AOT descriptors."""
import hashlib
import json
from pathlib import Path
import struct

from official_apk import ROOT
from official_metadata import Metadata
from official_source import OfficialSource
from official_unity_schema import ScriptSchemas
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator


class CurrentSchemas(ScriptSchemas):
    def __init__(self):
        source = OfficialSource()
        base = ROOT/'.tools/source-packages/current'/source.version
        code = json.loads((base/'code-manifest.json').read_bytes())
        raw = (base/'official-code/global-metadata.dat').read_bytes()
        if hashlib.sha256(raw).hexdigest() != code['metadata_sha256']:
            raise ValueError('Current official metadata changed')
        metadata = Metadata(raw)
        h = metadata.header
        self.engine_types = {}
        self.ambiguous_engine_types = set()
        for at in range(h[42], h[42]+h[43], 40):
            image = struct.unpack_from('<10i', raw, at)
            assembly = metadata.string(image[0])
            if not assembly.startswith('UnityEngine.'):
                continue
            for typ in metadata.types[image[2]:image[2]+image[3]]:
                if not typ['namespace'].startswith('UnityEngine'):
                    continue
                name = (typ['namespace']+'.' if typ['namespace'] else '')+typ['name']
                if name in self.engine_types:
                    self.ambiguous_engine_types.add(name)
                self.engine_types[name] = assembly
        self.generator = TypeTreeGenerator('2022.3.57f1', generator='AssetsTools')
        self.generator.load_local_dll_folder(str(base/'native-conversion/changed-inputs/Managed'))
        self.cache, self.records = {}, {}

    def tree(self, assembly, name):
        requested = assembly+':'+name
        if assembly.removesuffix('.dll') == 'UnityEngine' and name in self.ambiguous_engine_types:
            raise ValueError('Ambiguous original engine type: '+name)
        # Original MonoScripts can retain the legacy engine assembly identity.
        # Resolve it to the unique actual APK metadata image, never a guessed
        # substitute class or hand-written field layout.
        canonical = self.engine_types.get(name, assembly) if assembly.removesuffix('.dll') == 'UnityEngine' else assembly
        result = super().tree(canonical, name)
        if canonical != assembly:
            self.cache[requested] = self.cache[canonical+':'+name]
            self.records[requested] = self.records[canonical+':'+name]
        return result
