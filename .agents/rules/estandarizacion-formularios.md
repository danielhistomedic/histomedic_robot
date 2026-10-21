---
trigger: always_on
---

# HISTOMEDIC — Reglas unificadas de estandarización de formularios y controles

## 1. Prioridad, alcance y principio rector

- Aplicar únicamente al formulario y a la funcionalidad expresamente solicitados, su Designer y dependencias imprescindibles.
- **Prioridad máxima: conservar el comportamiento y el estilo original del código.** Optimizar el código existente, no reescribirlo.
- **Mínimo cambio necesario, máximo beneficio comprobable.** No crear clases, métodos, validaciones, variables, consultas, capas ni controles por iniciativa propia.
- Antes de modificar, identificar las reglas que aplican al caso. Las reglas de migración de controles o visores **no autorizan sustituciones masivas**: se aplican solo cuando el usuario lo solicite.
- Si dos reglas entran en conflicto, prevalecen: seguridad y autorización de producción → integridad clínica y financiera → reutilización del acceso a datos existente → alcance específico → conservación del código → diseño visual.
- Ante incertidumbre que pueda afectar datos o reglas de negocio, detenerse, explicar el riesgo y solicitar autorización.

## 2. Optimización conservadora y control de generación de código

- Mantener nombres, firmas, variables, eventos, estructura y estilo de programación existentes cuando funcionan correctamente.
- Optimizar directamente dentro de los métodos actuales. No fragmentar automáticamente métodos largos ni mover lógica de eventos por estética.
- Crear un método auxiliar únicamente si elimina duplicación real, reduce complejidad demostrablemente o resuelve una necesidad funcional clara.
- No introducir interfaces, patrones de diseño, clases de servicio, wrappers, helpers o validaciones defensivas redundantes sin justificación y autorización cuando amplíen el alcance.
- No sustituir una implementación simple por otra más extensa sin beneficio medible; evitar cambios masivos de formato y de Designer.
- No cambiar consultas, criterios de búsqueda, filtros, mensajes, permisos, procesos clínicos o financieros sin petición expresa.
- Analizar antes de optimizar; presentar diagnóstico breve y esperar autorización cuando la tarea sea una auditoría previa o el cambio sea estructural.
- Si se solicita implementar directamente, efectuar solo cambios acotados y seguros, sin convertir la tarea en una auditoría/refactorización general.

## 3. Acceso a datos — reutilización obligatoria de funciones parametrizadas

- **Para todos los `SELECT`, `INSERT`, `UPDATE` y `DELETE` nuevos o modificados, identificar y reutilizar la función o sobrecarga parametrizada YA EXISTENTE en el proyecto** y las clases de acceso a datos establecidas por HISTOMEDIC.
- Antes de escribir SQL nuevo, buscar cómo el mismo módulo realiza operaciones parametrizadas; respetar firmas, convenciones y el manejo de `Nothing`/`DBNull` ya utilizado.
- **No crear nuevos ejecutores SQL**, clases de conexión, wrappers de `MySqlConnection`/`MySqlCommand`, conexiones directas, concatenación de valores ni métodos paralelos si la funcionalidad parametrizada existente resuelve el caso.
- Nunca interpolar ni concatenar datos de usuario en SQL. Parametrizar también fechas, claves, importes, textos y valores opcionales; los identificadores SQL no se parametrizan y deben provenir de nombres fijos/validados.
- **No reescribir consultas existentes solo para cambiar su estilo.** Cuando una consulta requiera modificación, conservar su lógica y emplear la función parametrizada existente. Si ya está correctamente parametrizada, conservarla.
- Si una función parametrizada no soporta una operación necesaria, primero revisar otras sobrecargas o patrones ya disponibles. **No inventar nombres ni firmas de funciones**. Informar el faltante y solicitar autorización antes de ampliar infraestructura compartida.
- Conservar reglas de `HisConectores`, `HisPropiedades` y las reglas existentes de acceso a datos. No modificar esas clases compartidas salvo autorización explícita.
- En operaciones `DELETE`, respetar el procedimiento de autorización existente, incluido `CadenaAut = Get_Cadena_Delete` cuando corresponda al módulo.
- Considerar MySQL/MyISAM: no introducir `Transaction`, `COMMIT` ni `ROLLBACK` como garantía de reversión. Prevenir duplicados e inserciones parciales según las reglas actuales.
- No crear índices, tablas, campos ni ejecutar migraciones sin autorización. Proponer los cambios SQL por separado si fueran necesarios.
- No abrir conexiones a producción para inspeccionar esquemas o probar SQL sin autorización específica.

## 4. Limpieza, documentación y organización

- Eliminar funciones, variables, constantes y comentarios solo si su falta de uso es verificable; comprobar `Handles`, `AddHandler`, Designer, recursos y referencias indirectas. Si hay duda, conservar.
- Documentar brevemente decisiones relevantes, reglas clínicas y métodos complejos; no generar encabezados XML extensos para cada método ni comentar instrucciones evidentes.
- Conservar las regiones existentes. Añadir una región solo si agrupa una responsabilidad funcional real; nunca crear regiones vacías o una por método.
- Los eventos propios del formulario deben agruparse en `#Region "Eventos del formulario"` y los eventos de controles en `#Region "Eventos de controles"`. Respetar las regiones equivalentes ya establecidas cuando su reorganización no esté autorizada.
- Mantener indentación y espacios legibles sin expandir artificialmente instrucciones sencillas. No reorganizar el resto del archivo como efecto secundario.

## 5. Migración de controles y alcance

- Para controles no DotNetBar, homologar solo dentro del alcance solicitado y usando componentes ya disponibles. Si no hay sustituto seguro, conservar y reportar.
- No modificar automáticamente visores Crystal Reports, controles EO ni el Designer salvo solicitud específica.

## 5a. `ListViewX`/`ListViewEx`/`ListView` → DataGridViewX (solo por solicitud)

- Migrar `ListViewX`/`ListViewEx`/`ListView` a `DataGridViewX` **solo cuando se solicite**, preservando columnas, claves ocultas, selección, eventos, iconos, casillas, formatos, filtros, menús y comportamiento completo.
- Adaptar `Items`, `SubItems`, `SelectedItems`, `CheckedItems` y APIs relacionadas a `Rows`, `Cells` y selección de DataGridViewX sin cambios semánticos.

## 5b. Label y LabelX

- **Label y LabelX:** no reemplazar ni rediseñar sin instrucción expresa. Excepción: aplicar la propiedad `Font` del estándar de controles. Los estilos especiales de LabelX solo se aplican cuando se nombra explícitamente el control destinatario.

## 6. Prevención de parpadeo

- Revisar `New`, `Load`, `Shown` y cargas de controles únicamente cuando exista parpadeo o el requerimiento lo incluya.
- Reducir refrescos y asignaciones repetidas; usar `SuspendLayout`/`ResumeLayout(True)` con restauración segura y `DoubleBuffered` solo cuando el control lo admita.
- No usar `Thread.Sleep`, `Application.DoEvents`, `WS_EX_COMPOSITED` indiscriminado ni ocultar todo el formulario como solución general.
- Respetar el renderizado propio de DotNetBar, EO.WebControl y Crystal Reports.

## 7. Crystal Reports → EO.WebControl (solo por solicitud)

- Mantener Crystal Reports como motor del `.rpt` y exportar el resultado a PDF; reemplazar únicamente el visor indicado por `EO.WinForm.WebControl`.
- Tomar como referencia `frmRegistroPacientes.VistaPrevia_FichaIdentificacion`, verificando la API real de EO disponible.
- Generar PDF temporal único, verificar que exista y tenga contenido; convertir su ruta local a `New Uri(rutaPdf).AbsoluteUri` y navegar mediante la propiedad/método comprobado del `WebView` (por ejemplo `WebView.Url` si esa versión lo admite).
- Evitar mostrar un PDF anterior si falla la generación; no eliminar temporales aún abiertos; liberar objetos Crystal y streams correctamente.
- No utilizar `EO.PDF`, agregar dependencias ni alterar consultas, parámetros o diseño del reporte.

## 8. Codificación UTF-8

- Detectar y conservar la codificación original de cada archivo; utilizar UTF-8 en nuevos archivos cuando sea compatible.
- Preservar á, é, í, ó, ú, ñ, ü, ¿, ¡. Corregir caracteres dañados (`�`, `Ã³`, `Ã±`, `Â`) solo dentro del alcance y cuando el texto correcto pueda determinarse.
- No convertir masivamente archivos ni tocar recursos fuera del alcance. Revisar `.vb`, `.Designer.vb`, `.resx` y textos modificados.

## 9. Seguridad clínica y producción

- Preservar permisos, trazabilidad, asociaciones paciente-expediente, documentos, cargos y reglas de negocio.
- **PROHIBIDO conectarse a PRODUCCIÓN sin autorización explícita y específica del usuario.** No realizar consultas, transferencias, pruebas ni cambios en servidores, BD, FTP, APIs o servicios productivos.
- Credenciales, direcciones o cadenas de conexión presentes en archivos NO implican autorización. Ante dudas, detenerse.

## 10. Validación y entrega

- Comprobar compilación, asociaciones de eventos, equivalencia funcional, propiedades de controles afectados y ausencia de regresiones, solo en entornos autorizados.
- No afirmar pruebas no ejecutadas.
- Entregar un resumen breve de archivos y métodos modificados, razón técnica, código evitado/reutilizado, verificaciones y pendientes.
- **Criterio de aceptación:** el formulario conserva su estilo original y funcionalidad, el código no crece innecesariamente y las operaciones de datos nuevas o modificadas usan las funciones parametrizadas existentes.

---

# Estándares visuales y controles — especificaciones originales consolidadas

Estas especificaciones son referencia obligatoria **solo para controles nuevos o modificados dentro del alcance**. No autorizan recorrer y reestilizar todos los formularios.

## Fuentes y colores

* Para los controles `Bar`, utilizar:
  - Fuente: `Montserrat SemiBold`
  - Tamaño: `7.8pt`
  - Color: `RGB(15, 10, 74)`

* Para el resto de los controles internos del formulario, utilizar:
  - Fuente: `MS Reference Sans Serif`
  - Tamaño: `8.25pt`
  - Color: `RGB(15, 10, 74)`

## SuperTabControl

Aplicar este diseño estándar a los controles DevComponents.DotNetBar.SuperTabControl:

- BackColor = Color.Transparent
- Font = New Font("MS Reference Sans Serif", 8.25F)
- ForeColor = Color.FromArgb(15, 10, 74)
- TabFont = New Font("MS Reference Sans Serif", 8.25F)
- SelectedTabFont = New Font("MS Reference Sans Serif", 8.25F)
- TabStyle = eSuperTabStyle.WinMediaPlayer12
- ReorderTabsEnabled = True

Configurar el fondo de la franja de pestañas mediante:

- SuperTabLinearGradientColorTable.Colors = New Color() {Color.Transparent, Color.Transparent}
- SuperTabColorTable.Background = la tabla de degradado anterior.
- TabStripColor = la tabla de colores anterior.

Utilizar Dock = DockStyle.Fill cuando el control deba ocupar el espacio disponible del contenedor. Los SuperTabControlPanel asociados deben utilizar Dock = DockStyle.Fill.

Habilitar DoubleBuffered cuando la propiedad esté disponible, utilizando el mecanismo existente del proyecto.

Configurar Name, Text, Location, Size y TabIndex según el formulario. Seleccionar la pestaña inicial mediante SelectedTabIndex; utilizar 0 cuando corresponda iniciar en la primera pestaña.

Conservar las pestañas, sus paneles asociados, los elementos de ControlBox y los eventos existentes. Preservar colores especiales con significado funcional.

## TextBoxX

Aplicar este diseño estándar, basado en frm_lb_Paciente.txtBusca_Nombre:

- AutoSelectAll = True
- BackColor = Color.White
- Border.Class = "TextBoxBorder"
- Border.CornerType = eCornerType.Square
- Font = New Font("MS Reference Sans Serif", 8.25F)
- ForeColor = Color.FromArgb(15, 10, 74)
- FocusHighlightEnabled = True
- FocusHighlightColor = Color.FromArgb(255, 255, 192), como valor inicial del diseñador.
- WatermarkBehavior = eWatermarkBehavior.HideNonEmpty

Cuando el formulario aplique Funciones.set_HighlightColor, conservar el color de enfoque calculado a partir del color institucional.

Configurar WatermarkText según el contenido esperado; por ejemplo, "Nombre ...".

En controles de búsqueda que utilicen botón integrado, asignar ButtonCustom.Image al recurso btnBuscar2 y configurar su visibilidad según la funcionalidad.

Ajustar Name, Location, Size, Dock, Anchor y TabIndex según el formulario.

Conservar los eventos de búsqueda y las validaciones existentes.


## GroupPanel

Aplicar este diseño estándar a los controles DevComponents.DotNetBar.Controls.GroupPanel:

- BackColor = Color.Transparent
- CanvasColor = Color.Transparent
- ColorSchemeStyle = eDotNetBarStyle.StyleManagerControlled
- DrawTitleBox = False
- Font = New Font("MS Reference Sans Serif", 8.25F, FontStyle.Bold)
- Style.BackColor = Color.Transparent
- Style.BackColor2 = Color.Transparent
- Style.BackColorGradientAngle = 90
- Style.BorderColor = Color.FromArgb(48, 151, 246)
- Style.BorderTop = eStyleBorderType.Dash
- Style.BorderTopWidth = 1
- Style.BorderBottom = eStyleBorderType.Dash
- Style.BorderBottomWidth = 1
- Style.BorderLeft = eStyleBorderType.Dash
- Style.BorderLeftWidth = 1
- Style.BorderRight = eStyleBorderType.Dash
- Style.BorderRightWidth = 1
- Style.CornerDiameter = 4
- Style.CornerType = eCornerType.Rounded
- Style.TextColorSchemePart = eColorSchemePart.PanelText
- Style.TextLineAlignment = eStyleTextAlignment.Near
- StyleMouseDown.CornerType = eCornerType.Square
- StyleMouseOver.CornerType = eCornerType.Square

Configurar Text y TitleImage según la función del grupo. Ajustar Location, Size, Dock, Anchor y TabIndex al contenedor.

Preservar colores especiales con significado funcional. Si el estilo automático sobrescribe este diseño, utilizar Tag = "excluir_estilo", conforme al mecanismo existente.

Habilitar DoubleBuffered cuando esté disponible para reducir parpadeos.

## DataGridViewX

* Regla general para todos los controles DataGridViewX
  Aplicar estas propiedades visuales y de selección:
  .MultiSelect = True
  .BorderStyle = BorderStyle.Fixed3D
  .SelectionMode = DataGridViewSelectionMode.FullRowSelect
  .RowHeadersVisible = True
  .RowHeadersWidth = 24
  .ColumnHeadersVisible = True
  .BackgroundColor = Color.White
  .Font = New Font("MS Reference Sans Serif", 8.25F)
  .ForeColor = Color.FromArgb(15, 10, 74)
  .GridColor = Color.FromArgb(170, 170, 170)

  .DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft
  .DefaultCellStyle.ForeColor = Color.FromArgb(15, 10, 74)
  .DefaultCellStyle.SelectionBackColor = SystemColors.Highlight
  .DefaultCellStyle.SelectionForeColor = Color.FromArgb(15, 10, 74)
  .DefaultCellStyle.WrapMode = DataGridViewTriState.False

  .DefaultCellStyle.BackColor = HisPropiedades.pty_MainPrincipal.ColorPenList1
  .AlternatingRowsDefaultCellStyle.BackColor = HisPropiedades.pty_MainPrincipal.ColorPenList2
  Reglas según la función del control
  - En grillas de consulta: ReadOnly = True, AllowUserToAddRows = False y AllowUserToDeleteRows = False.
  - En grillas de captura: permitir edición, inserción o eliminación únicamente donde lo requiera la funcionalidad existente.
  - Configurar Dock, Anchor, tamaño y ubicación según el contenedor.
  - Mantener ocultas las columnas internas de claves e identificadores.
  - Alinear textos a la izquierda, fechas al centro y cantidades e importes a la derecha.
  - Ajustar los anchos según el contenido y el espacio disponible.
  - Conservar eventos, permisos, validaciones y resaltados propios de cada formulario.
  Esta regla debe aplicarse sin alterar el comportamiento funcional de las grillas existentes.

## Inicialización y estilo de formularios (solo cuando corresponda)

* Al crear u homologar un formulario, verificar si faltan estas llamadas y agregarlas solo cuando correspondan al alcance, sin duplicarlas:
  1) Funciones.CargarIconoFormulario(Me). Solo en caso de que el formulario
     sea de tipo Dialogo. 
  2) Me.set_HighlightColor() después de llenar los combobox del formulario.
  3) Me.ConfigurarEstiloControles() después de Me.set_HighlightColor()
  
* Si se requieren estas funciones y no existen ni hay equivalente reutilizable, utilizar la implementación actual del proyecto como referencia. El siguiente código es orientativo; no copiarlo indiscriminadamente:

    Private Sub ConfigurarEstiloControles()
        Funciones.ConfigurarEstiloControles(Me)
    End Sub

    Private Sub set_HighlightColor()
        '========================
        ' Focus Highlight
        '========================
        Dim thisCollection As New Collection()

        ' TextBox normales
        For Each txt As TextBox In Funciones.GetTypeControls(Of TextBox)(Me, False)
            thisCollection.Add(txt)
        Next

        ' TextBoxX
        For Each txtX As DevComponents.DotNetBar.Controls.TextBoxX In
            Funciones.GetTypeControls(Of DevComponents.DotNetBar.Controls.TextBoxX)(Me, False)
            thisCollection.Add(txtX)
        Next

        ' MaskedTextBoxAdv
        For Each mtxt As DevComponents.DotNetBar.Controls.MaskedTextBoxAdv In
            Funciones.GetTypeControls(Of DevComponents.DotNetBar.Controls.MaskedTextBoxAdv)(Me, False)
            thisCollection.Add(mtxt)
        Next

        Try
            Funciones.set_HighlightColor(thisCollection)
        Catch ex As Exception
            ' Registrar el error con el mecanismo existente. No ocultarlo silenciosamente.
        End Try

        '========================
        ' ExpandableSplitter
        '========================
        Dim thisCollectionSplitter As New Collection()

        For Each splitter As DevComponents.DotNetBar.ExpandableSplitter In
            Funciones.GetTypeControls(Of DevComponents.DotNetBar.ExpandableSplitter)(Me, False)
            thisCollectionSplitter.Add(splitter)
        Next

        Try
            Funciones.set_HighlightColorSplitter(thisCollectionSplitter)
        Catch ex As Exception
            ' Registrar el error con el mecanismo existente.
        End Try

        '========================
        ' ComboBoxEx
        '========================
        Dim thisCollectionComboBox As New Collection()

        For Each cbo As DevComponents.DotNetBar.Controls.ComboBoxEx In
            Funciones.GetTypeControls(Of DevComponents.DotNetBar.Controls.ComboBoxEx)(Me, False)
            thisCollectionComboBox.Add(cbo)
        Next

        Try
            Funciones.AjustarDropDownWidth(thisCollectionComboBox)
        Catch ex As Exception
            ' Registrar el error con el mecanismo existente.
        End Try

        ' Mantener BackColor = WhiteSmoke cuando reciban el foco los controles de solo lectura
        For Each txt As TextBox In Funciones.GetTypeControls(Of TextBox)(Me, False)
            If txt.ReadOnly Then
                txt.BackColor = Color.WhiteSmoke
            End If
        Next

        For Each txtX As DevComponents.DotNetBar.Controls.TextBoxX In Funciones.GetTypeControls(Of DevComponents.DotNetBar.Controls.TextBoxX)(Me, False)
            If txtX.ReadOnly Then
                txtX.BackColor = Color.WhiteSmoke
                txtX.FocusHighlightColor = Color.WhiteSmoke
            End If
        Next
    End Sub


## CrystalReportViewer

* En todos los controles `CrystalReportViewer`, establecer:
  `BorderStyle = None`

## Label
- Font = New Font("MS Reference Sans Serif", 8.25F)


## LabelX — Diseño aplicable únicamente por nombre explícito

* Aplicar este diseño solo cuando se indique expresamente el nombre del control destinatario. No aplicarlo automáticamente a todos    
  los LabelX ni a controles con nombres similares.

- Tipo = DevComponents.DotNetBar.LabelX
- Font = New Font("MS Reference Sans Serif", 8.25F)
- ForeColor = Color.FromArgb(15, 10, 74)
- BackgroundStyle.BackColor = Color.GhostWhite
- BackgroundStyle.BorderColor = Color.DarkGray
- BackgroundStyle.BorderTop = eStyleBorderType.Solid
- BackgroundStyle.BorderTopWidth = 1
- BackgroundStyle.BorderBottom = eStyleBorderType.Solid
- BackgroundStyle.BorderBottomWidth = 1
- BackgroundStyle.BorderLeft = eStyleBorderType.Solid
- BackgroundStyle.BorderLeftWidth = 1
- BackgroundStyle.BorderRight = eStyleBorderType.Solid
- BackgroundStyle.BorderRightWidth = 1
- BackgroundStyle.CornerType = eCornerType.Square
- BackgroundStyle.PaddingLeft = 10
- BackgroundStyle.PaddingRight = 10
- BackgroundStyle.PaddingBottom = 2

* Conservar el nombre, texto dinámico, eventos y función del control indicado. Ajustar Location, Size, Dock, Anchor y TabIndex según  
  su contenedor.

* Si no se especifica un nombre de control destinatario, esta regla no se aplica.

## Mensajes al usuario

* Para mensajes de advertencia únicamente informativos, utilizar:
  `Funciones.Msj_AdvOnly`

* Para mensajes de advertencia que requieran confirmación del usuario mediante **Sí/No**, utilizar:
  `Funciones.Msj_Adv`

* No sustituir estas funciones por `MessageBox.Show` cuando corresponda utilizar el estándar existente del proyecto.



## Regla de uso con Codex

> Aplica `estandarizacion-formularios.md` al formulario `[NOMBRE_FORMULARIO]` **solo dentro del alcance solicitado**. Conserva el estilo original, reutiliza las funciones parametrizadas existentes para operaciones SQL y evita generar código innecesario. Si se pide auditoría, entrega diagnóstico antes de modificar.


