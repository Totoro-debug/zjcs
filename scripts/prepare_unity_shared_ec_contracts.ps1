param(
    [Parameter(Mandatory)][string]$OriginalDirectory,
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$Destination,
    [Parameter(Mandatory)][string]$IdentitySeed,
    [string]$Cecil='D:/Tool/Unity/Editor/Data/Tools/ScriptUpdater/Unity.Cecil.dll'
)
$ErrorActionPreference='Stop'
Add-Type -Path $Cecil
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
function Get-ModuleId([string]$name) {
    $bytes=[System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($IdentitySeed+':'+$name))
    [byte[]]$guidBytes=$bytes[0..15]
    return [guid]::new($guidBytes)
}
function Write-Module($assembly,[string]$name) {
    $assembly.MainModule.Mvid=Get-ModuleId $name
    $parameters=[Mono.Cecil.WriterParameters]::new()
    $parameters.Timestamp=[uint32]0
    $assembly.Write((Join-Path $Destination ($name+'.dll')),$parameters)
}
$version=[Version]::new(0,0,0,0)
$contract=[Mono.Cecil.AssemblyDefinition]::CreateAssembly([Mono.Cecil.AssemblyNameDefinition]::new('GFrameCore',$version),'GFrameCore',[Mono.Cecil.ModuleKind]::Dll)
$type=[Mono.Cecil.TypeDefinition]::new('GFrameCore','IGSerializable',([Mono.Cecil.TypeAttributes]::Public -bor [Mono.Cecil.TypeAttributes]::Interface -bor [Mono.Cecil.TypeAttributes]::Abstract))
$method=[Mono.Cecil.MethodDefinition]::new('GetOpCode',([Mono.Cecil.MethodAttributes]::Public -bor [Mono.Cecil.MethodAttributes]::Abstract -bor [Mono.Cecil.MethodAttributes]::Virtual -bor [Mono.Cecil.MethodAttributes]::NewSlot -bor [Mono.Cecil.MethodAttributes]::HideBySig),$contract.MainModule.TypeSystem.UInt16)
$type.Methods.Add($method); $contract.MainModule.Types.Add($type)
Write-Module $contract 'GFrameCore'; $contract.Dispose()
# Only the game's logging ABI is adapted. Preserve all seven parameter lists
# and forward every argument to the corresponding pinned public ZLogger API.
$original=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $OriginalDirectory 'Console.dll'))
$package=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $PackageDirectory 'ZLogger.dll'))
$adapter=[Mono.Cecil.AssemblyDefinition]::CreateAssembly([Mono.Cecil.AssemblyNameDefinition]::new('GFrameCore.Extensions.ZLogger',$version),'GFrameCore.Extensions.ZLogger',[Mono.Cecil.ModuleKind]::Dll)
$wrapper=[Mono.Cecil.TypeDefinition]::new('GFrameCore.Extensions.ZLogger','GZLoggerExtensions',([Mono.Cecil.TypeAttributes]::Public -bor [Mono.Cecil.TypeAttributes]::Abstract -bor [Mono.Cecil.TypeAttributes]::Sealed),$adapter.MainModule.TypeSystem.Object)
$adapter.MainModule.Types.Add($wrapper)
$names=@{Debug='ZLogDebug';Error='ZLogError';Info='ZLogInformation';Log='ZLog';Trace='ZLogTrace';Warn='ZLogWarning'}
$targets=($package.MainModule.Types | Where-Object FullName -eq 'ZLogger.ZLoggerExtensions').Methods
$calls=@()
foreach($reference in $original.MainModule.GetMemberReferences() | Where-Object {$_.DeclaringType.Scope.Name -eq 'GFrameCore.Extensions.ZLogger'}) {
    $key=($reference.Parameters | ForEach-Object {$_.ParameterType.FullName}) -join '|'
    $matches=@($targets | Where-Object {$_.Name -eq $names[$reference.Name] -and (($_.Parameters | ForEach-Object {$_.ParameterType.FullName}) -join '|') -eq $key})
    if($matches.Count -ne 1){throw "Original logging ABI is not supported: $($reference.FullName)"}
    $target=$matches[0]
    $m=[Mono.Cecil.MethodDefinition]::new($reference.Name,([Mono.Cecil.MethodAttributes]::Public -bor [Mono.Cecil.MethodAttributes]::Static -bor [Mono.Cecil.MethodAttributes]::HideBySig),$adapter.MainModule.TypeSystem.Void)
    foreach($p in $target.Parameters){$m.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($p.Name,$p.Attributes,$adapter.MainModule.ImportReference($p.ParameterType)))}
    $il=$m.Body.GetILProcessor()
    foreach($p in $m.Parameters){$il.Emit([Mono.Cecil.Cil.OpCodes]::Ldarg,$p)}
    $il.Emit([Mono.Cecil.Cil.OpCodes]::Call,$adapter.MainModule.ImportReference($target)); $il.Emit([Mono.Cecil.Cil.OpCodes]::Ret)
    $wrapper.Methods.Add($m)
    $calls+=@{source=$reference.FullName;target=$target.FullName}
}
if($calls.Count -ne 7){throw "Original logging ABI count differs: $($calls.Count)"}
Write-Module $adapter 'GFrameCore.Extensions.ZLogger'
$adapter.Dispose(); $package.Dispose(); $original.Dispose()
$modules=@()
foreach($file in @('Console.dll','Luban.dll','Framework.FirstPass.Runtime.dll')) {
    $a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $OriginalDirectory $file))
    # Inspect type identities only, never animation SDK method bodies.
    if(@($a.MainModule.Types | Where-Object {$_.Namespace -eq 'Spine' -or $_.Namespace.StartsWith('Spine.')}).Count -ne 0){throw "Original payload includes an animation SDK implementation: $file"}
    $modules+=@{name=$a.Name.Name;moduleId=$a.MainModule.Mvid.ToString();file=$file;origin='original'}
    $a.Dispose()
}
foreach($entry in @(@{folder=$PackageDirectory;file='Microsoft.Extensions.Logging.Abstractions.dll';origin='package'},@{folder=$PackageDirectory;file='ZLogger.dll';origin='package'},@{folder=$Destination;file='GFrameCore.dll';origin='contract'},@{folder=$Destination;file='GFrameCore.Extensions.ZLogger.dll';origin='adapter'})) {
    $a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $entry.folder $entry.file))
    $modules+=@{name=$a.Name.Name;moduleId=$a.MainModule.Mvid.ToString();file=$entry.file;origin=$entry.origin}
    $a.Dispose()
}
@{modules=$modules;loggingCalls=$calls} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Destination 'contracts.json') -Encoding utf8
