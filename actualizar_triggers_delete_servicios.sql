-- Ejecutar en la base local con permisos para recrear los triggers y conservar su DEFINER.
-- Pausar los borrados de estas tablas durante el cambio: DROP y CREATE son operaciones separadas.
-- Requiere que histoclin.his_replica.guid_system ya exista.
USE histoclin;
SET @modo_sql_anterior = @@SESSION.sql_mode;
SET SESSION sql_mode = 'STRICT_TRANS_TABLES,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

DROP TRIGGER IF EXISTS trpa_del_tb_servicios_sistemas;
CREATE DEFINER = 'root'@'%' TRIGGER trpa_del_tb_servicios_sistemas
AFTER DELETE ON tb_servicios_sistemas
FOR EACH ROW
INSERT INTO his_replica (tabla_afectada, tipo_operacion, fecha, id_tabla_afectada, sinc, guid_system)
VALUES ('tb_servicios_sistemas', 'DELETE', NOW(), OLD.Id, 1, OLD.guid_system);

DROP TRIGGER IF EXISTS trpa_del_tb_servicios_sistemas_detalle;
CREATE DEFINER = 'root'@'%' TRIGGER trpa_del_tb_servicios_sistemas_detalle
AFTER DELETE ON tb_servicios_sistemas_detalle
FOR EACH ROW
INSERT INTO his_replica (tabla_afectada, tipo_operacion, fecha, id_tabla_afectada, sinc, guid_system)
VALUES ('tb_servicios_sistemas_detalle', 'DELETE', NOW(), OLD.Id, 1, OLD.guid_system);

SET SESSION sql_mode = @modo_sql_anterior;
