Option Explicit On
Option Strict On

Imports System.Data
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Globalization
Imports MySqlConnector

''' <summary>
''' Cargos de cama con control auxiliar persistente. Usa la configuración de conexión del Robot,
''' sin transacciones ni dependencias de otros proyectos. Nunca reintenta escrituras inciertas.
''' </summary>
Friend NotInheritable Class HisCargosHospitalizacionAutomaticos
#Region "Variables y constantes"
    Private ReadOnly conexion As MySqlConnection
    Private ReadOnly leer As Func(Of String, IDictionary(Of String, Object), DataTable)
    Private ReadOnly insertar As Func(Of String, IDictionary(Of String, Object), Long)
    Private ReadOnly ejecutarSql As Func(Of String, IDictionary(Of String, Object), Integer)
    Private ReadOnly cuentaPaciente As Func(Of String, Integer, String)
    Private ReadOnly primerIngreso As Func(Of String, Integer, DateTime)
    Private ReadOnly log As Action(Of String)
    Private ReadOnly clues As String
    Private ReadOnly materiales As New Dictionary(Of String, DataRow)(StringComparer.Ordinal)
    Private ReadOnly listas As New Dictionary(Of String, Integer)(StringComparer.Ordinal)
    Private ReadOnly precios As New Dictionary(Of String, Double)(StringComparer.Ordinal)
    Private factorIva As Nullable(Of Double)

    Private NotInheritable Class Parte
        Friend Cantidad As Double
        Friend Precio As Double
        Friend Paquete As Integer
        Friend Notificar As Integer
        Friend Division As Boolean
    End Class

    Friend Sub New(ByVal local As MySqlConnection, ByVal unidad As String,
                   ByVal cuenta As Func(Of String, Integer, String),
                   ByVal ingreso As Func(Of String, Integer, DateTime), ByVal registrar As Action(Of String))
        Me.New(Function(sql, valores) ConsultarConexion(local, sql, valores),
               Function(sql, valores) InsertarConexion(local, sql, valores),
               Function(sql, valores) EjecutarConexion(local, sql, valores), unidad, cuenta, ingreso, registrar)
        conexion = local
    End Sub

    ''' <summary>Punto de prueba para simular fallos y reinicios sin abrir una base de datos.</summary>
    Friend Sub New(ByVal consulta As Func(Of String, IDictionary(Of String, Object), DataTable),
                   ByVal insercion As Func(Of String, IDictionary(Of String, Object), Long),
                   ByVal escritura As Func(Of String, IDictionary(Of String, Object), Integer),
                   ByVal unidad As String, ByVal cuenta As Func(Of String, Integer, String),
                   ByVal ingreso As Func(Of String, Integer, DateTime), ByVal registrar As Action(Of String))
        leer = consulta
        insertar = insercion
        ejecutarSql = escritura
        clues = unidad
        cuentaPaciente = cuenta
        primerIngreso = ingreso
        log = registrar
    End Sub
#End Region

#Region "Pacientes hospitalizados"
    ''' <summary>Verifica las tablas auxiliares antes de cualquier escritura y aísla los errores por ingreso.</summary>
    Friend Sub Ejecutar()
        Dim total As Stopwatch = Stopwatch.StartNew()
        log("Camas: inicio de ejecución.")
        Try
            If conexion IsNot Nothing AndAlso conexion.State <> ConnectionState.Open Then conexion.Open()
            VerificarTablas()
            Dim consulta As Stopwatch = Stopwatch.StartNew()
            ' Conservar urgencias y su umbral heredado; cambiar ese alcance sería otra regla financiera.
            Using pacientes As DataTable = leer(
                "SELECT r.Id,r.ccvepaciente,r.cdscareaafectada,r.fchregistro,r.icvecama,c.ccvematerial AS material_cama " &
                "FROM tb_urg_recepcion r INNER JOIN cat_servicios s ON s.cdscservicio=r.cdscareaafectada " &
                "LEFT JOIN cat_camas c ON c.icvecama=r.icvecama WHERE r.iegresado=0 " &
                "AND s.cdscservicio NOT IN ('QUIROFANO','UTQ','RECUPERACION') " &
                "AND s.cdscareaafectada IN ('HOSPITALIZACION','URGENCIAS')", Nothing)
                Trace.WriteLine("Camas: consulta pacientes ms=" & consulta.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture))
                Dim repeticiones As New Dictionary(Of String, Integer)(StringComparer.Ordinal)
                For Each paciente As DataRow In pacientes.Rows
                    Dim clave As String = Texto(paciente, "ccvepaciente")
                    If Not repeticiones.ContainsKey(clave) Then repeticiones.Add(clave, 0)
                    repeticiones(clave) += 1
                Next
                For Each paciente As DataRow In pacientes.Rows
                    Dim tiempo As Stopwatch = Stopwatch.StartNew()
                    Try
                        If repeticiones(Texto(paciente, "ccvepaciente")) <> 1 Then
                            Throw New InvalidOperationException("Más de una recepción activa; no es posible determinar una cama inequívoca.")
                        End If
                        ProcesarPaciente(paciente)
                    Catch ex As Exception
                        log("Camas: ingreso " & Texto(paciente, "Id") & " omitido. " & Diagnostico(ex))
                    Finally
                        Trace.WriteLine("Camas: procesamiento paciente ms=" & tiempo.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture))
                    End Try
                Next
                log("Camas: ingresos evaluados=" & pacientes.Rows.Count.ToString(CultureInfo.InvariantCulture))
            End Using
        Finally
            For Each material As DataRow In materiales.Values
                material.Table.Dispose()
            Next
            materiales.Clear()
            listas.Clear()
            precios.Clear()
            factorIva = Nothing
            log("Camas: finalización; ms=" & total.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture))
        End Try
    End Sub

    Private Sub ProcesarPaciente(ByVal paciente As DataRow)
        Dim ahora As DateTime = DateTime.Now
        If Texto(paciente, "cdscareaafectada") = "URGENCIAS" AndAlso
            DateDiff(DateInterval.Hour, CDate(paciente("fchregistro")), ahora) <= 8 Then Return
        Dim cama As Integer = Entero(paciente, "icvecama")
        If cama <= 0 Then Throw New InvalidOperationException("Sin cama válida.")
        Dim consultaMaterial As Stopwatch = Stopwatch.StartNew()
        Dim material As DataRow = ObtenerMaterial(Texto(paciente, "material_cama"))
        Trace.WriteLine("Camas: cama y material ms=" & consultaMaterial.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture))
        Dim recepcion As Integer = Entero(paciente, "Id")
        Dim clavePaciente As String = Texto(paciente, "ccvepaciente")
        ' Esta función se ejecuta sin cambiar su código ni su criterio de selección.
        Dim numero As String = cuentaPaciente(clavePaciente, cama)
        If String.IsNullOrWhiteSpace(numero) OrElse numero.Length > 255 Then Throw New InvalidOperationException("Sin cuenta válida.")
        Using cuenta As DataTable = leer("SELECT icveticketec,cEstatus FROM tb_ticketsec WHERE cNumTicketec=@cuenta", P("@cuenta", numero))
            If cuenta.Rows.Count <> 1 OrElse Not String.Equals(Texto(cuenta.Rows(0), "cEstatus"), "ABIERTO", StringComparison.OrdinalIgnoreCase) Then
                Throw New InvalidOperationException("Cuenta no modificable o ambigua.")
            End If
            Dim idCuenta As Integer = Entero(cuenta.Rows(0), "icveticketec")
            Dim ingreso As DateTime = primerIngreso(clavePaciente, recepcion)
            Dim idIngreso As Integer = ValidarIngreso(clavePaciente, recepcion, ingreso, ahora)
            Dim facturables As Integer = HisPeriodosCama.Total(ingreso, ahora)
            Dim cobrados As HashSet(Of Integer) = LeerPeriodos(idIngreso, idCuenta, numero, ingreso)
            Dim existentes As Integer = 0
            Dim nuevos As Integer = 0
            For periodo As Integer = 1 To facturables
                If cobrados.Contains(periodo) Then
                    existentes += 1
                    Continue For
                End If
                Dim partes As List(Of Parte) = PrepararPartes(numero, clavePaciente, material)
                RegistrarPeriodo(idIngreso, idCuenta, numero, periodo, ingreso, recepcion,
                                 clavePaciente, cama, Texto(paciente, "cdscareaafectada"), material, partes)
                nuevos += 1
            Next
            log(String.Format(CultureInfo.InvariantCulture,
                "Camas: ingreso {0}; periodos={1}; existentes={2}; nuevos={3}.", recepcion, facturables, existentes, nuevos))
        End Using
    End Sub
#End Region

#Region "Cálculo y validación de periodos"
    Private Sub VerificarTablas()
        ' No ejecutar DDL desde el Timer ni transformar tablas existentes automáticamente.
        Using tabla As DataTable = leer("SELECT id,id_ingreso,id_cuenta,periodo,fecha_ingreso,numero_cuenta,id_cama,material,partes,estado,fecha_inicio,fecha_confirmacion FROM robot_camas_periodos WHERE 1=0", Nothing)
        End Using
        Using tabla As DataTable = leer("SELECT id_periodo,parte,cantidad,precio,paquete,id_cargo FROM robot_camas_detalles WHERE 1=0", Nothing)
        End Using
        Using indices As DataTable = leer(
            "SELECT TABLE_NAME,INDEX_NAME,SEQ_IN_INDEX,COLUMN_NAME,SUB_PART FROM information_schema.STATISTICS " &
            "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('robot_camas_periodos','robot_camas_detalles') " &
            "AND NON_UNIQUE=0 ORDER BY TABLE_NAME,INDEX_NAME,SEQ_IN_INDEX", Nothing)
            Dim grupos As New Dictionary(Of String, List(Of String))(StringComparer.Ordinal)
            For Each fila As DataRow In indices.Rows
                Dim clave As String = Texto(fila, "TABLE_NAME") & ":" & Texto(fila, "INDEX_NAME")
                If Not grupos.ContainsKey(clave) Then grupos.Add(clave, New List(Of String)())
                If Not fila.IsNull("SUB_PART") Then grupos(clave).Add("<PREFIJO>")
                grupos(clave).Add(Texto(fila, "COLUMN_NAME"))
            Next
            If Not TieneIndice(grupos, "robot_camas_periodos:", "id_ingreso,id_cuenta,periodo") OrElse
                Not TieneIndice(grupos, "robot_camas_detalles:", "id_periodo,parte") OrElse
                Not TieneIndice(grupos, "robot_camas_detalles:", "id_cargo") Then
                Throw New InvalidOperationException("Faltan índices únicos en las tablas auxiliares; no se permiten cargos.")
            End If
        End Using
    End Sub

    Private Shared Function TieneIndice(ByVal grupos As Dictionary(Of String, List(Of String)), ByVal tabla As String, ByVal columnas As String) As Boolean
        For Each indice As KeyValuePair(Of String, List(Of String)) In grupos
            If indice.Key.StartsWith(tabla, StringComparison.Ordinal) AndAlso String.Join(",", indice.Value) = columnas Then Return True
        Next
        Return False
    End Function

    ''' <summary>Valida la fecha devuelta por la función existente y fija la recepción que origina ese primer ingreso.</summary>
    Private Function ValidarIngreso(ByVal paciente As String, ByVal recepcion As Integer,
                                    ByVal ingreso As DateTime, ByVal ahora As DateTime) As Integer
        If ingreso = DateTime.MinValue OrElse ingreso > ahora Then Throw New InvalidOperationException("Fecha de ingreso inválida.")
        Dim id As Integer = Integer.MaxValue
        Using fechas As DataTable = leer(
            "SELECT r.Id,r.fchhorarecepcion FROM tb_urg_recepcion r INNER JOIN cat_servicios s " &
            "ON s.cdscservicio=r.cdscareaafectada WHERE r.ccvepaciente=@paciente " &
            "AND s.cdscareaafectada IN ('HOSPITALIZACION','URGENCIAS')", P("@paciente", paciente))
            For Each fila As DataRow In fechas.Rows
                If fila.IsNull("fchhorarecepcion") Then Continue For
                Dim referencia As DateTime = CDate(fila("fchhorarecepcion"))
                referencia = referencia.AddTicks(-(referencia.Ticks Mod TimeSpan.TicksPerMinute))
                If referencia = ingreso Then id = Math.Min(id, Entero(fila, "Id"))
            Next
        End Using
        If id = Integer.MaxValue Then Throw New InvalidOperationException("La fecha devuelta no corresponde a un ingreso verificable.")
        Using egresos As DataTable = leer("SELECT id FROM tb_notamedica_egreso WHERE ccvepaciente=@paciente AND id_recepcion=@recepcion LIMIT 1", P("@paciente", paciente, "@recepcion", recepcion))
            If egresos.Rows.Count > 0 Then Throw New InvalidOperationException("El ingreso tiene nota de egreso.")
        End Using
        Return id
    End Function

    ''' <summary>Un periodo en proceso o un vínculo incompleto es una operación incierta: no se cobra de nuevo.</summary>
    Private Function LeerPeriodos(ByVal ingreso As Integer, ByVal cuenta As Integer, ByVal numero As String,
                                  ByVal fecha As DateTime) As HashSet(Of Integer)
        Dim reloj As Stopwatch = Stopwatch.StartNew()
        Dim resultado As New HashSet(Of Integer)()
        Using periodos As DataTable = leer("SELECT id,id_ingreso,id_cuenta,periodo,fecha_ingreso,numero_cuenta,id_cama,material,partes,estado FROM robot_camas_periodos WHERE id_cuenta=@cuenta OR numero_cuenta=@numero", P("@cuenta", cuenta, "@numero", numero))
            For Each fila As DataRow In periodos.Rows
                If Entero(fila, "id_ingreso") <> ingreso OrElse Entero(fila, "id_cuenta") <> cuenta OrElse
                    Texto(fila, "numero_cuenta") <> numero OrElse CDate(fila("fecha_ingreso")) <> fecha Then
                    Throw New InvalidOperationException("La cuenta contiene un control de otro ingreso; requiere conciliación.")
                End If
                If Texto(fila, "estado") <> "COMPLETO" AndAlso Texto(fila, "estado") <> "EN_PROCESO" Then
                    Throw New InvalidOperationException("Estado de control desconocido; requiere revisión.")
                End If
                VerificarVinculos(fila)
                ' Tras un corte posterior al último vínculo, es seguro confirmar lo ya existente.
                ' Si falta un vínculo, VerificarVinculos detiene la cuenta: nunca insertar para completarlo.
                If Texto(fila, "estado") = "EN_PROCESO" Then
                    If ejecutarSql("UPDATE robot_camas_periodos SET estado='COMPLETO',fecha_confirmacion=CURRENT_TIMESTAMP WHERE id=@id AND estado='EN_PROCESO'", P("@id", CLng(fila("id")))) <> 1 Then
                        Throw New InvalidOperationException("No se pudo confirmar el periodo existente; no volver a cobrar.")
                    End If
                End If
                Dim periodo As Integer = Entero(fila, "periodo")
                If periodo <= 0 OrElse Not resultado.Add(periodo) Then Throw New InvalidOperationException("Control de periodos inconsistente.")
            Next
        End Using
        ' No asignar periodos a cargos heredados por su fecha ni por su material.
        Using anteriores As DataTable = leer(
            "SELECT d.icveticketecdetalle FROM tb_ticketsec_detalle d LEFT JOIN robot_camas_detalles a " &
            "ON a.id_cargo=d.icveticketecdetalle WHERE (d.icveticketec=@cuenta OR d.cNumTicketEC=@numero) " &
            "AND d.ccveusuario='servidor_his' AND a.id_cargo IS NULL LIMIT 1", P("@cuenta", cuenta, "@numero", numero))
            If anteriores.Rows.Count > 0 Then Throw New InvalidOperationException("Cargos heredados sin periodo; requieren conciliación, no recobro.")
        End Using
        Trace.WriteLine("Camas: validación cargos ms=" & reloj.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture))
        Return resultado
    End Function

    Private Sub VerificarVinculos(ByVal periodo As DataRow)
        Using detalles As DataTable = leer(
            "SELECT a.parte,a.cantidad,a.precio,a.paquete,a.id_cargo,d.icveticketecdetalle,d.icveticketec,d.cNumTicketEC," &
            "d.icvecama,d.ccvematerial,d.iCantidad,d.iPrecioVenta,d.iCubreSP,d.ccveusuario " &
            "FROM robot_camas_detalles a LEFT JOIN tb_ticketsec_detalle d ON d.icveticketecdetalle=a.id_cargo " &
            "WHERE a.id_periodo=@id", P("@id", CLng(periodo("id"))))
            Dim partes As New List(Of Integer)()
            For Each fila As DataRow In detalles.Rows
                If fila.IsNull("id_cargo") OrElse fila.IsNull("icveticketecdetalle") OrElse
                    Entero(fila, "icveticketec") <> Entero(periodo, "id_cuenta") OrElse
                    Texto(fila, "cNumTicketEC") <> Texto(periodo, "numero_cuenta") OrElse
                    Entero(fila, "icvecama") <> Entero(periodo, "id_cama") OrElse
                    Texto(fila, "ccvematerial") <> Texto(periodo, "material") OrElse
                    Math.Abs(Numero(fila, "iCantidad") - Numero(fila, "cantidad")) > 0.00000001 OrElse
                    Math.Abs(Numero(fila, "iPrecioVenta") - Numero(fila, "precio")) > 0.00000001 OrElse
                    Entero(fila, "iCubreSP") <> Entero(fila, "paquete") OrElse Texto(fila, "ccveusuario") <> "servidor_his" Then
                    Throw New InvalidOperationException("Un cargo vinculado falta o difiere del control; no se vuelve a cobrar.")
                End If
                partes.Add(Entero(fila, "parte"))
            Next
            If Not HisPeriodosCama.Completo(partes, Entero(periodo, "partes")) Then
                Throw New InvalidOperationException("Faltan fragmentos confirmados del periodo; requiere revisión.")
            End If
        End Using
    End Sub
#End Region

#Region "Camas, precios y paquetes"
    Private Function ObtenerMaterial(ByVal clave As String) As DataRow
        If String.IsNullOrWhiteSpace(clave) OrElse clave.Length > 255 Then Throw New InvalidOperationException("Material de cama no identificado.")
        If materiales.ContainsKey(clave) Then Return materiales(clave)
        Using tabla As DataTable = leer("SELECT ccvematerial,cDescripcion,icveclave,ccveunidad,ccveanterior,iPrecioPublicoOficial,iSujetoCatalogoOficial,iExcentoIVA,productos_paquete_id FROM tb_materiales WHERE ccvematerial=@material", P("@material", clave))
            If tabla.Rows.Count <> 1 Then Throw New InvalidOperationException("Material inexistente o ambiguo.")
            Dim copia As DataTable = tabla.Copy()
            materiales.Add(clave, copia.Rows(0))
            Return copia.Rows(0)
        End Using
    End Function

    Private Function PrepararPartes(ByVal cuenta As String, ByVal paciente As String, ByVal material As DataRow) As List(Of Parte)
        If Not listas.ContainsKey(paciente) Then
            Dim lista As Integer = 1
            Using tabla As DataTable = leer("SELECT f.icvefactorsocial FROM tb_paciente p INNER JOIN cat_factorsocial2 f ON f.cClasificacion=p.cClasificacion WHERE p.ccvepaciente=@paciente", P("@paciente", paciente))
                If tabla.Rows.Count > 1 Then Throw New InvalidOperationException("Lista de precios ambigua.")
                If tabla.Rows.Count = 1 Then lista = Entero(tabla.Rows(0), "icvefactorsocial")
            End Using
            listas.Add(paciente, lista)
        End If
        Dim clave As String = Texto(material, "ccvematerial")
        Dim clavePrecio As String = clave & ":" & listas(paciente).ToString(CultureInfo.InvariantCulture)
        If Not precios.ContainsKey(clavePrecio) Then
            Using tabla As DataTable = leer("SELECT iPrecio FROM tb_materiales_precios WHERE ccvematerial=@material AND lista_precios_id=@lista", P("@material", clave, "@lista", listas(paciente)))
                If tabla.Rows.Count <> 1 Then Throw New InvalidOperationException("Precio inexistente o ambiguo.")
                precios.Add(clavePrecio, Numero(tabla.Rows(0), "iPrecio"))
            End Using
        End If
        Dim paquete As Integer = 0
        Using tabla As DataTable = leer("SELECT paquete_id FROM tb_paciente_paquete WHERE ccvepaciente=@paciente AND estatus=0", P("@paciente", paciente))
            If tabla.Rows.Count > 1 Then Throw New InvalidOperationException("Paquete activo ambiguo.")
            If tabla.Rows.Count = 1 Then paquete = Entero(tabla.Rows(0), "paquete_id")
        End Using
        Dim partes As List(Of Parte) = If(paquete = 0, New List(Of Parte) From {New Parte With {.Cantidad = 1}}, DistribuirPaquete(cuenta, material, paquete))
        Dim factor As Double = 1
        If Texto(material, "iExcentoIVA") = "I" Then
            If Not factorIva.HasValue Then
                Using tabla As DataTable = leer("SELECT iFactor FROM cat_impuestos WHERE iVigente=1", Nothing)
                    If tabla.Rows.Count <> 1 Then Throw New InvalidOperationException("Factor de IVA inexistente o ambiguo.")
                    Dim valor As Double = Numero(tabla.Rows(0), "iFactor")
                    If valor <= 0 Then Throw New InvalidOperationException("Factor de IVA inválido.")
                    factorIva = valor
                End Using
            End If
            factor = factorIva.Value
        End If
        For Each parte As Parte In partes
            parte.Precio = Math.Round(Math.Round(precios(clavePrecio) * parte.Cantidad, 2) / factor, 2)
            If paquete > 0 AndAlso parte.Paquete = 0 Then parte.Notificar = paquete
        Next
        Return partes
    End Function

    ''' <summary>Conserva cobertura por material/grupo, consumo acumulado y división del excedente; los errores se propagan.</summary>
    Private Function DistribuirPaquete(ByVal cuenta As String, ByVal material As DataRow, ByVal paquete As Integer) As List(Of Parte)
        Dim clave As String = Texto(material, "ccvematerial")
        Dim grupo As Integer = If(material.IsNull("productos_paquete_id"), 0, Entero(material, "productos_paquete_id"))
        Dim autorizadas As DataTable = Nothing
        Try
            Using directo As DataTable = leer("SELECT id FROM tb_paquete_detalle WHERE paquete_id=@paquete AND ccvematerial=@material", P("@paquete", paquete, "@material", clave))
                If directo.Rows.Count > 0 AndAlso grupo = 0 Then
                    autorizadas = leer("SELECT p.iCantidad AS cantidad,m.ccvematerial FROM tb_paquete_detalle p INNER JOIN tb_materiales m ON m.ccvematerial=p.ccvematerial WHERE p.paquete_id=@paquete AND p.ccvematerial=@material", P("@paquete", paquete, "@material", clave))
                Else
                    autorizadas = leer("SELECT p.iCantidad AS cantidad,m.ccvematerial FROM tb_paquete_detalle p INNER JOIN tb_materiales m ON m.productos_paquete_id=p.productos_paquete_id WHERE p.paquete_id=@paquete AND p.productos_paquete_id=@grupo", P("@paquete", paquete, "@grupo", grupo))
                End If
            End Using
            Dim resultado As New List(Of Parte)()
            If autorizadas.Rows.Count = 0 Then
                resultado.Add(New Parte With {.Cantidad = 1})
                Return resultado
            End If
            Dim autorizado As Double = Numero(autorizadas.Rows(0), "cantidad")
            Dim codigos As New HashSet(Of String)(StringComparer.Ordinal)
            Dim nombres As New List(Of String)()
            Dim valores As Dictionary(Of String, Object) = P("@cuenta", cuenta)
            For Each fila As DataRow In autorizadas.Rows
                If Numero(fila, "cantidad") <> autorizado Then Throw New InvalidOperationException("Cantidad autorizada de paquete ambigua.")
                Dim codigo As String = Texto(fila, "ccvematerial")
                If codigos.Add(codigo) Then
                    Dim nombre As String = "@m" & nombres.Count.ToString(CultureInfo.InvariantCulture)
                    nombres.Add(nombre)
                    valores.Add(nombre, codigo)
                End If
            Next
            Dim consumido As Double
            Using tabla As DataTable = leer("SELECT COALESCE(SUM(iCantidad),0) AS cantidad FROM tb_ticketsec_detalle WHERE cNumTicketEC=@cuenta AND ccvematerial IN (" & String.Join(",", nombres) & ")", valores)
                consumido = Numero(tabla.Rows(0), "cantidad")
            End Using
            If consumido + 1 <= autorizado Then
                resultado.Add(New Parte With {.Cantidad = 1, .Paquete = paquete})
            ElseIf consumido > autorizado Then
                resultado.Add(New Parte With {.Cantidad = 1})
            Else
                Dim excedente As Double = consumido + 1 - autorizado
                resultado.Add(New Parte With {.Cantidad = excedente, .Division = True})
                If 1 - excedente > 0 Then resultado.Add(New Parte With {.Cantidad = 1 - excedente, .Paquete = paquete, .Division = True})
            End If
            Return resultado
        Finally
            If autorizadas IsNot Nothing Then autorizadas.Dispose()
        End Try
    End Function
#End Region

#Region "Registro de cargos"
    ''' <summary>Registra el inicio antes de cobrar. El control persiste tras errores y reinicios y nunca se retoma para insertar cargos automáticamente.</summary>
    Private Sub RegistrarPeriodo(ByVal ingresoId As Integer, ByVal cuentaId As Integer, ByVal cuenta As String,
                                 ByVal periodo As Integer, ByVal ingreso As DateTime, ByVal recepcion As Integer,
                                 ByVal paciente As String, ByVal cama As Integer, ByVal servicio As String,
                                 ByVal material As DataRow, ByVal partes As List(Of Parte))
        Dim clave As String = Texto(material, "ccvematerial")
        Dim publico As Double = Numero(material, "iPrecioPublicoOficial")
        Dim oficial As Integer = Entero(material, "iSujetoCatalogoOficial")
        Dim cobro As Integer = Entero(material, "icveclave")
        If ingresoId <= 0 OrElse cuentaId <= 0 OrElse cobro <= 0 Then Throw New InvalidOperationException("Identificadores de cargo inválidos.")
        For Each parte As Parte In partes
            If parte.Cantidad <= 0 OrElse Double.IsNaN(parte.Precio) OrElse Double.IsInfinity(parte.Precio) Then
                Throw New InvalidOperationException("Cantidad o importe inválido.")
            End If
        Next
        Dim diagnostico As String = ""
        Using tabla As DataTable = leer("SELECT icvdiagnostico FROM tb_ticketsec_detalle WHERE cNumTicketEC=@cuenta ORDER BY icveticketecdetalle DESC LIMIT 1", P("@cuenta", cuenta))
            If tabla.Rows.Count > 0 Then diagnostico = Texto(tabla.Rows(0), "icvdiagnostico")
        End Using
        Dim unidad As String = Texto(material, "ccveunidad")
        If String.IsNullOrWhiteSpace(unidad) Then unidad = "ser"
        ValidarVigencia(recepcion, paciente, cama, cuenta)
        Using previo As DataTable = leer("SELECT id FROM robot_camas_periodos WHERE id_ingreso=@ingreso AND id_cuenta=@cuenta AND periodo=@periodo LIMIT 1", P("@ingreso", ingresoId, "@cuenta", cuentaId, "@periodo", periodo))
            If previo.Rows.Count > 0 Then Throw New InvalidOperationException("El periodo ya tiene un registro de inicio; no insertar cargos.")
        End Using
        AjustarFechaCuenta(cuenta)
        Dim idControl As Long = insertar(
            "INSERT INTO robot_camas_periodos (id_ingreso,id_cuenta,periodo,fecha_ingreso,numero_cuenta,id_cama,material,partes,estado,fecha_inicio) " &
            "VALUES (@ingreso,@cuenta,@periodo,@fecha,@numero,@cama,@material,@partes,'EN_PROCESO',CURRENT_TIMESTAMP)",
            P("@ingreso", ingresoId, "@cuenta", cuentaId, "@periodo", periodo, "@fecha", ingreso, "@numero", cuenta,
              "@cama", cama, "@material", clave, "@partes", partes.Count))
        If idControl <= 0 Then Throw New InvalidOperationException("No se confirmó el inicio del periodo. No reintentar.")
        For i As Integer = 0 To partes.Count - 1
            If ejecutarSql("INSERT INTO robot_camas_detalles (id_periodo,parte,cantidad,precio,paquete) VALUES (@id,@parte,@cantidad,@precio,@paquete)",
                           P("@id", idControl, "@parte", i + 1, "@cantidad", partes(i).Cantidad, "@precio", partes(i).Precio, "@paquete", partes(i).Paquete)) <> 1 Then
                Throw New InvalidOperationException("No se confirmó el plan del periodo; conservar el periodo en proceso para revisión.")
            End If
        Next
        For i As Integer = 0 To partes.Count - 1
            Dim parte As Parte = partes(i)
            Dim valores As Dictionary(Of String, Object) = P("@clues", clues, "@id", cuentaId, "@cuenta", cuenta,
                "@cantidad", parte.Cantidad, "@descripcion", Texto(material, "cDescripcion").Trim(), "@material", clave,
                "@clave", cobro, "@cama", cama, "@anterior", Texto(material, "ccveanterior"), "@unidad", unidad,
                "@precio", parte.Precio, "@publico", publico, "@oficial", oficial, "@iva", Texto(material, "iExcentoIVA"),
                "@servicio", servicio, "@dx", diagnostico, "@paquete", parte.Paquete, "@recepcion", recepcion, "@paciente", paciente)
            ' El mismo INSERT comprueba cama, material, egreso y cuenta abierta; sin modificar columnas existentes.
            Dim cargo As Long = insertar(
                "INSERT INTO tb_ticketsec_detalle (clues,icveticketec,cNumTicketEC,iCantidad,cDescripcion,ccvematerial,icveclave,icvecama," &
                "cCodigoBarras,ccveanterior,ccveunidad,iPrecioVenta,iPrecioPublico,iPrecioSujetoCatOficial,cExentoIVA,cNumLote,iDescuento," &
                "cDoctoAbono,cdscareaafectada,icvdiagnostico,iCubreSP,ccvematerial_cubresp,fchregistro,motivo_registro_manual,ccveusuario) " &
                "SELECT @clues,@id,@cuenta,@cantidad,@descripcion,@material,@clave,@cama,@material,@anterior,@unidad,@precio," &
                "@publico,@oficial,@iva,'',0,'',@servicio,@dx,@paquete,'',CURRENT_TIMESTAMP,'','servidor_his' " &
                "FROM tb_urg_recepcion r INNER JOIN tb_ticketsec e ON e.ccvepaciente=r.ccvepaciente " &
                "INNER JOIN cat_camas c ON c.icvecama=r.icvecama WHERE r.Id=@recepcion AND r.ccvepaciente=@paciente " &
                "AND r.iegresado=0 AND r.icvecama=@cama AND c.ccvematerial=@material AND e.icveticketec=@id " &
                "AND e.cNumTicketec=@cuenta AND e.cEstatus='ABIERTO'", valores)
            If cargo <= 0 Then Throw New InvalidOperationException("Cargo no confirmado. Revisar el periodo en proceso sin recobrar.")
            If ejecutarSql("UPDATE robot_camas_detalles SET id_cargo=@cargo WHERE id_periodo=@id AND parte=@parte AND id_cargo IS NULL",
                           P("@cargo", cargo, "@id", idControl, "@parte", i + 1)) <> 1 Then
                Throw New InvalidOperationException("Cargo insertado sin confirmación del vínculo; no volver a cobrar.")
            End If
        Next
        Using control As DataTable = leer("SELECT id,id_cuenta,numero_cuenta,id_cama,material,partes FROM robot_camas_periodos WHERE id=@id", P("@id", idControl))
            If control.Rows.Count <> 1 Then Throw New InvalidOperationException("No se puede confirmar el periodo en proceso.")
            VerificarVinculos(control.Rows(0))
        End Using
        If ejecutarSql("UPDATE robot_camas_periodos SET estado='COMPLETO',fecha_confirmacion=CURRENT_TIMESTAMP WHERE id=@id AND estado='EN_PROCESO'", P("@id", idControl)) <> 1 Then
            Throw New InvalidOperationException("Confirmación incierta del periodo; conservar el periodo en proceso sin reintentos.")
        End If
        NotificarExcedentes(paciente, clave, Texto(material, "cDescripcion").Trim(), unidad, partes)
    End Sub

    Private Sub ValidarVigencia(ByVal recepcion As Integer, ByVal paciente As String, ByVal cama As Integer, ByVal cuenta As String)
        Using tabla As DataTable = leer("SELECT r.Id FROM tb_urg_recepcion r INNER JOIN tb_ticketsec e ON e.ccvepaciente=r.ccvepaciente WHERE r.Id=@id AND r.ccvepaciente=@paciente AND r.iegresado=0 AND r.icvecama=@cama AND e.cNumTicketec=@cuenta AND e.cEstatus='ABIERTO'", P("@id", recepcion, "@paciente", paciente, "@cama", cama, "@cuenta", cuenta))
            If tabla.Rows.Count <> 1 Then Throw New InvalidOperationException("Cambió el ingreso, la cama o la cuenta.")
        End Using
    End Sub

    Private Sub AjustarFechaCuenta(ByVal cuenta As String)
        Using tabla As DataTable = leer("SELECT e.fchTicketec,EXISTS(SELECT 1 FROM tb_ticketsec_detalle d WHERE d.cNumTicketEC=e.cNumTicketec) AS tiene_detalle FROM tb_ticketsec e WHERE e.cNumTicketec=@cuenta AND e.cEstatus='ABIERTO'", P("@cuenta", cuenta))
            If tabla.Rows.Count <> 1 Then Throw New InvalidOperationException("Cuenta no modificable.")
            Dim fecha As DateTime = CDate(tabla.Rows(0)("fchTicketec"))
            Dim ahora As DateTime = DateTime.Now
            If (Entero(tabla.Rows(0), "tiene_detalle") = 0 AndAlso fecha < ahora) OrElse fecha > ahora Then
                If ejecutarSql("UPDATE tb_ticketsec SET fchTicketec=@fecha WHERE cNumTicketec=@cuenta AND cEstatus='ABIERTO'", P("@fecha", ahora.Date, "@cuenta", cuenta)) <> 1 Then
                    Throw New InvalidOperationException("No se confirmó la fecha de la cuenta.")
                End If
            End If
        End Using
    End Sub

    Private Sub NotificarExcedentes(ByVal paciente As String, ByVal material As String, ByVal descripcion As String,
                                   ByVal unidad As String, ByVal partes As List(Of Parte))
        For Each parte As Parte In partes
            If parte.Notificar = 0 OrElse (Not parte.Division AndAlso parte.Precio <= 0) Then Continue For
            Try
                If ejecutarSql("INSERT INTO tb_paquetes_notificaciones (clues,paquete_id,ccvepaciente,ccvematerial,descripcion,cantidad_unitario,ccveunidad_unitario,enviado) VALUES (@clues,@paquete,@paciente,@material,@descripcion,@cantidad,@unidad,0)",
                    P("@clues", clues, "@paquete", parte.Notificar, "@paciente", paciente, "@material", material,
                      "@descripcion", descripcion, "@cantidad", parte.Cantidad, "@unidad", unidad)) <> 1 Then
                    Throw New InvalidOperationException("No se confirmó la notificación.")
                End If
            Catch ex As Exception
                log("Camas: cargos confirmados; notificación de excedente pendiente de revisión. " & ex.GetType().Name)
            End Try
        Next
    End Sub
#End Region

#Region "Conexión local y diagnóstico"
    ' Acceso exclusivo de esta rutina sobre la conexión local clonada del formulario.
    ' No utilizar los helpers heredados para cargos: reintentan INSERT y pueden ocultar errores.
    Private Shared Function Comando(ByVal local As MySqlConnection, ByVal sql As String, ByVal valores As IDictionary(Of String, Object)) As MySqlCommand
        Dim resultado As New MySqlCommand(sql, local)
        If valores IsNot Nothing Then
            For Each valor As KeyValuePair(Of String, Object) In valores
                resultado.Parameters.AddWithValue(valor.Key, If(valor.Value, DBNull.Value))
            Next
        End If
        Return resultado
    End Function

    Private Shared Function ConsultarConexion(ByVal local As MySqlConnection, ByVal sql As String, ByVal valores As IDictionary(Of String, Object)) As DataTable
        Dim tabla As New DataTable()
        Try
            Using cmd As MySqlCommand = Comando(local, sql, valores)
                Using adaptador As New MySqlDataAdapter(cmd)
                    adaptador.Fill(tabla)
                End Using
            End Using
            Return tabla
        Catch
            tabla.Dispose()
            Throw
        End Try
    End Function

    Private Shared Function InsertarConexion(ByVal local As MySqlConnection, ByVal sql As String, ByVal valores As IDictionary(Of String, Object)) As Long
        Using cmd As MySqlCommand = Comando(local, sql, valores)
            If cmd.ExecuteNonQuery() <> 1 OrElse cmd.LastInsertedId <= 0 Then Throw New InvalidOperationException("Inserción sin confirmación. No reintentar.")
            Return cmd.LastInsertedId
        End Using
    End Function

    Private Shared Function EjecutarConexion(ByVal local As MySqlConnection, ByVal sql As String, ByVal valores As IDictionary(Of String, Object)) As Integer
        Using cmd As MySqlCommand = Comando(local, sql, valores)
            Return cmd.ExecuteNonQuery()
        End Using
    End Function

    Private Shared Function P(ParamArray pares() As Object) As Dictionary(Of String, Object)
        Dim resultado As New Dictionary(Of String, Object)()
        For i As Integer = 0 To pares.Length - 1 Step 2
            resultado.Add(CStr(pares(i)), pares(i + 1))
        Next
        Return resultado
    End Function

    Private Shared Function Texto(ByVal fila As DataRow, ByVal columna As String) As String
        Return If(fila.IsNull(columna), String.Empty, Convert.ToString(fila(columna), CultureInfo.InvariantCulture))
    End Function

    Private Shared Function Entero(ByVal fila As DataRow, ByVal columna As String) As Integer
        If fila.IsNull(columna) Then Throw New InvalidOperationException("Falta un identificador requerido.")
        Return Convert.ToInt32(fila(columna), CultureInfo.InvariantCulture)
    End Function

    Private Shared Function Numero(ByVal fila As DataRow, ByVal columna As String) As Double
        If fila.IsNull(columna) Then Throw New InvalidOperationException("Falta un valor financiero requerido.")
        Dim valor As Double = Convert.ToDouble(fila(columna), CultureInfo.InvariantCulture)
        If Double.IsNaN(valor) OrElse Double.IsInfinity(valor) Then Throw New InvalidOperationException("Valor financiero no válido.")
        Return valor
    End Function

    Private Shared Function Diagnostico(ByVal ex As Exception) As String
        If ex.GetType() Is GetType(InvalidOperationException) AndAlso ex.InnerException Is Nothing Then Return ex.Message
        Return ex.GetType().Name & ": revisar conexión y esquema en desarrollo."
    End Function
#End Region
End Class
