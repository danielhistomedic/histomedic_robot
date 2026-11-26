-- Paso 2: ejecutar en el portal solo después de verificar ambos GUID en la sede.
USE `mirtheda_bahm`;

SET @guid_969 = '456b2acf-7dc9-432e-b85d-d559ca1c8ad8';
SET @guid_985 = '7a0e49aa-3b29-4d2c-83bb-9a0a5f63ed57';

-- Deben coincidir las dos cabeceras verificadas. Admite repetir el script con los mismos GUID.
SELECT COUNT(*) INTO @filas_validas
FROM tb_servicios_sistemas
WHERE
    (id = 969 AND folio = 'SS-00969' AND fchregistro = '2026-11-10 07:59:13'
     AND (guid_system IS NULL OR guid_system = @guid_969)
     AND (sinc_system = 1 OR guid_system = @guid_969))
 OR
    (id = 985 AND folio = 'SS-00985' AND fchregistro = '2026-11-24 10:34:46'
     AND (guid_system IS NULL OR guid_system = @guid_985)
     AND (sinc_system = 1 OR guid_system = @guid_985));

UPDATE tb_servicios_sistemas
SET guid_system = CASE id WHEN 969 THEN @guid_969 WHEN 985 THEN @guid_985 END
WHERE @filas_validas = 2
  AND guid_system IS NULL
  AND sinc_system = 1
  AND (
      (id = 969 AND folio = 'SS-00969' AND fchregistro = '2026-11-10 07:59:13')
   OR
      (id = 985 AND folio = 'SS-00985' AND fchregistro = '2026-11-24 10:34:46')
  );

SELECT @filas_validas AS filas_validadas, ROW_COUNT() AS filas_actualizadas;

SELECT id, folio, guid_system, sinc_system
FROM tb_servicios_sistemas
WHERE id IN (969, 985)
ORDER BY id;

