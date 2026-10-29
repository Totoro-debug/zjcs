"""Explicit converter-only access to separately verified official counterparts.

The pinned Apk reader and its original cache remain unchanged. Newer bundles
are parsed in their own UnityPy environment and retain their actual provenance.
Only an exact serialized CAB name can satisfy a missing original dependency.
"""
import hashlib
import json
from pathlib import Path
from official_apk import Apk, ROOT
import UnityPy


class CounterpartAssets:
    def __init__(self, apk):
        self.apk=apk
        self.env=UnityPy.Environment()
        self.available={}
        self.loaded={}
        self.sources={}
        self.original_find=apk.env.find_file
        self.original_provenance=apk.provenance
        self.requests={}
        self.records=json.loads((ROOT/'assets/reference/official-map/missing-resource-classification.json').read_text(encoding='utf8'))['files']
        for record in self.records:
            raw=self.bytes(record)
            independent=UnityPy.load(Apk.decode(raw))
            for asset in independent.assets:self.available[asset.name.lower()]=record
        self.env.find_file=self.find
        apk.env.find_file=self.fallback
        apk.provenance=self.provenance

    @staticmethod
    def bytes(record):
        file=ROOT/'.tools/source-packages/cloud-research/missing-counterparts'/(record['counterpart_md5']+'.bundle')
        raw=file.read_bytes()
        if len(raw)!=record['counterpart_size'] or hashlib.md5(raw).hexdigest()!=record['counterpart_md5'] or hashlib.sha256(raw).hexdigest()!=record['counterpart_sha256']:
            raise ValueError('Official counterpart checksum mismatch: '+str(file))
        return raw

    def find(self,name,*args,**kwargs):
        key=name.rsplit('/',1)[-1].lower()
        if key in self.apk.cabs:return self.original_find(name,*args,**kwargs)
        cached=self.env.get_cab(key)
        if cached is not None:return cached
        if key not in self.available:return self.original_find(name,*args,**kwargs)
        record=self.available[key]
        loaded=self.env.load_file(Apk.decode(self.bytes(record)),name=record['bundle'])
        self.loaded[record['bundle']]=record
        for asset in loaded.files.values():
            if hasattr(asset,'objects'):
                self.sources[asset.name.lower()]=record
                # Validate dependency identities before exposing the replacement.
                for external in asset.externals:
                    target=external.path.rsplit('/',1)[-1].lower()
                    if target not in self.apk.cabs and target not in self.available and target not in ['unity default resources','unity_builtin_extra']:
                        raise FileNotFoundError('Counterpart dependency has no verified source: '+target)
        asset=self.env.get_cab(key)
        if asset is None:raise ValueError('Counterpart serialized CAB identity changed: '+key)
        return asset

    def fallback(self,name,*args,**kwargs):
        try:return self.original_find(name,*args,**kwargs)
        except FileNotFoundError:
            key=name.rsplit('/',1)[-1].lower()
            if key not in self.available:raise
            result=self.find(name,*args,**kwargs)
            self.requests[key]=self.available[key]['bundle']
            return result

    def provenance(self,obj):
        record=self.sources.get(obj.assets_file.name.lower())
        if record is None:return self.original_provenance(obj)
        return dict(bundle=record['bundle'],bundle_hash=record['counterpart_md5'],path_id=str(obj.path_id),
            origin='official_cdn_counterpart',manifest=record['counterpart_manifest'],url=record['counterpart_url'],
            bundle_size=record['counterpart_size'],bundle_sha256=record['counterpart_sha256'],
            original_missing_md5=record['missing_original_md5'])

    def receipt(self):
        return dict(original_manifest='854_156383',counterpart_manifest='864_156496',
            operation='Explicit separate UnityPy environment; exact serialized CAB identity and available dependency validation; original cache and expected hashes preserved',
            requested_missing_cabs=self.requests,bundles=list(self.loaded.values()))
