Option Explicit On
Option Strict On

Imports System
Imports System.Data
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports MySqlConnector

' Integración optativa en 127.0.0.1: solo se escribe en tablas TEMPORARY de esta sesión.
' No lanzar el Robot. Ningún dato clínico se copia desde las tablas originales.
Module CargosCamaMySqlTests
    Private conexion As MySqlConnection
    Private ReadOnly temporales As String() = {"tb_urg_recepcion", "cat_servicios", "cat_camas", "tb_materiales",
        "tb_ticketsec", "tb_ticketsec_detalle", "tb_notamedica_egreso", "tb_paciente", "cat_factorsocial2",
        "tb_materiales_precios", "tb_paciente_paquete", "tb_paquete_detalle", "cat_impuestos",
        "tb_paquetes_notificaciones", "robot_camas_periodos", "robot_camas_detalles"}
    Private fechaIngreso As DateTime
    Private pruebas As Integer
    Private intentoCargo As Integer
    Private fallarCargo As Integer
    Private respuestaPerdida As Boolean
    Private fallarConfirmacion As Boolean
    Private ReadOnly mensajes As New List(Of String)()
    Private fase As String = "configuración local"

    Public Sub Main()
        Try
            Dim configuracion As String = Environment.GetEnvironmentVariable("HISTOMEDIC_CAMA_TEST_LOCAL")
            Dim valores As New MySqlConnectionStringBuilder(configuracion)
            If valores.Server <> "127.0.0.1" OrElse valores.Database <> "histoclin" Then Throw New InvalidOperationException("El arnés solo permite el entorno local confirmado.")
            Using local As New MySqlConnection(valores.ConnectionString)
                conexion = local
                fase = "abrir sesión local"
                local.Open()
                For Each nombre As String In temporales
                    fase = "crear copia temporal de " & nombre
                    Dim definicion As String
                    Using esquema As New MySqlCommand("SHOW CREATE TABLE " & nombre, local)
                        Using lector As MySqlDataReader = esquema.ExecuteReader()
                            If Not lector.Read() Then Throw New InvalidOperationException("Falta el esquema de prueba.")
                            definicion = lector.GetString(1)
                        End Using
                    End Using
                    If Not definicion.StartsWith("CREATE TABLE ", StringComparison.OrdinalIgnoreCase) Then Throw New InvalidOperationException("El objeto de origen no es una tabla.")
                    ' MySQL 5.1 no admite LIKE con el mismo nombre de destino temporal y origen.
                    Using cmd As New MySqlCommand("CREATE TEMPORARY TABLE " & definicion.Substring("CREATE TABLE ".Length), local)
                        cmd.ExecuteNonQuery()
                    End Using
                    VerificarTemporal(nombre)
                Next
                Preparar(72)
                Correr()
                Comprobar(Contar("tb_ticketsec_detalle") = 4, "72 horas generan cuatro cargos")
                Correr()
                Comprobar(Contar("tb_ticketsec_detalle") = 4, "repetición conserva cuatro cargos")
                Comprobar(Confirmados() = 4, "cuatro controles confirmados")
                Escribir("UPDATE cat_camas SET ccvematerial='OTRA-CAMA' WHERE icvecama=30", Nothing)
                Escribir("INSERT INTO tb_materiales (ccvematerial,cDescripcion,icveclave,ccveunidad,iPrecioPublicoOficial,iSujetoCatalogoOficial,iExcentoIVA,productos_paquete_id) VALUES ('OTRA-CAMA','Temporal',1,'ser',150,0,'I',0)", Nothing)
                Correr()
                Comprobar(Contar("tb_ticketsec_detalle") = 4, "cambio de material no duplica periodos")

                Preparar(2)
                Correr()
                Comprobar(Contar("tb_ticketsec_detalle") = 1, "cargo anticipado con dos horas")
                Using importe As DataTable = Consultar("SELECT iPrecioVenta,iPrecioPublico FROM tb_ticketsec_detalle", Nothing)
                    Comprobar(CDbl(importe.Rows(0)("iPrecioVenta")) = 100 AndAlso CDbl(importe.Rows(0)("iPrecioPublico")) = 150, "importes reales de precio e IVA")
                End Using
                Preparar(2)
                Escribir("UPDATE tb_ticketsec SET cEstatus='Abierto'", Nothing)
                Correr()
                Comprobar(Contar("tb_ticketsec_detalle") = 1, "estado abierto conserva comparación sin distinguir mayúsculas")

                Preparar(2)
                Escribir("UPDATE tb_ticketsec SET cEstatus='CERRADO'", Nothing)
                Correr()
                Comprobar(Contar("tb_ticketsec_detalle") = 0 AndAlso Contar("robot_camas_periodos") = 0, "cuenta cerrada")
                Preparar(2)
                Escribir("UPDATE tb_urg_recepcion SET iEgresado=1", Nothing)
                Correr()
                Comprobar(Contar("tb_ticketsec_detalle") = 0, "paciente egresado")

                Preparar(2)
                AgregarPaquete(0.5)
                Correr()
                Comprobar(Contar("tb_ticketsec_detalle") = 2 AndAlso Confirmados() = 1, "paquete dividido y confirmado")
                Comprobar(Contar("tb_paquetes_notificaciones") = 1, "notificación de excedente")
                Correr()
                Comprobar(Contar("tb_ticketsec_detalle") = 2, "repetición de paquete dividido")

                Preparar(2)
                AgregarPaquete(0.5)
                fallarCargo = 2
                Correr()
                Comprobar(Contar("tb_ticketsec_detalle") = 1 AndAlso Confirmados() = 0, "interrupción entre fragmentos MyISAM")
                fallarCargo = 0
                Correr()
                Comprobar(Contar("tb_ticketsec_detalle") = 1, "periodo incompleto impide recobro")

                Preparar(2)
                respuestaPerdida = True
                Correr()
                respuestaPerdida = False
                Correr()
                Comprobar(Contar("tb_ticketsec_detalle") = 1 AndAlso Confirmados() = 0, "respuesta perdida después del INSERT real")

                Preparar(2)
                fallarConfirmacion = True
                Correr()
                fallarConfirmacion = False
                Correr()
                Comprobar(Contar("tb_ticketsec_detalle") = 1 AndAlso Confirmados() = 1, "recuperar confirmación sin repetir INSERT")

                ' Se verifican restricciones reales del motor, no solo el estado en memoria.
                Dim duplicadoRechazado As Boolean = False
                Try
                    Dim duplicado As New Dictionary(Of String, Object) From {{"@fecha", fechaIngreso}}
                    Insertar("INSERT INTO robot_camas_periodos (id_ingreso,id_cuenta,periodo,fecha_ingreso,numero_cuenta,id_cama,material,partes,estado,fecha_inicio) VALUES (10,20,1,@fecha,'EC-H-TEST',30,'CAMA-TEST',1,'EN_PROCESO',CURRENT_TIMESTAMP)", duplicado)
                Catch ex As MySqlException
                    duplicadoRechazado = ex.Number = 1062
                End Try
                Comprobar(duplicadoRechazado, "índice único rechaza identidad repetida")
                Console.WriteLine("Integración MySQL local aprobada: " & pruebas.ToString() & " comprobaciones, solo tablas temporales.")
            End Using
        Catch ex As Exception
            Console.Error.WriteLine("Integración local fallida: " & ex.GetType().Name)
            Console.Error.WriteLine("Fase: " & fase)
            If TypeOf ex Is MySqlException Then
                Dim fallo As MySqlException = DirectCast(ex, MySqlException)
                Console.Error.WriteLine("Código MySQL: " & fallo.Number.ToString())
                If fallo.Number = 1054 OrElse fallo.Number = 1146 OrElse fallo.Number = 1136 OrElse fallo.Number = 1064 Then
                    Console.Error.WriteLine(fallo.Message)
                End If
            End If
            Dim causa As Exception = ex.InnerException
            While causa IsNot Nothing
                Console.Error.WriteLine("Causa: " & causa.GetType().Name)
                If TypeOf causa Is IO.FileNotFoundException Then Console.Error.WriteLine(DirectCast(causa, IO.FileNotFoundException).FileName)
                If TypeOf causa Is IO.FileLoadException Then Console.Error.WriteLine(DirectCast(causa, IO.FileLoadException).FileName)
                If TypeOf causa Is Net.Sockets.SocketException Then Console.Error.WriteLine(DirectCast(causa, Net.Sockets.SocketException).SocketErrorCode.ToString())
                causa = causa.InnerException
            End While
            If ex.GetType() Is GetType(InvalidOperationException) Then Console.Error.WriteLine(ex.Message)
            Environment.ExitCode = 1
        End Try
    End Sub

    Private Sub Preparar(ByVal horas As Double)
        For Each tabla As String In temporales
            Escribir("TRUNCATE TABLE " & tabla, Nothing)
        Next
        Using modo As New MySqlCommand("SET SESSION sql_mode='NO_AUTO_VALUE_ON_ZERO'", conexion)
            modo.ExecuteNonQuery()
        End Using
        Dim referencia As DateTime = DateTime.Now.AddHours(-horas)
        fechaIngreso = referencia.AddTicks(-(referencia.Ticks Mod TimeSpan.TicksPerMinute))
        Escribir("INSERT INTO cat_servicios (cdscservicio,cdscareaafectada) VALUES ('HOSPITALIZACION','HOSPITALIZACION')", Nothing)
        Escribir("INSERT INTO cat_camas (icvecama,ccvematerial) VALUES (30,'CAMA-TEST')", Nothing)
        Escribir("INSERT INTO tb_materiales (ccvematerial,cDescripcion,icveclave,ccveunidad,iPrecioPublicoOficial,iSujetoCatalogoOficial,iExcentoIVA,productos_paquete_id) VALUES ('CAMA-TEST','Solo temporal',1,'ser',150,0,'I',0)", Nothing)
        Escribir("INSERT INTO cat_factorsocial2 (icvefactorsocial,cClasificacion) VALUES (1,'TEST')", Nothing)
        Escribir("INSERT INTO tb_paciente (ccvepaciente,cClasificacion) VALUES ('ROBOT-TEST','TEST')", Nothing)
        Escribir("INSERT INTO tb_materiales_precios (ccvematerial,lista_precios_id,iPrecio) VALUES ('CAMA-TEST',1,116)", Nothing)
        Escribir("INSERT INTO cat_impuestos (iVigente,iFactor) VALUES (1,1.16)", Nothing)
        Dim p As New Dictionary(Of String, Object) From {{"@fecha", fechaIngreso}}
        Escribir("INSERT INTO tb_ticketsec (icveticketec,cNumTicketec,ccvepaciente,cEstatus,fchTicketec) VALUES (20,'EC-H-TEST','ROBOT-TEST','ABIERTO',@fecha)", p)
        Escribir("INSERT INTO tb_urg_recepcion (Id,ccvepaciente,cdscareaafectada,fchregistro,fchhorarecepcion,icvecama,iEgresado) VALUES (10,'ROBOT-TEST','HOSPITALIZACION',@fecha,@fecha,30,0)", p)
        intentoCargo = 0
        fallarCargo = 0
        respuestaPerdida = False
        fallarConfirmacion = False
        mensajes.Clear()
    End Sub

    Private Sub AgregarPaquete(ByVal cantidad As Double)
        Escribir("INSERT INTO tb_paciente_paquete (ccvepaciente,estatus,paquete_id) VALUES ('ROBOT-TEST',0,7)", Nothing)
        Escribir("INSERT INTO tb_paquete_detalle (paquete_id,ccvematerial,productos_paquete_id,iCantidad) VALUES (7,'CAMA-TEST',0,@cantidad)", New Dictionary(Of String, Object) From {{"@cantidad", cantidad}})
    End Sub

    Private Sub Correr()
        Dim servicio As New HisCargosHospitalizacionAutomaticos(AddressOf Consultar, AddressOf Insertar, AddressOf Escribir,
            "TEST", Function(paciente, cama) "EC-H-TEST", Function(paciente, recepcion) fechaIngreso,
            Sub(mensaje) mensajes.Add(mensaje))
        servicio.Ejecutar()
    End Sub

    Private Sub Comprobar(ByVal condicion As Boolean, ByVal descripcion As String)
        If Not condicion Then
            For Each mensaje As String In mensajes
                Console.Error.WriteLine(mensaje)
            Next
            Throw New InvalidOperationException("FALLÓ: " & descripcion)
        End If
        pruebas += 1
    End Sub

    Private Sub VerificarTemporal(ByVal nombre As String)
        If conexion.State <> ConnectionState.Open Then Throw New InvalidOperationException("La sesión temporal se perdió; no reconectar.")
        If Array.IndexOf(temporales, nombre) < 0 Then Throw New InvalidOperationException("Tabla de prueba no permitida.")
        Using cmd As New MySqlCommand("SHOW CREATE TABLE " & nombre, conexion)
            Using lector As MySqlDataReader = cmd.ExecuteReader()
                If Not lector.Read() OrElse Not lector.GetString(1).StartsWith("CREATE TEMPORARY TABLE", StringComparison.OrdinalIgnoreCase) Then
                    Throw New InvalidOperationException("Se rechazó una escritura sobre una tabla que no es temporal.")
                End If
            End Using
        End Using
    End Sub

    Private Function CrearComando(ByVal sql As String, ByVal parametros As IDictionary(Of String, Object)) As MySqlCommand
        If conexion.State <> ConnectionState.Open Then Throw New InvalidOperationException("Sesión de prueba cerrada; no reconectar.")
        Dim cmd As New MySqlCommand(sql, conexion)
        If parametros IsNot Nothing Then
            For Each p As KeyValuePair(Of String, Object) In parametros
                cmd.Parameters.AddWithValue(p.Key, p.Value)
            Next
        End If
        Return cmd
    End Function

    Private Sub VerificarDestino(ByVal sql As String)
        Dim destino As Match = Regex.Match(sql, "^(?:INSERT INTO|UPDATE|TRUNCATE TABLE)\s+([a-z_][a-z0-9_]*)", RegexOptions.IgnoreCase)
        If Not destino.Success Then Throw New InvalidOperationException("Escritura no permitida por el arnés temporal.")
        VerificarTemporal(destino.Groups(1).Value)
    End Sub

    Private Function Consultar(ByVal sql As String, ByVal parametros As IDictionary(Of String, Object)) As DataTable
        fase = "consulta de prueba: " & sql.Substring(0, Math.Min(80, sql.Length))
        Using cmd As MySqlCommand = CrearComando(sql, parametros)
            Using lector As MySqlDataReader = cmd.ExecuteReader()
                Dim tabla As New DataTable()
                tabla.Load(lector)
                Return tabla
            End Using
        End Using
    End Function

    Private Function Insertar(ByVal sql As String, ByVal parametros As IDictionary(Of String, Object)) As Long
        VerificarDestino(sql)
        If sql.StartsWith("INSERT INTO tb_ticketsec_detalle", StringComparison.Ordinal) Then
            intentoCargo += 1
            If intentoCargo = fallarCargo Then Throw New InvalidOperationException("Corte simulado antes del segundo cargo.")
        End If
        Using cmd As MySqlCommand = CrearComando(sql, parametros)
            If cmd.ExecuteNonQuery() <> 1 Then Throw New InvalidOperationException("Inserción no confirmada en prueba.")
            If respuestaPerdida AndAlso sql.StartsWith("INSERT INTO tb_ticketsec_detalle", StringComparison.Ordinal) Then
                Throw New InvalidOperationException("Corte simulado después del INSERT persistido.")
            End If
            Return cmd.LastInsertedId
        End Using
    End Function

    Private Function Escribir(ByVal sql As String, ByVal parametros As IDictionary(Of String, Object)) As Integer
        VerificarDestino(sql)
        fase = "escritura temporal: " & sql.Substring(0, Math.Min(80, sql.Length))
        If fallarConfirmacion AndAlso sql.StartsWith("UPDATE robot_camas_periodos", StringComparison.Ordinal) Then Throw New InvalidOperationException("Corte simulado antes de confirmar.")
        Using cmd As MySqlCommand = CrearComando(sql, parametros)
            Return cmd.ExecuteNonQuery()
        End Using
    End Function

    Private Function Contar(ByVal tabla As String) As Integer
        VerificarTemporal(tabla)
        Using cmd As New MySqlCommand("SELECT COUNT(*) FROM " & tabla, conexion)
            Return Convert.ToInt32(cmd.ExecuteScalar())
        End Using
    End Function

    Private Function Confirmados() As Integer
        VerificarTemporal("robot_camas_periodos")
        Using cmd As New MySqlCommand("SELECT COUNT(*) FROM robot_camas_periodos WHERE estado='COMPLETO'", conexion)
            Return Convert.ToInt32(cmd.ExecuteScalar())
        End Using
    End Function
End Module
