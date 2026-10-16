$ErrorActionPreference = 'Stop'
$raizPruebas = Split-Path -Parent $PSScriptRoot
$salidaPruebas = Join-Path $raizPruebas 'obj\CargosCamaTests'
New-Item -ItemType Directory -Path $salidaPruebas -Force | Out-Null
$compiladorPruebas = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\vbc.exe'
$sdkPruebas = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5'
$ejecutablePruebas = Join-Path $salidaPruebas 'CargosCamaTests.exe'
$conectorPruebas = Join-Path $raizPruebas 'lib\MySqlConnector\MySqlConnector.dll'
$argumentosPruebas = @('/nologo', '/target:exe', '/optionstrict+',
    "/out:$ejecutablePruebas", "/sdkpath:$sdkPruebas",
    "/reference:System.dll,System.Core.dll,System.Data.dll,Microsoft.VisualBasic.dll,$conectorPruebas",
    (Join-Path $raizPruebas 'HisPeriodosCama.vb'),
    (Join-Path $raizPruebas 'HisCargosHospitalizacionAutomaticos.vb'),
    (Join-Path $PSScriptRoot 'CargosCamaTests.vb'))
& $compiladorPruebas @argumentosPruebas
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de pruebas.' }
# Solo dependencias ya incluidas en el Robot. El simulador nunca abre una conexión.
Get-ChildItem -LiteralPath (Join-Path $raizPruebas 'lib\MySqlConnector') -Filter '*.dll' |
    Copy-Item -Destination $salidaPruebas -Force
& $ejecutablePruebas
if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas de cargos por cama.' }
