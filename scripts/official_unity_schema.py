"""Read script fields from the pinned managed assemblies with strict byte checks."""
import hashlib
import os
from pathlib import Path
import subprocess
from official_apk import ROOT
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
from UnityPy.helpers.TypeTreeNode import TypeTreeNode
from UnityPy.helpers import TypeTreeHelper


class ScriptSchemas:
    def __init__(self, assembly_overrides=None, code_root=None):
        folder = ROOT / '.tools/unity-schema'
        folder.mkdir(exist_ok=True)
        source = ROOT / 'scripts/unity_schema_types.cs'
        target = folder / 'UnityEngine.CoreModule.dll'
        fingerprint = hashlib.sha256(source.read_bytes()+b'schema-v5').hexdigest()
        # PowerShell includes a C# compiler; a .NET SDK is not required.
        if not target.exists() or not (folder / 'source.sha256').exists() or (folder / 'source.sha256').read_text() != fingerprint:
            script = folder / 'build.ps1'
            script.write_text('''$ErrorActionPreference = 'Stop'
param()
'''.replace('param()\n','') + f'''
Add-Type -Path '{source.as_posix()}' -OutputAssembly '{(folder/'temporary.dll').as_posix()}'
Add-Type -Path '{(ROOT/'.tools/ilspycmd/tools/net10.0/any/Mono.Cecil.dll').as_posix()}'
$schemaAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('{(folder/'temporary.dll').as_posix()}')
$schemaAssembly.Name.Name = 'UnityEngine.CoreModule'
$schemaAssembly.MainModule.Name = 'UnityEngine.CoreModule.dll'
$schemaAssembly.Write('{target.as_posix()}')
$schemaAssembly.Name.Name = 'UnityEngine.TilemapModule'
$schemaAssembly.MainModule.Name = 'UnityEngine.TilemapModule.dll'
$schemaAssembly.Write('{(folder/'UnityEngine.TilemapModule.dll').as_posix()}')
$schemaAssembly.Name.Name = 'UnityEngine'
$schemaAssembly.MainModule.Name = 'UnityEngine.dll'
$schemaAssembly.Write('{(folder/'UnityEngine.dll').as_posix()}')
$schemaAssembly.Name.Name = 'UnityEngine.DirectorModule'
$schemaAssembly.MainModule.Name = 'UnityEngine.DirectorModule.dll'
$schemaAssembly.Write('{(folder/'UnityEngine.DirectorModule.dll').as_posix()}')
foreach ($schemaModule in @('UnityEngine.GridModule','SimpleAnimationComponent','UnityEngine.UIModule','UnityEngine.AnimationModule','UnityEngine.ParticleSystemModule','spine-unity','Unity.RenderPipelines.Universal.Runtime','UnityEngine.UI','Unity.TextMeshPro','AK.Wwise.Unity.API.WwiseTypes','Unity.Timeline','Cinemachine','spine-timeline','AK.Wwise.Unity.Timeline')) {{
    $schemaAssembly.Name.Name = $schemaModule
    $schemaAssembly.MainModule.Name = $schemaModule+'.dll'
    $schemaAssembly.Write('{folder.as_posix()}/'+$schemaModule+'.dll')
}}
$schemaAssembly.Dispose()
Remove-Item -LiteralPath '{(folder/'temporary.dll').as_posix()}'
''', encoding='utf8')
            subprocess.run(['pwsh','-NoProfile','-File',str(script)],check=True,stdout=subprocess.DEVNULL)
            (folder / 'source.sha256').write_text(fingerprint)
        self.generator = TypeTreeGenerator('2022.3.60f1', generator='AssetsTools')
        selected_code = Path(code_root) if code_root is not None else ROOT / '.tools/source-packages/official-code'
        self.generator.load_local_dll_folder(str(selected_code / 'managed/decoded'))
        self.generator.load_dll(target.read_bytes())
        self.generator.load_dll((folder/'UnityEngine.TilemapModule.dll').read_bytes())
        self.generator.load_dll((folder/'UnityEngine.dll').read_bytes())
        self.generator.load_dll((folder/'UnityEngine.DirectorModule.dll').read_bytes())
        overrides = dict(assembly_overrides or {})
        for module in ['UnityEngine.GridModule','SimpleAnimationComponent','UnityEngine.UIModule','UnityEngine.AnimationModule','UnityEngine.ParticleSystemModule','spine-unity','Unity.RenderPipelines.Universal.Runtime','UnityEngine.UI','Unity.TextMeshPro','AK.Wwise.Unity.API.WwiseTypes','Unity.Timeline','Cinemachine','spine-timeline','AK.Wwise.Unity.Timeline']:
            self.generator.load_dll(overrides.pop(module, folder/(module+'.dll')).read_bytes())
        for path in overrides.values():self.generator.load_dll(path.read_bytes())
        self.generator.load_dll(Path(os.environ.get('WINDIR','C:/Windows'),'Microsoft.NET/Framework64/v4.0.30319/mscorlib.dll').read_bytes())
        self.generator.load_dll(Path(os.environ.get('WINDIR','C:/Windows'),'Microsoft.NET/Framework64/v4.0.30319/System.Core.dll').read_bytes())
        dotnet = Path(subprocess.check_output(['where.exe','dotnet'],text=True).splitlines()[0]).parent
        runtime = sorted((dotnet/'shared/Microsoft.NETCore.App').iterdir(),key=lambda p:tuple(map(int,p.name.split('.'))))[-1]
        for name in ['System.Runtime','System.Private.CoreLib','System.Collections']:
            self.generator.load_dll((runtime/(name+'.dll')).read_bytes())
        self.cache = {}
        self.records = {}

    def tree(self, assembly, name):
        key = assembly+':'+name
        if key not in self.cache:
            try:nodes = self.generator.get_nodes(assembly, name)
            except Exception as ex:raise ValueError('Cannot build original schema '+key) from ex
            # Generator array containers use the element type as their name.
            # UnityPy interprets primitive/string containers as scalar values.
            generated = [dict(level=n.m_Level, type=('vector' if i+1<len(nodes) and
                nodes[i+1].m_Name=='Array' and nodes[i+1].m_Level==n.m_Level+1 and
                not (n.m_Type=='string' and nodes[i+3].m_Type=='char') else n.m_Type),
                name=n.m_Name, flags=n.m_MetaFlag | (0x4000 if n.m_Name=='m_Enabled' else 0)) for i,n in enumerate(nodes)]
            # AssetsTools appends a managed-reference registry for each class
            # in an inheritance chain. Unity writes one registry after all
            # base and derived fields; keep only the final generated registry.
            registries = [i for i,n in enumerate(generated) if n['level']==1 and n['type']=='ManagedReferencesRegistry']
            skipped = set()
            for start in registries[:-1]:
                end = next((i for i in range(start+1,len(generated)) if generated[i]['level']<=1),len(generated))
                skipped.update(range(start,end))
            self.cache[key] = [n for i,n in enumerate(generated) if i not in skipped]
        schema = TypeTreeNode.from_list([TypeTreeNode(n['level'],n['type'],n['name'],0,0,m_MetaFlag=n['flags']) for n in self.cache[key]])
        self.records[key] = self.cache[key]
        return schema

    def read(self, obj):
        basic = obj.read(check_read=False)
        script = basic.m_Script.read()
        name = (script.m_Namespace+'.' if script.m_Namespace else '')+script.m_ClassName
        for ref in obj.assets_file.ref_types or []:
            if ref.node is None and ref.m_ClassName and ref.m_AssemblyName:
                full=(ref.m_NameSpace+'.' if ref.m_NameSpace else '')+ref.m_ClassName
                ref.node=self.tree(ref.m_AssemblyName+'.dll',full)
        tree=self.tree(script.m_AssemblyName,name)
        # Stripped bundles omit ref-type metadata, but each managed reference
        # includes its real assembly/namespace/class in its serialized registry.
        # Resolve those exact types from the pinned managed assemblies.
        key=script.m_AssemblyName+':'+name
        if any(n['type']=='ReferencedObject' for n in self.cache[key]):
            original=TypeTreeHelper.get_ref_type_node;boost=TypeTreeHelper.read_typetree_boost
            def resolve(reference,assetsfile):
                typ=reference['type'];cls=typ['class']
                if not cls:return None
                full=(typ['ns']+'.' if typ['ns'] else '')+cls
                self.tree(typ['asm']+'.dll',full)
                # AssetsTools emits a MonoBehaviour header even for plain
                # serializable classes. Managed-reference payloads contain
                # only the class fields, with no Unity object header.
                raw=self.cache[typ['asm']+'.dll:'+full]
                fields=[];skip=False
                for field in raw:
                    if field['level']==1:skip=field['name'] in ['m_GameObject','m_Enabled','m_Script','m_Name']
                    if not skip:fields.append(field)
                return TypeTreeNode.from_list([TypeTreeNode(n['level'],n['type'],n['name'],0,0,m_MetaFlag=n['flags']) for n in fields])
            try:
                TypeTreeHelper.get_ref_type_node=resolve;TypeTreeHelper.read_typetree_boost=None
                result=obj.read_typetree(nodes=tree)
            finally:
                TypeTreeHelper.get_ref_type_node=original;TypeTreeHelper.read_typetree_boost=boost
        else:result=obj.read_typetree(nodes=tree)
        return result
