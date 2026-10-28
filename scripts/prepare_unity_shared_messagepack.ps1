# Restore only the shared serialization ABI and native raw-single encoding.
# Console IL and its generated formatters remain unchanged.
param(
 [Parameter(Mandatory)][string]$OriginalConsole,
 [Parameter(Mandatory)][string]$BaseContract,
 [Parameter(Mandatory)][string]$PublisherDll,
 [Parameter(Mandatory)][string]$Destination,
 [Parameter(Mandatory)][string]$IdentitySeed,
 [string]$Cecil='D:/Tool/Unity/Editor/Data/Tools/ScriptUpdater/Unity.Cecil.dll'
)
$ErrorActionPreference='Stop'
Add-Type -Path $Cecil
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($PublisherDll)
$binary=$a.MainModule.GetType('MessagePack.MessagePackBinary')
$write=$binary.Methods | Where-Object {$_.Name -eq 'WriteSingle' -and $_.Parameters.Count -eq 3}
$instructions=$write.Body.Instructions
foreach($i in 4..8){$instructions[$i].OpCode=[Mono.Cecil.Cil.OpCodes]::Nop;$instructions[$i].Operand=$null}
foreach($i in $instructions){
 if($i.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_I4_5){$i.OpCode=[Mono.Cecil.Cil.OpCodes]::Ldc_I4_4}
 elseif($i.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_I4_1){$i.OpCode=[Mono.Cecil.Cil.OpCodes]::Ldc_I4_0}
 elseif($i.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_I4_2){$i.OpCode=[Mono.Cecil.Cil.OpCodes]::Ldc_I4_1}
 elseif($i.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_I4_3){$i.OpCode=[Mono.Cecil.Cil.OpCodes]::Ldc_I4_2}
 elseif($i.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_I4_4){$i.OpCode=[Mono.Cecil.Cil.OpCodes]::Ldc_I4_3}
}
$read=$binary.Methods | Where-Object {$_.Name -eq 'ReadSingle' -and $_.Parameters.Count -eq 3}
$instructions=$read.Body.Instructions
$instructions[1].OpCode=[Mono.Cecil.Cil.OpCodes]::Ldc_I4;$instructions[1].Operand=[int]202
foreach($i in 2..3){$instructions[$i].OpCode=[Mono.Cecil.Cil.OpCodes]::Nop;$instructions[$i].Operand=$null}
$decode=$a.MainModule.GetType('MessagePack.Decoders.Float32Single').Methods | Where-Object Name -eq Read
foreach($i in $decode.Body.Instructions){
 if($i.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_I4_5){$i.OpCode=[Mono.Cecil.Cil.OpCodes]::Ldc_I4_4}
 elseif($i.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_I4_1){$i.OpCode=[Mono.Cecil.Cil.OpCodes]::Ldc_I4_0}
}
$publisherSeed=[Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($IdentitySeed+':MessagePack-raw-single'))
$a.MainModule.Mvid=[guid]::new([byte[]]$publisherSeed[0..15])
$writer=[Mono.Cecil.WriterParameters]::new();$writer.Timestamp=[uint32]0
$a.Write((Join-Path $Destination 'MessagePack.dll'),$writer);$a.Dispose()

$contract=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($BaseContract)
$package=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $Destination 'MessagePack.dll'))
$reference=[Mono.Cecil.AssemblyNameReference]::Parse($package.Name.FullName)
foreach($existing in @($contract.MainModule.ExportedTypes | Where-Object {$_.Namespace.StartsWith('MessagePack')})){$contract.MainModule.ExportedTypes.Remove($existing) | Out-Null}
foreach($existing in @($contract.MainModule.AssemblyReferences | Where-Object {$_.Name -eq 'MessagePack'})){$contract.MainModule.AssemblyReferences.Remove($existing) | Out-Null}
$contract.MainModule.AssemblyReferences.Add($reference)
$count=0
foreach($t in $package.MainModule.Types | Where-Object {$_.IsPublic -and $_.Namespace.StartsWith('MessagePack')}) {
 $forwarder=[Mono.Cecil.ExportedType]::new($t.Namespace,$t.Name,$contract.MainModule,$reference)
 $forwarder.Attributes=[Mono.Cecil.TypeAttributes]::Public -bor [Mono.Cecil.TypeAttributes]::Forwarder
 $contract.MainModule.ExportedTypes.Add($forwarder);$count++
}
$contractSeed=[Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($IdentitySeed+':GFrameCore-serialization'))
$contract.MainModule.Mvid=[guid]::new([byte[]]$contractSeed[0..15])
$contract.Write((Join-Path $Destination 'GFrameCore.dll'),$writer)
$contract.Dispose();$package.Dispose()

$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($OriginalConsole)
$t=$a.MainModule.GetType('MessagePack.Resolvers.GeneratedResolverGetFormatterHelper')
$get=$t.Methods | Where-Object Name -eq GetFormatter
$switches=@($get.Body.Instructions | Where-Object {$_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Switch})
$tokens=@(($t.Methods | Where-Object Name -eq '.cctor').Body.Instructions | Where-Object {$_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldtoken})
if($switches.Count -ne 2 -or $switches[0].Operand.Count -ne 6540 -or $tokens.Count -ne 6544){throw 'Original formatter lookup changed'}
function TypeRecord($type) {
 if($type -is [Mono.Cecil.ArrayType]){return [ordered]@{Array=TypeRecord $type.ElementType;Rank=$type.Rank}}
 if($type -is [Mono.Cecil.GenericInstanceType]){return [ordered]@{Name=$type.ElementType.FullName;Assembly=$type.ElementType.Scope.Name;Arguments=@($type.GenericArguments | ForEach-Object {TypeRecord $_})}}
 return [ordered]@{Name=$type.FullName;Assembly=$type.Scope.Name;Arguments=@()}
}
$rows=@()
for($i=0;$i -lt 6540;$i++) {
 $target=$switches[0].Operand[$i]
 if($target.OpCode.Code -ne [Mono.Cecil.Cil.Code]::Newobj -or $target.Next.OpCode.Code -ne [Mono.Cecil.Cil.Code]::Ret){throw "Non-constructor formatter branch: $i"}
 $rows+=[ordered]@{Key=$tokens[$i].Operand.FullName;Type=TypeRecord $target.Operand.DeclaringType}
}
$open=@()
for($i=0;$i -lt 4;$i++) {
 $target=$switches[1].Operand[$i]
 if($target.OpCode.Code -ne [Mono.Cecil.Cil.Code]::Ldtoken){throw "Open formatter branch differs: $i"}
 $open+=[ordered]@{Key=$tokens[6540+$i].Operand.FullName;Type=TypeRecord $target.Operand}
}
[ordered]@{Closed=$rows;Open=$open} | ConvertTo-Json -Depth 30 -Compress | Set-Content -LiteralPath (Join-Path $Destination 'formatter-map.json') -Encoding UTF8
$a.Dispose();Write-Output "Recovered $($rows.Count) closed and $($open.Count) open original formatter selections"
$identities=@()
foreach($name in @('GFrameCore','MessagePack')) {
 $module=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $Destination ($name+'.dll')))
 $identities+=[ordered]@{Name=$name;ModuleId=$module.MainModule.Mvid.ToString();Identity=$module.Name.FullName}
 $module.Dispose()
}
$identities | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Destination 'assemblies.json') -Encoding UTF8
