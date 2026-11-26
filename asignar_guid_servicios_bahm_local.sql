-- Paso 1: ejecutar en la sede 100.120.248.35, en una sola sesión.
USE `histoclin`;

SET @guid_969 = '456b2acf-7dc9-432e-b85d-d559ca1c8ad8';
SET @guid_985 = '7a0e49aa-3b29-4d2c-83bb-9a0a5f63ed57';

-- Deben coincidir las dos cabeceras verificadas. Admite repetir el script con los mismos GUID.
SELECT COUNT(*) INTO @filas_validas
FROM tb_servicios_sistemas
WHERE
    (id = 969 AND folio = 'SS-00969' AND fchregistro = '2026-11-10 07:59:13'
     AND (guid_system IS NULL OR guid_system = @guid_969))
 OR
    (id = 985 AND folio = 'SS-00985' AND fchregistro = '2026-11-24 10:34:46'
     AND (guid_system IS NULL OR guid_system = @guid_985));

-- Evita que esta asignación local genere otro movimiento en his_replica.
SET @bloqueo = CONCAT('histomedic_descarga_portal:', CONNECTION_ID());
SET @bloqueo_obtenido = GET_LOCK(@bloqueo, 0);

UPDATE tb_servicios_sistemas
SET guid_system = CASE id WHEN 969 THEN @guid_969 WHEN 985 THEN @guid_985 END
WHERE @filas_validas = 2
  AND @bloqueo_obtenido = 1
  AND IS_USED_LOCK(@bloqueo) = CONNECTION_ID()
  AND guid_system IS NULL
  AND (
      (id = 969 AND folio = 'SS-00969' AND fchregistro = '2026-11-10 07:59:13')
   OR
      (id = 985 AND folio = 'SS-00985' AND fchregistro = '2026-11-24 10:34:46')
  );

SELECT @filas_validas AS filas_validadas, ROW_COUNT() AS filas_actualizadas;
SELECT RELEASE_LOCK(@bloqueo) AS bloqueo_liberado;

SELECT id, folio, guid_system, sinc_system
FROM tb_servicios_sistemas
WHERE id IN (969, 985)
ORDER BY id;

