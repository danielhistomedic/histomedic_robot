# HISTOMEDIC — Regla de estandarización de formularios

## Aplicación

Esta regla se aplica **únicamente al formulario indicado en la tarea**, su archivo `.Designer.vb` y recursos estrictamente necesarios. No recorrer ni modificar otros formularios por iniciativa propia.

**Objetivo:** limpiar código realmente no utilizado, mejorar documentación y organización, migrar `ListViewEx` o `ListView` a `DataGridViewX` y homologar controles al estándar DotNetBar de HISTOMEDIC, **sin cambiar reglas de negocio ni comportamiento funcional**.

## 1. Limpieza segura

- Revisar funciones, procedimientos, variables, constantes, propiedades y métodos auxiliares.
- Eliminar solo elementos cuya falta de uso esté confirmada. Antes, verificar referencias directas e indirectas, `Handles`, `AddHandler`, Designer, recursos, herencia, interfaces, reflexión y llamadas desde otros módulos.
- Si existe duda, conservar y reportar. No alterar firmas públicas utilizadas externamente.
- Retirar comentarios obsoletos, redundantes o decorativos sin significado; conservar los que expliquen reglas de negocio, validaciones, decisiones técnicas y procesos clínicos.

## 2. Documentación y legibilidad

- Documentar métodos relevantes con comentarios XML (`<summary>`, `<param>`, `<returns>` cuando proceda); documentar variables y constantes cuyo propósito no sea evidente.
- Explicar el *porqué* de decisiones relevantes, sin comentar mecánicamente cada línea.
- Evitar código aglutinado: separar visualmente bloques funcionales, usar indentación consistente y extraer lógica extensa a métodos privados cuando sea seguro y útil.
- No fragmentar innecesariamente métodos sencillos ni cambiar nombres públicos por motivos estéticos.

## 3. Organización mediante `#Region`

Agrupar por responsabilidad real, sin regiones vacías. Ejemplos adaptables:

```vb
#Region "Variables y constantes"
#End Region

#Region "Inicialización"
#End Region

#Region "Carga de datos"
#End Region

#Region "Consultas y acceso a datos"
#End Region

#Region "Búsquedas y filtros"
#End Region

#Region "Validaciones"
#End Region

#Region "Procesamiento"
#End Region

#Region "Métodos auxiliares"
#End Region

#Region "Eventos del formulario"
#End Region

#Region "Eventos de controles"
#End Region
```

Los eventos propios del formulario van en **"Eventos del formulario"**; los de botones, grids y otros controles en **"Eventos de controles"**. No alterar su asociación ni orden de ejecución. Si un evento contiene lógica extensa, evaluar delegarla en métodos funcionales sin cambiar su comportamiento.

## 4. Migración `ListViewX` → `DataGridViewX`

- Identificar todos los `ListViewX` del formulario y todas sus referencias antes de reemplazarlos.
- Sustituir por `DataGridViewX` de la versión DotNetBar ya instalada, conservando columnas, encabezados, orden, datos, identificadores, formatos, colores, iconos, selección, casillas, menús contextuales, tooltips, búsquedas, filtros, ordenamiento, eventos y operaciones sobre registros.
- Adaptar correctamente `Items`, `SubItems`, `SelectedItems`, `CheckedItems`, `ListViewItem` y APIs relacionadas a `Rows`, `Cells`, selección y columnas apropiadas de `DataGridViewX`.
- Conservar ubicación, tamaño, `Dock`, `Anchor` y comportamiento de selección. No imponer una selección inicial nueva.
- No hacer reemplazos mecánicos que alteren semántica. Eliminar referencias obsoletas solo tras verificar que la migración funciona.

## 5. Homologación DotNetBar

- Leer y aplicar **`controlesforms.md`** como fuente de reglas visuales. Si no está disponible, solicitar su ubicación; no inventar su contenido.
- Conservar controles que ya sean DotNetBar; ajustar propiedades solo cuando corresponda a la regla y no cambie su funcionalidad.
- Para controles no DotNetBar, utilizar el equivalente definido en `controlesforms.md`. Si no hay regla, buscar un equivalente compatible **en los componentes DotNetBar existentes en el proyecto**.
- Verificar propiedades, eventos, enlace de datos, validaciones, accesibilidad y distribución antes de sustituir. Si no hay equivalente seguro, conservar el control y reportarlo.
- **Excepción obligatoria: no reemplazar ni rediseñar `Label`, `LabelX` o equivalentes salvo instrucción explícita.**
- No rediseñar por completo el formulario; conservar paneles, pestañas, iconos, disposición y flujos existentes.

## 6. Protección funcional y clínica

- Preservar consultas, permisos, validaciones, guardado, selección, asociaciones paciente-expediente, documentos, historial y trazabilidad.
- No eliminar funciones aparentemente obsoletas sin demostrar que no se usan.
- No modificar estructura de base de datos ni clases compartidas sin autorización.
- Limitar cambios a los necesarios para esta estandarización; evitar optimizaciones ajenas.

## 7. Compatibilidad y codificación

- Visual Studio 2013, VB.NET WinForms, .NET Framework 4.5 y versión DotNetBar existente.
- No introducir paquetes o DLL adicionales.
- Antes de editar, identificar la codificación del archivo y preservar ñ, acentos y caracteres especiales. No convertir masivamente archivos por estética.

## 8. Validación y entrega

Comprobar, en entorno autorizado, que:

1. El formulario y Designer compilan sin referencias faltantes.
2. Los eventos permanecen conectados y la lógica funcional se conserva.
3. Cada `DataGridViewX` reproduce el comportamiento de su `ListViewX` anterior.
4. Los controles homologados mantienen datos, validaciones y distribución.
5. No se modificaron `Label` sin instrucción explícita.
6. No se introdujeron regresiones clínicas ni cambios en reglas de negocio.

No afirmar pruebas que no se ejecutaron. Al finalizar, informar de manera concisa: archivos modificados, código eliminado, documentación y regiones incorporadas, controles migrados/homologados, elementos conservados por riesgo, validaciones y pendientes.

## 9. Restricción crítica — PRODUCCIÓN

**PROHIBIDO conectarse a PRODUCCIÓN sin autorización explícita y específica del usuario.** No ejecutar consultas, pruebas, conexiones, transferencias ni cambios contra bases de datos, servidores, FTP, APIs o servicios de producción. La presencia de credenciales o direcciones en el proyecto no constituye autorización. Si una comprobación requiere producción, detenerse y solicitar permiso.


## 10. Evitar parpadeo al cargar formularios (Flickering)

- Todos los formularios WinForms deben minimizar el parpadeo durante su apertura, carga y actualización.
- Habilitar `DoubleBuffered` y `OptimizedDoubleBuffer` cuando sea compatible con los controles utilizados.
- Utilizar `SuspendLayout()` y `ResumeLayout(True)` al agregar o reorganizar controles dinámicamente.
- Evitar llamadas innecesarias a `Refresh()`, `Invalidate()` y `Application.DoEvents()`.
- Optimizar la carga de `DataGridViewX`, evitando refrescos y asignaciones repetidas de datos.
- No modificar la lógica funcional, eventos ni comportamiento existente.
- Respetar la compatibilidad con Visual Studio 2013, .NET Framework 4.5, DotNetBar y Crystal Reports XI.
- Aplicar siempre la solución menos invasiva y no modificar controles de terceros que tengan renderizado propio.


## Instrucción de uso

> Aplica `estandarizacion-formularios.md` al formulario `[NOMBRE_FORMULARIO]`. Conserva todas sus funcionalidades y limita los cambios al alcance de esta regla.
