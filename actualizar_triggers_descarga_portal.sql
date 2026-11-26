-- Aplicar en histoclin local. Pausar las altas y actualizaciones durante el cambio.
-- El robot mantiene un bloqueo con el ID de su propia conexión solo mientras guarda una descarga.
USE histoclin;
SET @modo_sql_anterior = @@SESSION.sql_mode;
SET SESSION sql_mode = 'STRICT_TRANS_TABLES,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

DROP TRIGGER IF EXISTS trpa_tb_servicios_sistemas;
CREATE DEFINER = 'root'@'%' TRIGGER trpa_tb_servicios_sistemas
AFTER INSERT ON tb_servicios_sistemas
FOR EACH ROW
INSERT INTO his_replica (tabla_afectada, tipo_operacion, fecha, id_tabla_afectada, sinc, guid_system)
SELECT 'tb_servicios_sistemas', 'INSERT', NOW(), NEW.Id, 1, NEW.guid_system
FROM DUAL
WHERE NOT (IS_USED_LOCK(CONCAT('histomedic_descarga_portal:', CONNECTION_ID())) <=> CONNECTION_ID());

DROP TRIGGER IF EXISTS trpa_up_tb_servicios_sistemas;
CREATE DEFINER = 'root'@'%' TRIGGER trpa_up_tb_servicios_sistemas
AFTER UPDATE ON tb_servicios_sistemas
FOR EACH ROW
INSERT INTO his_replica (tabla_afectada, tipo_operacion, fecha, id_tabla_afectada, sinc, guid_system)
SELECT 'tb_servicios_sistemas', 'UPDATE', NOW(), OLD.Id, 1, NEW.guid_system
FROM DUAL
WHERE NOT (IS_USED_LOCK(CONCAT('histomedic_descarga_portal:', CONNECTION_ID())) <=> CONNECTION_ID());

DROP TRIGGER IF EXISTS trpa_tb_servicios_sistemas_detalle;
CREATE DEFINER = 'root'@'%' TRIGGER trpa_tb_servicios_sistemas_detalle
AFTER INSERT ON tb_servicios_sistemas_detalle
FOR EACH ROW
INSERT INTO his_replica (tabla_afectada, tipo_operacion, fecha, id_tabla_afectada, sinc, guid_system)
SELECT 'tb_servicios_sistemas_detalle', 'INSERT', NOW(), NEW.Id, 1, NEW.guid_system
FROM DUAL
WHERE NOT (IS_USED_LOCK(CONCAT('histomedic_descarga_portal:', CONNECTION_ID())) <=> CONNECTION_ID());

DROP TRIGGER IF EXISTS trpa_up_tb_servicios_sistemas_detalle;
CREATE DEFINER = 'root'@'%' TRIGGER trpa_up_tb_servicios_sistemas_detalle
AFTER UPDATE ON tb_servicios_sistemas_detalle
FOR EACH ROW
INSERT INTO his_replica (tabla_afectada, tipo_operacion, fecha, id_tabla_afectada, sinc, guid_system)
SELECT 'tb_servicios_sistemas_detalle', 'UPDATE', NOW(), OLD.Id, 1, NEW.guid_system
FROM DUAL
WHERE NOT (IS_USED_LOCK(CONCAT('histomedic_descarga_portal:', CONNECTION_ID())) <=> CONNECTION_ID());

SET SESSION sql_mode = @modo_sql_anterior;
