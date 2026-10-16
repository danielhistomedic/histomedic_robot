-- Ejecutar únicamente en la base de DESARROLLO autorizada, después de revisar el esquema.
-- No contiene ALTER, modificaciones de estructuras existentes ni backfill de cargos antiguos.
-- Si los nombres ya existen, comparar SHOW CREATE TABLE antes de continuar; no sobrescribirlos.
CREATE TABLE IF NOT EXISTS robot_camas_periodos (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    id_ingreso INT UNSIGNED NOT NULL,
    id_cuenta INT UNSIGNED NOT NULL,
    periodo INT UNSIGNED NOT NULL,
    fecha_ingreso DATETIME NOT NULL,
    numero_cuenta VARCHAR(255) NOT NULL,
    id_cama INT UNSIGNED NOT NULL,
    material VARCHAR(255) NOT NULL,
    partes TINYINT UNSIGNED NOT NULL,
    estado VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    fecha_inicio DATETIME NOT NULL,
    fecha_confirmacion DATETIME DEFAULT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY ux_robot_camas_periodo (id_ingreso, id_cuenta, periodo),
    KEY ix_robot_camas_cuenta (id_cuenta)
) ENGINE=MyISAM DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS robot_camas_detalles (
    id_periodo BIGINT UNSIGNED NOT NULL,
    parte TINYINT UNSIGNED NOT NULL,
    cantidad DOUBLE NOT NULL,
    precio DOUBLE NOT NULL,
    paquete INT UNSIGNED NOT NULL,
    id_cargo INT UNSIGNED DEFAULT NULL,
    PRIMARY KEY (id_periodo, parte),
    UNIQUE KEY ux_robot_camas_cargo (id_cargo)
) ENGINE=MyISAM DEFAULT CHARSET=utf8;

-- Protocolo MyISAM:
-- 1. Registrar el inicio del periodo y guardar TODOS sus fragmentos antes de insertar cargos.
-- 2. Insertar cada cargo SIN reintentos y vincular su LastInsertedId al fragmento.
-- 3. Confirmar el periodo únicamente después de comprobar todos los vínculos.
-- Un periodo en proceso con vínculos incompletos exige conciliación; jamás se vuelve a cobrar automáticamente.
-- Si todos los cargos están vinculados y verificados, se permite confirmar únicamente el control.
-- Las tablas auxiliares son parte del respaldo de los cargos y deben conservarse juntas.
-- Coste: dos tablas nuevas e índices exclusivamente propios. No se cambian tablas financieras.
-- Reversión: retirar el código y eliminar las tablas SOLO si nunca se han registrado inicios de periodo.
-- Después de usarlas, eliminarlas perdería la identidad y permitiría duplicar cobros.
-- No se incluye un DROP ejecutable para evitar borrar controles utilizados.
