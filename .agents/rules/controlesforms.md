---
trigger: always_on
---

# Reglas de formularios y controles

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

## Actualizacion y/o Optimización de Formularios

* En el load de formularios, siempre agregar las funciones:
  1) Funciones.CargarIconoFormulario(Me). Solo en caso de que el formulario
     sea de tipo Dialogo. 
  2) Me.set_HighlightColor() después de llenar los combobox del formulario.
  3) Me.ConfigurarEstiloControles() después de Me.set_HighlightColor()
  
* Agregar las funciones en caso de que no exitan en el formulario:

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
            ' Aquí conviene registrar el error
            ' MessageBox.Show(ex.Message)
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
            ' Registrar error si deseas
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
            ' Registrar error si deseas
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


## Codificación y caracteres

* Permitir y conservar el uso nativo de caracteres en español, incluyendo:
  - Acentos: `á, é, í, ó, ú`
  - Mayúsculas acentuadas: `Á, É, Í, Ó, Ú`
  - `ñ` y `Ñ`
  - Signos `¿` y `¡`

* No reemplazar estos caracteres por secuencias escapadas, entidades HTML ni caracteres equivalentes cuando no sea necesario.

* Mantener la codificación utilizada actualmente por el proyecto y evitar introducir caracteres corruptos o problemas de encoding.

## CrystalReportViewer

* En todos los controles `CrystalReportViewer`, establecer:
  `BorderStyle = None`

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

## Reglas generales

* Al modificar un formulario existente, conservar estas reglas en todos los controles nuevos o modificados.

* No cambiar estilos existentes que cumplan estas reglas sin una razón funcional.

* Mantener compatibilidad con:
  - VB.NET
  - Visual Studio 2013
  - .NET Framework 4.5


## Organización del código mediante Regions

* Todo código nuevo debe mantenerse organizado mediante `#Region` y `#End Region` cuando la clase ya utilice regiones o cuando el nuevo código forme un bloque funcional claramente identificable.

* Antes de crear una nueva región, revisar las regiones existentes en la clase.

* Si el código nuevo pertenece funcionalmente a una región existente, integrarlo dentro de dicha región en lugar de crear una región adicional.

* Crear una nueva región únicamente cuando no exista una región relacionada y el código agregado represente una funcionalidad claramente diferenciada.

* Utilizar nombres descriptivos y consistentes para las regiones.

Ejemplo:

#Region "Control de entregas"

    Private Sub RegistrarEntrada(...)
        ...
    End Sub

    Private Function ObtenerCantidadPendiente(...) As Double
        ...
    End Function

    Private Sub ActualizarEstatusEntrega(...)
        ...
    End Sub

#End Region

* Agrupar dentro de una misma región los eventos, funciones y métodos auxiliares que pertenezcan a la misma funcionalidad cuando esto facilite la lectura del código.

* Evitar crear una región independiente para cada función o evento.

* No crear regiones genéricas como:
  - `Código nuevo`
  - `Funciones nuevas`
  - `Cambios`
  - `Codex`
  - `Pendientes`

* Preferir nombres funcionales como:
  - `Control de entregas`
  - `Recepción de mercancía`
  - `Seguimiento de proyectos`
  - `Validaciones`
  - `Archivos adjuntos`
  - `Expandir y contraer`
  - `Funciones`
  - `Eventos Form`
  - `Campos y Propiedadaes`

* No reorganizar ni mover código existente a otras regiones salvo que sea necesario para integrar correctamente el nuevo código.

* No realizar refactorizaciones masivas de regiones como efecto secundario de una modificación puntual.

* Mantener el estilo y convención de nombres de las regiones existentes en cada clase.

### Regla de prioridad

Al agregar código, aplicar este orden:

1. Buscar una región existente relacionada con la funcionalidad.
2. Si existe, integrar ahí el código nuevo.
3. Si no existe, determinar si el código justifica una nueva región funcional.
4. Crear la nueva región con un nombre descriptivo.
5. Evitar modificar la organización del resto de la clase.

## REGLA CRÍTICA — ENTORNO DE PRODUCCIÓN

- NUNCA conectarse al entorno de PRODUCCIÓN sin autorización explícita del usuario.
- No ejecutar en PRODUCCIÓN consultas SQL, scripts, pruebas, migraciones, actualizaciones, INSERT, UPDATE, DELETE, ALTER, CREATE ni ningún otro comando.
- No conectarse a bases de datos, servidores, APIs, FTP, recursos compartidos, servicios o archivos de PRODUCCIÓN.
- No utilizar IP, credenciales, cadenas de conexión o configuraciones de PRODUCCIÓN aunque estén disponibles en el código o archivos de configuración.
- Para desarrollo, diagnóstico y pruebas utilizar exclusivamente entornos de desarrollo/pruebas autorizados.
- La existencia de credenciales de PRODUCCIÓN NO implica autorización para utilizarlas.
- No realizar pruebas de conectividad contra PRODUCCIÓN como parte de un diagnóstico.
- Si para continuar es indispensable consultar o probar PRODUCCIÓN, DETENERSE y solicitar autorización explícita antes de realizar cualquier conexión.
- La autorización aplica únicamente a la tarea específicamente indicada; no debe interpretarse como autorización permanente para futuras tareas.
- Ante cualquier duda sobre si un recurso pertenece a PRODUCCIÓN, tratarlo como PRODUCCIÓN y no conectarse hasta recibir confirmación.


