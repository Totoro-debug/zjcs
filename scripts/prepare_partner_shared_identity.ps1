param(
    [Parameter(Mandatory)][string]$OriginalDirectory,
    [Parameter(Mandatory)][string]$Destination,
    [string]$Cecil='D:/Tool/Unity/Editor/Data/Tools/ScriptUpdater/Unity.Cecil.dll'
)
$ErrorActionPreference='Stop'
Add-Type -Path $Cecil
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$names=@{
    'Console'='Zjcs.Partners884.Console'
    'Framework.FirstPass.Runtime'='Zjcs.Partners884.Framework.FirstPass.Runtime'
}
$resolver=[Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory($OriginalDirectory)
# IL2CPP metadata is used only to resolve attribute/enum signatures while
# writing the selected modules. Dummy assemblies are never runtime payloads.
$resolver.AddSearchDirectory((Join-Path $OriginalDirectory '../../aot-metadata/DummyDll'))
$reader=[Mono.Cecil.ReaderParameters]::new()
$reader.AssemblyResolver=$resolver
function Get-MethodFingerprint($module) {
    # Names and IL operands retain their original type/method names. Assembly
    # scopes are the only adaptation; no method body is edited or substituted.
    $lines=[System.Collections.Generic.List[string]]::new()
    foreach($type in $module.GetTypes()) {
        $lines.Add('T '+$type.FullName+' '+[int]$type.Attributes)
        foreach($field in $type.Fields) {$lines.Add('F '+$field.FullName+' '+[int]$field.Attributes)}
        foreach($method in $type.Methods) {
            if($type.FullName -eq 'Common.RawFileUtility' -and $method.Name -eq 'GetRawFilePath') {continue}
            $lines.Add('M '+$method.FullName+' '+[int]$method.Attributes+' '+[int]$method.ImplAttributes)
            if(-not $method.HasBody) {continue}
            $body=$method.Body
            $lines.Add('B '+$body.InitLocals+' '+$body.MaxStackSize)
            foreach($variable in $body.Variables) {$lines.Add('V '+$variable.VariableType.FullName)}
            foreach($instruction in $body.Instructions) {$lines.Add('I '+$instruction.ToString())}
            foreach($handler in $body.ExceptionHandlers) {
                $lines.Add('E '+$handler.HandlerType+' '+$handler.TryStart+' '+$handler.TryEnd+' '+$handler.HandlerStart+' '+$handler.HandlerEnd+' '+$handler.FilterStart+' '+$handler.CatchType)
            }
        }
    }
    $raw=[Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($raw)).ToLowerInvariant()
}
$records=@()
foreach($name in @('Framework.FirstPass.Runtime','Console')) {
    $source=Join-Path $OriginalDirectory ($name+'.dll')
    $assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($source,$reader)
    try {
        if($assembly.Name.Name -ne $name -or $assembly.Name.HasPublicKey) {throw "Unexpected original identity: $name"}
        if(@($assembly.MainModule.GetTypes() | Where-Object {$_.Namespace -eq 'Spine' -or $_.Namespace.StartsWith('Spine.')}).Count) {throw "Animation SDK is outside this migration: $name"}
        $before=Get-MethodFingerprint $assembly.MainModule
        $oldMvid=$assembly.MainModule.Mvid.ToString()
        $assembly.Name.Name=$names[$name]
        $assembly.MainModule.Name=$names[$name]+'.dll'
        foreach($reference in $assembly.MainModule.AssemblyReferences) {
            if($names.ContainsKey($reference.Name)) {$reference.Name=$names[$reference.Name]}
        }
        $platformBinding=$null
        if($name -eq 'Console') {
            # Replace only YooAsset's cached Android rawfile lookup. RootDir is
            # supplied by the offline owner and contains the bundled binary.
            # LevelPropBinaryReader and every numerical/parser method stay IL.
            $utility=$assembly.MainModule.GetType('Common.RawFileUtility')
            $method=@($utility.Methods | Where-Object Name -eq 'GetRawFilePath')
            if($method.Count -ne 1 -or $method[0].Parameters.Count -ne 1 -or $method[0].ReturnType.FullName -ne 'System.String') {throw 'Rawfile platform signature differs'}
            $method=$method[0]
            $originalInstructions=@($method.Body.Instructions | ForEach-Object ToString)
            $path=[Mono.Cecil.TypeReference]::new('System.IO','Path',$assembly.MainModule,$assembly.MainModule.TypeSystem.Object.Scope)
            $getFullPath=[Mono.Cecil.MethodReference]::new('GetFullPath',$assembly.MainModule.TypeSystem.String,$path)
            $getFullPath.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($assembly.MainModule.TypeSystem.String))
            $method.Body=[Mono.Cecil.Cil.MethodBody]::new($method)
            $il=$method.Body.GetILProcessor()
            $il.Emit([Mono.Cecil.Cil.OpCodes]::Ldarg_0)
            $il.Emit([Mono.Cecil.Cil.OpCodes]::Call,$getFullPath)
            $il.Emit([Mono.Cecil.Cil.OpCodes]::Ret)
            $platformBinding=@{method=$method.FullName;originalInstructions=$originalInstructions;windowsInstructions=@($method.Body.Instructions | ForEach-Object ToString);operation='Resolve bundled local absolute path; no YooAsset or network cache lookup.'}
        }
        $seed=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash+':'+$names[$name]+':windows-rawfile-v1'
        $digest=[Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($seed))
        $assembly.MainModule.Mvid=[guid]::new([byte[]]$digest[0..15])
        $target=Join-Path $Destination ($names[$name]+'.bytes')
        $parameters=[Mono.Cecil.WriterParameters]::new()
        $parameters.Timestamp=[uint32]0
        $assembly.Write($target,$parameters)
        $restored=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($target,$reader)
        try {
            $after=Get-MethodFingerprint $restored.MainModule
            if($before -ne $after) {throw "Original method/type fingerprint changed: $name"}
            if($null -ne $platformBinding) {
                $bound=@($restored.MainModule.GetType('Common.RawFileUtility').Methods | Where-Object Name -eq 'GetRawFilePath')[0]
                $platformBinding.windowsInstructions=@($bound.Body.Instructions | ForEach-Object ToString)
                if(($bound.Body.Instructions.OpCode.Code -join ',') -ne 'Ldarg_0,Call,Ret' -or $bound.Body.Instructions[1].Operand.FullName -ne 'System.String System.IO.Path::GetFullPath(System.String)') {throw 'Windows rawfile binding differs'}
            }
            $records+=@{source=$name;name=$names[$name];sourceModuleId=$oldMvid;moduleId=$restored.MainModule.Mvid.ToString();methodFingerprint=$after;platformBinding=$platformBinding;sha256=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()}
        } finally {$restored.Dispose()}
    } finally {$assembly.Dispose()}
}
$publisher=Join-Path $Destination 'LiteDB.bytes'
$packageAssembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($publisher,$reader)
try {
    if($packageAssembly.Name.FullName -ne 'LiteDB, Version=5.0.20.0, Culture=neutral, PublicKeyToken=4ee40123013c9f27') {throw 'Unexpected LiteDB publisher identity'}
    $records+=@{source='publisher LiteDB 5.0.20 netstandard2.0';name='LiteDB';moduleId=$packageAssembly.MainModule.Mvid.ToString();sha256=(Get-FileHash -LiteralPath $publisher -Algorithm SHA256).Hash.ToLowerInvariant()}
} finally {$packageAssembly.Dispose()}
@{operation='Retarget assembly identities for historical map coexistence and bind RawFileUtility to bundled Windows files. All other type/field/method instructions must retain the original fingerprint.';modules=$records} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $Destination 'identities.json') -Encoding utf8
