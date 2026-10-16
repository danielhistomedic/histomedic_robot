-- Migración de terminología exclusiva de la tabla auxiliar creada por el Robot.
-- Aplicar una sola vez en desarrollo si todavía existe la columna fecha_reserva.
-- No modifica tablas financieras, importes, periodos, identificadores ni vínculos de cargos.
ALTER TABLE robot_camas_periodos
    CHANGE COLUMN fecha_reserva fecha_inicio DATETIME NOT NULL;

UPDATE robot_camas_periodos SET estado='EN_PROCESO' WHERE estado='RESERVADO';

-- Reversión, solo junto con la versión anterior del código:
-- UPDATE robot_camas_periodos SET estado='RESERVADO' WHERE estado='EN_PROCESO';
-- ALTER TABLE robot_camas_periodos CHANGE COLUMN fecha_inicio fecha_reserva DATETIME NOT NULL;
