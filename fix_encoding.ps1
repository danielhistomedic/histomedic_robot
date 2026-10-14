param()
# Script para corregir caracteres corruptos en frmInterface.vb
# El archivo tiene la secuencia UTF-8 EF BF BD (U+FFFD reemplazo)

$filePath = "frmInterface.vb"
$backupPath = "frmInterface.vb.backup"

# Hacer backup primero
Copy-Item $filePath $backupPath -Force
Write-Host "Backup creado: $backupPath"

# Leer el archivo como array de bytes
$bytes = [System.IO.File]::ReadAllBytes($filePath)
Write-Host "Total de bytes en archivo: $($bytes.Length)"

# Leer el contenido como Latin-1 (iso-8859-1) - cada byte = un char
$latin1 = [System.Text.Encoding]::GetEncoding("iso-8859-1")
$content = $latin1.GetString($bytes)

# La secuencia corrupta EF BF BD en Latin-1
$badSeq = [char]0xEF + [char]0xBF + [char]0xBD

$countBad = ([regex]::Matches($content, [regex]::Escape($badSeq))).Count
Write-Host "Total de secuencias corruptas encontradas: $countBad"

# Reemplazos usando codigos Unicode directos para evitar problemas de codificacion del script
# Los caracteres correctos:
$o_acento  = [char]0x00F3  # o con acento agudo: o
$a_acento  = [char]0x00ED  # i con acento agudo: i (para Cirugía)
$u_acento  = [char]0x00FA  # u con acento agudo: u
$e_acento  = [char]0x00E9  # e con acento agudo: e
$a_acento2 = [char]0x00E1  # a con acento agudo: a
$O_acento  = [char]0x00D3  # O con acento agudo: O
$I_acento  = [char]0x00CD  # I con acento agudo: I (para CIRUGÍA)
$enye      = [char]0x00F1  # n con virgulilla: n
$i_acento  = [char]0x00ED  # i con acento agudo: i (para vía)

$newContent = $content

# Definir los pares de reemplazo como arrays de dos elementos
$pairs = @(
    # "Conexion" -> "Conexion" (con acento en o)
    ("Conexi" + $badSeq + "n"),
    ("Conexi" + $o_acento + "n"),

    # "Cirugia" agendada -> con acento en la i
    ("Cirug" + $badSeq + "a agendada"),
    ("Cirug" + $i_acento + "a agendada"),

    # numero aleatorio
    ("n" + $badSeq + "mero aleatorio"),
    ("n" + $u_acento + "mero aleatorio"),

    # Operacion
    ("Operaci" + $badSeq + "n. Devielve"),
    ("Operaci" + $o_acento + "n. Devielve"),

    # CONFIGURACION
    ("CONFIGURACI" + $badSeq + "N DE PROTOCOLOS"),
    ("CONFIGURACI" + $O_acento + "N DE PROTOCOLOS"),

    # CIRUGIA
    ("CIRUG" + $badSeq + "A"),
    ("CIRUG" + $I_acento + "A"),

    # actualizacion
    ("actualizaci" + $badSeq + "n == =="),
    ("actualizaci" + $o_acento + "n == =="),

    # CONFIRMACION
    ("CONFIRMACI" + $badSeq + "N DE ORDEN"),
    ("CONFIRMACI" + $O_acento + "N DE ORDEN"),

    # electronico, debera, via, telefonica
    ("electr" + $badSeq + "nico"),
    ("electr" + $o_acento + "nico"),

    ("deber" + $badSeq + " notificar"),
    ("deber" + $a_acento2 + " notificar"),

    ("v" + $badSeq + "a telef"),
    ("v" + $i_acento + "a telef"),

    ("telef" + $badSeq + "nica"),
    ("telef" + $o_acento + "nica"),

    # aplicacion, ejecutandose
    ("aplicaci" + $badSeq + "n sigue"),
    ("aplicaci" + $o_acento + "n sigue"),

    ("ejecut" + $badSeq + "ndose"),
    ("ejecut" + $a_acento2 + "ndose"),

    # Medica
    ("Unidad M" + $badSeq + "dica"),
    ("Unidad M" + $e_acento + "dica"),

    # tamanio
    ("tama" + $badSeq + "o del log"),
    ("tama" + $enye + "o del log")
)

# Procesar pares (old, new)
for ($i = 0; $i -lt $pairs.Length; $i += 2) {
    $oldText = $pairs[$i]
    $newText = $pairs[$i + 1]
    if ($newContent.Contains($oldText)) {
        $newContent = $newContent.Replace($oldText, $newText)
        Write-Host "OK: Reemplazado: [$oldText] -> [$newText]"
    } else {
        Write-Host "ADVERTENCIA: No encontrado indice $i"
    }
}

# Contar cuantos corruptos quedan
$countRemaining = ([regex]::Matches($newContent, [regex]::Escape($badSeq))).Count
Write-Host ""
Write-Host "Secuencias corruptas restantes: $countRemaining"

if ($countRemaining -gt 0) {
    $lines2 = $newContent -split "`n"
    for ($i = 0; $i -lt $lines2.Length; $i++) {
        if ($lines2[$i].Contains($badSeq)) {
            Write-Host "  Linea $($i+1): $($lines2[$i])"
        }
    }
}

# Guardar el archivo en UTF-8 con BOM para que Visual Studio lo interprete bien
$utf8WithBom = New-Object System.Text.UTF8Encoding($true)
[System.IO.File]::WriteAllText($filePath, $newContent, $utf8WithBom)
Write-Host ""
Write-Host "Archivo guardado en UTF-8 con BOM: $filePath"
Write-Host "Proceso finalizado correctamente."
