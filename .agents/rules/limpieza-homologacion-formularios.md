# HISTOMEDIC — Limpieza y homologación conservadora de formularios

## Aplicación

- Aplicar las reglas vigentes de `estandarizacion-formularios.md` si están disponibles en el proyecto.
- Limitar los cambios al formulario indicado, su Designer y los recursos estrictamente necesarios.
- **Prioridad:** conservar las funcionalidades, reglas de negocio, estilo de programación y estructura original. Realizar únicamente los cambios necesarios, sin generar código indiscriminadamente.

## 1. Limpieza segura del código

- Revisar funciones, procedimientos, constantes y variables para identificar elementos que realmente no se utilizan.
- Antes de eliminar, comprobar referencias directas e indirectas, eventos `Handles` y `AddHandler`, Designer y posibles usos desde otros módulos.
- Eliminar únicamente elementos cuya falta de uso esté confirmada. Ante dudas, conservarlos y reportarlos.
- No modificar firmas públicas ni métodos compartidos por motivos estéticos.

## 2. Limpieza de comentarios

- Eliminar comentarios obsoletos, redundantes, decorativos o sin significado.
- Conservar los comentarios que expliquen reglas de negocio, validaciones, procesos clínicos o decisiones técnicas importantes.

## 3. Documentación completa y verificación de cobertura

- Documentar todos los elementos del formulario, incluidos los existentes que no se modifiquen:
  - Clase.
  - Funciones, procedimientos y eventos.
  - Propiedades, campos, variables y constantes.
  - Enumeraciones y cada uno de sus miembros.
  - Variables locales, parámetros y valores de retorno.
- Explicar el propósito real de cada elemento y, cuando corresponda, sus unidades, valores admitidos, significado de claves y efectos sobre otros datos.
- Utilizar comentarios XML para clases, métodos, eventos, propiedades y miembros que lo admitan; comentarios breves para variables locales y bloques internos.
- No considerar suficiente el nombre del elemento ni un comentario general de región como documentación individual.
- Revisar también código parcial y archivos asociados. Excluir únicamente código generado automáticamente del Designer y dependencias ajenas al formulario; informar expresamente estas exclusiones.
- Conservar la documentación existente cuando sea correcta y completar la que falte.
- Antes de terminar, comparar un inventario de declaraciones con los elementos documentados. Informar cantidades revisadas, documentadas y pendientes.
- No declarar completada la documentación mientras existan elementos sin documentar dentro del alcance.
- La documentación no debe modificar instrucciones ejecutables, firmas, eventos, consultas ni reglas de negocio.
- No documentar los parámetros `sender` y `e`, ya que su propósito es conocido y no requiere explicación.
- No documentar ex cuando sea: excepción de esta operación, tratada por la recuperación existente del bloque


## 4. Organización por regiones

- Agrupar métodos y declaraciones mediante `#Region` según su funcionalidad real, reutilizando las regiones existentes.
- No crear regiones vacías ni una región por cada método.
- Mantener los eventos propios del formulario en `#Region "Eventos del formulario"` y los eventos de controles en `#Region "Eventos de controles"`.
- No reorganizar código ajeno al alcance ni alterar su ejecución.

## 5. Homologación DotNetBar

- Aplicar `estandarizacion-formularios.md` al formulario objetivo, conservando íntegramente sus funcionalidades.
- Si un control no DotNetBar no aparece en `estandarizacion-formularios.md`, buscar un equivalente en la versión DotNetBar ya utilizada por el proyecto.
- Antes de reemplazar, verificar eventos, propiedades, enlaces de datos, validaciones y comportamiento; si no existe un equivalente seguro, conservar el control y reportarlo.
- No reemplazar ni rediseñar `Label`, `LabelX` o equivalentes salvo indicación explícita; aplicar únicamente la propiedad `Font` conforme al estándar vigente.
- Conservar ubicación, dimensiones, `Dock`, `Anchor`, diseño general y funcionalidad de los controles.

## 6. Acceso a datos y restricciones

- Conservar las consultas y reglas de negocio existentes. Si se requiere modificar una operación MySQL, reutilizar las funciones parametrizadas ya existentes de `HisConectores` para `SELECT`, `INSERT`, `UPDATE` y `DELETE`, según corresponda.
- No crear conexiones, funciones SQL paralelas ni sobrecargas duplicadas; no modificar `HisConectores` ni `HisPropiedades` como parte de esta tarea.
- Mantener compatibilidad con Visual Studio 2013, VB.NET WinForms, .NET Framework 4.5 y la versión DotNetBar instalada.
- Preservar la codificación de los archivos y los caracteres en español; no agregar dependencias externas.
- **PRODUCCIÓN:** prohibido conectarse o ejecutar consultas, pruebas o cambios sin autorización explícita y específica.

## 7. Validación y entrega

- Verificar que el formulario compile, que sus eventos sigan asociados y que las operaciones originales conserven su comportamiento.
- Informar brevemente los elementos eliminados, comentarios ajustados, documentación incorporada, regiones organizadas y controles homologados o conservados.
- No afirmar que se realizaron pruebas si no se ejecutaron.

**Criterio principal:** mínimo cambio necesario, máximo beneficio posible. No reescribir el formulario ni introducir complejidad innecesaria.


## Regla de uso con Codex
Aplica limpieza-homologacion-formularios.md al formulario `[NOMBRE_FORMULARIO]`, conservando su funcionalidad y estilo original.