"""Read verified official CDN supplements without making network requests."""
import hashlib
import json
import uuid


class CloudCache:
    def __init__(self, path, manifest=None):
        self.path = path
        self.manifest = manifest or path.name
        self.versions = {}
        inventory_path = path / 'inventory.json'
        if inventory_path.exists():
            inventory = json.loads(inventory_path.read_text(encoding='utf-8'))
            self.versions = {m['package']: m['version'] for m in inventory.get('manifests', [])}
        self.receipts = {}
        receipt_path = path / 'receipts.jsonl'
        self.receipt_digest = None
        if receipt_path.exists():
            # The downloader may still be appending its final line.
            raw = receipt_path.read_bytes()
            self.receipt_digest = hashlib.sha256(raw).hexdigest()
            complete = raw.decode('utf-8').rsplit('\n', 1)[0]
            for line in complete.splitlines():
                receipt = json.loads(line)
                self.receipts[receipt['file']] = receipt

    def index_cabs(self, bundles):
        from official_bundle_directory import cab_names

        index_path = self.path / 'cab-index.json'
        if index_path.exists():
            cached = json.loads(index_path.read_text(encoding='utf-8'))
            if cached.get('receipts_sha256') == self.receipt_digest:
                return cached['cabs']
        result = {}
        for bundle in bundles:
            key = 'DefaultPackage/' + bundle['file']
            receipt = self.receipts.get(key)
            path = self.path / key
            if not receipt or not path.exists() or not bundle['name'].endswith('.bundle'):
                continue
            if path.stat().st_size != bundle['size']:
                raise ValueError(f'Official CDN cache size mismatch: {key}')
            with path.open('rb') as stream:
                for name in cab_names(stream):
                    result[name.lower()] = {'bundle': bundle['name'], 'hash': bundle['hash']}
        if self.receipt_digest is not None:
            temporary = index_path.with_name(index_path.name + '.' + uuid.uuid4().hex + '.tmp')
            temporary.write_text(json.dumps({'receipts_sha256': self.receipt_digest, 'cabs': result}, indent=2) + '\n', encoding='utf-8')
            temporary.replace(index_path)
        return result

    def read(self, package, bundle):
        key = package + '/' + bundle['file']
        path = self.path / key
        if not path.exists():
            return None
        receipt = self.receipts.get(key)
        if not receipt or not receipt.get('url'):
            raise ValueError(f'Official CDN cache has no source receipt: {key}')
        data = path.read_bytes()
        if (len(data) != bundle['size'] or hashlib.md5(data).hexdigest() != bundle['hash']
                or hashlib.sha256(data).hexdigest() != receipt['sha256']
                or receipt['md5'] != bundle['hash'] or receipt['size'] != bundle['size']):
            raise ValueError(f'Official CDN cache failed verification: {key}')
        source = {'origin': 'official_cdn', 'url': receipt['url'],
                  'manifest': self.versions.get(package, self.manifest),
                  'size': len(data), 'sha256': receipt['sha256']}
        if receipt.get('source_manifest'):
            source['source_manifest'] = receipt['source_manifest']
        return data, source
