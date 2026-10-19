# Builds the client plugin: .\build.ps1 -Spt "C:\path\to\SPT" [-Csc <csc.exe>]
# Needs Roslyn's csc.exe (Visual Studio or Build Tools, or the Microsoft.Net.Compilers.Toolset package); the game's own
# assemblies (SPT's publicized Assembly-CSharp, Unity, BepInEx, spt-common) are referenced from the SPT folder.
param([Parameter(Mandatory = $true)][string]$Spt, [string]$Csc = 'csc.exe')
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
New-Item -ItemType Directory -Force build | Out-Null
(Get-Content build.rsp.template -Raw).Replace('{SPT}', $Spt.TrimEnd('\', '/')) | Set-Content build.rsp -Encoding UTF8
& $Csc '@build.rsp'
