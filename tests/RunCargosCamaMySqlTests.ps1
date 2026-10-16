# Integración explícita y exclusiva del entorno local confirmado por el usuario.
# Las escrituras del ejecutable verifican primero que el destino sea TEMPORARY.
$ErrorActionPreference = 'Stop'
$raizPruebas = Split-Path -Parent $PSScriptRoot
$salidaPruebas = Join-Path $raizPruebas 'obj\CargosCamaMySqlTests'
New-Item -ItemType Directory -Path $salidaPruebas -Force | Out-Null
$compiladorPruebas = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\vbc.exe'
$sdkPruebas = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5'
$ejecutablePruebas = Join-Path $salidaPruebas 'CargosCamaMySqlTests.exe'
$conectorPruebas = Join-Path $raizPruebas 'lib\MySqlConnector\MySqlConnector.dll'
$argumentosPruebas = @('/nologo', '/target:exe', '/optionstrict+',
    "/out:$ejecutablePruebas", "/sdkpath:$sdkPruebas",
    "/reference:System.dll,System.Core.dll,System.Data.dll,Microsoft.VisualBasic.dll,$conectorPruebas",
    (Join-Path $raizPruebas 'HisPeriodosCama.vb'),
    (Join-Path $raizPruebas 'HisCargosHospitalizacionAutomaticos.vb'),
    (Join-Path $PSScriptRoot 'CargosCamaMySqlTests.vb'))
& $compiladorPruebas @argumentosPruebas
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de integración.' }
Get-ChildItem -LiteralPath (Join-Path $raizPruebas 'lib\MySqlConnector') -Filter '*.dll' |
    Copy-Item -Destination $salidaPruebas -Force
# Heredar solo runtime/startup, sin copiar otros ajustes ni cadenas de conexión.
$configAplicacionPruebas = New-Object Xml.XmlDocument
$configAplicacionPruebas.Load((Join-Path $raizPruebas 'app.config'))
$configEjecutablePruebas = New-Object Xml.XmlDocument
$nodoConfiguracionPruebas = $configEjecutablePruebas.CreateElement('configuration')
[void]$configEjecutablePruebas.AppendChild($nodoConfiguracionPruebas)
foreach ($nombreNodoPruebas in @('startup', 'runtime')) {
    $nodoOrigenPruebas = $configAplicacionPruebas.SelectSingleNode('/configuration/' + $nombreNodoPruebas)
    if ($null -ne $nodoOrigenPruebas) {
        [void]$nodoConfiguracionPruebas.AppendChild($configEjecutablePruebas.ImportNode($nodoOrigenPruebas, $true))
    }
}
$configEjecutablePruebas.Save($ejecutablePruebas + '.config')
$fuentePruebas = [IO.File]::ReadAllText((Join-Path $raizPruebas 'frmInterface.vb'))
$bloquePruebas = [regex]::Match($fuentePruebas, '(?s)Private Function Conectar_Local\(\).*?End Function').Value
$configPruebas = [regex]::Match($bloquePruebas, 'Port=(?<puerto>\d+);uid=(?<usuario>[^;]+);pwd=(?<clave>[^;]+);database=(?<base>[^";]+)')
if (-not $configPruebas.Success -or $configPruebas.Groups['base'].Value -ne 'histoclin') { throw 'Configuración local no identificada.' }
$anteriorPruebas = $env:HISTOMEDIC_CAMA_TEST_LOCAL
try {
    $env:HISTOMEDIC_CAMA_TEST_LOCAL = 'Server=127.0.0.1;Port=' + $configPruebas.Groups['puerto'].Value +
        ';User ID=' + $configPruebas.Groups['usuario'].Value + ';Password=' + $configPruebas.Groups['clave'].Value +
        ';Database=histoclin;SslMode=None;'
    & $ejecutablePruebas
    if ($LASTEXITCODE -ne 0) { throw 'Falló la integración MySQL local.' }
} finally {
    $env:HISTOMEDIC_CAMA_TEST_LOCAL = $anteriorPruebas
}
