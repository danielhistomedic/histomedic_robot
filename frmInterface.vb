Imports System.Drawing.Printing
Imports System.IO
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Net
Imports System.Xml
Imports Newtonsoft.Json

Public Class frmInterface

    Public cnn_MySQL_Admin As New MySqlConnector.MySqlConnection
    Public cnn_MySQL_Central As New MySqlConnector.MySqlConnection
    Public cnn_MySQL_Local As New MySqlConnector.MySqlConnection
    Public cnn_MySQL_CentralAsync As New MySqlConnector.MySqlConnection
    Public cnn_MySQL_LocalAsync As New MySqlConnector.MySqlConnection

    Public cnn_MySQL_CentralAsyncALM As New MySqlConnector.MySqlConnection
    Public cnn_MySQL_LocalAsyncALM As New MySqlConnector.MySqlConnection

    Dim tb_sucursales_plat As DataTable

    Dim PorcDescAplicado As String = ""
    Dim PrecioPublico As Double = 0

    Dim AlmacenBase As String = ""
    Dim SucursalId As Integer = 0
    Public CLUES As String = ""
    Dim cadena As String = ""
    'Dim IdCaja As Integer = 0
    Dim cSiglas As String = ""

    Dim pbMaterial As New PictureBox
    Dim pbLienzoPromo As New PictureBox
    Dim pbLienzoRemate As New PictureBox
    Dim pbLienzoDirecto As New PictureBox

    Dim Impresora As String = ""
    Dim version As Integer = 0

    Dim tb_ClienteData As DataTable
    Dim IpServidor As String = ""

    Private cts As CancellationTokenSource
    Private export_hosting_tarea As Task

    ' Los comandos y el cierre de canales centrales usan el mismo bloqueo.
    Private ReadOnly bloqueoConexionCentral As New Object()
    Private ReadOnly bloqueoProcesoSP As New Object()
    Private estadoConexionCentral As Integer = 0
    Private comprobacionCentralActiva As Integer = 0
    Private robotActivo As Integer = 0
    Private cerrandoRobot As Integer = 0
    Private intentosReconexionCentral As Integer = 0
    Private proximaComprobacionCentralTicks As Long = 0
    Private WithEvents TimerReconexionCentral As New System.Timers.Timer(5000)

#Region "Propiedades"

    Protected str_FTP_USUARIO As String
    Public Property FTP_USUARIO() As String
        Get
            Return str_FTP_USUARIO
        End Get
        Set(ByVal Value As String)
            str_FTP_USUARIO = Value
        End Set
    End Property

    Protected str_FTP_PASSWORD As String
    Public Property FTP_PASSWORD() As String
        Get
            Return str_FTP_PASSWORD
        End Get
        Set(ByVal Value As String)
            str_FTP_PASSWORD = Value
        End Set
    End Property

    Protected str_FTP_IP As String
    Public Property FTP_IP() As String
        Get
            Return str_FTP_IP
        End Get
        Set(ByVal Value As String)
            str_FTP_IP = Value
        End Set
    End Property


#End Region

#Region "Funciones"

    Private Function HabilitarEstatusConexionCentral(ByVal valor As Boolean)

        If Me.IsDisposed OrElse Me.Disposing OrElse Not Me.IsHandleCreated Then Return Nothing
        If Me.InvokeRequired Then
            Me.BeginInvoke(New Action(Of Boolean)(Sub(estado) HabilitarEstatusConexionCentral(estado)), valor)
            Return Nothing
        End If

        If valor Then
            Me.btnConectarDBCentral.TextColor = Color.Green
        Else
            Me.btnConectarDBCentral.TextColor = Color.Crimson
        End If

    End Function

    Private ReadOnly Property CentralDisponible As Boolean
        Get
            Return Interlocked.CompareExchange(estadoConexionCentral, 0, 0) = 1
        End Get
    End Property

    Private Shared Function EsperaReconexionCentral(ByVal intentos As Integer) As Integer
        Select Case intentos
            Case 0 : Return 20000
            Case 1 : Return 60000
            Case 2 : Return 180000
            Case Else : Return 300000
        End Select
    End Function

    Private Shared Function EsFalloDeTransporte(ByVal fallo As Exception, ByVal canal As MySqlConnector.MySqlConnection) As Boolean
        If canal Is Nothing OrElse canal.State <> ConnectionState.Open Then Return True
        Dim causa As Exception = fallo
        While causa IsNot Nothing
            If TypeOf causa Is System.Net.Sockets.SocketException OrElse TypeOf causa Is IOException OrElse TypeOf causa Is TimeoutException Then Return True
            If TypeOf causa Is MySqlConnector.MySqlException Then
                Select Case DirectCast(causa, MySqlConnector.MySqlException).Number
                    Case 1042, 2002, 2003, 2006, 2013 : Return True
                End Select
            End If
            causa = causa.InnerException
        End While
        Return False
    End Function

    Private Sub RegistrarErrorCentral(ByVal fallo As Exception, ByVal canal As MySqlConnector.MySqlConnection, ByVal operacion As String)
        If EsFalloDeTransporte(fallo, canal) Then NotificarDesconexionCentral()
        Dim codigo As String = fallo.GetType().Name
        If TypeOf fallo Is MySqlConnector.MySqlException Then codigo &= " MySQL=" & DirectCast(fallo, MySqlConnector.MySqlException).Number.ToString()
        ' No registrar consultas, datos de pacientes ni credenciales.
        AgregarLog(100, "[Central] " & operacion & ": " & codigo & ". La función no reintenta la operación SQL.")
    End Sub

    Private Sub NotificarDesconexionCentral(Optional ByVal motivo As String = "")
        If Interlocked.Exchange(estadoConexionCentral, 0) = 1 OrElse Not String.IsNullOrWhiteSpace(motivo) Then
            intentosReconexionCentral = 0
            Interlocked.Exchange(proximaComprobacionCentralTicks, DateTime.UtcNow.AddMilliseconds(EsperaReconexionCentral(0)).Ticks)
            AgregarLog(100, "[Central] Conexión no disponible; primer reintento en 20 segundos.")
        End If
        If Not String.IsNullOrWhiteSpace(motivo) Then AgregarLog(100, "[Central] " & motivo)
        HabilitarEstatusConexionCentral(False)
        DetenerProcesoSP()
    End Sub

    Public Function DetectarEstadoConexionCentral() As Boolean
        SyncLock bloqueoConexionCentral
            Dim comprobado As MySqlConnector.MySqlConnection = Nothing
            Try
                For Each canal As MySqlConnector.MySqlConnection In New MySqlConnector.MySqlConnection() {cx_MySQL_Central, cx_MySQL_CentralAsync, cx_MySQL_CentralAsyncALM}
                    comprobado = canal
                    If canal Is Nothing OrElse canal.State <> ConnectionState.Open Then
                        NotificarDesconexionCentral()
                        Return False
                    End If
                    Using comando As New MySqlConnector.MySqlCommand("SELECT 1", canal)
                        comando.CommandTimeout = 5
                        comando.ExecuteScalar()
                    End Using
                Next
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, comprobado, "Comprobar disponibilidad")
                Return False
            End Try
        End SyncLock
    End Function

    Private Sub CerrarConexionesCentrales()
        SyncLock bloqueoConexionCentral
            For Each canal As MySqlConnector.MySqlConnection In New MySqlConnector.MySqlConnection() {cx_MySQL_Central, cx_MySQL_CentralAsync, cx_MySQL_CentralAsyncALM, cx_MySQL_Admin}
                If canal Is Nothing Then Continue For
                Try
                    canal.Close()
                    MySqlConnector.MySqlConnection.ClearPool(canal)
                Catch ex As Exception
                    LogEventos.Escribir("[Central] No se pudo liberar un canal: " & ex.GetType().Name)
                End Try
            Next
        End SyncLock
    End Sub

    Private Sub RevisarConexionCentral()
        Try
            If Not CentralDisponible Then
                DetenerProcesoSP()
                SyncLock bloqueoProcesoSP
                    ' Esperar el fin del lote cancelado sin bloquear la interfaz ni cerrar sus canales.
                    If export_hosting_tarea IsNot Nothing AndAlso Not export_hosting_tarea.IsCompleted Then
                        Interlocked.Exchange(proximaComprobacionCentralTicks, DateTime.UtcNow.AddSeconds(5).Ticks)
                        Return
                    End If
                End SyncLock
            End If
            SyncLock bloqueoConexionCentral
                If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 Then Return
                If CentralDisponible Then
                    Interlocked.Exchange(proximaComprobacionCentralTicks, DateTime.UtcNow.AddSeconds(30).Ticks)
                    DetectarEstadoConexionCentral()
                Else
                    intentosReconexionCentral += 1
                    CerrarConexionesCentrales()
                    Dim conectado As Boolean = Not String.IsNullOrWhiteSpace(Me.CLUES) AndAlso Conectar_Central(Me.CLUES)
                    If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 Then
                        CerrarConexionesCentrales()
                        Return
                    End If
                    If conectado Then
                        Interlocked.Exchange(estadoConexionCentral, 1)
                        intentosReconexionCentral = 0
                        Interlocked.Exchange(proximaComprobacionCentralTicks, DateTime.UtcNow.AddSeconds(30).Ticks)
                        HabilitarEstatusConexionCentral(True)
                        AgregarLog(100, "[Central] Los tres canales están conectados.")
                    Else
                        CerrarConexionesCentrales()
                        Dim espera As Integer = EsperaReconexionCentral(intentosReconexionCentral)
                        Interlocked.Exchange(proximaComprobacionCentralTicks, DateTime.UtcNow.AddMilliseconds(espera).Ticks)
                        HabilitarEstatusConexionCentral(False)
                        AgregarLog(100, "[Central] Intento no completado; próximo intento en " & (espera \ 1000).ToString() & " segundos.")
                    End If
                End If
            End SyncLock
            If CentralDisponible Then ReiniciarProcesoSP()
        Catch ex As Exception
            Interlocked.Exchange(estadoConexionCentral, 0)
            DetenerProcesoSP()
            HabilitarEstatusConexionCentral(False)
            Interlocked.Exchange(proximaComprobacionCentralTicks, DateTime.UtcNow.AddMinutes(5).Ticks)
            AgregarLog(100, "[Central] Falló la comprobación: " & ex.GetType().Name & ". Próximo intento en cinco minutos.")
        Finally
            Interlocked.Exchange(comprobacionCentralActiva, 0)
        End Try
    End Sub

    Private Function HabilitarEstatusConexionLocal(ByVal valor As Boolean)

        If valor Then
            Me.btnConectarLocal.TextColor = Color.Green
        Else
            Me.btnConectarLocal.TextColor = Color.Crimson
        End If

    End Function

    Private Function Conectar_Central(clues) As Boolean

        ' Durante Load todavía no está iniciado el temporizador de reconexión.
        If load_init Then Interlocked.Exchange(comprobacionCentralActiva, 1)
        Try

            Dim Database As String = ""
            Dim Uid As String = ""
            Dim Pwd As String = ""

            ' ==================================================================================================================================
            Dim cadena_conexion_admin As String = "Server=histomedic.mx;Database=mirtheda_admin;Uid=mirtheda_root;Pwd=Bsapmd2cKb*5;SSL Mode=None;"
            If cx_MySQL_Admin.State = ConnectionState.Closed Then
                If Not Test_MySQL_Admin(cadena_conexion_admin) Then
                    Return False
                End If
            End If

            tb_ClienteData = tb_Recordset_MySQL_Admin("SELECT * FROM ssf_clientes WHERE clues = '" & clues & "'")
            If tb_ClienteData.Rows.Count = 0 Then
                AgregarLog(500, ".Error de Conexión con el Servidor. ")
                Return False
            End If

            Database = tb_ClienteData.Rows(0).Item("db_name").ToString
            Uid = tb_ClienteData.Rows(0).Item("db_user").ToString
            Pwd = tb_ClienteData.Rows(0).Item("db_pass").ToString
            ' ==================================================================================================================================


            Dim cadena_conexion As String = "Server=histomedic.mx;Database=" & Database & ";Uid=" & Uid & ";Pwd=" & Pwd & ";SSL Mode=None;"
            Dim conectado As Boolean = Test_MySQL_Central(cadena_conexion) AndAlso Test_MySQL_CentralAsync(cadena_conexion) AndAlso Test_MySQL_CentralAsyncALM(cadena_conexion)
            If conectado Then
                Interlocked.Exchange(estadoConexionCentral, 1)
                HabilitarEstatusConexionCentral(True)
            End If
            Return conectado

        Catch ex As Exception
            RegistrarErrorCentral(ex, cx_MySQL_Central, "Conectar_Central")
            Return False
        Finally
            If load_init Then Interlocked.Exchange(comprobacionCentralActiva, 0)
        End Try

        Return False

    End Function

    Private Function Conectar_Local() As Boolean

        If cx_MySQL_local.State = ConnectionState.Open Then
            Me.HabilitarEstatusConexionLocal(True)
            Return True
        End If

        If cx_MySQL_local.State = ConnectionState.Closed Then
            If Test_MySQL_local("server=" & IpServidor & ";Port=1865;uid=root;pwd=Bhytmd2cKb*5;database=histoclin") Then
                Test_MySQL_localAsync("server=" & IpServidor & ";Port=1865;uid=root;pwd=Bhytmd2cKb*5;database=histoclin")
                Test_MySQL_localAsyncALM("server=" & IpServidor & ";Port=1865;uid=root;pwd=Bhytmd2cKb*5;database=histoclin")
                Me.HabilitarEstatusConexionLocal(True)
                Return True
            End If
        End If

        Me.HabilitarEstatusConexionLocal(False)

        Return False

    End Function

    ''' <summary>
    ''' Obtiene la ip del servidor desde el registro de windows en: "LOCAL_MACHINE\SOFTWARE\HISTOCLIN\IpServidor" al cual apunta el sistema.
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub Get_IpServidor()

        Dim key_Clave As Microsoft.Win32.RegistryKey
        Try
            key_Clave = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\histomedic\")
            IpServidor = key_Clave.GetValue("IpServidor", "127.0.0.1")
        Catch ex As Exception
        End Try

    End Sub

    Private Function Calcula_FechaActual() As Date

        Dim fecha As String
        fecha = Format(Today, "dd/MM/yyyy") & " " & Format(TimeOfDay, "HH:mm:ss")

        Return CDate(fecha)

    End Function

    Private Sub IniciarRutina()

        Try

            TimerEnlace.Enabled = False

            '======================================
            ' Rutinas Importar
            '======================================

            '- - Importar Cirugía agendada en el portal. - - -
            If CentralDisponible AndAlso Interlocked.CompareExchange(robotActivo, 0, 0) = 1 Then
                Try
                    Me.ImportarCirugiaTemporal()
                Catch ex As Exception
                    LogEventos.Escribir("ImportarCirugiaTemporal. " & ex.Message)
                End Try
            End If

            '- - Servicios de Sistemas y Servicios Generales - - - - - - - - - - -
            If CentralDisponible AndAlso Interlocked.CompareExchange(robotActivo, 0, 0) = 1 Then
                Try
                    ImportarFirmasRecibido()
                Catch ex As Exception
                    LogEventos.Escribir("ImportarFirmasRecibido. " & ex.Message)
                End Try
            End If

            '- - Activo Fijo - - - - - - - - - - -
            If CentralDisponible AndAlso Interlocked.CompareExchange(robotActivo, 0, 0) = 1 Then
                Try
                    ImportarFotoActivoFijo()
                Catch ex As Exception
                    LogEventos.Escribir("ImportarFotoActivoFijo. " & ex.Message)
                End Try
            End If


            ''======================================
            '' Rutinas Proceso Internos
            ''======================================

            '- - Notificacions Paquetes - - - - - - - - - - -
            Try
                Me.notificarAlertaProductoCantidadFueraPaquete()
            Catch ex As Exception
                LogEventos.Escribir("notificarAlertaProductoCantidadFueraPaquete. " & ex.Message)
            End Try

            '- - Cargar Camas - - - - - - - - - - -
            Try
                Me.Cargar_Costo_Dias_Cama_Hosp()
            Catch ex As Exception
                LogEventos.Escribir("Cargar_Costo_Dias_Cama_Hosp. " & ex.GetType().Name)
            End Try



            ''======================================
            '' Rutinas Exportar
            ''======================================

            ''== Exportar Tablas de Sistema al Hosting
            If CentralDisponible AndAlso Interlocked.CompareExchange(robotActivo, 0, 0) = 1 Then
                Try
                    Me.ExportarDataToHostingTablasEnlace()
                Catch ex As Exception
                    LogEventos.Escribir("ExportarDataToHostingTablasEnlace. " & ex.GetType().Name)
                End Try

                Try
                    Me.ExportarDataToHostingExpediente()
                Catch ex As Exception
                    LogEventos.Escribir("ExportarDataToHostingExpediente. " & ex.GetType().Name)
                End Try

                Try
                    Me.ExportarDataToHostingTablasEstudios()
                Catch ex As Exception
                    LogEventos.Escribir("ExportarDataToHostingTablasEstudios. " & ex.GetType().Name)
                End Try
            End If


            '' ======================================

            Me.ProgressBarX1.Value = 0

            '== Reinicia la aplicacion si el log, tiene mas de 5 registros.  == == == ==
            Try
                If lstLog.Items.Count > 5 Then
                    Me.lstLog.Items.Clear()
                    Me.DetenerProcesoSP()
                    TimerEnlace.Enabled = False
                    Application.Restart()
                End If
            Catch ex As Exception
                LogEventos.Escribir("Application.Restart. " & ex.Message)
            End Try

            ' == == == == == == == == == == == == == == == == == == == == == == == == ==

            ' == Cotinuamente Verificando si la task ya fue terminada
            If CentralDisponible AndAlso Interlocked.CompareExchange(robotActivo, 0, 0) = 1 Then
                Try
                    ReiniciarProcesoSP()
                Catch ex As Exception
                    LogEventos.Escribir("ReiniciarProcesoSP. " & ex.Message)
                End Try
            End If
            ' == == == == == == == == == == == == == == == =

            TimerEnlace.Enabled = True

        Catch ex As Exception
            TimerEnlace.Enabled = True
            LogEventos.Escribir("Error general. " & ex.Message)
        End Try

    End Sub

    Private Sub ExportarDataToHostingSP_Load(token As CancellationToken)

        While Not token.IsCancellationRequested
            If CentralDisponible AndAlso Interlocked.CompareExchange(robotActivo, 0, 0) = 1 Then
                Me.ExportarDataToHostingSP()
                If Not token.IsCancellationRequested AndAlso CentralDisponible Then Me.ExportarDataToHostingSP_Almacen()
            End If
            token.WaitHandle.WaitOne(1000)
        End While

    End Sub

    ''' <summary>
    ''' Sube al hosting el bloque de tablas que no estan en GetQuery_TablasAlmacen, GetQuery_TablasEnlace, GetQuery_TablasEstudios, GetQuery_TablasMedicas
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub ExportarDataToHostingSP()

        If Not CentralDisponible OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse (cts IsNot Nothing AndAlso cts.IsCancellationRequested) Then Return
        Try

            Dim limittext As String = Me.txtLimitRegistros.Text.Trim
            If Len(Me.txtLimitRegistros.Text) = 0 Then
                limittext = 2000
            End If

            Dim tb_his_replica_local As DataTable
            tb_his_replica_local = tb_Recordset_MySQL_localAsync("Select * from his_replica " & _
                                                                 "WHERE " & _
                                                                 "tabla_afectada NOT IN (" & _
                                                                 "" & GetQuery_TablasAlmacen().Trim & ", " & _
                                                                 "" & GetQuery_TablasEnlace().Trim & ", " & _
                                                                 "" & GetQuery_TablasEstudios().Trim & ", " & _
                                                                 "" & GetQuery_TablasMedicas().Trim & "" & _
                                                                 ") and " & _
                                                                 "sinc = 1 " & _
                                                                 "order by id LIMIT " & limittext & "")

            Dim tb_temp_insert As DataTable
            If tb_his_replica_local.Rows.Count = 0 Then
                Update_CentralAsync("fchactual", _
                               "fchActual = current_timestamp", _
                               "Id", 1)
                Exit Sub
            End If

            Dim campo_llave As String = ""
            Dim campo_llave_value_id As Integer = 0
            Dim fecha_str As String = ""
            Dim campo_nombre As String = ""

            ' Cache de Esquemas y Llaves Primarias para optimizar el rendimiento
            Dim columnCache As New Dictionary(Of String, DataTable)()
            Dim primaryKeyCache As New Dictionary(Of String, String)()

            Try
                If lstLog.InvokeRequired Then
                    lstLog.Invoke(Sub()
                                      Me.ProgressBarX_SP.Minimum = 0
                                      Me.ProgressBarX_SP.Maximum = tb_his_replica_local.Rows.Count
                                      Me.ProgressBarX_SP.Text = "0 de " & tb_his_replica_local.Rows.Count
                                  End Sub)
                Else
                    Me.ProgressBarX_SP.Minimum = 0
                    Me.ProgressBarX_SP.Maximum = tb_his_replica_local.Rows.Count
                    Me.ProgressBarX_SP.Text = "0 de " & tb_his_replica_local.Rows.Count
                End If
            Catch ex2 As Exception
            End Try

            Dim set_value_row As String = ""
            Dim longblob_filed_contain As Boolean = False
            Dim name_file_blob As String = ""

            For i As Integer = 0 To tb_his_replica_local.Rows.Count - 1

                If Not CentralDisponible OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse (cts IsNot Nothing AndAlso cts.IsCancellationRequested) Then Return
                longblob_filed_contain = False

                Dim table_name As String = tb_his_replica_local.Rows(i).Item("tabla_afectada").ToString

                Dim set_value As String = ""

                ' Obtener llave primaria de cachÃ© o de BD
                If Not primaryKeyCache.TryGetValue(table_name, campo_llave) Then
                    Dim tb_campo_llave As DataTable = tb_Recordset_MySQL_localAsync("SHOW COLUMNS FROM " & table_name & " WHERE Extra = 'auto_increment';")
                    If tb_campo_llave.Rows.Count > 0 Then
                        campo_llave = tb_campo_llave.Rows(0).Item("Field").ToString
                    Else
                        campo_llave = ""
                    End If
                    primaryKeyCache(table_name) = campo_llave
                End If

                If tb_his_replica_local.Rows(i).Item("tipo_operacion").ToString = "INSERT" Then

                    campo_llave_value_id = tb_his_replica_local.Rows(i).Item("id_tabla_afectada").ToString
                    tb_temp_insert = tb_Recordset_MySQL_localAsync("Select * from " & table_name & " where " & campo_llave & " = " & campo_llave_value_id & "")

                    If tb_temp_insert.Rows.Count > 0 Then

                        Dim tb_campos As DataTable = Nothing
                        If Not columnCache.TryGetValue(table_name, tb_campos) Then
                            tb_campos = tb_Recordset_MySQL_localAsync("SHOW COLUMNS FROM " & table_name & ";")
                            columnCache(table_name) = tb_campos
                        End If

                        For col As Integer = 0 To tb_campos.Rows.Count - 1

                            campo_nombre = tb_campos.Rows(col).Item("Field").ToString

                            Dim tipo As String = ""
                            tipo = tb_campos.Rows(col).Item("Type")
                            Dim texto_buscar As String = tipo
                            If texto_buscar.ToLower().Contains("int") Then
                                tipo = "int"
                            End If

                            If tipo = "date" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "datetime" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd HH:mm:ss") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "longblob" Then
                                longblob_filed_contain = True

                            ElseIf tipo = "int" Then
                                Dim campo_int As String = ""
                                Try
                                    If tb_temp_insert.Rows(0).Item(col).ToString = "" Then
                                        campo_int = "null"
                                    Else
                                        campo_int = tb_temp_insert.Rows(0).Item(col).ToString
                                    End If
                                Catch ex As Exception
                                    campo_int = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & campo_int & ", "

                            Else

                                set_value_row = ""
                                set_value_row = tb_temp_insert.Rows(0).Item(col).ToString
                                set_value_row = set_value_row.Replace("'", "\'")
                                set_value = set_value & " " & campo_nombre & " = '" & set_value_row & "', "
                            End If

                        Next

                        Dim caracterARemover As Char = " "
                        set_value = set_value.TrimEnd(New Char() {caracterARemover})

                        Dim caracterARemover_coma As Char = ","
                        set_value = set_value.TrimEnd(New Char() {caracterARemover_coma})

                    End If

                    If set_value = "" Then
                        Update_localAsync("his_replica", _
                                     "sinc = 0", _
                                     "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                    Else
                        If Insert_CentralAsync(table_name, _
                                          set_value) Then

                            If longblob_filed_contain = True Then
                                Dim tb_longblob As DataTable = tb_Recordset_MySQL_localAsync("SHOW COLUMNS FROM " & table_name & " WHERE Type = 'longblob';")
                                For col_lb As Integer = 0 To tb_longblob.Rows.Count - 1
                                    name_file_blob = tb_longblob.Rows(col_lb).Item("Field").ToString
                                    If Not IsDBNull(tb_temp_insert.Rows(0).Item(name_file_blob)) Then
                                        Try
                                            Update_FotoSistema_CentralAsync(name_file_blob, table_name, campo_llave, campo_llave_value_id, tb_temp_insert.Rows(0).Item(name_file_blob))
                                        Catch ex As Exception
                                            AgregarLog(500, "Error desconocido en Exportar Data: " & ex.Message)
                                        End Try
                                    End If
                                Next

                            End If

                            Update_CentralAsync("fchactual", _
                                           "fchActual = current_timestamp", _
                                           "Id", 1)

                            Update_localAsync("his_replica", _
                                         "sinc = 0", _
                                         "id", tb_his_replica_local.Rows(i).Item("id").ToString)

                        Else

                            Dim tb_valida_ins As DataTable
                            tb_valida_ins = tb_Recordset_MySQL_CentralAsync("Select " & campo_llave & " from " & table_name & " WHERE " & campo_llave & " = '" & campo_llave_value_id & "'")

                            If tb_valida_ins.Rows.Count > 0 Then
                                Update_localAsync("his_replica", _
                                             "sinc = 0", _
                                             "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                            End If

                        End If

                    End If


                ElseIf tb_his_replica_local.Rows(i).Item("tipo_operacion").ToString = "UPDATE" Then

                    campo_llave_value_id = tb_his_replica_local.Rows(i).Item("id_tabla_afectada").ToString
                    tb_temp_insert = tb_Recordset_MySQL_localAsync("Select * from " & table_name & " where " & campo_llave & " = " & campo_llave_value_id & "")

                    If tb_temp_insert.Rows.Count > 0 Then

                        Dim tb_campos As DataTable = Nothing
                        If Not columnCache.TryGetValue(table_name, tb_campos) Then
                            tb_campos = tb_Recordset_MySQL_localAsync("SHOW COLUMNS FROM " & table_name & ";")
                            columnCache(table_name) = tb_campos
                        End If

                        'Diccionario con las columnas excluidas por tabla
                        Dim columnasExcluidas As New Dictionary(Of String, HashSet(Of String))(StringComparer.OrdinalIgnoreCase) From {
                            {
                                "tb_servicios_sistemas",
                                New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
                                    "ccveusuario_recibe",
                                    "firmado",
                                    "fchregistro_recibe",
                                    "firma_recibe",
                                    "sinc",
                                    "foto_servicio"
                                }
                            }
                        }

                        ',
                        '    {
                        '        "tb_pacientes",
                        '        New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
                        '            "foto",
                        '            "huella",
                        '            "firma"
                        '        }
                        '    },
                        '    {
                        '        "tb_usuarios",
                        '        New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
                        '            "password",
                        '            "salt",
                        '            "token"
                        '        }
                        '    }

                        Dim columnasExcluir As HashSet(Of String) = Nothing

                        If Not columnasExcluidas.TryGetValue(table_name, columnasExcluir) Then
                            'Si la tabla no existe en el diccionario, no excluye ninguna columna
                            columnasExcluir = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                        End If


                        For col As Integer = 0 To tb_campos.Rows.Count - 1

                            campo_nombre = tb_campos.Rows(col).Item("Field").ToString

                            'Excluir columnas especÃ­ficas de tb_servicios_sistemas
                            If columnasExcluir.Contains(campo_nombre) Then
                                Continue For
                            End If

                            Dim tipo As String = ""
                            tipo = tb_campos.Rows(col).Item("Type")
                            Dim texto_buscar As String = tipo
                            If texto_buscar.ToLower().Contains("int") Then
                                tipo = "int"
                            End If

                            If tipo = "date" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "datetime" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd HH:mm:ss") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "longblob" Then

                                If Not IsDBNull(tb_temp_insert.Rows(0).Item(col)) Then
                                    Try
                                        Update_FotoSistema_CentralAsync(campo_nombre, table_name, campo_llave, campo_llave_value_id, tb_temp_insert.Rows(0).Item(col))
                                    Catch ex As Exception
                                        AgregarLog(500, "Error desconocido en Exportar Data: " & ex.Message)
                                    End Try
                                End If

                            ElseIf tipo = "int" Then

                                Dim campo_int As String = ""
                                Try

                                    If tb_temp_insert.Rows(0).Item(col).ToString = "" Then
                                        campo_int = "null"
                                    Else
                                        campo_int = tb_temp_insert.Rows(0).Item(col).ToString
                                    End If
                                Catch ex As Exception
                                    campo_int = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & campo_int & ", "

                            Else

                                set_value_row = ""
                                set_value_row = tb_temp_insert.Rows(0).Item(col).ToString
                                set_value_row = set_value_row.Replace("'", "\'")
                                set_value = set_value & " " & campo_nombre & " = '" & set_value_row & "', "
                            End If

                        Next

                        Dim caracterARemover As Char = " "
                        set_value = set_value.TrimEnd(New Char() {caracterARemover})

                        Dim caracterARemover_coma As Char = ","
                        set_value = set_value.TrimEnd(New Char() {caracterARemover_coma})

                    End If

                    If set_value = "" Then
                        Update_localAsync("his_replica", _
                                     "sinc = 0", _
                                     "id", tb_his_replica_local.Rows(i).Item("id").ToString)

                    Else
                        If Update_CentralAsync(table_name, _
                                       set_value, _
                                       campo_llave, campo_llave_value_id) Then

                            Update_CentralAsync("fchactual", _
                                           "fchActual = current_timestamp", _
                                           "Id", 1)

                            Update_localAsync("his_replica", _
                                         "sinc = 0", _
                                         "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                        End If
                    End If


                ElseIf tb_his_replica_local.Rows(i).Item("tipo_operacion").ToString = "DELETE" Then

                    campo_llave_value_id = tb_his_replica_local.Rows(i).Item("id_tabla_afectada").ToString

                    If Delete_CentralAsync("DELETE FROM " & table_name & " WHERE " & campo_llave & " = " & campo_llave_value_id) Then

                        Update_CentralAsync("fchactual", _
                                       "fchActual = current_timestamp", _
                                       "Id", 1)

                        Update_localAsync("his_replica", _
                                     "sinc = 0", _
                                     "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                    End If

                End If

                ' Optimización del progreso de la barra de progreso (throttling de UI)
                Dim current_val As Integer = i + 1
                If i Mod 10 = 0 OrElse i = tb_his_replica_local.Rows.Count - 1 Then
                    Try
                        If ProgressBarX_SP.InvokeRequired Then
                            ProgressBarX_SP.Invoke(Sub()
                                                       Me.ProgressBarX_SP.Value = current_val
                                                       Me.ProgressBarX_SP.Text = current_val.ToString() & " de " & tb_his_replica_local.Rows.Count
                                                   End Sub)
                        Else
                            Me.ProgressBarX_SP.Value = current_val
                            Me.ProgressBarX_SP.Text = current_val.ToString() & " de " & tb_his_replica_local.Rows.Count
                        End If
                    Catch ex2 As Exception
                    End Try
                End If

            Next

            '== Eliminar Cargados === 
            Delete_localAsync("DELETE from his_replica " & _
                                "WHERE " & _
                                "tabla_afectada NOT IN (" & _
                                "" & GetQuery_TablasAlmacen() & "" & _
                                "" & GetQuery_TablasEnlace() & "" & _
                                "" & GetQuery_TablasEstudios() & "" & _
                                "" & GetQuery_TablasMedicas() & "" & _
                                ") and " & _
                                "sinc = 0 ")
            '== Eliminar Cargados === 

            Try
                If ProgressBarX_SP.InvokeRequired Then
                    ProgressBarX_SP.Invoke(Sub() Me.ProgressBarX_SP.Value = 0)
                Else
                    Me.ProgressBarX_SP.Value = 0
                End If
            Catch ex2 As Exception
            End Try

        Catch ex As Exception
            AgregarLog(500, "Error desconocido en ExportarDataToHosting: " & ex.Message)
        End Try

    End Sub


    Private Sub ExportarDataToHostingSP_Almacen()

        If Not CentralDisponible OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse (cts IsNot Nothing AndAlso cts.IsCancellationRequested) Then Return
        Try

            Dim limittext As String = Me.txtLimitRegistros.Text.Trim
            If Len(Me.txtLimitRegistros.Text) = 0 Then
                limittext = 2000
            End If

            Dim tb_his_replica_local As DataTable
            tb_his_replica_local = tb_Recordset_MySQL_localAsyncALM("Select * from his_replica " & _
                                                                 "WHERE " & _
                                                                 "tabla_afectada IN (" & _
                                                                 "" & GetQuery_TablasAlmacen() & "" & _
                                                                 ") and " & _
                                                                 "sinc = 1 " & _
                                                                 "order by id LIMIT " & limittext & "")
            Dim tb_temp_insert As DataTable
            If tb_his_replica_local.Rows.Count = 0 Then
                Update_CentralAsyncALM("fchactual", _
                               "fchActual = current_timestamp", _
                               "Id", 1)
                Exit Sub
            End If

            Dim campo_llave As String = ""
            Dim campo_llave_value_id As Integer = 0
            Dim fecha_str As String = ""
            Dim campo_nombre As String = ""

            ' Cache de Esquemas y Llaves Primarias para optimizar el rendimiento
            Dim columnCache As New Dictionary(Of String, DataTable)()
            Dim primaryKeyCache As New Dictionary(Of String, String)()

            Try
                If ProgressBarX_SPALM.InvokeRequired Then
                    lstLog.Invoke(Sub()
                                      Me.ProgressBarX_SPALM.Minimum = 0
                                      Me.ProgressBarX_SPALM.Maximum = tb_his_replica_local.Rows.Count
                                      Me.ProgressBarX_SPALM.Text = "0 de " & tb_his_replica_local.Rows.Count
                                  End Sub)
                Else
                    Me.ProgressBarX_SPALM.Minimum = 0
                    Me.ProgressBarX_SPALM.Maximum = tb_his_replica_local.Rows.Count
                    Me.ProgressBarX_SPALM.Text = "0 de " & tb_his_replica_local.Rows.Count
                End If
            Catch ex2 As Exception
            End Try

            Dim set_value_row As String = ""
            Dim longblob_filed_contain As Boolean = False
            Dim name_file_blob As String = ""

            For i As Integer = 0 To tb_his_replica_local.Rows.Count - 1
                If Not CentralDisponible OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse (cts IsNot Nothing AndAlso cts.IsCancellationRequested) Then Return

                longblob_filed_contain = False

                Dim table_name As String = tb_his_replica_local.Rows(i).Item("tabla_afectada").ToString

                Dim set_value As String = ""

                ' Obtener llave primaria de cachÃ© o de BD
                If Not primaryKeyCache.TryGetValue(table_name, campo_llave) Then
                    Dim tb_campo_llave As DataTable = tb_Recordset_MySQL_localAsyncALM("SHOW COLUMNS FROM " & table_name & " WHERE Extra = 'auto_increment';")
                    If tb_campo_llave.Rows.Count > 0 Then
                        campo_llave = tb_campo_llave.Rows(0).Item("Field").ToString
                    Else
                        campo_llave = ""
                    End If
                    primaryKeyCache(table_name) = campo_llave
                End If

                If tb_his_replica_local.Rows(i).Item("tipo_operacion").ToString = "INSERT" Then

                    campo_llave_value_id = tb_his_replica_local.Rows(i).Item("id_tabla_afectada").ToString
                    tb_temp_insert = tb_Recordset_MySQL_localAsyncALM("Select * from " & table_name & " where " & campo_llave & " = " & campo_llave_value_id & "")

                    If tb_temp_insert.Rows.Count > 0 Then

                        Dim tb_campos As DataTable = Nothing
                        If Not columnCache.TryGetValue(table_name, tb_campos) Then
                            tb_campos = tb_Recordset_MySQL_localAsyncALM("SHOW COLUMNS FROM " & table_name & ";")
                            columnCache(table_name) = tb_campos
                        End If

                        For col As Integer = 0 To tb_campos.Rows.Count - 1

                            campo_nombre = tb_campos.Rows(col).Item("Field").ToString

                            Dim tipo As String = ""
                            tipo = tb_campos.Rows(col).Item("Type")
                            Dim texto_buscar As String = tipo
                            If texto_buscar.ToLower().Contains("int") Then
                                tipo = "int"
                            End If

                            If tipo = "date" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "datetime" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd HH:mm:ss") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "longblob" Then
                                longblob_filed_contain = True

                            ElseIf tipo = "int" Then
                                Dim campo_int As String = ""
                                Try
                                    If tb_temp_insert.Rows(0).Item(col).ToString = "" Then
                                        campo_int = "null"
                                    Else
                                        campo_int = tb_temp_insert.Rows(0).Item(col).ToString
                                    End If
                                Catch ex As Exception
                                    campo_int = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & campo_int & ", "

                            Else

                                set_value_row = ""
                                set_value_row = tb_temp_insert.Rows(0).Item(col).ToString
                                set_value_row = set_value_row.Replace("'", "\'")
                                set_value = set_value & " " & campo_nombre & " = '" & set_value_row & "', "
                            End If

                        Next

                        Dim caracterARemover As Char = " "
                        set_value = set_value.TrimEnd(New Char() {caracterARemover})

                        Dim caracterARemover_coma As Char = ","
                        set_value = set_value.TrimEnd(New Char() {caracterARemover_coma})

                    End If

                    If set_value = "" Then
                        Update_localAsyncALM("his_replica", _
                                     "sinc = 0", _
                                     "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                    Else
                        If Insert_CentralAsyncALM(table_name, _
                                          set_value) Then

                            If longblob_filed_contain = True Then
                                Dim tb_longblob As DataTable = tb_Recordset_MySQL_localAsyncALM("SHOW COLUMNS FROM " & table_name & " WHERE Type = 'longblob';")
                                For col_lb As Integer = 0 To tb_longblob.Rows.Count - 1
                                    name_file_blob = tb_longblob.Rows(col_lb).Item("Field").ToString
                                    If Not IsDBNull(tb_temp_insert.Rows(0).Item(name_file_blob)) Then
                                        Try
                                            Update_FotoSistema_CentralAsyncALM(name_file_blob, table_name, campo_llave, campo_llave_value_id, tb_temp_insert.Rows(0).Item(name_file_blob))
                                        Catch ex As Exception
                                            AgregarLog(500, "Error desconocido en Exportar Data: " & ex.Message)
                                        End Try
                                    End If
                                Next

                            End If

                            Update_CentralAsyncALM("fchactual", _
                                           "fchActual = current_timestamp", _
                                           "Id", 1)

                            Update_localAsyncALM("his_replica", _
                                         "sinc = 0", _
                                         "id", tb_his_replica_local.Rows(i).Item("id").ToString)

                        Else

                            Dim tb_valida_ins As DataTable
                            tb_valida_ins = tb_Recordset_MySQL_CentralAsyncALM("Select " & campo_llave & " from " & table_name & " WHERE " & campo_llave & " = '" & campo_llave_value_id & "'")

                            If tb_valida_ins.Rows.Count > 0 Then
                                Update_localAsyncALM("his_replica", _
                                             "sinc = 0", _
                                             "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                            End If

                        End If

                    End If


                ElseIf tb_his_replica_local.Rows(i).Item("tipo_operacion").ToString = "UPDATE" Then

                    campo_llave_value_id = tb_his_replica_local.Rows(i).Item("id_tabla_afectada").ToString
                    tb_temp_insert = tb_Recordset_MySQL_localAsyncALM("Select * from " & table_name & " where " & campo_llave & " = " & campo_llave_value_id & "")

                    If tb_temp_insert.Rows.Count > 0 Then

                        Dim tb_campos As DataTable = Nothing
                        If Not columnCache.TryGetValue(table_name, tb_campos) Then
                            tb_campos = tb_Recordset_MySQL_localAsyncALM("SHOW COLUMNS FROM " & table_name & ";")
                            columnCache(table_name) = tb_campos
                        End If


                        'Diccionario con las columnas excluidas por tabla
                        Dim columnasExcluidas As New Dictionary(Of String, HashSet(Of String))(StringComparer.OrdinalIgnoreCase) From {
                            {
                                "tb_almacen_existencias_series2",
                                New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
                                    "foto",
                                    "sinc"
                                }
                            }
                        }
                        ',
                        '    {
                        '        "tb_pacientes",
                        '        New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
                        '            "foto",
                        '            "huella",
                        '            "firma"
                        '        }
                        '    },

                        Dim columnasExcluir As HashSet(Of String) = Nothing

                        If Not columnasExcluidas.TryGetValue(table_name, columnasExcluir) Then
                            'Si la tabla no existe en el diccionario, no excluye ninguna columna
                            columnasExcluir = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                        End If

                        For col As Integer = 0 To tb_campos.Rows.Count - 1

                            campo_nombre = tb_campos.Rows(col).Item("Field").ToString

                            'Excluir columnas especÃ­ficas de tb_servicios_sistemas
                            If columnasExcluir.Contains(campo_nombre) Then
                                Continue For
                            End If

                            Dim tipo As String = ""
                            tipo = tb_campos.Rows(col).Item("Type")
                            Dim texto_buscar As String = tipo
                            If texto_buscar.ToLower().Contains("int") Then
                                tipo = "int"
                            End If

                            If tipo = "date" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "datetime" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd HH:mm:ss") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "longblob" Then

                                If Not IsDBNull(tb_temp_insert.Rows(0).Item(col)) Then
                                    Try
                                        Update_FotoSistema_CentralAsyncALM(campo_nombre, table_name, campo_llave, campo_llave_value_id, tb_temp_insert.Rows(0).Item(col))
                                    Catch ex As Exception
                                        AgregarLog(500, "Error desconocido en Exportar Data: " & ex.Message)
                                    End Try
                                End If

                            ElseIf tipo = "int" Then
                                Dim campo_int As String = ""
                                Try
                                    If tb_temp_insert.Rows(0).Item(col).ToString = "" Then
                                        campo_int = "null"
                                    Else
                                        campo_int = tb_temp_insert.Rows(0).Item(col).ToString
                                    End If
                                Catch ex As Exception
                                    campo_int = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & campo_int & ", "

                            Else

                                set_value_row = ""
                                set_value_row = tb_temp_insert.Rows(0).Item(col).ToString
                                set_value_row = set_value_row.Replace("'", "\'")
                                set_value = set_value & " " & campo_nombre & " = '" & set_value_row & "', "
                            End If

                        Next

                        Dim caracterARemover As Char = " "
                        set_value = set_value.TrimEnd(New Char() {caracterARemover})

                        Dim caracterARemover_coma As Char = ","
                        set_value = set_value.TrimEnd(New Char() {caracterARemover_coma})

                    End If

                    If set_value = "" Then
                        Update_localAsyncALM("his_replica", _
                                     "sinc = 0", _
                                     "id", tb_his_replica_local.Rows(i).Item("id").ToString)

                    Else
                        If Update_CentralAsyncALM(table_name, _
                                       set_value, _
                                       campo_llave, campo_llave_value_id) Then

                            Update_CentralAsyncALM("fchactual", _
                                           "fchActual = current_timestamp", _
                                           "Id", 1)

                            Update_localAsyncALM("his_replica", _
                                         "sinc = 0", _
                                         "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                        End If
                    End If


                ElseIf tb_his_replica_local.Rows(i).Item("tipo_operacion").ToString = "DELETE" Then

                    If Delete_CentralAsyncALM("DELETE FROM " & table_name & " WHERE " & campo_llave & " = " & campo_llave_value_id) Then

                        Update_CentralAsyncALM("fchactual", _
                                       "fchActual = current_timestamp", _
                                       "Id", 1)

                        Update_localAsyncALM("his_replica", _
                                     "sinc = 0", _
                                     "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                    End If

                End If

                ' OptimizaciÃ³n del progreso de la barra de progreso (throttling de UI)
                Dim current_val As Integer = i + 1
                If i Mod 10 = 0 OrElse i = tb_his_replica_local.Rows.Count - 1 Then
                    Try
                        If ProgressBarX_SPALM.InvokeRequired Then
                            ProgressBarX_SPALM.Invoke(Sub()
                                                          Me.ProgressBarX_SPALM.Value = current_val
                                                          Me.ProgressBarX_SPALM.Text = current_val.ToString() & " de " & tb_his_replica_local.Rows.Count
                                                      End Sub)
                        Else
                            Me.ProgressBarX_SPALM.Value = current_val
                            Me.ProgressBarX_SPALM.Text = current_val.ToString() & " de " & tb_his_replica_local.Rows.Count
                        End If
                    Catch ex2 As Exception
                    End Try
                End If

            Next

            '== Eliminar Cargados === 
            Delete_localAsync("DELETE from his_replica " & _
                                "WHERE " & _
                                "tabla_afectada IN (" & _
                                "" & GetQuery_TablasAlmacen() & "" & _
                                ") and " & _
                                "sinc = 0 ")
            '== Eliminar Cargados === 

            Try
                If ProgressBarX_SPALM.InvokeRequired Then
                    ProgressBarX_SPALM.Invoke(Sub() Me.ProgressBarX_SPALM.Value = 0)
                Else
                    Me.ProgressBarX_SPALM.Value = 0
                End If
            Catch ex2 As Exception
            End Try

        Catch ex As Exception
            AgregarLog(500, "Error desconocido en ExportarDataToHosting ALM: " & ex.Message)
        End Try

    End Sub

    Private Function GetQuery_TablasMedicas() As String

        Dim tablas As String = ""
        tablas = "'tb_alertas', " & _
                "'tb_anest_hojatransanestesica', " & _
                "'tb_antecedente', " & _
                "'tb_antecedente_aux', " & _
                "'tb_antecedente_neonatal', " & _
                "'tb_antecedente_obstetricia', " & _
                "'tb_antecedente_pediatria', " & _
                "'tb_antecedente_psicologia', " & _
                "'tb_antecedente_rn', " & _
                "'tb_antecedente_terapialenguaje', " & _
                "'tb_antecedente_tl_lenguaje', " & _
                "'tb_antecedentesanestesicos', " & _
                "'tb_antecedentesquirurgicos', " & _
                "'tb_apysistem', " & _
                "'tb_cirugias', " & _
                "'tb_cirugias_ftp', " & _
                "'tb_cirugiascontrol_mo', " & _
                "'tb_citas', " & _
                "'tb_citas_online', " & _
                "'tb_citasservicios', " & _
                "'tb_consulta', " & _
                "'tb_cortecaja', " & _
                "'tb_ctlnutricionmenor', " & _
                "'tb_cuentasxcobrar', " & _
                "'tb_cuestionario', " & _
                "'tb_cx_motivocancelacion', " & _
                "'tb_diagnostico', " & _
                "'tb_diarreaira', " & _
                "'tb_discapacidades', " & _
                "'tb_ece_historico', " & _
                "'tb_ece_historico_compl', " & _
                "'tb_ece_historico_ftp', " & _
                "'tb_enf_admonmed', " & _
                "'tb_enf_aldrete', " & _
                "'tb_enf_alojamientoconjunto', " & _
                "'tb_enf_balanceliquidos', " & _
                "'tb_enf_balanceliquidos_terapias', " & _
                "'tb_enf_balanceliquidosobs', " & _
                "'tb_enf_balanceliquidosobs_terapias', " & _
                "'tb_enf_balanceliquidosterapia', " & _
                "'tb_enf_caidas', " & _
                "'tb_enf_caidas_jhd_modif', " & _
                "'tb_enf_conclusionesmed', " & _
                "'tb_enf_conteomaterial', " & _
                "'tb_enf_controlmat', " & _
                "'tb_enf_dolor', " & _
                "'tb_enf_dolor_intervenciones', " & _
                "'tb_enf_dolor_neonatos', " & _
                "'tb_enf_edusalud', " & _
                "'tb_enf_formdieta', " & _
                "'tb_enf_h_dispositivo', " & _
                "'tb_enf_h_drenaje', " & _
                "'tb_enf_h_gasometrico', " & _
                "'tb_enf_h_inhaloterapia', " & _
                "'tb_enf_h_lab', " & _
                "'tb_enf_h_neurologico', " & _
                "'tb_enf_h_respiratorio', " & _
                "'tb_enf_listacxsegura', " & _
                "'tb_enf_listacxsegura_hnam', " & _
                "'tb_enf_observaciones', " & _
                "'tb_enf_obstetricia', " & _
                "'tb_enf_reactivos', " & _
                "'tb_enf_signosvitales', " & _
                "'tb_enf_solicitud_dietas', " & _
                "'tb_enf_somatometria', " & _
                "'tb_enf_ulceras', " & _
                "'tb_enf_ulcerasq', " & _
                "'tb_esp_alego_notas', " & _
                "'tb_esp_alg_inmunoterapia', " & _
                "'tb_esp_alg_inmunoterapia_detalle', " & _
                "'tb_esp_alg_pruebascutaneas', " & _
                "'tb_esp_alg_pruebascutaneas2', " & _
                "'tb_esp_dial_controltx', " & _
                "'tb_esp_end_crecimiento', " & _
                "'tb_esp_end_graficos', " & _
                "'tb_esp_end_graficos_ftp', " & _
                "'tb_esp_gineco_controlembarazo', " & _
                "'tb_esp_gineco_crecuterino', " & _
                "'tb_esp_gineco_crecuterino_ftp', " & _
                "'tb_esp_gineco_evolucionobstetrica', " & _
                "'tb_esp_gineco_indiceganancia', " & _
                "'tb_esp_gineco_notaposparto', " & _
                "'tb_esp_gineco_notaposparto_ftp', " & _
                "'tb_esp_gineco_part_enc', " & _
                "'tb_esp_gineco_part_fcf', " & _
                "'tb_esp_gineco_riesreprobst', " & _
                "'tb_esp_gineco_tamizajeobst', " & _
                "'tb_esp_gineco_tamizajeobst2', " & _
                "'tb_esp_gineco_vigila_0datosiniciales', " & _
                "'tb_esp_gineco_vigila_1mitademb', " & _
                "'tb_esp_gineco_vigila_2mitademb_esquema', " & _
                "'tb_esp_gineco_vigila_2mitademb_esquema_ftp', " & _
                "'tb_esp_inh_controltx', " & _
                "'tb_esp_inh_controlventmec', " & _
                "'tb_esp_nefro_controlhemodialisis', " & _
                "'tb_esp_nefro_controlhemodialisis_detalle', " & _
                "'tb_esp_neo_vrn_apgar', " & _
                "'tb_esp_neo_vrn_ballard', " & _
                "'tb_esp_neo_vrn_capurro', " & _
                "'tb_esp_neo_vrn_grafeg', " & _
                "'tb_esp_neo_vrn_silverman', " & _
                "'tb_esp_nut_caractalim', " & _
                "'tb_esp_nut_datosbioquimicos', " & _
                "'tb_esp_nut_datosclinicos', " & _
                "'tb_esp_nut_diagnosticonutricional', " & _
                "'tb_esp_nut_escalaestres', " & _
                "'tb_esp_nut_graficos', " & _
                "'tb_esp_nut_historiaclinicodietetica', " & _
                "'tb_esp_nut_historiadietahabifrecalim', " & _
                "'tb_esp_nut_medantropometricas', " & _
                "'tb_esp_nut_medicionesantropomtricas', " & _
                "'tb_esp_nut_planalimentacion', " & _
                "'tb_esp_nut_r24horas', " & _
                "'tb_esp_nut_r24horas_detalle', " & _
                "'tb_esp_nut_solnutparped', " & _
                "'tb_esp_odonto_analisissocial', " & _
                "'tb_esp_odonto_aparatologia', " & _
                "'tb_esp_odonto_aparatologiadetalle', " & _
                "'tb_esp_odonto_atencionespecializada', " & _
                "'tb_esp_odonto_biberon', " & _
                "'tb_esp_odonto_caractalim', " & _
                "'tb_esp_odonto_cirugias', " & _
                "'tb_esp_odonto_clasificacion', " & _
                "'tb_esp_odonto_cotizacion', " & _
                "'tb_esp_odonto_cotizacion_detalle', " & _
                "'tb_esp_odonto_craneofacial', " & _
                "'tb_esp_odonto_cuestionario', " & _
                "'tb_esp_odonto_despsicomotor', " & _
                "'tb_esp_odonto_expregiones', " & _
                "'tb_esp_odonto_famreverso', " & _
                "'tb_esp_odonto_gabinete', " & _
                "'tb_esp_odonto_highabalim', " & _
                "'tb_esp_odonto_hmortodoncia', " & _
                "'tb_esp_odonto_hojaurgencias', " & _
                "'tb_esp_odonto_lph', " & _
                "'tb_esp_odonto_oclusion', " & _
                "'tb_esp_odonto_odontograma', " & _
                "'tb_esp_odonto_odontograma_ftp', " & _
                "'tb_esp_odonto_pieza_clasificacion', " & _
                "'tb_esp_odonto_pieza_notastrabajo', " & _
                "'tb_esp_odonto_plantratgeneral', " & _
                "'tb_esp_onco_aspirado', " & _
                "'tb_esp_onco_hojaquimio', " & _
                "'tb_esp_onco_regnalcancer', " & _
                "'tb_esp_onco_regnalcancerfam', " & _
                "'tb_esp_ped_triagepediatrico', " & _
                "'tb_esp_psic_cuestionarioinventariorasgoestado', " & _
                "'tb_esp_psic_cuestionariozung', " & _
                "'tb_esp_psic_escalaautoevaluacion', " & _
                "'tb_esp_psic_escalafamhijos', " & _
                "'tb_esp_psic_escalafampadres', " & _
                "'tb_esp_psic_psicoanalisis', " & _
                "'tb_esp_quim_ambulatoria', " & _
                "'tb_esp_quim_controltx', " & _
                "'tb_esp_terapia_notas', " & _
                "'tb_esp_vac_vacunas', " & _
                "'tb_est_preguntas', " & _
                "'tb_explespesquema', " & _
                "'tb_explespesquema_ftp', " & _
                "'tb_exploracionfisicageneral', " & _
                "'tb_exploraciongenral', " & _
                "'tb_exploraciongenral_ftp', " & _
                "'tb_exploraciongenral_huella', " & _
                "'tb_fotosalergo', " & _
                "'tb_fotospadecimiento', " & _
                "'tb_fotospadecimiento_ftp', " & _
                "'tb_fotospadecimiento_ftp2', " & _
                "'tb_gineco_part_fcf', " & _
                "'tb_graficos', " & _
                "'tb_graficosobs', " & _
                "'tb_hoja_violencialesion', " & _
                "'tb_hoja_violencialesion_esquemas', " & _
                "'tb_hoja_violencialesion_esquemas_ftp', " & _
                "'tb_neo_reanimaciones', " & _
                "'tb_notamedica_defuncion', " & _
                "'tb_notamedica_egreso', " & _
                "'tb_notamedica_egresoutq', " & _
                "'tb_notamedica_hojainformes_detalle', " & _
                "'tb_notamedica_hojainformes_detalle_ftp', " & _
                "'tb_notamedica_hojainformes_huella', " & _
                "'tb_notamedica_ingreso', " & _
                "'tb_notamedica_ingreso_odonto', " & _
                "'tb_notamedica_interconsulta', " & _
                "'tb_notamedica_postanestesica', " & _
                "'tb_notamedica_postoperatoria', " & _
                "'tb_notamedica_postoperatoria_dxtx', " & _
                "'tb_notamedica_postoperatoria_ftp', " & _
                "'tb_notamedica_preanestesica', " & _
                "'tb_notamedica_preoperatoria', " & _
                "'tb_notamedica_preoperatoria_dx', " & _
                "'tb_notamedica_preoperatoria_ftp', " & _
                "'tb_notamedica_solicitudinterconsulta', " & _
                "'tb_notamedica_transanestesica', " & _
                "'tb_notamedica_transopertoria', " & _
                "'tb_notamedica_triagepediatrico_hnam', " & _
                "'tb_pacientediabetico', " & _
                "'tb_padecimiento', " & _
                "'tb_procedimiento', " & _
                "'tb_receta', " & _
                "'tb_recetaestudiolab', " & _
                "'tb_recetaestudiorx', " & _
                "'tb_recetalab', " & _
                "'tb_recetamedicina', " & _
                "'tb_recetamedicina_sp', " & _
                "'tb_recetarx', " & _
                "'tb_referencia', " & _
                "'tb_reslab', " & _
                "'tb_reslab_estudios', " & _
                "'tb_reslab_etiquetas', " & _
                "'tb_reslab_parametros', " & _
                "'tb_respiratoriasira', " & _
                "'tb_sigho', " & _
                "'tb_svs_recepcion', " & _
                "'tb_transsang_reacciones', " & _
                "'tb_transsang_solicitud', " & _
                "'tb_tratamiento', " & _
                "'tb_tratamientomed', " & _
                "'tb_ts_doctos', " & _
                "'tb_ts_doctos_ftp', " & _
                "'tb_ts_doctosfirma', " & _
                "'tb_ts_doctosfirma2', " & _
                "'tb_ts_doctosfirma_altavoluntaria', " & _
                "'tb_ts_doctosfirma_constancias', " & _
                "'tb_ts_doctosfirma_hosp', " & _
                "'tb_ts_doctosfirma_huella', " & _
                "'tb_ts_doctosfirma_ucin', " & _
                "'tb_ts_estudiosocioeconomico', " & _
                "'tb_ts_estudiosocioeconomico_ftp', " & _
                "'tb_urg_recepcion', " & _
                "'tb_videospadecimiento', " & _
                "'tb_videospadecimiento_ftp', " & _
                "'ttb_esp_end_graficos_ftp' "

        Return tablas

    End Function

    Private Function GetQuery_TablasAlmacen() As String

        Dim tablas As String = ""
        tablas = "'tb_almacen_bitacora', " & _
                "'tb_almacen_bitacora_detalle', " & _
                "'tb_almacen_cierre', " & _
                "'tb_almacen_cierre_detalle', " & _
                "'tb_almacen_existencias', " & _
                "'tb_almacen_existencias_fisico', " & _
                "'tb_almacen_existencias_lote', " & _
                "'tb_almacen_existencias_marca', " & _
                "'tb_almacen_existencias_series', " & _
                "'tb_almacen_existencias_series2', " & _
                "'tb_almacen_existencias_series2_qr', " & _
                "'tb_almacen_inventario', " & _
                "'tb_almacen_inventario_detalle', " & _
                "'tb_almacen_inventario_no_contados', " & _
                "'tb_almacen_stocks', " & _
                "'tb_almacen_ubicacion', " & _
                "'tb_materiales', " & _
                "'tb_materiales_categoria', " & _
                "'tb_materiales_codigobarras', " & _
                "'tb_materiales_comisionistas', " & _
                "'tb_materiales_equivalencias', " & _
                "'tb_materiales_fotos', " & _
                "'tb_materiales_ftp', " & _
                "'tb_materiales_historialcostos', " & _
                "'tb_materiales_monedero', " & _
                "'tb_materiales_org', " & _
                "'tb_materiales_paquetes', " & _
                "'tb_materiales_precios', " & _
                "'tb_materiales_promocion', " & _
                "'tb_materiales_remate', " & _
                "'tb_materiales_resp', " & _
                "'tb_materiales_ubicacion', " & _
                "'tb_movimientos_material', " & _
                "'tb_notasalida', " & _
                "'tb_notasalida_detalle', " & _
                "'tb_notasalida_empaquetado', " & _
                "'tb_recibos', " & _
                "'tb_recibos_compdigitales', " & _
                "'tb_recibos_detalle', " & _
                "'tb_recibos_detalle_checkin', " & _
                "'tb_recibosmaterial' "

        Return tablas


    End Function

    Private Function GetQuery_TablasEnlace() As String

        Dim tablas As String = ""
        tablas = "'tb_cirugiascontrol', " & _
                "'tb_citasingresos', " & _
                "'tb_paciente', " & _
                "'tb_paciente2', " & _
                "'tb_paciente_aux_ficha_ide', " & _
                "'tb_paciente_ftp' "

        Return tablas

    End Function

    Private Function GetQuery_TablasEstudios() As String

        Dim tablas As String = ""
        tablas = "'tb_estudiosapoyo', " & _
                "'tb_estudiosapoyo_recepcion', " & _
                "'tb_estudiosapoyodetalle', " & _
                "'tb_estudiosapoyodetalle_ppto', " & _
                "'tb_estudiosapoyores_url', " & _
                "'tb_estudiosapoyoresdicom', " & _
                "'tb_estudiosapoyoresdicom_ftp', " & _
                "'tb_estudiosapoyoreselectro', " & _
                "'tb_estudiosapoyoresescrito', " & _
                "'tb_estudiosapoyoresescrito_aux', " & _
                "'tb_estudiosapoyoresescrito_ftp', " & _
                "'tb_estudiosapoyoresimagen', " & _
                "'tb_estudiosapoyoresimagen_ftp', " & _
                "'tb_estudiosapoyoreslab', " & _
                "'tb_estudiosapoyoreslabexterno', " & _
                "'tb_estudiosapoyorespatologia', " & _
                "'tb_estudiosapoyorespdf', " & _
                "'tb_estudiosapoyoresvideo', " & _
                "'tb_estudioscitologicos', " & _
                "'tb_estudiosgabinete' "

        Return tablas

    End Function


    Private Sub ExportarDataToHostingTablasEnlace()
        If Not CentralDisponible OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 Then Return

        Try

            'Dim random As New Random() ' Crea una instancia de la clase Random
            'Dim randomNumber As Integer = random.Next(3000, 5000) ' Genera un número aleatorio entre 1 y 9 (inclusive)
            'Threading.Thread.Sleep(randomNumber)
            Dim limittext As String = Me.txtLimitRegistros.Text.Trim
            If Len(Me.txtLimitRegistros.Text) = 0 Then
                limittext = 2000
            End If

            Dim tb_his_replica_local As DataTable
            tb_his_replica_local = tb_Recordset_MySQL_local("Select * from his_replica " & _
                                                                 "WHERE " & _
                                                                 "tabla_afectada IN (" & _
                                                                 "" & GetQuery_TablasEnlace() & "" & _
                                                                 ") and " & _
                                                                 "sinc = 1 " & _
                                                                 "order by id LIMIT " & limittext & "")

            Dim tb_temp_insert As DataTable
            If tb_his_replica_local.Rows.Count = 0 Then
                Update_Central("fchactual", _
                               "fchActual = current_timestamp", _
                               "Id", 1)
                Exit Sub
            End If

            Dim campo_llave As String = ""
            Dim campo_llave_value_id As Integer = 0
            Dim fecha_str As String = ""
            Dim campo_nombre As String = ""

            Try
                Me.ProgressBarX1.Minimum = 0
                Me.ProgressBarX1.Maximum = tb_his_replica_local.Rows.Count
                Me.ProgressBarX1.Text = "0 de " & tb_his_replica_local.Rows.Count
            Catch ex2 As Exception
            End Try

            Dim set_value_row As String = ""
            Dim longblob_filed_contain As Boolean = False
            Dim name_file_blob As String = ""

            For i As Integer = 0 To tb_his_replica_local.Rows.Count - 1
                If Not CentralDisponible OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 Then Return

                longblob_filed_contain = False

                Dim table_name As String = tb_his_replica_local.Rows(i).Item("tabla_afectada").ToString

                Dim set_value As String = ""

                If tb_his_replica_local.Rows(i).Item("tipo_operacion").ToString = "INSERT" Then

                    Dim tb_campo_llave As DataTable
                    tb_campo_llave = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & " WHERE Extra = 'auto_increment';")
                    campo_llave = tb_campo_llave.Rows(0).Item("Field").ToString
                    campo_llave_value_id = tb_his_replica_local.Rows(i).Item("id_tabla_afectada").ToString
                    tb_temp_insert = tb_Recordset_MySQL_local("Select * from " & table_name & " where " & campo_llave & " = " & campo_llave_value_id & "")

                    If tb_temp_insert.Rows.Count > 0 Then

                        Dim tb_campos As DataTable
                        tb_campos = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & ";")
                        For col As Integer = 0 To tb_campos.Rows.Count - 1

                            campo_nombre = tb_campos.Rows(col).Item("Field").ToString

                            Dim tipo As String = ""
                            tipo = tb_campos.Rows(col).Item("Type")
                            Dim texto_buscar As String = tipo
                            If texto_buscar.ToLower().Contains("int") Then
                                tipo = "int"
                            End If

                            If tipo = "date" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "datetime" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd HH:mm:ss") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "longblob" Then
                                longblob_filed_contain = True

                            ElseIf tipo = "int" Then
                                Dim campo_int As String = ""
                                Try
                                    If tb_temp_insert.Rows(0).Item(col).ToString = "" Then
                                        campo_int = "null"
                                    Else
                                        campo_int = tb_temp_insert.Rows(0).Item(col).ToString
                                    End If
                                Catch ex As Exception
                                    campo_int = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & campo_int & ", "

                            Else

                                set_value_row = ""
                                set_value_row = tb_temp_insert.Rows(0).Item(col).ToString
                                set_value_row = set_value_row.Replace("'", "\'")
                                set_value = set_value & " " & campo_nombre & " = '" & set_value_row & "', "
                            End If

                        Next

                        Dim caracterARemover As Char = " "
                        set_value = set_value.TrimEnd(New Char() {caracterARemover})

                        Dim caracterARemover_coma As Char = ","
                        set_value = set_value.TrimEnd(New Char() {caracterARemover_coma})

                    End If

                    If set_value = "" Then
                        Update_local("his_replica", _
                                     "sinc = 0", _
                                     "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                    Else
                        If Insert_Central(table_name, _
                                          set_value) Then

                            If longblob_filed_contain = True Then
                                Dim tb_longblob As DataTable
                                tb_longblob = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & " WHERE Type = 'longblob';")
                                For col_lb As Integer = 0 To tb_longblob.Rows.Count - 1
                                    name_file_blob = tb_longblob.Rows(col_lb).Item("Field").ToString
                                    If Not IsDBNull(tb_temp_insert.Rows(0).Item(name_file_blob)) Then
                                        Try
                                            Update_FotoSistema_Central(name_file_blob, table_name, campo_llave, campo_llave_value_id, tb_temp_insert.Rows(0).Item(name_file_blob))
                                        Catch ex As Exception
                                            AgregarLog(500, "Error desconocido en Exportar Data: " & ex.Message)
                                            'Me.lstLog.Items.Add(Calcula_FechaActual)
                                            'Me.lstLog.Items(lstLog.Items.Count - 1).SubItems.Add("Error desconocido en Exportar Data: " & ex.Message)
                                        End Try
                                    End If
                                Next

                            End If

                            Update_Central("fchactual", _
                                           "fchActual = current_timestamp", _
                                           "Id", 1)

                            Update_local("his_replica", _
                                         "sinc = 0", _
                                         "id", tb_his_replica_local.Rows(i).Item("id").ToString)

                        Else

                            Dim tb_valida_ins As DataTable
                            tb_valida_ins = tb_Recordset_MySQL_Central("Select " & campo_llave & " from " & table_name & " WHERE " & campo_llave & " = '" & campo_llave_value_id & "'")

                            If tb_valida_ins.Rows.Count > 0 Then
                                Update_local("his_replica", _
                                             "sinc = 0", _
                                             "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                            End If

                        End If

                    End If


                ElseIf tb_his_replica_local.Rows(i).Item("tipo_operacion").ToString = "UPDATE" Then

                    Dim tb_campo_llave As DataTable
                    tb_campo_llave = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & " WHERE Extra = 'auto_increment';")
                    campo_llave = tb_campo_llave.Rows(0).Item("Field").ToString
                    campo_llave_value_id = tb_his_replica_local.Rows(i).Item("id_tabla_afectada").ToString
                    tb_temp_insert = tb_Recordset_MySQL_local("Select * from " & table_name & " where " & campo_llave & " = " & campo_llave_value_id & "")

                    If tb_temp_insert.Rows.Count > 0 Then

                        Dim tb_campos As DataTable
                        tb_campos = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & ";")
                        For col As Integer = 0 To tb_campos.Rows.Count - 1

                            campo_nombre = tb_campos.Rows(col).Item("Field").ToString

                            Dim tipo As String = ""
                            tipo = tb_campos.Rows(col).Item("Type")
                            Dim texto_buscar As String = tipo
                            If texto_buscar.ToLower().Contains("int") Then
                                tipo = "int"
                            End If

                            If tipo = "date" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "datetime" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd HH:mm:ss") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "longblob" Then

                                If Not IsDBNull(tb_temp_insert.Rows(0).Item(col)) Then
                                    Try
                                        Update_FotoSistema_Central(campo_nombre, table_name, campo_llave, campo_llave_value_id, tb_temp_insert.Rows(0).Item(col))
                                    Catch ex As Exception
                                        AgregarLog(500, "Error desconocido en Exportar Data: " & ex.Message)
                                    End Try
                                End If

                            ElseIf tipo = "int" Then
                                Dim campo_int As String = ""
                                Try
                                    If tb_temp_insert.Rows(0).Item(col).ToString = "" Then
                                        campo_int = "null"
                                    Else
                                        campo_int = tb_temp_insert.Rows(0).Item(col).ToString
                                    End If
                                Catch ex As Exception
                                    campo_int = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & campo_int & ", "

                            Else

                                set_value_row = ""
                                set_value_row = tb_temp_insert.Rows(0).Item(col).ToString
                                set_value_row = set_value_row.Replace("'", "\'")
                                set_value = set_value & " " & campo_nombre & " = '" & set_value_row & "', "
                            End If

                        Next

                        Dim caracterARemover As Char = " "
                        set_value = set_value.TrimEnd(New Char() {caracterARemover})

                        Dim caracterARemover_coma As Char = ","
                        set_value = set_value.TrimEnd(New Char() {caracterARemover_coma})

                    End If

                    If set_value = "" Then
                        Update_local("his_replica", _
                                     "sinc = 0", _
                                     "id", tb_his_replica_local.Rows(i).Item("id").ToString)

                    Else
                        If Update_Central(table_name, _
                                       set_value, _
                                       campo_llave, campo_llave_value_id) Then

                            Update_Central("fchactual", _
                                           "fchActual = current_timestamp", _
                                           "Id", 1)

                            Update_local("his_replica", _
                                         "sinc = 0", _
                                         "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                        End If
                    End If


                ElseIf tb_his_replica_local.Rows(i).Item("tipo_operacion").ToString = "DELETE" Then

                    Dim tb_campo_llave As DataTable
                    tb_campo_llave = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & " WHERE Extra = 'auto_increment';")
                    campo_llave = tb_campo_llave.Rows(0).Item("Field").ToString
                    campo_llave_value_id = tb_his_replica_local.Rows(i).Item("id_tabla_afectada").ToString

                    If Delete_Central("DELETE FROM " & table_name & " WHERE " & campo_llave & " = " & campo_llave_value_id) Then

                        Update_Central("fchactual", _
                                       "fchActual = current_timestamp", _
                                       "Id", 1)

                        Update_local("his_replica", _
                                     "sinc = 0", _
                                     "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                    End If

                End If

                Try
                    If ProgressBarX1.InvokeRequired Then
                        ProgressBarX1.Invoke(Sub() Me.ProgressBarX1.Value = Me.ProgressBarX1.Value + 1)
                        ProgressBarX1.Invoke(Sub() Me.ProgressBarX1.Text = Me.ProgressBarX1.Value & " de " & tb_his_replica_local.Rows.Count)
                    Else
                        Me.ProgressBarX1.Value = Me.ProgressBarX1.Value + 1
                        Me.ProgressBarX1.Text = Me.ProgressBarX1.Value & " de " & tb_his_replica_local.Rows.Count
                    End If
                Catch ex2 As Exception
                End Try

            Next


            '== Eliminar Cargados === 
            Delete_localAsync("DELETE from his_replica " & _
                                "WHERE " & _
                                "tabla_afectada IN (" & _
                                "" & GetQuery_TablasEnlace() & "" & _
                                ") and " & _
                                "sinc = 0 ")
            '== Eliminar Cargados === 

            Try
                If ProgressBarX1.InvokeRequired Then
                    ProgressBarX1.Invoke(Sub() Me.ProgressBarX1.Value = 0)
                Else
                    Me.ProgressBarX1.Value = 0
                End If
            Catch ex2 As Exception
            End Try



        Catch ex As Exception
            AgregarLog(500, "Error desconocido en ExportarDataToHosting TABLAS ENCLACE: " & ex.Message)
            'Me.lstLog.Items.Add(Calcula_FechaActual)
            'Me.lstLog.Items(lstLog.Items.Count - 1).SubItems.Add("Error desconocido en Exportar Data: " & ex.Message)
        End Try

    End Sub

    Private Sub ExportarDataToHostingTablasEstudios()
        If Not CentralDisponible OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 Then Return

        Try

            'Dim random As New Random() ' Crea una instancia de la clase Random
            'Dim randomNumber As Integer = random.Next(3000, 5000) ' Genera un número aleatorio entre 1 y 9 (inclusive)
            'Threading.Thread.Sleep(randomNumber)
            Dim limittext As String = Me.txtLimitRegistros.Text.Trim
            If Len(Me.txtLimitRegistros.Text) = 0 Then
                limittext = 2000
            End If

            Dim tb_his_replica_local As DataTable
            tb_his_replica_local = tb_Recordset_MySQL_local("Select * from his_replica " & _
                                                                 "WHERE " & _
                                                                 "tabla_afectada IN (" & _
                                                                 "" & GetQuery_TablasEstudios() & "" & _
                                                                 ") and " & _
                                                                 "sinc = 1 " & _
                                                                 "order by id LIMIT " & limittext & "")

            Dim tb_temp_insert As DataTable
            If tb_his_replica_local.Rows.Count = 0 Then
                Update_Central("fchactual", _
                               "fchActual = current_timestamp", _
                               "Id", 1)
                Exit Sub
            End If

            Dim campo_llave As String = ""
            Dim campo_llave_value_id As Integer = 0
            Dim fecha_str As String = ""
            Dim campo_nombre As String = ""

            Try
                Me.ProgressBarX1.Minimum = 0
                Me.ProgressBarX1.Maximum = tb_his_replica_local.Rows.Count
                Me.ProgressBarX1.Text = "0 de " & tb_his_replica_local.Rows.Count
            Catch ex2 As Exception
            End Try

            Dim set_value_row As String = ""
            Dim longblob_filed_contain As Boolean = False
            Dim name_file_blob As String = ""

            For i As Integer = 0 To tb_his_replica_local.Rows.Count - 1
                If Not CentralDisponible OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 Then Return

                longblob_filed_contain = False

                Dim table_name As String = tb_his_replica_local.Rows(i).Item("tabla_afectada").ToString

                Dim set_value As String = ""

                If tb_his_replica_local.Rows(i).Item("tipo_operacion").ToString = "INSERT" Then

                    Dim tb_campo_llave As DataTable
                    tb_campo_llave = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & " WHERE Extra = 'auto_increment';")
                    campo_llave = tb_campo_llave.Rows(0).Item("Field").ToString
                    campo_llave_value_id = tb_his_replica_local.Rows(i).Item("id_tabla_afectada").ToString
                    tb_temp_insert = tb_Recordset_MySQL_local("Select * from " & table_name & " where " & campo_llave & " = " & campo_llave_value_id & "")

                    If tb_temp_insert.Rows.Count > 0 Then

                        Dim tb_campos As DataTable
                        tb_campos = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & ";")
                        For col As Integer = 0 To tb_campos.Rows.Count - 1

                            campo_nombre = tb_campos.Rows(col).Item("Field").ToString

                            Dim tipo As String = ""
                            tipo = tb_campos.Rows(col).Item("Type")
                            Dim texto_buscar As String = tipo
                            If texto_buscar.ToLower().Contains("int") Then
                                tipo = "int"
                            End If

                            If tipo = "date" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "datetime" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd HH:mm:ss") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "longblob" Then
                                longblob_filed_contain = True

                            ElseIf tipo = "int" Then
                                Dim campo_int As String = ""
                                Try
                                    If tb_temp_insert.Rows(0).Item(col).ToString = "" Then
                                        campo_int = "null"
                                    Else
                                        campo_int = tb_temp_insert.Rows(0).Item(col).ToString
                                    End If
                                Catch ex As Exception
                                    campo_int = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & campo_int & ", "

                            Else

                                set_value_row = ""
                                set_value_row = tb_temp_insert.Rows(0).Item(col).ToString
                                set_value_row = set_value_row.Replace("'", "\'")
                                set_value = set_value & " " & campo_nombre & " = '" & set_value_row & "', "
                            End If

                        Next

                        Dim caracterARemover As Char = " "
                        set_value = set_value.TrimEnd(New Char() {caracterARemover})

                        Dim caracterARemover_coma As Char = ","
                        set_value = set_value.TrimEnd(New Char() {caracterARemover_coma})

                    End If

                    If set_value = "" Then
                        Update_local("his_replica", _
                                     "sinc = 0", _
                                     "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                    Else
                        If Insert_Central(table_name, _
                                          set_value) Then

                            If longblob_filed_contain = True Then
                                Dim tb_longblob As DataTable
                                tb_longblob = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & " WHERE Type = 'longblob';")
                                For col_lb As Integer = 0 To tb_longblob.Rows.Count - 1
                                    name_file_blob = tb_longblob.Rows(col_lb).Item("Field").ToString
                                    If Not IsDBNull(tb_temp_insert.Rows(0).Item(name_file_blob)) Then
                                        Try
                                            Update_FotoSistema_Central(name_file_blob, table_name, campo_llave, campo_llave_value_id, tb_temp_insert.Rows(0).Item(name_file_blob))
                                        Catch ex As Exception
                                            AgregarLog(500, "Error desconocido en Exportar Data: " & ex.Message)
                                            'Me.lstLog.Items.Add(Calcula_FechaActual)
                                            'Me.lstLog.Items(lstLog.Items.Count - 1).SubItems.Add("Error desconocido en Exportar Data: " & ex.Message)
                                        End Try
                                    End If
                                Next

                            End If

                            Update_Central("fchactual", _
                                           "fchActual = current_timestamp", _
                                           "Id", 1)

                            Update_local("his_replica", _
                                         "sinc = 0", _
                                         "id", tb_his_replica_local.Rows(i).Item("id").ToString)

                        Else

                            Dim tb_valida_ins As DataTable
                            tb_valida_ins = tb_Recordset_MySQL_Central("Select " & campo_llave & " from " & table_name & " WHERE " & campo_llave & " = '" & campo_llave_value_id & "'")

                            If tb_valida_ins.Rows.Count > 0 Then
                                Update_local("his_replica", _
                                             "sinc = 0", _
                                             "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                            End If

                        End If

                    End If


                ElseIf tb_his_replica_local.Rows(i).Item("tipo_operacion").ToString = "UPDATE" Then

                    Dim tb_campo_llave As DataTable
                    tb_campo_llave = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & " WHERE Extra = 'auto_increment';")
                    campo_llave = tb_campo_llave.Rows(0).Item("Field").ToString
                    campo_llave_value_id = tb_his_replica_local.Rows(i).Item("id_tabla_afectada").ToString
                    tb_temp_insert = tb_Recordset_MySQL_local("Select * from " & table_name & " where " & campo_llave & " = " & campo_llave_value_id & "")

                    If tb_temp_insert.Rows.Count > 0 Then

                        Dim tb_campos As DataTable
                        tb_campos = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & ";")
                        For col As Integer = 0 To tb_campos.Rows.Count - 1

                            campo_nombre = tb_campos.Rows(col).Item("Field").ToString

                            Dim tipo As String = ""
                            tipo = tb_campos.Rows(col).Item("Type")
                            Dim texto_buscar As String = tipo
                            If texto_buscar.ToLower().Contains("int") Then
                                tipo = "int"
                            End If

                            If tipo = "date" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "datetime" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd HH:mm:ss") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "longblob" Then

                                If Not IsDBNull(tb_temp_insert.Rows(0).Item(col)) Then
                                    Try
                                        Update_FotoSistema_Central(campo_nombre, table_name, campo_llave, campo_llave_value_id, tb_temp_insert.Rows(0).Item(col))
                                    Catch ex As Exception
                                        AgregarLog(500, "Error desconocido en Exportar Data: " & ex.Message)
                                    End Try
                                End If

                            ElseIf tipo = "int" Then
                                Dim campo_int As String = ""
                                Try
                                    If tb_temp_insert.Rows(0).Item(col).ToString = "" Then
                                        campo_int = "null"
                                    Else
                                        campo_int = tb_temp_insert.Rows(0).Item(col).ToString
                                    End If
                                Catch ex As Exception
                                    campo_int = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & campo_int & ", "

                            Else

                                set_value_row = ""
                                set_value_row = tb_temp_insert.Rows(0).Item(col).ToString
                                set_value_row = set_value_row.Replace("'", "\'")
                                set_value = set_value & " " & campo_nombre & " = '" & set_value_row & "', "
                                'set_value = set_value & " " & campo_nombre & " = '" & tb_temp_insert.Rows(0).Item(col).ToString & "', "
                            End If

                        Next

                        Dim caracterARemover As Char = " "
                        set_value = set_value.TrimEnd(New Char() {caracterARemover})

                        Dim caracterARemover_coma As Char = ","
                        set_value = set_value.TrimEnd(New Char() {caracterARemover_coma})

                    End If

                    If set_value = "" Then
                        Update_local("his_replica", _
                                     "sinc = 0", _
                                     "id", tb_his_replica_local.Rows(i).Item("id").ToString)

                    Else
                        If Update_Central(table_name, _
                                       set_value, _
                                       campo_llave, campo_llave_value_id) Then

                            Update_Central("fchactual", _
                                           "fchActual = current_timestamp", _
                                           "Id", 1)

                            Update_local("his_replica", _
                                         "sinc = 0", _
                                         "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                        End If
                    End If


                ElseIf tb_his_replica_local.Rows(i).Item("tipo_operacion").ToString = "DELETE" Then

                    Dim tb_campo_llave As DataTable
                    tb_campo_llave = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & " WHERE Extra = 'auto_increment';")
                    campo_llave = tb_campo_llave.Rows(0).Item("Field").ToString
                    campo_llave_value_id = tb_his_replica_local.Rows(i).Item("id_tabla_afectada").ToString

                    If Delete_Central("DELETE FROM " & table_name & " WHERE " & campo_llave & " = " & campo_llave_value_id) Then

                        Update_Central("fchactual", _
                                       "fchActual = current_timestamp", _
                                       "Id", 1)

                        Update_local("his_replica", _
                                     "sinc = 0", _
                                     "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                    End If

                End If

                Try
                    Me.ProgressBarX1.Value = Me.ProgressBarX1.Value + 1
                    Me.ProgressBarX1.Text = Me.ProgressBarX1.Value & " de " & tb_his_replica_local.Rows.Count
                Catch ex2 As Exception
                End Try

            Next

            '== Eliminar Cargados === 
            Delete_localAsync("DELETE from his_replica " & _
                                "WHERE " & _
                                "tabla_afectada IN (" & _
                                "" & GetQuery_TablasEstudios() & "" & _
                                ") and " & _
                                "sinc = 0")
            '== Eliminar Cargados === 

            Try
                If ProgressBarX1.InvokeRequired Then
                    ProgressBarX1.Invoke(Sub() Me.ProgressBarX1.Value = 0)
                Else
                    Me.ProgressBarX1.Value = 0
                End If
            Catch ex2 As Exception
            End Try



        Catch ex As Exception
            AgregarLog(500, "Error desconocido en ExportarDataToHosting TABLAS ESTUDIOS: " & ex.Message)
            'Me.lstLog.Items.Add(Calcula_FechaActual)
            'Me.lstLog.Items(lstLog.Items.Count - 1).SubItems.Add("Error desconocido en Exportar Data: " & ex.Message)
        End Try

    End Sub

    Private Sub ExportarDataToHostingExpediente()
        If Not CentralDisponible OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 Then Return

        Try

            'Dim random As New Random() ' Crea una instancia de la clase Random
            'Dim randomNumber As Integer = random.Next(3000, 5000) ' Genera un número aleatorio entre 1 y 9 (inclusive)
            'Threading.Thread.Sleep(randomNumber)
            Dim limittext As String = Me.txtLimitRegistros.Text.Trim
            If Len(Me.txtLimitRegistros.Text) = 0 Then
                limittext = 2000
            End If

            Dim tb_his_replica_local As DataTable
            tb_his_replica_local = tb_Recordset_MySQL_local("Select * from his_replica " & _
                                                                 "WHERE " & _
                                                                 "tabla_afectada IN (" & _
                                                                 "" & GetQuery_TablasMedicas() & "" & _
                                                                 ") and " & _
                                                                 "sinc = 1 " & _
                                                                 "order by id LIMIT " & limittext & "")

            Dim tb_temp_insert As DataTable
            If tb_his_replica_local.Rows.Count = 0 Then
                Update_Central("fchactual", _
                               "fchActual = current_timestamp", _
                               "Id", 1)
                Exit Sub
            End If

            Dim campo_llave As String = ""
            Dim campo_llave_value_id As Integer = 0
            Dim fecha_str As String = ""
            Dim campo_nombre As String = ""

            Try
                Me.ProgressBarX1.Minimum = 0
                Me.ProgressBarX1.Maximum = tb_his_replica_local.Rows.Count
                Me.ProgressBarX1.Text = "0 de " & tb_his_replica_local.Rows.Count
            Catch ex2 As Exception
            End Try

            Dim set_value_row As String = ""
            Dim longblob_filed_contain As Boolean = False
            Dim name_file_blob As String = ""

            For i As Integer = 0 To tb_his_replica_local.Rows.Count - 1
                If Not CentralDisponible OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 Then Return

                longblob_filed_contain = False

                Dim table_name As String = tb_his_replica_local.Rows(i).Item("tabla_afectada").ToString

                Dim set_value As String = ""

                If tb_his_replica_local.Rows(i).Item("tipo_operacion").ToString = "INSERT" Then

                    Dim tb_campo_llave As DataTable
                    tb_campo_llave = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & " WHERE Extra = 'auto_increment';")
                    campo_llave = tb_campo_llave.Rows(0).Item("Field").ToString
                    campo_llave_value_id = tb_his_replica_local.Rows(i).Item("id_tabla_afectada").ToString
                    tb_temp_insert = tb_Recordset_MySQL_local("Select * from " & table_name & " where " & campo_llave & " = " & campo_llave_value_id & "")

                    If tb_temp_insert.Rows.Count > 0 Then

                        Dim tb_campos As DataTable
                        tb_campos = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & ";")
                        For col As Integer = 0 To tb_campos.Rows.Count - 1

                            campo_nombre = tb_campos.Rows(col).Item("Field").ToString

                            Dim tipo As String = ""
                            tipo = tb_campos.Rows(col).Item("Type")
                            Dim texto_buscar As String = tipo
                            If texto_buscar.ToLower().Contains("int") Then
                                tipo = "int"
                            End If

                            If tipo = "date" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "datetime" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd HH:mm:ss") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "longblob" Then
                                longblob_filed_contain = True

                            ElseIf tipo = "int" Then
                                Dim campo_int As String = ""
                                Try
                                    If tb_temp_insert.Rows(0).Item(col).ToString = "" Then
                                        campo_int = "null"
                                    Else
                                        campo_int = tb_temp_insert.Rows(0).Item(col).ToString
                                    End If
                                Catch ex As Exception
                                    campo_int = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & campo_int & ", "

                            Else

                                set_value_row = ""
                                set_value_row = tb_temp_insert.Rows(0).Item(col).ToString
                                set_value_row = set_value_row.Replace("'", "\'")
                                set_value = set_value & " " & campo_nombre & " = '" & set_value_row & "', "
                            End If

                        Next

                        Dim caracterARemover As Char = " "
                        set_value = set_value.TrimEnd(New Char() {caracterARemover})

                        Dim caracterARemover_coma As Char = ","
                        set_value = set_value.TrimEnd(New Char() {caracterARemover_coma})

                    End If

                    If set_value = "" Then
                        Update_local("his_replica", _
                                     "sinc = 0", _
                                     "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                    Else
                        If Insert_Central(table_name, _
                                          set_value) Then

                            If longblob_filed_contain = True Then
                                Dim tb_longblob As DataTable
                                tb_longblob = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & " WHERE Type = 'longblob';")
                                For col_lb As Integer = 0 To tb_longblob.Rows.Count - 1
                                    name_file_blob = tb_longblob.Rows(col_lb).Item("Field").ToString
                                    If Not IsDBNull(tb_temp_insert.Rows(0).Item(name_file_blob)) Then
                                        Try
                                            Update_FotoSistema_Central(name_file_blob, table_name, campo_llave, campo_llave_value_id, tb_temp_insert.Rows(0).Item(name_file_blob))
                                        Catch ex As Exception
                                            AgregarLog(500, "Error desconocido en Exportar Data: " & ex.Message)
                                            'Me.lstLog.Items.Add(Calcula_FechaActual)
                                            'Me.lstLog.Items(lstLog.Items.Count - 1).SubItems.Add("Error desconocido en Exportar Data: " & ex.Message)
                                        End Try
                                    End If
                                Next

                            End If

                            Update_Central("fchactual", _
                                           "fchActual = current_timestamp", _
                                           "Id", 1)

                            Update_local("his_replica", _
                                         "sinc = 0", _
                                         "id", tb_his_replica_local.Rows(i).Item("id").ToString)

                        Else

                            Dim tb_valida_ins As DataTable
                            tb_valida_ins = tb_Recordset_MySQL_Central("Select " & campo_llave & " from " & table_name & " WHERE " & campo_llave & " = '" & campo_llave_value_id & "'")

                            If tb_valida_ins.Rows.Count > 0 Then
                                Update_local("his_replica", _
                                             "sinc = 0", _
                                             "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                            End If

                        End If

                    End If


                ElseIf tb_his_replica_local.Rows(i).Item("tipo_operacion").ToString = "UPDATE" Then

                    Dim tb_campo_llave As DataTable
                    tb_campo_llave = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & " WHERE Extra = 'auto_increment';")
                    campo_llave = tb_campo_llave.Rows(0).Item("Field").ToString
                    campo_llave_value_id = tb_his_replica_local.Rows(i).Item("id_tabla_afectada").ToString
                    tb_temp_insert = tb_Recordset_MySQL_local("Select * from " & table_name & " where " & campo_llave & " = " & campo_llave_value_id & "")

                    If tb_temp_insert.Rows.Count > 0 Then

                        Dim tb_campos As DataTable
                        tb_campos = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & ";")
                        For col As Integer = 0 To tb_campos.Rows.Count - 1

                            campo_nombre = tb_campos.Rows(col).Item("Field").ToString

                            Dim tipo As String = ""
                            tipo = tb_campos.Rows(col).Item("Type")
                            Dim texto_buscar As String = tipo
                            If texto_buscar.ToLower().Contains("int") Then
                                tipo = "int"
                            End If

                            If tipo = "date" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "datetime" Then
                                Try
                                    fecha_str = "'" & Format(CDate(tb_temp_insert.Rows(0).Item(col).ToString), "yyyy-MM-dd HH:mm:ss") & "'"
                                Catch ex As Exception
                                    fecha_str = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & fecha_str & ", "

                            ElseIf tipo = "longblob" Then

                                If Not IsDBNull(tb_temp_insert.Rows(0).Item(col)) Then
                                    Try
                                        Update_FotoSistema_Central(campo_nombre, table_name, campo_llave, campo_llave_value_id, tb_temp_insert.Rows(0).Item(col))
                                    Catch ex As Exception
                                        AgregarLog(500, "Error desconocido en Exportar Data: " & ex.Message)
                                        'Me.lstLog.Items.Add(Calcula_FechaActual)
                                        'Me.lstLog.Items(lstLog.Items.Count - 1).SubItems.Add("Error desconocido en Exportar Data: " & ex.Message)
                                    End Try
                                End If

                            ElseIf tipo = "int" Then
                                Dim campo_int As String = ""
                                Try
                                    If tb_temp_insert.Rows(0).Item(col).ToString = "" Then
                                        campo_int = "null"
                                    Else
                                        campo_int = tb_temp_insert.Rows(0).Item(col).ToString
                                    End If
                                Catch ex As Exception
                                    campo_int = "null"
                                End Try
                                set_value = set_value & " " & campo_nombre & " = " & campo_int & ", "

                            Else

                                set_value_row = ""
                                set_value_row = tb_temp_insert.Rows(0).Item(col).ToString
                                set_value_row = set_value_row.Replace("'", "\'")
                                set_value = set_value & " " & campo_nombre & " = '" & set_value_row & "', "
                                'set_value = set_value & " " & campo_nombre & " = '" & tb_temp_insert.Rows(0).Item(col).ToString & "', "
                            End If

                        Next

                        Dim caracterARemover As Char = " "
                        set_value = set_value.TrimEnd(New Char() {caracterARemover})

                        Dim caracterARemover_coma As Char = ","
                        set_value = set_value.TrimEnd(New Char() {caracterARemover_coma})

                    End If

                    If set_value = "" Then
                        Update_local("his_replica", _
                                     "sinc = 0", _
                                     "id", tb_his_replica_local.Rows(i).Item("id").ToString)

                    Else
                        If Update_Central(table_name, _
                                       set_value, _
                                       campo_llave, campo_llave_value_id) Then

                            Update_Central("fchactual", _
                                           "fchActual = current_timestamp", _
                                           "Id", 1)

                            Update_local("his_replica", _
                                         "sinc = 0", _
                                         "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                        End If
                    End If


                ElseIf tb_his_replica_local.Rows(i).Item("tipo_operacion").ToString = "DELETE" Then

                    Dim tb_campo_llave As DataTable
                    tb_campo_llave = tb_Recordset_MySQL_local("SHOW COLUMNS FROM " & table_name & " WHERE Extra = 'auto_increment';")
                    campo_llave = tb_campo_llave.Rows(0).Item("Field").ToString
                    campo_llave_value_id = tb_his_replica_local.Rows(i).Item("id_tabla_afectada").ToString

                    If Delete_Central("DELETE FROM " & table_name & " WHERE " & campo_llave & " = " & campo_llave_value_id) Then

                        Update_Central("fchactual", _
                                       "fchActual = current_timestamp", _
                                       "Id", 1)

                        Update_local("his_replica", _
                                     "sinc = 0", _
                                     "id", tb_his_replica_local.Rows(i).Item("id").ToString)
                    End If

                End If

                Try
                    If ProgressBarX1.InvokeRequired Then
                        ProgressBarX1.Invoke(Sub() Me.ProgressBarX1.Value = Me.ProgressBarX1.Value + 1)
                        ProgressBarX1.Invoke(Sub() Me.ProgressBarX1.Text = Me.ProgressBarX1.Value & " de " & tb_his_replica_local.Rows.Count)
                    Else
                        Me.ProgressBarX1.Value = Me.ProgressBarX1.Value + 1
                        Me.ProgressBarX1.Text = Me.ProgressBarX1.Value & " de " & tb_his_replica_local.Rows.Count
                    End If
                Catch ex2 As Exception
                End Try

            Next

            '== Eliminar Cargados === 
            Delete_localAsync("DELETE from his_replica " & _
                                "WHERE " & _
                                "tabla_afectada IN (" & _
                                "" & GetQuery_TablasMedicas() & "" & _
                                ") and " & _
                                "sinc = 0 ")
            '== Eliminar Cargados === 

            Try
                If ProgressBarX1.InvokeRequired Then
                    ProgressBarX1.Invoke(Sub() Me.ProgressBarX1.Value = 0)
                Else
                    Me.ProgressBarX1.Value = 0
                End If
            Catch ex2 As Exception
            End Try



        Catch ex As Exception
            AgregarLog(500, "Error desconocido en ExportarDataToHosting TABLAS MEDICAS: " & ex.Message)
        End Try

    End Sub


    ''' <summary>
    ''' convertir imagen a binario
    ''' </summary>
    ''' <param name="Imagen"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function Update_FotoSistema_Central(ByVal Campo As String, ByVal Tabla As String, _
                                        ByVal IdCampo As String, ByVal IdValor As String, _
                                        ByVal Imagen As Byte()) As Boolean
        Dim cmdtext As String
        cmdtext = "UPDATE " & Tabla & " set " & Campo & "=?File where " & IdCampo & " = '" & IdValor & "'"
        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_Central.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(cmdtext, cx_MySQL_Central)
                    cmm_Comando.Parameters.AddWithValue("?File", Imagen)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_Central, "Update_FotoSistema_Central")
                Return False
            End Try
        End SyncLock
    End Function

    Private Function UsuarioNombre(ByVal CveMedico As String) As String

        Dim tb_medicos As DataTable
        tb_medicos = tb_Recordset_MySQL_local("Select ccvemedico, cnombre, cpriapellido, " & _
                                                   "csegapellido, cdscareaafectada, cgrado, cempleo, " & _
                                                   "cCedulaProf from cat_medico where ccvemedico = '" & CveMedico & "'")

        Dim usuario As String

        With tb_medicos
            Try
                usuario = .Rows(0).Item(1).ToString
                If usuario.Trim = "" Then
                    Return ""
                Else
                    usuario = .Rows(0).Item(6).ToString & " " & _
                                      .Rows(0).Item(1).ToString & " " & _
                                      .Rows(0).Item(2).ToString & " " & _
                                      .Rows(0).Item(3).ToString
                    Return usuario.Trim
                End If
            Catch ex As Exception
                Return ""
            End Try

        End With

        Return ""

    End Function

    Public Async Sub AgregarLog(ByVal tiempo As Integer, ByVal error_s As String)

        ' Introducir un retraso de 2 segundos (2000 milisegundos)
        Await System.Threading.Tasks.Task.Delay(tiempo)

        Try
            If lstLog.InvokeRequired Then
                lstLog.Invoke(Sub() lstLog.Items.Add(Calcula_FechaActual.ToString))
                lstLog.Invoke(Sub() Me.lstLog.Items(lstLog.Items.Count - 1).SubItems.Add(error_s))
            Else
                Me.lstLog.Items.Add(Calcula_FechaActual)
                Me.lstLog.Items(lstLog.Items.Count - 1).SubItems.Add(error_s)
            End If
        Catch ex2 As Exception
            LogEventos.Escribir("AgregarLog. " & ex2.Message)
        End Try

    End Sub

    'DETENER proceso

    Private Sub DetenerProcesoSP()

        SyncLock bloqueoProcesoSP
            If cts IsNot Nothing Then cts.Cancel()
        End SyncLock

    End Sub

    Private Sub IniciarProcesosSP()

        SyncLock bloqueoProcesoSP
            If Not CentralDisponible OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 Then Return
            If export_hosting_tarea IsNot Nothing AndAlso Not export_hosting_tarea.IsCompleted Then Return
            If cts IsNot Nothing Then cts.Dispose()
            Dim solicitud As New CancellationTokenSource()
            cts = solicitud
            export_hosting_tarea = Task.Run(Sub() ExportarDataToHostingSP_Load(solicitud.Token), solicitud.Token)
        End SyncLock

    End Sub

    Private Sub ReiniciarProcesoSP()

        IniciarProcesosSP()

    End Sub

    Dim load_init As Boolean = True

#End Region

#Region "Portal Cliente"

    ''' <summary>
    ''' Configuarciones Varioas de Operación. Devielve el valor configurado en Datos de Unidad
    ''' </summary>
    ''' <param name="concepto"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function getValorConfiguracion(ByVal concepto As String) As String

        Try
            Dim tb_valor As DataTable
            tb_valor = tb_Recordset_MySQL_local("SELECT ccvematerial as valor from cat_materiales_configuracion where cConcepto = '" & concepto & "'")

            Return tb_valor.Rows(0).Item("valor").ToString
        Catch ex As Exception
            Return "0"
        End Try

        Return "0"

    End Function

    'Shared Function getClienteData(clues As String) As DataTable

    '    Dim dt As New System.Data.DataTable

    '    Try

    '        Dim version As Integer = 0
    '        Dim json As String = ""
    '        Dim url As String = ""

    '        ' ======= CONFIGURACIÓN DE PROTOCOLOS DE SEGURIDAD ========
    '        ' Forzar el uso de TLS 1.2, TLS 1.1 y TLS 1.0
    '        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12

    '        '=========================================

    '        ' URL de hsitomedic
    '        url = String.Format("https://admin.histomedic.mx/Enlace/getDataClues")

    '        Dim request As WebRequest
    '        request = WebRequest.Create(url)
    '        Dim response As WebResponse
    '        Dim postData As String = "clues=" & clues
    '        Dim data As Byte() = Encoding.UTF8.GetBytes(postData)

    '        request.Method = "POST"
    '        request.ContentType = "application/x-www-form-urlencoded"
    '        request.ContentLength = data.Length

    '        Dim stream As Stream = request.GetRequestStream()
    '        stream.Write(data, 0, data.Length)
    '        stream.Close()

    '        response = request.GetResponse()
    '        Dim sr As New StreamReader(response.GetResponseStream())

    '        json = sr.ReadToEnd

    '        'Dim cliente_data = Newtonsoft.Json.JsonConvert.DeserializeObject(json)
    '        dt = Newtonsoft.Json.JsonConvert.DeserializeObject(Of DataTable)(json)
    '        Return dt

    '    Catch ex As Exception
    '        Return dt
    '    End Try

    '    Return dt

    'End Function

    '=== Cirugias === === === === === === === === === === === === ===

    Private Sub EnviarWhatsApp(ByVal IdCirugia As Integer)

        Try

            If IdCirugia = 0 Then
                Exit Sub
            End If

            Dim tb_cirugiascontrol As DataTable
            tb_cirugiascontrol = tb_Recordset_MySQL_local("SELECT " & _
                                                     "tb_cirugiascontrol.fchCirugia, tb_cirugiascontrol.ccvemedicoInterviene, " & _
                                                     "tb_cirugiascontrol.clasificacion, tb_cirugiascontrol.cIndicacionesPaciente, tb_cirugiascontrol.cTipoIntervencion, tb_cirugiascontrol.ccvepaciente " & _
                                                     "FROM tb_cirugiascontrol " & _
                                                     "where " & _
                                                     "tb_cirugiascontrol.id = '" & IdCirugia & "'")
            Dim OI_FechaCirugia As String = ""
            Try
                OI_FechaCirugia = Format(CDate(tb_cirugiascontrol.Rows(0).Item("fchCirugia").ToString), "dd/MM/yyyy HH:mm")
            Catch ex As Exception
                OI_FechaCirugia = "Pendiente Programar"
                Exit Sub
            End Try

            Dim tb_paciente As DataTable
            tb_paciente = tb_Recordset_MySQL_local("Select nombre_completo " & _
                                                   "from tb_paciente " & _
                                                   "where ccvepaciente = '" & tb_cirugiascontrol.Rows(0).Item("ccvepaciente").ToString & "'")

            Dim tb_paciente2 As DataTable
            tb_paciente2 = tb_Recordset_MySQL_local("Select cTelFax " & _
                                                   "from tb_paciente2 " & _
                                                   "where ccvepaciente = '" & tb_cirugiascontrol.Rows(0).Item("ccvepaciente").ToString & "'")


            Dim cat_consultorio As DataTable
            cat_consultorio = tb_Recordset_MySQL_local("Select MensajeCemaCorreoOI, cNombreAbreviado, cDomicilio, cTelefono from cat_consultorio where cCLUES = '" & Me.CLUES & "'")


            ' == WhatsApp == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == ==

            Dim cIndicacionesPaciente As String = tb_cirugiascontrol.Rows(0).Item("cIndicacionesPaciente").ToString
            If Len(cIndicacionesPaciente.Trim) = 0 Then
                cIndicacionesPaciente = "NR"
            End If

            Dim tb_cliente As New DataTable
            tb_cliente = tb_ClienteData

            If Len(tb_paciente2.Rows(0).Item("cTelFax").ToString) = 10 Then
                If Funciones.WA_OrdenInternamientoCirugia(
                     tb_cliente,
                     tb_paciente2.Rows(0).Item("cTelFax").ToString, _
                     tb_paciente.Rows(0).Item("nombre_completo").ToString, _
                     cat_consultorio.Rows(0).Item("cNombreAbreviado").ToString, _
                     Format(CDate(tb_cirugiascontrol.Rows(0).Item("fchCirugia").ToString), "dd/MM/yyyy"), _
                     Format(CDate(tb_cirugiascontrol.Rows(0).Item("fchCirugia").ToString), "HH:mm"), _
                     Me.UsuarioNombre(tb_cirugiascontrol.Rows(0).Item("ccvemedicoInterviene").ToString), _
                     "", "", "", _
                     "CIRUGÍA", _
                     Me.getValorConfiguracion("TIEMPO_INGRESO_CIRUGIAS"), _
                     cIndicacionesPaciente, _
                     cat_consultorio.Rows(0).Item("cDomicilio").ToString, _
                     cat_consultorio.Rows(0).Item("cTelefono").ToString,
                     tb_cirugiascontrol.Rows(0).Item("cTipoIntervencion").ToString, Format(IdCirugia, "000000")
                    ) Then

                End If
            End If

            ' == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == == ==

        Catch ex As Exception

            AgregarLog(500, ex.Message & ". Error al enviar WA Folio: " & IdCirugia)

            'Me.lstLog.Items.Add(Calcula_FechaActual.ToString)
            'Me.lstLog.Items(lstLog.Items.Count - 1).SubItems.Add(ex.Message & ". Error al enviar WA Folio: " & IdCirugia)
        End Try

    End Sub

    ''' <summary>
    ''' Importar Cirugias Temporal OK
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub ImportarCirugiaTemporal()


        Dim ssf_cirugiascontrol_central As DataTable
        ssf_cirugiascontrol_central = tb_Recordset_MySQL_Central("SELECT " & _
                                                                 "ssf_cirugiascontrol.* " & _
                                                                 "FROM ssf_cirugiascontrol " & _
                                                                 "where ssf_cirugiascontrol.sinc = 0")

        If ssf_cirugiascontrol_central.Rows.Count = 0 Then
            Exit Sub
        End If

        Me.ProgressBarX1.Minimum = 0
        Me.ProgressBarX1.Maximum = ssf_cirugiascontrol_central.Rows.Count
        Dim IdPlataforma As Integer = 0

        For i As Integer = 0 To ssf_cirugiascontrol_central.Rows.Count - 1

            '== Obtenemos el id de cliente de plataforma == ==
            IdPlataforma = ssf_cirugiascontrol_central.Rows(i).Item("Id").ToString

            If Me.GuardarCx(ssf_cirugiascontrol_central, i) Then
                '== Actualiza bandera de platafroma para terminar actualizaciÓn == ==
                Me.Update_Central("Update ssf_cirugiascontrol set " & _
                                  "sinc = 1 " & _
                                  "where " & _
                                  "Id = '" & IdPlataforma & "'")
            End If

            Me.ProgressBarX1.Value = Me.ProgressBarX1.Value + 1

        Next

        Me.ProgressBarX1.Value = 0

    End Sub

    Private Function getPacienteNombreCompleto(ByVal paciente_id As Integer) As String

        Dim tb_paciente As DataTable
        tb_paciente = tb_Recordset_MySQL_local("Select CONCAT_WS(' ', cNombre, cPriApellido, cSegApellido) AS nombre_completo from tb_paciente where icvepaciente = " & paciente_id & "")
        If tb_paciente.Rows.Count = 0 Then
            Return ""
        Else
            Return tb_paciente.Rows(0).Item("nombre_completo").ToString
        End If

    End Function

    Private Function getPacienteNombreCompletoFromClave(ByVal ccvepaciente As String) As String

        Dim tb_paciente As DataTable
        tb_paciente = tb_Recordset_MySQL_local("Select CONCAT_WS(' ', cNombre, cPriApellido, cSegApellido) AS nombre_completo from tb_paciente where ccvepaciente = '" & ccvepaciente & "'")
        If tb_paciente.Rows.Count = 0 Then
            Return ""
        Else
            Return tb_paciente.Rows(0).Item("nombre_completo").ToString
        End If

    End Function

    Private Function getPacienteNumExpediente(ByVal ccvepaciente As String) As String

        Dim tb_paciente As DataTable
        tb_paciente = tb_Recordset_MySQL_local("Select iNumExpediente from tb_paciente where ccvepaciente = '" & ccvepaciente & "'")
        If tb_paciente.Rows.Count = 0 Then
            Return ""
        Else
            Return tb_paciente.Rows(0).Item("iNumExpediente").ToString
        End If

    End Function

    Private Function getCvePaciente(ByVal paciente_id As Integer) As String

        Dim tb_paciente As DataTable
        tb_paciente = tb_Recordset_MySQL_local("Select ccvepaciente from tb_paciente where icvepaciente = " & paciente_id & "")
        If tb_paciente.Rows.Count = 0 Then
            Return ""
        Else
            Return tb_paciente.Rows(0).Item("ccvepaciente").ToString
        End If

    End Function

    Private Function getPacienteEmail(ByVal paciente_id As Integer) As String

        Dim tb_paciente As DataTable
        tb_paciente = tb_Recordset_MySQL_local("Select tb_paciente2.email from " & _
                                               "tb_paciente " & _
                                               "INNER JOIN tb_paciente2 ON (tb_paciente2.ccvepaciente = tb_paciente.ccvepaciente) " & _
                                               "where tb_paciente.icvepaciente = " & paciente_id & "")
        If tb_paciente.Rows.Count = 0 Then
            Return ""
        Else
            Return tb_paciente.Rows(0).Item("email").ToString
        End If

    End Function

    Private Function getEspecialidadMedico(ByVal ccvemedico As String) As String

        Dim temp As DataTable
        temp = tb_Recordset_MySQL_local("SELECT cEspecialidad FROM cat_medico where ccvemedico = '" & ccvemedico & "'")

        Dim cat_servicios As DataTable
        cat_servicios = tb_Recordset_MySQL_local("Select * from cat_servicios where cdscservicio = '" & Trim(temp.Rows(0).Item("cEspecialidad").ToString) & "'")

        If cat_servicios.Rows.Count > 0 Then
            Return Trim(temp.Rows(0).Item("cEspecialidad").ToString)
        Else

            If Insert_local("cat_servicios", _
                              "cdscareaafectada = 'CONSULTA EXTERNA', " & _
                              "cdscservicio = '" & Trim(temp.Rows(0).Item("cEspecialidad").ToString) & "', " & _
                              "ccvematerial = '', " & _
                              "cCluesDivision = '" & Me.CLUES & "', " & _
                              "iConsultaGeneral = '0', " & _
                              "iRecepcionUrgencias = '0', " & _
                              "iEgresoGrupal = '0', " & _
                              "iCert_PreservConsejeria = '0', " & _
                              "iOrdenInternamiento = '0', " & _
                              "iValidacionCE = '0', " & _
                              "iPermiteConcpetosRepetidos = '0', " & _
                              "iRequiereHC = '0', " & _
                              "iObligatorio_EqUtilizado_NE = '0', " & _
                              "iObligatorio_CIEMorfo = '0', " & _
                              "iPermiteMultipleCita = '0', " & _
                              "iAgendaSubsecuentes = '0', " & _
                              "iAgendaLab = '0', " & _
                              "icveclave = '0', " & _
                              "fchregistro = current_timestamp, " & _
                              "ccveusuario = 'histomedic_portal', " & _
                              "iActivo = '1'") Then

                Return Trim(temp.Rows(0).Item("cEspecialidad").ToString)

            End If

        End If

    End Function

    Public Function GuardarCx(tbcx_central As DataTable, row_i As Integer) As Boolean


        Try

            Dim paciente_id As Integer = 0
            If tbcx_central.Rows(row_i).Item("paciente_id").ToString <> "" Then
                paciente_id = tbcx_central.Rows(row_i).Item("paciente_id").ToString
            End If

            If paciente_id = 0 Then
                If Not setPaciente(tbcx_central, row_i, paciente_id) Then
                    Return False
                End If
            End If

            Dim CvePaciente As String = ""
            CvePaciente = getCvePaciente(paciente_id)

            If CvePaciente = "" Then
                Return False
            End If

            Dim FechaCx_Str As String = ""
            Dim OrdenInt As Integer = 0

            Dim EstatusCx As String = ""

            Try
                OrdenInt = 0
                FechaCx_Str = Format(CDate(tbcx_central.Rows(row_i).Item("fecha_cirugia").ToString), "yyyy-MM-dd") & " " & tbcx_central.Rows(row_i).Item("fecha_cirugia_hora_ini").ToString & ""
                FechaCx_Str = "'" & Format(CDate(tbcx_central.Rows(row_i).Item("fecha_cirugia").ToString), "yyyy-MM-dd") & " " & tbcx_central.Rows(row_i).Item("fecha_cirugia_hora_ini").ToString & "'"

                EstatusCx = "PROGRAMADA"
            Catch ex As Exception
                OrdenInt = 1
                FechaCx_Str = "Null"
                EstatusCx = "PENDIENTE"
                Return False
            End Try

            Dim id_cirugia_local As Integer
            id_cirugia_local = tbcx_central.Rows(row_i).Item("id_cirugia_local").ToString

            'If id_cirugia_local = 0 Then
            '    If Me.ValidaAgendaCirugias(tbcx_central.Rows(row_i).Item("quirofano_id").ToString, FechaCx, tbcx_central.Rows(row_i).Item("duracion_cirugia").ToString, 0) Then
            '        Return False
            '    End If
            'End If

            Dim CveMedicoAdscrito As String = ""
            CveMedicoAdscrito = tbcx_central.Rows(row_i).Item("doctor_id").ToString


            'cClasificacionIngreso
            Dim ClasifIngreso As String
            ClasifIngreso = "CIRUGÍA"

            Dim iDiasEstancia As Integer
            Try
                iDiasEstancia = 1
            Catch ex As Exception
                iDiasEstancia = 0
            End Try

            Dim especialidad As String = ""
            especialidad = Me.getEspecialidadMedico(CveMedicoAdscrito)

            Dim IdCirugia As Integer = 0

            If id_cirugia_local > 0 Then
                IdCirugia = tbcx_central.Rows(row_i).Item("id_cirugia_local").ToString
                GoTo no_insertar_cx
            End If

            If Insert_local("tb_cirugiascontrol", _
                                       "clues = '" & tbcx_central.Rows(row_i).Item("clues").ToString & "', " & _
                                       "fchregistro=current_timestamp, " & _
                                       "ccvepaciente='" & CvePaciente & "', " & _
                                       "clasificacion='CIRUGÍA', " & _
                                       "icvequirofano='" & tbcx_central.Rows(row_i).Item("quirofano_id").ToString & "', " & _
                                       "duracion='" & tbcx_central.Rows(row_i).Item("duracion_cirugia").ToString & "', " & _
                                       "ccvemedico='" & tbcx_central.Rows(row_i).Item("ccveusuario").ToString & "', " & _
                                       "cdscareaafectada='" & especialidad & "', " & _
                                       "Id_Recepcion = '0', " & _
                                       "IdDestino='1', " & _
                                       "destino_especifique='HOSPITALIZACION', " & _
                                       "IdProcedencia = '2', " & _
                                       "cEstatus='" & EstatusCx & "', " & _
                                       "iOrdenInternProgCx = '" & OrdenInt & "', " & _
                                       "cClasificacionIngreso='" & ClasifIngreso & "', " & _
                                       "fchCirugia = " & FechaCx_Str & ", " & _
                                       "ccvemedicoInterviene='" & CveMedicoAdscrito & "', " & _
                                       "icvediagnostico='', " & _
                                       "cDxAmpliado = '', " & _
                                       "boolTipoD='', " & _
                                       "cTipoIntervencion='" & tbcx_central.Rows(row_i).Item("cirugia").ToString & "', " & _
                                       "ccvematerial ='', " & _
                                       "iOI_Costo = '0', " & _
                                       "ccveanterior ='', " & _
                                       "icveprocedimiento='', " & _
                                       "boolProgramado='SI', " & _
                                       "cClasificacion='CIRUGIA ELECTIVA', " & _
                                       "cOI_ReqHosp='SI', " & _
                                       "iDiasEstancia = '" & iDiasEstancia & "', " & _
                                       "cdscservicioIngresa = 'HOSPITALIZACION', " & _
                                       "cRequerimientos = '" & tbcx_central.Rows(row_i).Item("requerimientos_especiales").ToString & "', " & _
                                       "cRequerimientosAdicionales = '" & tbcx_central.Rows(row_i).Item("requerimientos_especiales").ToString & "', " & _
                                       "cOI_PresnetarseEn = 'ADMISION HOSPITALARIA', " & _
                                       "email_confirma = '" & Me.getPacienteEmail(paciente_id) & "', " & _
                                       "cIndicacionesPaciente = '" & tbcx_central.Rows(row_i).Item("indicaciones_paciente").ToString & "', " & _
                                       "cIndicacionesEnfermeria = '" & tbcx_central.Rows(row_i).Item("indicaciones_enfermeria").ToString & "', " & _
                                       "cOI_DonacionSangre = '', " & _
                                       "iOI_NumDonadores = '0', " & _
                                       "iNueva = 1") Then


no_insertar_cx:


                Dim tb_temp_cx As DataTable
                If IdCirugia = 0 Then
                    tb_temp_cx = tb_Recordset_MySQL_local("SELECT Id FROM tb_cirugiascontrol where " & _
                                      "ccvepaciente='" & CvePaciente & "' and " & _
                                      "ccvemedico='" & tbcx_central.Rows(row_i).Item("ccveusuario").ToString & "' " & _
                                      "order by id desc")
                    IdCirugia = tb_temp_cx.Rows(0).Item(0).ToString
                End If


                '== actualiza el id de ciorugia para no volverlo a regsitrar ===  ===  ===  ===  === 
                If id_cirugia_local = 0 Then
                    Update_Central("ssf_cirugiascontrol", _
                                   "id_cirugia_local = '" & IdCirugia & "'", _
                                   "Id", tbcx_central.Rows(row_i).Item("Id").ToString)
                End If

                '  ===  ===  ===  ===  ===  ===  ===  ===  ===  ===  ===  ===  ===  ===  ===  === 

                If IdCirugia = 0 Then
                    If Insert_local("tb_citasingresos", _
                                 "clues = '" & Me.CLUES & "', " & _
                                 "Id_tb_cirugiascontrol = '" & IdCirugia & "', " & _
                                 "ccvepaciente='" & CvePaciente & "', " & _
                                 "ccvemedico='" & CveMedicoAdscrito & "', " & _
                                 "cdscareaafectada='HOSPITALIZACION', " & _
                                 "fchCita = " & FechaCx_Str & ", " & _
                                 "cEstatus='PROGRAMADA', " & _
                                 "iEstatus = 0, " & _
                                 "cMotivo='" & tbcx_central.Rows(row_i).Item("cirugia").ToString & "', " & _
                                 "cdscServicioSolicita='" & especialidad & "', " & _
                                 "clasificacion='" & ClasifIngreso & "', " & _
                                 "fchregistro=current_timestamp, " & _
                                 "ccvemedicoreg='" & tbcx_central.Rows(row_i).Item("ccveusuario").ToString & "'") Then

                    End If
                Else

                    Dim tb_temp_cx_svs As DataTable
                    tb_temp_cx_svs = tb_Recordset_MySQL_local("Select Id_tb_cirugiascontrol from tb_citasingresos where Id_tb_cirugiascontrol = '" & IdCirugia & "'")
                    If tb_temp_cx_svs.Rows.Count = 0 Then
                        If Insert_local("tb_citasingresos", _
                                  "clues = '" & Me.CLUES & "', " & _
                                  "Id_tb_cirugiascontrol = '" & IdCirugia & "', " & _
                                  "ccvepaciente='" & CvePaciente & "', " & _
                                  "ccvemedico='" & CveMedicoAdscrito & "', " & _
                                  "cdscareaafectada='HOSPITALIZACION', " & _
                                  "fchCita = " & FechaCx_Str & ", " & _
                                  "cEstatus='PROGRAMADA', " & _
                                  "iEstatus = 0, " & _
                                  "cMotivo='" & tbcx_central.Rows(row_i).Item("cirugia").ToString & "', " & _
                                  "cdscServicioSolicita='" & especialidad & "', " & _
                                  "clasificacion='" & ClasifIngreso & "', " & _
                                  "fchregistro=current_timestamp, " & _
                                  "ccvemedicoreg='" & CveMedicoAdscrito & "'") Then

                        End If
                    End If
                End If

                Try
                    EnviarWhatsApp(IdCirugia)
                Catch ex As Exception
                End Try

                Try
                    Me.EnviarMail(IdCirugia, Me.getPacienteEmail(paciente_id), getPacienteNombreCompleto(paciente_id), CveMedicoAdscrito)
                Catch ex As Exception
                End Try

                Return True

            End If

            Return True

        Catch ex As Exception
            Return False
        End Try

    End Function

    '=== === === === === === === === === === === === === === === === 

    Public Function EnviarMail(ByVal IdCirugia As Integer, ByVal email As String, ByVal nombre_completo As String, ByVal CveMedicoAdscrito As String) As Boolean

        Try

            If IdCirugia = 0 Then
                Return False
            End If

            Dim temp As DataTable
            temp = tb_Recordset_MySQL_local("SELECT " & _
                                                     "tb_cirugiascontrol.fchCirugia, tb_cirugiascontrol.ccvepaciente, tb_cirugiascontrol.cTipoIntervencion, tb_cirugiascontrol.cIndicacionesPaciente " & _
                                                     "FROM tb_cirugiascontrol " & _
                                                     "where " & _
                                                     "tb_cirugiascontrol.id = '" & IdCirugia & "'")
            Dim OI_FechaCirugia As String = ""
            Try
                OI_FechaCirugia = Format(CDate(temp.Rows(0).Item("fchCirugia").ToString), "dd/MM/yyyy HH:mm")
            Catch ex As Exception
                OI_FechaCirugia = "Pendiente Programar"
            End Try


            Dim cat_consultorio As DataTable
            cat_consultorio = tb_Recordset_MySQL_local("Select MensajeCemaCorreoOI, cNombreAbreviado, cEncabezadoSria from cat_consultorio where cCLUES = '" & Me.CLUES & "'")

            Dim Mensaje As String = cat_consultorio.Rows(0).Item("MensajeCemaCorreoOI").ToString
            Mensaje = Mensaje.Replace("Encabezado_1", cat_consultorio.Rows(0).Item("cNombreAbreviado").ToString)
            Mensaje = Mensaje.Replace("Encabezado_2", cat_consultorio.Rows(0).Item("cEncabezadoSria").ToString)
            Mensaje = Mensaje.Replace("OI_NumExpediente", getPacienteNumExpediente(temp.Rows(0).Item("ccvepaciente").ToString))
            Mensaje = Mensaje.Replace("OI_Folio", Format(IdCirugia, "000000"))
            Mensaje = Mensaje.Replace("OI_FechaCirugia", OI_FechaCirugia)
            Mensaje = Mensaje.Replace("OI_Cirugia", temp.Rows(0).Item("cTipoIntervencion").ToString)
            Mensaje = Mensaje.Replace("OI_IndicacionesPaciente", temp.Rows(0).Item("cIndicacionesPaciente").ToString)

            '********************************************************************************
            If email <> "" Then
                If FacturacionEnviarOrdenInternamiento.EnviarOrdenInternamiento(nombre_completo, _
                                                                       email, _
                                                                       "CONFIRMACIÓN DE ORDEN DE INTERNAMIENTO", _
                                                                        Mensaje, IdCirugia, False) Then
                End If
            End If

            '********************************************************************************

            '********************************************************************************
            Dim tb_mail As DataTable
            tb_mail = tb_Recordset_MySQL_local("Select email from cat_medico where ccvemedico = '" & CveMedicoAdscrito & "'")

            If tb_mail.Rows(0).Item("email").ToString <> "" Then
                'Funciones.Msj_AdvOnly("El medico seleccionado no tiene correo electrónico registrado, deberá notificar vía telefónica")
                FacturacionEnviarOrdenInternamiento.EnviarOrdenInternamiento(nombre_completo, _
                                                                             tb_mail.Rows(0).Item("email").ToString, _
                                                                             "CONFIRMACIÓN DE ORDEN DE INTERNAMIENTO", _
                                                                              Mensaje, IdCirugia, True)
            End If
            '********************************************************************************

        Catch ex As Exception
        End Try

    End Function


#End Region

#Region "MySQL"

    Public Function Insert_Admin(ByVal query As String) As Boolean

        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_Admin.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(query, cx_MySQL_Admin)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_Admin, "Insert_Admin")
                Return False
            End Try
        End SyncLock
    End Function

    Public Function Insert_Admin(ByVal NomTabla As String, ByVal ValorSET As String) As Boolean
        Dim Cadena As String = ""
        Cadena = "Insert into " & NomTabla & _
                 " SET " & ValorSET


        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_Admin.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(Cadena, cx_MySQL_Admin)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_Admin, "Insert_Admin")
                Return False
            End Try
        End SyncLock
    End Function

    Public Property cx_MySQL_Admin() As MySqlConnector.MySqlConnection
        Get
            Return cnn_MySQL_Admin
        End Get
        Set(ByVal Value As MySqlConnector.MySqlConnection)
            cnn_MySQL_Admin = Value
        End Set

    End Property

    Public Function tb_Recordset_MySQL_Admin(ByVal Comando As String) As Data.DataTable
        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse (Not CentralDisponible AndAlso Interlocked.CompareExchange(comprobacionCentralActiva, 0, 0) = 0) Then Return New DataTable()
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse (Not CentralDisponible AndAlso Interlocked.CompareExchange(comprobacionCentralActiva, 0, 0) = 0) Then Return New DataTable()
            If cx_MySQL_Admin.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return New DataTable()
            End If
            Dim tabla As New DataTable()
            Try
                Using comandoCentral As New MySqlConnector.MySqlCommand(Comando, cx_MySQL_Admin)
                    Using adaptador As New MySqlConnector.MySqlDataAdapter(comandoCentral)
                        adaptador.Fill(tabla)
                    End Using
                End Using
                Return tabla
            Catch ex As Exception
                tabla.Dispose()
                RegistrarErrorCentral(ex, cx_MySQL_Admin, "tb_Recordset_MySQL_Admin")
                Return New DataTable()
            End Try
        End SyncLock
    End Function

    Public Function Update_Admin(ByVal Cadena As String) As Boolean

        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_Admin.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(Cadena, cx_MySQL_Admin)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_Admin, "Update_Admin")
                Return False
            End Try
        End SyncLock
    End Function

    Public Function Update_Admin(ByVal NomTabla As String, ByVal ValorSET As String, ByVal CampoCond As String, ByVal ValorFiltro As String) As Boolean
        Dim Cadena As String = ""
        Cadena = "Update " & NomTabla & _
                 " SET " & ValorSET & _
                 " where " & CampoCond & _
                 " = '" & ValorFiltro & "'"


        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_Admin.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(Cadena, cx_MySQL_Admin)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_Admin, "Update_Admin")
                Return False
            End Try
        End SyncLock
    End Function

    Public Function Test_MySQL_Admin(ByVal str_ConStr As String) As Boolean
        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 Then Return False
            Try
                If cx_MySQL_Admin.State = ConnectionState.Open Then Return True
                If cx_MySQL_Admin.State <> ConnectionState.Closed Then cx_MySQL_Admin.Close()
                Dim opciones As New MySqlConnector.MySqlConnectionStringBuilder(str_ConStr)
                opciones.ConnectionTimeout = 10
                opciones.DefaultCommandTimeout = 30
                opciones.Keepalive = 60
                cx_MySQL_Admin.ConnectionString = opciones.ConnectionString
                cx_MySQL_Admin.Open()
                Using comando As New MySqlConnector.MySqlCommand("SET time_zone = 'America/Mexico_City';", cx_MySQL_Admin)
                    comando.CommandTimeout = 10
                    comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_Admin, "Test_MySQL_Admin")
                Return False
            End Try
        End SyncLock
    End Function

    '****** Central Async ***********************************************

    Public Function Insert_CentralAsync(ByVal query As String) As Boolean

        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_CentralAsync.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(query, cx_MySQL_CentralAsync)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_CentralAsync, "Insert_CentralAsync")
                Return False
            End Try
        End SyncLock
    End Function

    Public Function Insert_CentralAsync(ByVal NomTabla As String, ByVal ValorSET As String) As Boolean
        Dim Cadena As String = ""
        Cadena = "Insert into " & NomTabla & _
                 " SET " & ValorSET


        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_CentralAsync.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(Cadena, cx_MySQL_CentralAsync)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_CentralAsync, "Insert_CentralAsync")
                Return False
            End Try
        End SyncLock
    End Function

    Public Property cx_MySQL_CentralAsync() As MySqlConnector.MySqlConnection
        Get
            Return cnn_MySQL_CentralAsync
        End Get
        Set(ByVal Value As MySqlConnector.MySqlConnection)
            cnn_MySQL_CentralAsync = Value
        End Set

    End Property

    Public Function tb_Recordset_MySQL_CentralAsync(ByVal Comando As String) As Data.DataTable
        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return New DataTable()
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return New DataTable()
            If cx_MySQL_CentralAsync.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return New DataTable()
            End If
            Dim tabla As New DataTable()
            Try
                Using comandoCentral As New MySqlConnector.MySqlCommand(Comando, cx_MySQL_CentralAsync)
                    Using adaptador As New MySqlConnector.MySqlDataAdapter(comandoCentral)
                        adaptador.Fill(tabla)
                    End Using
                End Using
                Return tabla
            Catch ex As Exception
                tabla.Dispose()
                RegistrarErrorCentral(ex, cx_MySQL_CentralAsync, "tb_Recordset_MySQL_CentralAsync")
                Return New DataTable()
            End Try
        End SyncLock
    End Function

    Public Function Update_CentralAsync(ByVal Cadena As String) As Boolean

        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_CentralAsync.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(Cadena, cx_MySQL_CentralAsync)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_CentralAsync, "Update_CentralAsync")
                Return False
            End Try
        End SyncLock
    End Function

    Public Function Update_CentralAsync(ByVal NomTabla As String, ByVal ValorSET As String, ByVal CampoCond As String, ByVal ValorFiltro As String) As Boolean
        Dim Cadena As String = ""
        Cadena = "Update " & NomTabla & _
                 " SET " & ValorSET & _
                 " where " & CampoCond & _
                 " = '" & ValorFiltro & "'"


        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_CentralAsync.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(Cadena, cx_MySQL_CentralAsync)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_CentralAsync, "Update_CentralAsync")
                Return False
            End Try
        End SyncLock
    End Function

    Public Function Test_MySQL_CentralAsync(ByVal str_ConStr As String) As Boolean
        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 Then Return False
            Try
                If cx_MySQL_CentralAsync.State = ConnectionState.Open Then Return True
                If cx_MySQL_CentralAsync.State <> ConnectionState.Closed Then cx_MySQL_CentralAsync.Close()
                Dim opciones As New MySqlConnector.MySqlConnectionStringBuilder(str_ConStr)
                opciones.ConnectionTimeout = 10
                opciones.DefaultCommandTimeout = 30
                opciones.Keepalive = 60
                cx_MySQL_CentralAsync.ConnectionString = opciones.ConnectionString
                cx_MySQL_CentralAsync.Open()
                Using comando As New MySqlConnector.MySqlCommand("SET time_zone = 'America/Mexico_City';", cx_MySQL_CentralAsync)
                    comando.CommandTimeout = 10
                    comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_CentralAsync, "Test_MySQL_CentralAsync")
                Return False
            End Try
        End SyncLock
    End Function

    Private Function Update_FotoSistema_CentralAsync(ByVal Campo As String, ByVal Tabla As String, _
                                       ByVal IdCampo As String, ByVal IdValor As String, _
                                       ByVal Imagen As Byte()) As Boolean
        Dim cmdtext As String
        cmdtext = "UPDATE " & Tabla & " set " & Campo & "=?File where " & IdCampo & " = '" & IdValor & "'"
        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_CentralAsync.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(cmdtext, cx_MySQL_CentralAsync)
                    cmm_Comando.Parameters.AddWithValue("?File", Imagen)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_CentralAsync, "Update_FotoSistema_CentralAsync")
                Return False
            End Try
        End SyncLock
    End Function

    Public Function Delete_CentralAsync(ByVal Cadena As String) As Boolean

        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_CentralAsync.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(Cadena, cx_MySQL_CentralAsync)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_CentralAsync, "Delete_CentralAsync")
                Return False
            End Try
        End SyncLock
    End Function


    '****** Central Async ALM ***********************************************

    Public Function Insert_CentralAsyncALM(ByVal query As String) As Boolean

        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_CentralAsyncALM.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(query, cx_MySQL_CentralAsyncALM)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_CentralAsyncALM, "Insert_CentralAsyncALM")
                Return False
            End Try
        End SyncLock
    End Function

    Public Function Insert_CentralAsyncALM(ByVal NomTabla As String, ByVal ValorSET As String) As Boolean
        Dim Cadena As String = ""
        Cadena = "Insert into " & NomTabla & _
                 " SET " & ValorSET


        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_CentralAsyncALM.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(Cadena, cx_MySQL_CentralAsyncALM)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_CentralAsyncALM, "Insert_CentralAsyncALM")
                Return False
            End Try
        End SyncLock
    End Function

    Public Property cx_MySQL_CentralAsyncALM() As MySqlConnector.MySqlConnection
        Get
            Return cnn_MySQL_CentralAsyncALM
        End Get
        Set(ByVal Value As MySqlConnector.MySqlConnection)
            cnn_MySQL_CentralAsyncALM = Value
        End Set

    End Property

    Public Function tb_Recordset_MySQL_CentralAsyncALM(ByVal Comando As String) As Data.DataTable
        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return New DataTable()
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return New DataTable()
            If cx_MySQL_CentralAsyncALM.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return New DataTable()
            End If
            Dim tabla As New DataTable()
            Try
                Using comandoCentral As New MySqlConnector.MySqlCommand(Comando, cx_MySQL_CentralAsyncALM)
                    Using adaptador As New MySqlConnector.MySqlDataAdapter(comandoCentral)
                        adaptador.Fill(tabla)
                    End Using
                End Using
                Return tabla
            Catch ex As Exception
                tabla.Dispose()
                RegistrarErrorCentral(ex, cx_MySQL_CentralAsyncALM, "tb_Recordset_MySQL_CentralAsyncALM")
                Return New DataTable()
            End Try
        End SyncLock
    End Function

    Public Function Update_CentralAsyncALM(ByVal Cadena As String) As Boolean

        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_CentralAsyncALM.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(Cadena, cx_MySQL_CentralAsyncALM)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_CentralAsyncALM, "Update_CentralAsyncALM")
                Return False
            End Try
        End SyncLock
    End Function

    Public Function Update_CentralAsyncALM(ByVal NomTabla As String, ByVal ValorSET As String, ByVal CampoCond As String, ByVal ValorFiltro As String) As Boolean
        Dim Cadena As String = ""
        Cadena = "Update " & NomTabla & _
                 " SET " & ValorSET & _
                 " where " & CampoCond & _
                 " = '" & ValorFiltro & "'"


        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_CentralAsyncALM.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(Cadena, cx_MySQL_CentralAsyncALM)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_CentralAsyncALM, "Update_CentralAsyncALM")
                Return False
            End Try
        End SyncLock
    End Function

    Public Function Test_MySQL_CentralAsyncALM(ByVal str_ConStr As String) As Boolean
        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 Then Return False
            Try
                If cx_MySQL_CentralAsyncALM.State = ConnectionState.Open Then Return True
                If cx_MySQL_CentralAsyncALM.State <> ConnectionState.Closed Then cx_MySQL_CentralAsyncALM.Close()
                Dim opciones As New MySqlConnector.MySqlConnectionStringBuilder(str_ConStr)
                opciones.ConnectionTimeout = 10
                opciones.DefaultCommandTimeout = 30
                opciones.Keepalive = 60
                cx_MySQL_CentralAsyncALM.ConnectionString = opciones.ConnectionString
                cx_MySQL_CentralAsyncALM.Open()
                Using comando As New MySqlConnector.MySqlCommand("SET time_zone = 'America/Mexico_City';", cx_MySQL_CentralAsyncALM)
                    comando.CommandTimeout = 10
                    comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_CentralAsyncALM, "Test_MySQL_CentralAsyncALM")
                Return False
            End Try
        End SyncLock
    End Function

    Private Function Update_FotoSistema_CentralAsyncALM(ByVal Campo As String, ByVal Tabla As String, _
                                       ByVal IdCampo As String, ByVal IdValor As String, _
                                       ByVal Imagen As Byte()) As Boolean
        Dim cmdtext As String
        cmdtext = "UPDATE " & Tabla & " set " & Campo & "=?File where " & IdCampo & " = '" & IdValor & "'"
        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_CentralAsyncALM.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(cmdtext, cx_MySQL_CentralAsyncALM)
                    cmm_Comando.Parameters.AddWithValue("?File", Imagen)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_CentralAsyncALM, "Update_FotoSistema_CentralAsyncALM")
                Return False
            End Try
        End SyncLock
    End Function

    Public Function Delete_CentralAsyncALM(ByVal Cadena As String) As Boolean

        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_CentralAsyncALM.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(Cadena, cx_MySQL_CentralAsyncALM)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_CentralAsyncALM, "Delete_CentralAsyncALM")
                Return False
            End Try
        End SyncLock
    End Function


    '*****************************************************

    Public Function Insert_Central(ByVal query As String) As Boolean

        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_Central.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(query, cx_MySQL_Central)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_Central, "Insert_Central")
                Return False
            End Try
        End SyncLock
    End Function

    Public Function Insert_Central(ByVal NomTabla As String, ByVal ValorSET As String) As Boolean
        Dim Cadena As String = ""
        Cadena = "Insert into " & NomTabla & _
                 " SET " & ValorSET


        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_Central.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(Cadena, cx_MySQL_Central)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_Central, "Insert_Central")
                Return False
            End Try
        End SyncLock
    End Function

    Public Property cx_MySQL_Central() As MySqlConnector.MySqlConnection
        Get
            Return cnn_MySQL_Central
        End Get
        Set(ByVal Value As MySqlConnector.MySqlConnection)
            cnn_MySQL_Central = Value
        End Set

    End Property

    Public Function tb_Recordset_MySQL_Central(ByVal Comando As String) As Data.DataTable
        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return New DataTable()
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return New DataTable()
            If cx_MySQL_Central.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return New DataTable()
            End If
            Dim tabla As New DataTable()
            Try
                Using comandoCentral As New MySqlConnector.MySqlCommand(Comando, cx_MySQL_Central)
                    Using adaptador As New MySqlConnector.MySqlDataAdapter(comandoCentral)
                        adaptador.Fill(tabla)
                    End Using
                End Using
                Return tabla
            Catch ex As Exception
                tabla.Dispose()
                RegistrarErrorCentral(ex, cx_MySQL_Central, "tb_Recordset_MySQL_Central")
                Return New DataTable()
            End Try
        End SyncLock
    End Function

    Public Function Update_Central(ByVal Cadena As String) As Boolean

        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_Central.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(Cadena, cx_MySQL_Central)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_Central, "Update_Central")
                Return False
            End Try
        End SyncLock
    End Function

    Public Function Update_Central(ByVal NomTabla As String, ByVal ValorSET As String, ByVal CampoCond As String, ByVal ValorFiltro As String) As Boolean
        Dim Cadena As String = ""
        Cadena = "Update " & NomTabla & _
                 " SET " & ValorSET & _
                 " where " & CampoCond & _
                 " = '" & ValorFiltro & "'"


        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_Central.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(Cadena, cx_MySQL_Central)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_Central, "Update_Central")
                Return False
            End Try
        End SyncLock
    End Function

    Public Function Test_MySQL_Central(ByVal str_ConStr As String) As Boolean
        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 Then Return False
            Try
                If cx_MySQL_Central.State = ConnectionState.Open Then Return True
                If cx_MySQL_Central.State <> ConnectionState.Closed Then cx_MySQL_Central.Close()
                Dim opciones As New MySqlConnector.MySqlConnectionStringBuilder(str_ConStr)
                opciones.ConnectionTimeout = 10
                opciones.DefaultCommandTimeout = 30
                opciones.Keepalive = 60
                cx_MySQL_Central.ConnectionString = opciones.ConnectionString
                cx_MySQL_Central.Open()
                Using comando As New MySqlConnector.MySqlCommand("SET time_zone = 'America/Mexico_City';", cx_MySQL_Central)
                    comando.CommandTimeout = 10
                    comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_Central, "Test_MySQL_Central")
                Return False
            End Try
        End SyncLock
    End Function

    '*****************************************************

    Public Function Insert_local(ByVal NomTabla As String, ByVal ValorSET As String) As Boolean

        Dim Cadena As String = ""
        Cadena = "Insert into " & NomTabla & _
                 " SET " & ValorSET

        Dim cmm_Comando As New Global.MySqlConnector.MySqlCommand(Cadena, cx_MySQL_local)
        Dim ciclo As Integer = 0
        Try
intenta_otravz:
            If cx_MySQL_local.State = ConnectionState.Closed Then cx_MySQL_local.Open()
            cmm_Comando.ExecuteNonQuery()
        Catch ex As Exception
            If cx_MySQL_local.State = ConnectionState.Closed Then
                ciclo = ciclo + 1
                If ciclo = 3 Then
                    AgregarLog(500, ex.Message & ", Error al insertar registro local: " & Cadena)
                    Exit Try
                End If
                GoTo intenta_otravz
            Else
                AgregarLog(500, ex.Message & ", Error al insertar registro local: " & Cadena)
                Return False
            End If
            Return False
        End Try

        Return True

    End Function

    Public Property cx_MySQL_local() As MySqlConnector.MySqlConnection
        Get
            Return cnn_MySQL_Local
        End Get
        Set(ByVal Value As MySqlConnector.MySqlConnection)
            cnn_MySQL_Local = Value
        End Set

    End Property

    Public Function tb_Recordset_MySQL_local(ByVal Comando As String) As Data.DataTable

        Dim da_Adaptador As New MySqlConnector.MySqlDataAdapter
        Dim cmm_Comando As New MySqlConnector.MySqlCommand(Comando, cx_MySQL_local)
        Dim dt_DataTable As New System.Data.DataTable
        da_Adaptador.SelectCommand = cmm_Comando
        Dim ciclo As Integer = 0
        Try
intenta_otravz:
            If cx_MySQL_local.State = ConnectionState.Closed Then cx_MySQL_local.Open()
            da_Adaptador.Fill(dt_DataTable)
        Catch ex As Exception
            If cx_MySQL_local.State = ConnectionState.Closed Then
                ciclo = ciclo + 1
                If ciclo = 3 Then
                    AgregarLog(500, ex.Message & ", Error al leer registro local: " & Comando)
                    Exit Try
                End If
                GoTo intenta_otravz
            Else
                AgregarLog(500, ex.Message & ", Error al leer registro local: " & Comando)
            End If
        End Try

        Return dt_DataTable

    End Function

    Public Function Delete_Central(ByVal Cadena As String) As Boolean

        If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
        SyncLock bloqueoConexionCentral
            If Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Not CentralDisponible Then Return False
            If cx_MySQL_Central.State <> ConnectionState.Open Then
                NotificarDesconexionCentral()
                Return False
            End If
            Try
                Using cmm_Comando As New MySqlConnector.MySqlCommand(Cadena, cx_MySQL_Central)
                    cmm_Comando.ExecuteNonQuery()
                End Using
                Return True
            Catch ex As Exception
                RegistrarErrorCentral(ex, cx_MySQL_Central, "Delete_Central")
                Return False
            End Try
        End SyncLock
    End Function


    Public Function Update_local(ByVal NomTabla As String, ByVal ValorSET As String, ByVal CampoCond As String, ByVal ValorFiltro As String) As Boolean

        Dim Cadena As String = ""
        Cadena = "Update " & NomTabla & _
                 " SET " & ValorSET & _
                 " where " & CampoCond & _
                 " = '" & ValorFiltro & "'"

        Dim cmm_Comando As New Global.MySqlConnector.MySqlCommand(Cadena, cx_MySQL_local)
        Dim ciclo As Integer = 0
        Try
intenta_otravz:
            If cx_MySQL_local.State = ConnectionState.Closed Then cx_MySQL_local.Open()
            cmm_Comando.ExecuteNonQuery()
        Catch ex As Exception
            If cx_MySQL_local.State = ConnectionState.Closed Then
                ciclo = ciclo + 1
                If ciclo = 3 Then
                    AgregarLog(500, ex.Message & ", Error al actualizar registro local: " & Cadena)
                    Exit Try
                End If
                GoTo intenta_otravz
            Else
                AgregarLog(500, ex.Message & ", Error al actualizar registro local: " & Cadena)
                Return False
            End If
        End Try
        Return True

    End Function

    Public Function Update_local(ByVal Cadena As String) As Boolean

        Dim cmm_Comando As New Global.MySqlConnector.MySqlCommand(Cadena, cx_MySQL_local)
        Dim ciclo As Integer = 0
        Try
intenta_otravz:
            If cx_MySQL_local.State = ConnectionState.Closed Then cx_MySQL_local.Open()
            cmm_Comando.ExecuteNonQuery()
        Catch ex As Exception
            If cx_MySQL_local.State = ConnectionState.Closed Then
                ciclo = ciclo + 1
                If ciclo = 3 Then
                    AgregarLog(500, ex.Message & ", Error al actualizar registro local: " & Cadena)
                    Exit Try
                End If
                GoTo intenta_otravz
            Else
                AgregarLog(500, ex.Message & ", Error al actualizar registro local: " & Cadena)
                Return False
            End If

        End Try

        Return True

    End Function

    Public Function Update_Scripts_local(ByVal Cadena As String, ByRef error_s As String) As Boolean

        Dim cmm_Comando As New Global.MySqlConnector.MySqlCommand(Cadena, cx_MySQL_local)
        Dim ciclo As Integer = 0
        Try
intenta_otravz:
            If cx_MySQL_local.State = ConnectionState.Closed Then cx_MySQL_local.Open()
            cmm_Comando.ExecuteNonQuery()
        Catch ex As Exception
            If cx_MySQL_local.State = ConnectionState.Closed Then
                ciclo = ciclo + 1
                If ciclo = 3 Then
                    AgregarLog(500, ex.Message)
                    error_s = ex.Message
                    Exit Try
                End If
                GoTo intenta_otravz
            Else
                AgregarLog(500, ex.Message)
                error_s = ex.Message
                Return False
            End If

        End Try

        Return True

    End Function

    Public Function Test_MySQL_local(ByVal str_ConStr As String) As Boolean

        Dim ciclo As Integer = 0
        Try

intenta_otravz:

            If cx_MySQL_local.State = ConnectionState.Open Then Return True
            cx_MySQL_local.ConnectionString = str_ConStr
            cx_MySQL_local.Open()
            Return True
        Catch ex As Exception

            ciclo = ciclo + 1
            If ciclo = 3 Then
                Return False
            End If
            GoTo intenta_otravz
            Return False
        End Try

    End Function

    ' Local Async ****************************************


    Public Property cx_MySQL_localAsync() As MySqlConnector.MySqlConnection
        Get
            Return cnn_MySQL_LocalAsync
        End Get
        Set(ByVal Value As MySqlConnector.MySqlConnection)
            cnn_MySQL_LocalAsync = Value
        End Set

    End Property

    Public Function tb_Recordset_MySQL_localAsync(ByVal Comando As String) As Data.DataTable

        Dim da_Adaptador As New MySqlConnector.MySqlDataAdapter
        Dim cmm_Comando As New MySqlConnector.MySqlCommand(Comando, cx_MySQL_localAsync)
        Dim dt_DataTable As New System.Data.DataTable
        da_Adaptador.SelectCommand = cmm_Comando

        Dim ciclo As Integer = 0
        Try
intenta_otravz:
            If cx_MySQL_localAsync.State = ConnectionState.Closed Then cx_MySQL_localAsync.Open()
            da_Adaptador.Fill(dt_DataTable)

        Catch ex As Exception
            If cx_MySQL_localAsync.State = ConnectionState.Closed Then
                ciclo = ciclo + 1
                If ciclo = 3 Then
                    AgregarLog(500, ex.Message & ", Error al leer registro local Async: " & Comando)
                    Exit Try
                End If
                GoTo intenta_otravz
            Else
                AgregarLog(500, ex.Message & ", Error al leer registro local Async: " & Comando)
            End If
        End Try

        Return dt_DataTable

    End Function

    Public Function Delete_localAsync(ByVal Cadena As String) As Boolean

        Dim cmm_Comando As New MySqlConnector.MySqlCommand(Cadena, cx_MySQL_localAsync)
        Dim ciclo As Integer = 0
        Try
intenta_otravz:
            If cx_MySQL_localAsync.State = ConnectionState.Closed Then cx_MySQL_localAsync.Open()

            cmm_Comando.ExecuteNonQuery()
        Catch ex As Exception
            If cx_MySQL_localAsync.State = ConnectionState.Closed Then
                ciclo = ciclo + 1
                If ciclo = 3 Then
                    AgregarLog(500, ex.Message & ". Error al eliminar registro: " & Cadena)
                    Exit Try
                End If
                GoTo intenta_otravz
            Else
                AgregarLog(500, ex.Message & ". Error al eliminar registro: " & Cadena)
                Return False
            End If
        End Try

        Return True

    End Function

    Public Function Update_localAsync(ByVal NomTabla As String, ByVal ValorSET As String, ByVal CampoCond As String, ByVal ValorFiltro As String) As Boolean

        Dim Cadena As String = ""
        Cadena = "Update " & NomTabla & _
                 " SET " & ValorSET & _
                 " where " & CampoCond & _
                 " = '" & ValorFiltro & "'"

        Dim cmm_Comando As New Global.MySqlConnector.MySqlCommand(Cadena, cx_MySQL_localAsync)
        Dim ciclo As Integer = 0
        Try
intenta_otravz:
            If cx_MySQL_localAsync.State = ConnectionState.Closed Then cx_MySQL_localAsync.Open()
            cmm_Comando.ExecuteNonQuery()
        Catch ex As Exception
            If cx_MySQL_localAsync.State = ConnectionState.Closed Then
                ciclo = ciclo + 1
                If ciclo = 3 Then
                    AgregarLog(500, ex.Message & ", Error al actualizar registro local Async: " & Cadena)
                    Exit Try
                End If
                GoTo intenta_otravz
            Else
                AgregarLog(500, ex.Message & ", Error al actualizar registro local Async: " & Cadena)
                Return False
            End If
        End Try
        Return True

    End Function

    Public Function Update_localAsync(ByVal Cadena As String) As Boolean

        Dim cmm_Comando As New Global.MySqlConnector.MySqlCommand(Cadena, cx_MySQL_localAsync)
        Dim ciclo As Integer = 0
        Try
intenta_otravz:
            If cx_MySQL_localAsync.State = ConnectionState.Closed Then cx_MySQL_localAsync.Open()
            cmm_Comando.ExecuteNonQuery()
        Catch ex As Exception
            If cx_MySQL_localAsync.State = ConnectionState.Closed Then
                ciclo = ciclo + 1
                If ciclo = 3 Then
                    AgregarLog(500, ex.Message & ", Error al actualizar registro local Async: " & Cadena)
                    Exit Try
                End If
                GoTo intenta_otravz
            Else
                AgregarLog(500, ex.Message & ", Error al actualizar registro local Async: " & Cadena)
                Return False
            End If

        End Try

        Return True

    End Function


    Public Function Test_MySQL_localAsync(ByVal str_ConStr As String) As Boolean

        Dim ciclo As Integer = 0
        Try

intenta_otravz:

            If cx_MySQL_localAsync.State = ConnectionState.Open Then Return True
            cx_MySQL_localAsync.ConnectionString = str_ConStr
            cx_MySQL_localAsync.Open()
            Return True
        Catch ex As Exception

            ciclo = ciclo + 1
            If ciclo = 3 Then
                Return False
            End If
            GoTo intenta_otravz
            Return False
        End Try

    End Function


    ' Local Async Almacen ****************************************


    Public Property cx_MySQL_localAsyncALM() As MySqlConnector.MySqlConnection
        Get
            Return cnn_MySQL_LocalAsyncALM
        End Get
        Set(ByVal Value As MySqlConnector.MySqlConnection)
            cnn_MySQL_LocalAsyncALM = Value
        End Set

    End Property

    Public Function tb_Recordset_MySQL_localAsyncALM(ByVal Comando As String) As Data.DataTable

        Dim da_Adaptador As New MySqlConnector.MySqlDataAdapter
        Dim cmm_Comando As New MySqlConnector.MySqlCommand(Comando, cx_MySQL_localAsyncALM)
        Dim dt_DataTable As New System.Data.DataTable
        da_Adaptador.SelectCommand = cmm_Comando
        Dim ciclo As Integer = 0
        Try
intenta_otravz:
            If cx_MySQL_localAsyncALM.State = ConnectionState.Closed Then cx_MySQL_localAsyncALM.Open()
            da_Adaptador.Fill(dt_DataTable)
        Catch ex As Exception
            If cx_MySQL_localAsyncALM.State = ConnectionState.Closed Then
                ciclo = ciclo + 1
                If ciclo = 3 Then
                    AgregarLog(500, ex.Message & ", Error al leer registro local AsyncALM: " & Comando)
                    Exit Try
                End If
                GoTo intenta_otravz
            Else
                AgregarLog(500, ex.Message & ", Error al leer registro local AsyncALM: " & Comando)
            End If
        End Try

        Return dt_DataTable

    End Function


    Public Function Update_localAsyncALM(ByVal NomTabla As String, ByVal ValorSET As String, ByVal CampoCond As String, ByVal ValorFiltro As String) As Boolean

        Dim Cadena As String = ""
        Cadena = "Update " & NomTabla & _
                 " SET " & ValorSET & _
                 " where " & CampoCond & _
                 " = '" & ValorFiltro & "'"

        Dim cmm_Comando As New MySqlConnector.MySqlCommand(Cadena, cx_MySQL_localAsyncALM)
        Dim ciclo As Integer = 0
        Try
intenta_otravz:
            If cx_MySQL_localAsyncALM.State = ConnectionState.Closed Then cx_MySQL_localAsyncALM.Open()
            cmm_Comando.ExecuteNonQuery()
        Catch ex As Exception
            If cx_MySQL_localAsyncALM.State = ConnectionState.Closed Then
                ciclo = ciclo + 1
                If ciclo = 3 Then
                    AgregarLog(500, ex.Message & ", Error al actualizar registro local AsyncALM: " & Cadena)
                    Exit Try
                End If
                GoTo intenta_otravz
            Else
                AgregarLog(500, ex.Message & ", Error al actualizar registro local AsyncALM: " & Cadena)
                Return False
            End If
        End Try
        Return True

    End Function

    Public Function Update_localAsyncALM(ByVal Cadena As String) As Boolean

        Dim cmm_Comando As New Global.MySqlConnector.MySqlCommand(Cadena, cx_MySQL_localAsyncALM)
        Dim ciclo As Integer = 0
        Try
intenta_otravz:
            If cx_MySQL_localAsyncALM.State = ConnectionState.Closed Then cx_MySQL_localAsyncALM.Open()
            cmm_Comando.ExecuteNonQuery()
        Catch ex As Exception
            If cx_MySQL_localAsyncALM.State = ConnectionState.Closed Then
                ciclo = ciclo + 1
                If ciclo = 3 Then
                    AgregarLog(500, ex.Message & ", Error al actualizar registro local AsyncALM: " & Cadena)
                    Exit Try
                End If
                GoTo intenta_otravz
            Else
                AgregarLog(500, ex.Message & ", Error al actualizar registro local AsyncALM: " & Cadena)
                Return False
            End If

        End Try

        Return True

    End Function


    Public Function Test_MySQL_localAsyncALM(ByVal str_ConStr As String) As Boolean

        Dim ciclo As Integer = 0
        Try

intenta_otravz:

            If cx_MySQL_localAsyncALM.State = ConnectionState.Open Then Return True
            cx_MySQL_localAsyncALM.ConnectionString = str_ConStr
            cx_MySQL_localAsyncALM.Open()
            Return True
        Catch ex As Exception

            ciclo = ciclo + 1
            If ciclo = 3 Then
                Return False
            End If
            GoTo intenta_otravz
            Return False
        End Try

    End Function


#End Region

#Region "Form"

    Private Sub frmInterface_Resize(sender As Object, e As EventArgs) Handles Me.Resize

        If Me.WindowState = FormWindowState.Minimized Then
            Me.ShowInTaskbar = False
            HistoMedicRPA.ShowBalloonTip(2000, "HistoMedic activo", "La aplicación sigue ejecutándose", ToolTipIcon.Info)
        End If

    End Sub

    Private Sub frmMonitor_FormClosing(ByVal sender As Object, ByVal e As System.Windows.Forms.FormClosingEventArgs) Handles Me.FormClosing

        Me.WindowState = FormWindowState.Minimized

        e.Cancel = True

    End Sub

    Private Sub frmMomitor_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load

        TimerReconexionCentral.AutoReset = False

        Me.Get_IpServidor()

        If Me.Conectar_Local() Then

            'SELECT ftp_usuario, ftp_password FROM cat_consultorio t;
            Dim tb_cat_consultorio As DataTable
            tb_cat_consultorio = tb_Recordset_MySQL_local("Select cClues, ftp_usuario, ftp_password, cSiglas from cat_consultorio")
            Me.CLUES = tb_cat_consultorio.Rows(0).Item("cClues").ToString
            Me.cSiglas = tb_cat_consultorio.Rows(0).Item("cSiglas").ToString

            Me.FTP_IP = "ftp://" & IpServidor & "/"
            Me.FTP_USUARIO = tb_cat_consultorio.Rows(0).Item("ftp_usuario").ToString
            Me.FTP_PASSWORD = tb_cat_consultorio.Rows(0).Item("ftp_password").ToString

            Interlocked.Exchange(robotActivo, 1)
            If Me.Conectar_Central(Me.CLUES) Then
                Me.Text = Me.Text & " " & Application.ProductVersion
                chkActivar.Checked = True

                'Proceso en segundo plano
                IniciarProcesosSP()
                Interlocked.Exchange(proximaComprobacionCentralTicks, DateTime.UtcNow.AddSeconds(30).Ticks)
            Else
                Me.Text = Me.Text & " " & Application.ProductVersion
                chkActivar.Checked = True
                Me.NotificarDesconexionCentral("No fue posible conectar al servidor central al inicio.")
            End If

        Else

            Application.ExitThread()

        End If

        load_init = False
        If Interlocked.CompareExchange(robotActivo, 0, 0) = 1 Then TimerReconexionCentral.Start()

    End Sub

    Private Sub HistoclinMonitor_MouseDoubleClick(ByVal sender As System.Object, ByVal e As System.Windows.Forms.MouseEventArgs) Handles HistoMedicRPA.MouseDoubleClick

        Me.AbrirApp()

    End Sub

    Private Sub AbrirApp()

        Me.Show()
        Me.WindowState = FormWindowState.Normal
        Me.ShowInTaskbar = True
        Me.BringToFront()
        Me.Activate()
        Me.Refresh()

    End Sub

    Private Sub MenuAbrir_Click(sender As Object, e As EventArgs) Handles MenuAbrir.Click

        AbrirApp()

        'Me.HistoMedicRPA.Visible = False
        'Me.Bounds = Me.RestoreBounds
        'Me.Show()
        'Me.WindowState = FormWindowState.Normal

        ''Me.Show()
        ''Me.WindowState = FormWindowState.Normal

    End Sub

    Private Sub MenuCerrar_Click(sender As Object, e As EventArgs) Handles MenuCerrar.Click

        Interlocked.Exchange(cerrandoRobot, 1)
        Interlocked.Exchange(robotActivo, 0)
        Me.TimerReconexionCentral.Stop()
        Me.TimerEnlace.Enabled = False
        Me.DetenerProcesoSP()
        Application.ExitThread()

    End Sub

    Private Sub TimerSTP_Tick(sender As Object, e As EventArgs) Handles TimerEnlace.Tick

        Me.IniciarRutina()

    End Sub

    Private Sub chkActivar_CheckedChanged(sender As Object, e As EventArgs) Handles chkActivar.CheckedChanged

        If Me.chkActivar.Checked Then
            Interlocked.Exchange(robotActivo, 1)
            Me.TimerEnlace.Enabled = True
            If Not load_init Then
                Interlocked.Exchange(proximaComprobacionCentralTicks, DateTime.UtcNow.Ticks)
                Me.ReiniciarProcesoSP()
                Me.TimerReconexionCentral.Start()
            End If

        Else
            Interlocked.Exchange(robotActivo, 0)
            Me.TimerReconexionCentral.Stop()
            Me.TimerEnlace.Enabled = False
            Me.DetenerProcesoSP()
        End If

    End Sub

    Private Sub TimerReconexionCentral_Elapsed(sender As Object, e As System.Timers.ElapsedEventArgs) Handles TimerReconexionCentral.Elapsed
        Try
            If load_init OrElse Interlocked.CompareExchange(robotActivo, 0, 0) = 0 OrElse Interlocked.CompareExchange(cerrandoRobot, 0, 0) <> 0 Then Return
            If CentralDisponible Then ReiniciarProcesoSP()
            If DateTime.UtcNow.Ticks < Interlocked.Read(proximaComprobacionCentralTicks) Then Return
            If Interlocked.CompareExchange(comprobacionCentralActiva, 1, 0) <> 0 Then Return
            RevisarConexionCentral()
        Catch ex As Exception
            LogEventos.Escribir("[Central] No se pudo comprobar la conexión: " & ex.GetType().Name)
        Finally
            If Not load_init AndAlso Interlocked.CompareExchange(robotActivo, 0, 0) = 1 AndAlso Interlocked.CompareExchange(cerrandoRobot, 0, 0) = 0 Then TimerReconexionCentral.Start()
        End Try
    End Sub

#End Region

#Region "Registro Pacientes"

    Private Function AsignarNumeroExpediente() As String

        Dim NumeroExp As String
        Dim tb_exp As DataTable
        tb_exp = tb_Recordset_MySQL_local("Select iNumExpediente from tb_paciente where " & _
                                                 "ccvepaciente <> '' and iNumExpediente like '" & cSiglas & "%' order by iNumExpediente desc limit 2")
        If tb_exp.Rows.Count = 0 Then
            NumeroExp = cSiglas & "-000001"
            Return NumeroExp
        End If

        Dim txtTempNumExp As New TextBox
        txtTempNumExp.Text = tb_exp.Rows(0).Item(0).ToString
        Dim Inicio As Integer = Len(cSiglas) + 1
        Dim Longitud As Integer = Len(txtTempNumExp.Text) - Inicio
        txtTempNumExp.Select(Inicio, Longitud)

        Dim Consecutivo As Integer
        Consecutivo = CInt(txtTempNumExp.SelectedText)
        Consecutivo = Consecutivo + 1

        NumeroExp = cSiglas & "-" & Format(Consecutivo, "000000")

        Return NumeroExp

    End Function

    Private Function AsignarClavePaciente(tbcx_central As DataTable, row_i As Integer) As String

        Dim Paterno As String
        Dim Materno As String
        Dim Nombre1 As String
        Dim Nombre2 As String
        Dim FechaNacimiento As String

        Dim txtPaterno As New TextBox
        Dim txtMaterno As New TextBox
        Dim txtNombre As New TextBox
        Dim txtNacimiento As New TextBox

        Dim tb_temp_px As DataTable


        txtPaterno.Text = tbcx_central.Rows(row_i).Item("paterno").ToString
        txtMaterno.Text = tbcx_central.Rows(row_i).Item("materno").ToString
        txtNombre.Text = tbcx_central.Rows(row_i).Item("nombre").ToString
        txtNacimiento.Text = tbcx_central.Rows(row_i).Item("fecha_nacimiento").ToString

        txtPaterno.Select(0, 2)
        Paterno = txtPaterno.SelectedText

        txtMaterno.Select(0, 1)
        Materno = txtMaterno.SelectedText

        Dim i As Integer

        txtNombre.Select(0, 1)
        Nombre1 = txtNombre.SelectedText

        Try
            For i = 1 To Len(txtNombre.Text)
                txtNombre.Select(i, 1)
                If txtNombre.SelectedText = " " Then
                    txtNombre.Select(i + 1, 1)
                    Nombre2 = txtNombre.SelectedText
                    Exit For
                End If
            Next
        Catch ex As Exception
        End Try

        FechaNacimiento = Format(CDate(txtNacimiento.Text), "yyMMdd")
        Dim cvepaciente As String
        If Nombre2 = Nothing Then
            cvepaciente = Paterno & Materno & Nombre1 & FechaNacimiento

        Else
            cvepaciente = Paterno & Materno & Nombre1 & Nombre2 & FechaNacimiento

        End If
        tb_temp_px = tb_Recordset_MySQL_local("Select ccvepaciente from tb_paciente where " & _
                                                             "ccvepaciente = '" & cvepaciente & "'")
        If tb_temp_px.Rows.Count <> 0 Then
            Try
                For i = 2 To Len(txtNombre.Text)
                    txtNombre.Select(0, i)
                    Nombre1 = txtNombre.SelectedText
                    cvepaciente = Paterno & Materno & Nombre1 & FechaNacimiento
                    tb_temp_px = tb_Recordset_MySQL_local("Select ccvepaciente from tb_paciente where " & _
                                                          "ccvepaciente = '" & cvepaciente & "'")
                    If tb_temp_px.Rows.Count = 0 Then Exit For
                Next
            Catch ex As Exception
            End Try
        End If

        If Nombre2 = Nothing Then
            cvepaciente = Paterno & Materno & Nombre1 & FechaNacimiento
            Return cvepaciente
        Else
            cvepaciente = Paterno & Materno & Nombre1 & Nombre2 & FechaNacimiento
            Return cvepaciente
        End If

    End Function

    Private Function setPaciente(ByVal tbcx_central As DataTable, ByVal row_i As Integer, ByRef paciente_id As Integer) As Boolean


        Try

            Dim CvePaciente As String = ""
            Dim NumExp As String

            Dim tb_paciente As DataTable
            Dim paciente_id_local As Integer = 0
            paciente_id_local = tbcx_central.Rows(row_i).Item("paciente_id_local").ToString
            If paciente_id_local > 0 Then
                tb_paciente = tb_Recordset_MySQL_local("Select ccvepaciente, iNumExpediente from tb_paciente WHERE " & _
                                                        "icvepaciente = '" & paciente_id_local & "'")
                If tb_paciente.Rows.Count > 0 Then
                    CvePaciente = tb_paciente.Rows(0).Item("ccvepaciente").ToString
                    NumExp = tb_paciente.Rows(0).Item("iNumExpediente").ToString
                End If
            End If


            If CvePaciente = "" Then
                CvePaciente = AsignarClavePaciente(tbcx_central, row_i)
                NumExp = Me.AsignarNumeroExpediente
            End If


            ' DROP TABLE IF EXISTS `mirtheda_kayeli`.`ssf_cirugiascontrol`;
            'CREATE TABLE  `mirtheda_kayeli`.`ssf_cirugiascontrol` (
            '  `Id` int(10) unsigned NOT NULL AUTO_INCREMENT,
            '  `clues` varchar(255) DEFAULT NULL,
            '  `ccveusuario` varchar(95) DEFAULT NULL,
            '  `fchregistro` date DEFAULT NULL,
            '  `usuario_id` int(10) unsigned DEFAULT '0',
            '  `quirofano_id` int(10) unsigned DEFAULT '0',
            '  `fecha_cirugia` date DEFAULT NULL,
            '  `fecha_cirugia_hora_ini` time DEFAULT NULL,
            '  `fecha_cirugia_hora_fin` time DEFAULT '00:00:00',
            '  `duracion_cirugia` double DEFAULT '0',
            '  `cirugia` varchar(255) DEFAULT NULL,
            '  `paciente_id` varchar(45) DEFAULT NULL COMMENT 'ccvepaciente',
            '  `paciente` varchar(255) DEFAULT NULL,
            '  `nombre` varchar(95) DEFAULT NULL,
            '  `paterno` varchar(95) DEFAULT NULL,
            '  `materno` varchar(95) DEFAULT NULL,
            '  `fecha_nacimiento` date DEFAULT NULL,
            '  `doctor_id` varchar(45) DEFAULT NULL COMMENT 'ccvemedico',
            '  `sexo_id` int(10) unsigned DEFAULT '0',
            '  `sinc` int(10) unsigned DEFAULT NULL COMMENT '0 = en proceso de descarga\r\n1 = descargdo por el servidor',

            If paciente_id_local > 0 Then
                GoTo no_insertar
            End If

            Dim txtPaterno As New TextBox
            Dim txtMaterno As New TextBox
            Dim txtNombre As New TextBox
            Dim txtNacimiento As New TextBox
            txtPaterno.Text = tbcx_central.Rows(row_i).Item("paterno").ToString
            txtMaterno.Text = tbcx_central.Rows(row_i).Item("materno").ToString
            txtNombre.Text = tbcx_central.Rows(row_i).Item("nombre").ToString
            txtNacimiento.Text = tbcx_central.Rows(row_i).Item("fecha_nacimiento").ToString

            Dim sexo As String = ""
            If tbcx_central.Rows(row_i).Item("sexo_id").ToString = 2 Then
                sexo = "FEMENINO"
            Else
                sexo = "MASCULINO"
            End If

            Dim Edad As Integer
            Try
                Edad = Funciones.Calcula_EdadAbs_Int(CDate(txtNacimiento.Text))
            Catch ex As Exception
                Edad = 0
            End Try

            Dim Edad_str As String
            Try
                Edad_str = Funciones.Calcula_EdadActual_Str(CDate(txtNacimiento.Text))
            Catch ex As Exception
                Edad_str = ""
            End Try

            Dim cClasificacion As String = "L01"
            Dim tb_lista_precios As DataTable
            tb_lista_precios = tb_Recordset_MySQL_local("SELECT cClasificacion FROM cat_factorsocial2 where icvefactorsocial = 1")
            If tb_lista_precios.Rows.Count > 0 Then
                cClasificacion = tb_lista_precios.Rows(0).Item("cClasificacion").ToString
            End If

            Dim tb_temp_px As DataTable

            If Insert_local("tb_paciente", _
                            "clues = '" & Me.CLUES & "', " & _
                            "cempleo='', " & _
                            "nombre_completo='" & Trim(txtNombre.Text) & " " & Trim(txtPaterno.Text) & " " & Trim(txtMaterno.Text) & "', " & _
                            "cnombre='" & Trim(txtNombre.Text) & "', " & _
                            "cpriapellido='" & Trim(txtPaterno.Text) & "', " & _
                            "csegapellido='" & Trim(txtMaterno.Text) & "', " & _
                            "boolsexo='" & sexo & "', " & _
                            "fchnac='" & Format(CDate(txtNacimiento.Text), "yyyy-MM-dd") & "', " & _
                            "iEdad='" & Edad & "', " & _
                            "booltipo='PARTICULAR', " & _
                            "icvetipopaciente='1', " & _
                            "ccvemedico='histomedic_portal', " & _
                            "cCURP='', " & _
                            "cDomicilio='', " & _
                            "cEstadoCivil='', " & _
                            "cTipoSangre='', " & _
                            "cEscolaridad='', " & _
                            "cdscgrupoetnico='Ninguno', " & _
                            "cdscreligion='', " & _
                            "iCapacidadesDiferentes='0', " & _
                            "cNumExterior='', " & _
                            "cNumInterior='', " & _
                            "fchregistro = current_timestamp, " & _
                            "iNumExpediente='" & NumExp & "', " & _
                            "cEdad='" & Edad_str & "', " & _
                            "ccvepaciente='" & CvePaciente & "', " & _
                            "cLugarNacimiento='', " & _
                            "cNacionalidad='MEXICO', " & _
                            "cOcupPadre='', " & _
                            "cNomPadre='', " & _
                            "cOcupMadre='', " & _
                            "cNomMadre = '', " & _
                            "cConyuge = '', " & _
                            "cClasificacion = '" & cClasificacion & "'") Then

no_insertar:

                'ART-000001
                Dim largo As Integer = 0
                largo = Len(Me.cSiglas) + 2

                Dim largo2 As Integer = 0
                largo2 = Len(Me.cSiglas) + 1

                Update_local("UPDATE tb_paciente SET " & _
                                        "cFolio = (SELECT SUBSTRING(tb_paciente.iNumExpediente," & largo & ",10)), " & _
                                        "cNumEmpleado = (SELECT SUBSTRING(tb_paciente.iNumExpediente,1," & largo2 & ")) " & _
                                        "WHERE " & _
                                        "tb_paciente.ccvepaciente = '" & CvePaciente & "';")

                If paciente_id_local = 0 Then
                    tb_temp_px = tb_Recordset_MySQL_local("Select icvepaciente from tb_paciente " & _
                                                   "WHERE ccvepaciente = '" & CvePaciente & "'")
                    paciente_id = tb_temp_px.Rows(0).Item("icvepaciente").ToString

                    '== actualiza el id de paciente para no volverlo a regsitrar ===  ===  ===  ===  === 
                    Update_Central("ssf_cirugiascontrol", _
                                   "paciente_id_local = '" & paciente_id & "'", _
                                   "Id", tbcx_central.Rows(row_i).Item("Id").ToString)
                    '  ===  ===  ===  ===  ===  ===  ===  ===  ===  ===  ===  ===  ===  ===  ===  === 

                Else
                    paciente_id = paciente_id_local
                End If

                Dim tb_paciente2 As DataTable
                tb_paciente2 = tb_Recordset_MySQL_local("SELECT ccvepaciente FROM tb_paciente2 WHERE ccvepaciente = '" & CvePaciente & "' ")
                If tb_paciente2.Rows.Count = 0 Then
                    If Not Insert_local("tb_paciente2", _
                                        "clues = '" & Me.CLUES & "', " & _
                                        "ccvepaciente='" & CvePaciente & "', " & _
                                        "email='', " & _
                                        "cTelFax='', " & _
                                        "cTelFax2='', " & _
                                        "cEntidad='', " & _
                                        "cMunicipio='', " & _
                                        "cLocalidad='', " & _
                                        "cColonia='', " & _
                                        "tipo_asentamiento='COLONIA', " & _
                                        "tipo_vialidad='CALLE', " & _
                                        "cInstitucion='PUBLICO GENERAL', " & _
                                        "cSegPopClasif='PUBLICO GENERAL', " & _
                                        "clasificacion_social_id='1', " & _
                                        "cPoliza='', " & _
                                        "cCodigoPostal='', " & _
                                        "cObservaciones='REGISTRO ORIGINAL DESDE PORTAL HISTOMEDIC', " & _
                                        "cEdadAparente='', " & _
                                        "cSenasParticulares='', " & _
                                        "cMatriculaDesconocido='', " & _
                                        "cLengua='NINGUNA', " & _
                                        "cNomTutor='', " & _
                                        "cParentescoTutor='', " & _
                                        "MotivoNoRegistraCURP = '', " & _
                                        "fchPolizaIni=null, " & _
                                        "fchPolizaFin=null, " & _
                                        "iCoaseguro='0', " & _
                                        "iDeducible='0', " & _
                                        "fchRegistro=current_timestamp") Then
                        Return False
                        'Me.HisConectores.UploadFile_FTP(Me.CvePaciente, Descripciones.PacienteNumExpediente(Me.CvePaciente), _
                        '                                        Funciones.Calcula_FechaActual, Me.pbPaciente.Image, _
                        '                                        HistoMedicConectores.CARPETAS_FTP.TB_PACIENTE, "", "", 1, HisPropiedades.FTP_IP, HisPropiedades.FTP_USUARIO, HisPropiedades.FTP_PASSWORD)
                    End If
                End If


                'Me.InsertarHistoricoIdentificacion(Me.CvePaciente)

                Return True

            Else

                Return False

            End If
        Catch ex As Exception
            Return False
        End Try


    End Function

#End Region

#Region "Paquetes"

    ''' <summary>
    ''' Valida Precio de Venta y Envia correo de alerta de precios si el precio es mayor al costo.
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub notificarAlertaProductoCantidadFueraPaquete()

        Try

            Dim CvePaciente As String = ""
            Dim CveMaterial As String = ""
            Dim Cantidad_Unitario As Double = 0
            Dim CveUnidad_Unitario As String = ""
            Dim Descripcion As String = ""
            Dim paquete_id As Integer = 0
            Dim clues As String = ""

            Dim tb_paquetes_notificaciones As DataTable
            tb_paquetes_notificaciones = tb_Recordset_MySQL_local("Select * from tb_paquetes_notificaciones " & _
                                                                    "WHERE " & _
                                                                    "enviado = 0 LIMIT 10")

            If tb_paquetes_notificaciones.Rows.Count = 0 Then
                Exit Sub
            End If


            For i As Integer = 0 To tb_paquetes_notificaciones.Rows.Count - 1

                clues = tb_paquetes_notificaciones.Rows(i).Item("clues").ToString
                paquete_id = tb_paquetes_notificaciones.Rows(i).Item("paquete_id").ToString
                CvePaciente = tb_paquetes_notificaciones.Rows(i).Item("ccvepaciente").ToString
                CveMaterial = tb_paquetes_notificaciones.Rows(i).Item("ccvematerial").ToString
                Cantidad_Unitario = tb_paquetes_notificaciones.Rows(i).Item("cantidad_unitario").ToString
                CveUnidad_Unitario = tb_paquetes_notificaciones.Rows(i).Item("ccveunidad_unitario").ToString
                Descripcion = tb_paquetes_notificaciones.Rows(i).Item("descripcion").ToString

                'Unidad Médica : Data_Sucursal()
                'Datos de Paciente:	Data_Paciente
                'Datos de Paquete:	Data_Paquete
                'Datos Producto o Cant. Agregado(a) Fuera Pqt.:	Data_Producto
                'ALTER TABLE `histoclin`.`cat_consultorio` ADD COLUMN `email_alerta_paquetes` VARCHAR(95) AFTER `MensajeCemaAlertaPaquete`;

                Dim cat_consultorio As DataTable
                cat_consultorio = tb_Recordset_MySQL_local("Select MensajeCemaAlertaPaquete, cNombreAbreviado, email_alerta_paquetes from cat_consultorio where cCLUES = '" & clues & "'")

                Dim Mensaje As String = cat_consultorio.Rows(0).Item("MensajeCemaAlertaPaquete").ToString
                Mensaje = Mensaje.Replace("Encabezado_1", cat_consultorio.Rows(0).Item("cNombreAbreviado").ToString)
                'Mensaje = Mensaje.Replace("Encabezado_2", cat_consultorio.Rows(0).Item("cEncabezadoSria").ToString)
                Mensaje = Mensaje.Replace("Data_Sucursal", cat_consultorio.Rows(0).Item("cNombreAbreviado").ToString)
                Mensaje = Mensaje.Replace("Data_Paciente", getPacienteNombreCompletoFromClave(CvePaciente))

                Dim paquete As DataTable
                paquete = Me.getPaquete(paquete_id)

                Dim paquete_str As String = ""
                paquete_str = "Folio: " & paquete.Rows(0).Item("folio").ToString & ", Paquete: " & paquete.Rows(0).Item("paquete").ToString
                Mensaje = Mensaje.Replace("Data_Paquete", paquete_str)

                Dim producto_str As String = ""
                producto_str = "Clave: " & CveMaterial & ",  Cantidad Agregada: " & Cantidad_Unitario & ",  Unidad: " & Me.cdscUnidadMedida(CveUnidad_Unitario) & ",  Producto/Servicio: " & Descripcion

                Mensaje = Mensaje.Replace("Data_Producto", producto_str)

                If cat_consultorio.Rows(0).Item("email_alerta_paquetes").ToString <> "" Then
                    If EnviarCorreos.EnviarNotificacionGeneral("ALERTA PRODUCTO O CANTIDAD FUERA DE PAQUETE", _
                                                           Trim(cat_consultorio.Rows(0).Item("email_alerta_paquetes").ToString), _
                                                          "Reporte de Producto o Cantidad Fuera de Paquete", _
                                                           Mensaje) Then
                        Update_local("tb_paquetes_notificaciones", _
                                     "enviado = 1", _
                                     "id", tb_paquetes_notificaciones.Rows(i).Item("id").ToString)

                    End If
                Else
                    Update_local("tb_paquetes_notificaciones", _
                                 "enviado = 1", _
                                 "id", tb_paquetes_notificaciones.Rows(i).Item("id").ToString)
                End If

                If paquete.Rows(0).Item("correo_notifica").ToString <> "" Then
                    If EnviarCorreos.EnviarNotificacionGeneral("ALERTA PRODUCTO O CANTIDAD FUERA DE PAQUETE", _
                                                           Trim(paquete.Rows(0).Item("correo_notifica").ToString), _
                                                          "Reporte de Producto o Cantidad Fuera de Paquete", _
                                                           Mensaje) Then

                    End If
                End If

            Next

        Catch ex As Exception
            AgregarLog(500, ex.Message & ", Error desconocdio en notificarAlertaProductoCantidadFueraPaquete.")
        End Try

    End Sub

    Private Function getPaquete(ByVal paquete_id As Integer) As DataTable

        Dim tb_temp As DataTable
        Try
            tb_temp = tb_Recordset_MySQL_local("SELECT p.*, cat_convenios.cdscconvenio as convenio, cat_convenios.cTelefono as telefono_notifica, cat_convenios.email as correo_notifica FROM " & _
                                                       "tb_paquete p " & _
                                                       "LEFT JOIN cat_convenios ON (cat_convenios.icveconvenio = p.convenio_id) " & _
                                                       "where p.id = '" & paquete_id & "'")

            Return tb_temp
        Catch ex As Exception
            Return tb_temp
        End Try

        Return tb_temp

    End Function

    Private Function cdscUnidadMedida(ByVal ccveunidad As String) As String

        Dim tb_temp As DataTable
        tb_temp = tb_Recordset_MySQL_local("Select cdscUnidadMedida from cat_unidades where " & _
                                           "ccveunidad = '" & ccveunidad & "'")
        Try
            Return tb_temp.Rows(0).Item(0).ToString
        Catch ex As Exception
            Return "pza"
        End Try

        Return "pza"

    End Function

#End Region

#Region "Cargar Camas"

    Private cargosCamaActivos As Integer = 0
    Private ReadOnly relojCargosCama As New System.Diagnostics.Stopwatch()
    Private siguienteRevisionCamas As Long = 300000L

    ''' <summary>
    ''' Programa los cargos cada cinco minutos sin cambiar el Timer compartido.
    ''' Usa una copia de la conexión local del Robot para no compartir conexiones activas entre hilos.
    ''' </summary>
    Private Sub Cargar_Costo_Dias_Cama_Hosp()
        If Not relojCargosCama.IsRunning Then
            relojCargosCama.Start()
            Return
        End If
        If relojCargosCama.ElapsedMilliseconds < siguienteRevisionCamas Then Return
        If Interlocked.CompareExchange(cargosCamaActivos, 1, 0) <> 0 Then Return
        siguienteRevisionCamas = relojCargosCama.ElapsedMilliseconds + 300000L
        Dim conexionCamas As MySqlConnector.MySqlConnection = Nothing
        Try
            Dim configuracion As String = Funciones.getValorConfiguracion("ACTIVAR_REGISTRO_AUTOMATICO_CAMAS")
            If Not String.Equals(configuracion, "1", StringComparison.Ordinal) Then
                RegistrarLogCamas("Camas: configuración deshabilitada o inválida; ejecución omitida.")
                Interlocked.Exchange(cargosCamaActivos, 0)
                Return
            End If
            ' Reutilizar la configuración actual sin nuevos servidores, credenciales ni referencias externas.
            conexionCamas = DirectCast(Me.cx_MySQL_local.Clone(), MySqlConnector.MySqlConnection)
            Dim servicio As New HisCargosHospitalizacionAutomaticos(conexionCamas, Me.CLUES,
                AddressOf BuscarCuentaCamas, AddressOf ObtenerIngresoCamas, AddressOf RegistrarLogCamas,
                AddressOf RecalcularTotalesCamas)
            Dim conexionTrabajo As MySqlConnector.MySqlConnection = conexionCamas
            Task.Run(Sub()
                         Try
                             servicio.Ejecutar()
                         Catch ex As Exception
                             RegistrarLogCamas("Camas: ejecución omitida (" & ex.GetType().Name &
                                               "). Revisar tablas auxiliares, índices y conexión en desarrollo.")
                         Finally
                             Try
                                 conexionTrabajo.Dispose()
                             Catch ex As Exception
                                 RegistrarLogCamas("Camas: error al liberar la conexión local (" & ex.GetType().Name & ").")
                             Finally
                                 Interlocked.Exchange(cargosCamaActivos, 0)
                             End Try
                         End Try
                     End Sub)
        Catch ex As Exception
            Try
                If conexionCamas IsNot Nothing Then conexionCamas.Dispose()
            Catch exLiberacion As Exception
                System.Diagnostics.Trace.WriteLine("Camas: no se pudo liberar la conexión local (" & exLiberacion.GetType().Name & ").")
            Finally
                Interlocked.Exchange(cargosCamaActivos, 0)
            End Try
            RegistrarLogCamas("Camas: no se pudo iniciar la ejecución (" & ex.GetType().Name & ").")
        End Try
    End Sub

    ''' <summary>Mantiene NumCuentaPaciente intacta y ejecuta su acceso heredado en el hilo del formulario.</summary>
    Private Function BuscarCuentaCamas(ByVal paciente As String, ByVal cama As Integer) As String
        If Me.IsDisposed OrElse Me.Disposing OrElse Not Me.IsHandleCreated Then Throw New InvalidOperationException("Formulario no disponible.")
        If Me.InvokeRequired Then
            Return CStr(Me.Invoke(New Func(Of String, Integer, String)(AddressOf BuscarCuentaCamas), paciente, cama))
        End If
        Return Me.NumCuentaPaciente(paciente, cama)
    End Function

    ''' <summary>Reutiliza Get_FechaHoraIngresoHospitalario sin cambiar su implementación ni sus conexiones globales.</summary>
    Private Function ObtenerIngresoCamas(ByVal paciente As String, ByVal recepcion As Integer) As DateTime
        If Me.IsDisposed OrElse Me.Disposing OrElse Not Me.IsHandleCreated Then Throw New InvalidOperationException("Formulario no disponible.")
        If Me.InvokeRequired Then
            Return CDate(Me.Invoke(New Func(Of String, Integer, DateTime)(AddressOf ObtenerIngresoCamas), paciente, recepcion))
        End If
        Return Funciones.Get_FechaHoraIngresoHospitalario(paciente, recepcion)
    End Function

    ''' <summary>Recalcula una vez por cuenta al finalizar el recorrido de inserciones, en el hilo de los accesos globales.</summary>
    Private Sub RecalcularTotalesCamas(ByVal numeroCuenta As String)
        If Me.IsDisposed OrElse Me.Disposing OrElse Not Me.IsHandleCreated Then Throw New InvalidOperationException("Formulario no disponible.")
        If Me.InvokeRequired Then
            Me.Invoke(New Action(Of String)(AddressOf RecalcularTotalesCamas), numeroCuenta)
            Return
        End If
        Funciones.RecalcularTotales(numeroCuenta)
    End Sub

    Private Sub RegistrarLogCamas(ByVal mensaje As String)
        System.Diagnostics.Trace.WriteLine(mensaje)
        Try
            If Me.IsDisposed OrElse Me.Disposing OrElse Not Me.IsHandleCreated Then Return
            If Me.InvokeRequired Then
                Me.BeginInvoke(New Action(Of String)(AddressOf RegistrarLogCamas), mensaje)
            Else
                LogEventos.Escribir(mensaje)
            End If
        Catch ex As InvalidOperationException
            System.Diagnostics.Trace.WriteLine("Camas: formulario cerrado durante el diagnóstico.")
        End Try
    End Sub

#End Region

#Region "Procesos Estados de Cuenta"

    Private Function NumCuentaPaciente(ByVal cvepaciente As String, icvecama As Integer) As String

        Dim tb_ticketsec As DataTable
        tb_ticketsec = Me.tb_Recordset_MySQL_local("Select cNumTicketec from tb_ticketsec where " & _
                                                   "cNumTicketec Like 'EC-H-%' and " & _
                                                   "ccvepaciente = '" & cvepaciente & "' and " & _
                                                   "cEstatus = 'ABIERTO'")

        If tb_ticketsec.Rows.Count = 0 Then
            Return ""
        End If

        Try
            Return tb_ticketsec.Rows(0).Item(0).ToString
        Catch ex As Exception
            Return ""
        End Try

        Return ""


    End Function

    Private Function Get_tb_materiales(ByVal ccvematerial As String) As DataTable

        Dim tb_materiales As DataTable
        tb_materiales = Me.tb_Recordset_MySQL_local("Select * " & _
                                                    "from tb_materiales where ccvematerial = '" & ccvematerial & "'")
        Try
            Return tb_materiales
        Catch ex As Exception
            Return tb_materiales
        End Try

        Return tb_materiales

    End Function

    Private Function Get_DxNumTicketEstadoCuenta(ByVal NumCuenta As String) As String

        Dim tb_folio As DataTable
        tb_folio = tb_Recordset_MySQL_local("SELECT icvdiagnostico FROM tb_ticketsec_detalle where " & _
                                            "cNumTicketEC = '" & NumCuenta & "' order by icveticketecdetalle desc limit 1")

        If tb_folio.Rows.Count > 0 Then
            Return tb_folio.Rows(0).Item(0).ToString
        End If

        Return ""

    End Function

    Private Function Material_icveClaveCobro(ByVal cvematerial As String) As Integer

        Dim tb As DataTable
        tb = tb_Recordset_MySQL_local("Select icveclave from tb_materiales where " & _
                                   "ccvematerial = '" & cvematerial & "'")
        Try
            Return tb.Rows(0).Item(0).ToString
        Catch ex As Exception
            Return 0
        End Try

        Return 0

    End Function

    Private Function Get_Dx_NumTicketEstadoCuenta(NumCuenta As String) As String

        Dim tb_folio As DataTable
        tb_folio = tb_Recordset_MySQL_local("SELECT icvdiagnostico FROM tb_ticketsec_detalle where " & _
                                                    "cNumTicketEC = '" & NumCuenta & "' order by icveticketecdetalle desc limit 1")

        If tb_folio.Rows.Count > 0 Then
            Return tb_folio.Rows(0).Item(0).ToString
        End If

        Return ""

    End Function

    Private Function Material_CveUnidadMedida(ByVal ccvematerial As String) As String

        Dim tb As DataTable
        tb = tb_Recordset_MySQL_local("Select ccveunidad from tb_materiales where " & _
                                           "ccvematerial = '" & ccvematerial & "'")
        Try
            Return tb.Rows(0).Item(0).ToString
        Catch ex As Exception
            Return ""
        End Try

        Return ""

    End Function

    ''' <summary>
    ''' Muestra la Descripcion y Presentacion del material concatenados.
    ''' </summary>
    ''' <param name="ccvematerial"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function MaterialDescripcion(ByVal ccvematerial As String) As String

        Dim proveedor As DataTable
        proveedor = tb_Recordset_MySQL_local("Select cDescripcion  " & _
                                             "from tb_materiales where ccvematerial = '" & ccvematerial & "'")
        Try
            Dim Desc As String
            Return Trim(proveedor.Rows(0).Item(0).ToString)
            'Return Trim(Desc)
        Catch ex As Exception
            Return ""
        End Try

        Return ""

    End Function

    Private Function Material_CveAnterior(ByVal ccvematerial As String) As String

        Dim tb As DataTable
        tb = tb_Recordset_MySQL_local("Select ccveanterior from tb_materiales where " & _
                                           "ccvematerial = '" & ccvematerial & "'")
        Try
            Return tb.Rows(0).Item(0).ToString
        Catch ex As Exception
            Return ""
        End Try

        Return ""

    End Function

    Private Function Material_SujetoCostoOficial(ByVal ccvematerial As String) As Integer

        Dim tb As DataTable
        tb = tb_Recordset_MySQL_local("Select iSujetoCatalogoOficial from tb_materiales where " & _
                                           "ccvematerial = '" & ccvematerial & "'")
        Try
            Return tb.Rows(0).Item(0).ToString
        Catch ex As Exception
            Return 0
        End Try

        Return 0

    End Function

    Private Function Material_ExentoIVA(ByVal ccvematerial As String) As String

        Dim tb_materiales As DataTable
        tb_materiales = tb_Recordset_MySQL_local("Select iExcentoIVA " & _
                                                  "from tb_materiales where ccvematerial = '" & ccvematerial & "'")
        Try
            Return tb_materiales.Rows(0).Item(0).ToString
        Catch ex As Exception
            Return "X"
        End Try

        Return "X"

    End Function

    Private Function PacienteListaPrecios(ByVal ccvepaciente As String) As String

        If ccvepaciente = "" Then
            Return 1
        End If

        Dim tb As DataTable
        tb = tb_Recordset_MySQL_local("Select cat_factorsocial2.icvefactorsocial as lista_precios from tb_paciente " & _
                                              "INNER JOIN cat_factorsocial2 ON (cat_factorsocial2.cClasificacion = tb_paciente.cClasificacion) " & _
                                              "where " & _
                                              "ccvepaciente = '" & ccvepaciente & "'")
        Try
            Return tb.Rows(0).Item("lista_precios").ToString
        Catch ex As Exception
            Return 1
        End Try

        Return 1

    End Function

#End Region

#Region "Importar"


    ''' <summary>
    ''' Importar Firmas Recibido
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub ImportarFirmasRecibido()

        Dim tb_servicios_sistemas As DataTable
        tb_servicios_sistemas = tb_Recordset_MySQL_Central("SELECT " & _
                                                          "tss.* " & _
                                                          "FROM tb_servicios_sistemas tss " & _
                                                          "where tss.sinc = 1 AND tss.firmado = 1 LIMIT 5")

        If tb_servicios_sistemas.Rows.Count = 0 Then
            Exit Sub
        End If

        Try
            Me.ProgressBarX1.Minimum = 0
            Me.ProgressBarX1.Maximum = tb_servicios_sistemas.Rows.Count
            Me.ProgressBarX1.Text = Me.ProgressBarX1.Text & " avance: 0 de " & tb_servicios_sistemas.Rows.Count
        Catch ex2 As Exception
        End Try

        For i As Integer = 0 To tb_servicios_sistemas.Rows.Count - 1

            '== Obtenemos el id de registro DE PORTAL == ==
            Dim IdRegistroServicio_It As Integer = 0
            IdRegistroServicio_It = tb_servicios_sistemas.Rows(i).Item("id").ToString

            '== Verificamos si el id de servicio de plataforma existe en la DB local == ==
            Dim tb_temp_servicios As DataTable
            tb_temp_servicios = tb_Recordset_MySQL_local("Select id from tb_servicios_sistemas where id = '" & IdRegistroServicio_It & "'")

            Dim IdClienteLocal As Integer = 0
            If tb_temp_servicios.Rows.Count = 0 Then

                LogEventos.Escribir("ADVERTENCIA: El servicio fimado con id: " & tb_servicios_sistemas.Rows(i).Item("id").ToString & " no existe en base de datos local, verifique.")

                If Not Update_Central("tb_servicios_sistemas", _
                               "sinc = 0", _
                               "id", IdRegistroServicio_It) Then
                    LogEventos.Escribir("Error al sincronizar el dato en la base de datos tb_servicios_sistemas del servicio firmado con id: " & tb_servicios_sistemas.Rows(i).Item("id").ToString & "")
                End If

            Else

                '== PASO 1: Descargar imagen desde FTP remoto a carpeta temporal == ==
                Dim NombreFotoServicio As String = tb_servicios_sistemas.Rows(i).Item("foto_servicio").ToString
                If NombreFotoServicio <> "" Then
                    Try
                        '-- Ruta temporal local para guardar la imagen descargada --
                        Dim RutaTemporal As String = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Histomedic")
                        If Not System.IO.Directory.Exists(RutaTemporal) Then
                            System.IO.Directory.CreateDirectory(RutaTemporal)
                        End If
                        Dim ArchivoTemporal As String = System.IO.Path.Combine(RutaTemporal, NombreFotoServicio)
                        Try
                            System.IO.File.Delete(ArchivoTemporal)
                        Catch ex As Exception
                        End Try

                        '-- Datos FTP remoto (histomedic.mx) --
                        Dim FTP_Remoto_Host As String = "ftp://histomedic.mx"
                        Dim FTP_Remoto_Usuario As String = "ftp_admin@histomedic.mx"
                        Dim FTP_Remoto_Password As String = "Jhytmd2cKb*5"
                        Dim FTP_Remoto_Ruta As String = FTP_Remoto_Host & "/portal.histomedic.mx/Assets/files/servicios/" & NombreFotoServicio

                        Try
                            My.Computer.Network.DownloadFile(FTP_Remoto_Ruta, ArchivoTemporal, FTP_Remoto_Usuario, FTP_Remoto_Password)
                            'LogEventos.Escribir("Imagen descargada desde FTP remoto: " & NombreFotoServicio & " para el servicio id: " & IdRegistroServicio_It.ToString())
                        Catch ex As Exception
                            LogEventos.Escribir("Error al descargar imagen desde FTP remoto para servicio id: " & IdRegistroServicio_It.ToString() & " - " & ex.Message)
                        End Try


                        '== PASO 2: Subir imagen al FTP local (127.0.0.1) en carpeta FTP_SERVICIOS == ==
                        If System.IO.File.Exists(ArchivoTemporal) Then
                            Try
                                Dim FTP_Local_Host As String = IpServidor
                                Dim FTP_Local_Usuario As String = Me.FTP_USUARIO
                                Dim FTP_Local_Password As String = Me.FTP_PASSWORD
                                Dim FTP_Local_Ruta As String = "ftp://" & FTP_Local_Host & "/FTP_SERVICIOS/" & NombreFotoServicio

                                Dim ftpRequest As System.Net.FtpWebRequest = CType(System.Net.WebRequest.Create(FTP_Local_Ruta), System.Net.FtpWebRequest)
                                ftpRequest.Method = System.Net.WebRequestMethods.Ftp.UploadFile
                                ftpRequest.Credentials = New System.Net.NetworkCredential(FTP_Local_Usuario, FTP_Local_Password)
                                ftpRequest.UseBinary = True
                                ftpRequest.UsePassive = True
                                ftpRequest.KeepAlive = False

                                Dim bytesArchivo() As Byte = System.IO.File.ReadAllBytes(ArchivoTemporal)
                                ftpRequest.ContentLength = bytesArchivo.Length

                                Using streamSalida As System.IO.Stream = ftpRequest.GetRequestStream()
                                    streamSalida.Write(bytesArchivo, 0, bytesArchivo.Length)
                                End Using

                                Using ftpResponse As System.Net.FtpWebResponse = CType(ftpRequest.GetResponse(), System.Net.FtpWebResponse)
                                    'LogEventos.Escribir("Imagen subida a FTP local (FTP_SERVICIOS): " & NombreFotoServicio & " - Estado: " & ftpResponse.StatusDescription.Trim())
                                End Using

                            Catch ex As Exception
                                LogEventos.Escribir("Error al subir imagen al FTP local para servicio id: " & IdRegistroServicio_It.ToString() & " - " & ex.Message)
                            End Try
                        End If

                    Catch ex As Exception
                        LogEventos.Escribir("Error general al procesar imagen FTP del servicio id: " & IdRegistroServicio_It.ToString() & " - " & ex.Message)
                    End Try
                End If

                If Not Update_local("tb_servicios_sistemas", _
                                     "estatus = '" & tb_servicios_sistemas.Rows(i).Item("estatus").ToString & "', " & _
                                     "firmado = '" & tb_servicios_sistemas.Rows(i).Item("firmado").ToString & "', " & _
                                     "ccveusuario_recibe = '" & tb_servicios_sistemas.Rows(i).Item("ccveusuario_recibe").ToString & "', " & _
                                     "fchregistro_recibe = '" & Format(CDate(tb_servicios_sistemas.Rows(i).Item("fchregistro_recibe").ToString), "yyyy-MM-dd HH:mm:ss") & "', " & _
                                     "firma_recibe = '" & tb_servicios_sistemas.Rows(i).Item("firma_recibe").ToString & "', " & _
                                     "foto_servicio = '" & tb_servicios_sistemas.Rows(i).Item("foto_servicio").ToString & "', " & _
                                     "sinc = 0", _
                                     "id", IdRegistroServicio_It) Then
                    LogEventos.Escribir("Error al sincronizar el dato en la base de datos tb_servicios_sistemas del servicio firmado con id: " & tb_servicios_sistemas.Rows(i).Item("id").ToString & "")
                Else
                    If Not Update_Central("tb_servicios_sistemas", _
                              "sinc = 0", _
                              "id", IdRegistroServicio_It) Then
                        LogEventos.Escribir("Error al actualizar el valor sinc = 0 de tb_servicios_sistemas en el portal con id: " & tb_servicios_sistemas.Rows(i).Item("id").ToString & "")
                    End If
                End If

            End If

            Me.ProgressBarX1.Value = Me.ProgressBarX1.Value + 1
            Me.ProgressBarX1.Text = "ImportarFirmasRecibido. " & Me.ProgressBarX1.Value & " de " & Me.ProgressBarX1.Maximum

        Next

        Me.ProgressBarX1.Value = 0


    End Sub


    ''' <summary>
    ''' Importar Foto Activo Fijo
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub ImportarFotoActivoFijo()

        Dim tb_foto_activo_fijo As DataTable
        tb_foto_activo_fijo = tb_Recordset_MySQL_Central("SELECT " & _
                                                          "t.Id, t.foto, t.sinc " & _
                                                          "FROM tb_almacen_existencias_series2 t " & _
                                                          "where t.sinc = 1 LIMIT 5")

        If tb_foto_activo_fijo.Rows.Count = 0 Then
            Exit Sub
        End If

        Try
            Me.ProgressBarX1.Minimum = 0
            Me.ProgressBarX1.Maximum = tb_foto_activo_fijo.Rows.Count
            Me.ProgressBarX1.Text = Me.ProgressBarX1.Text & " avance: 0 de " & tb_foto_activo_fijo.Rows.Count
        Catch ex2 As Exception
        End Try

        For i As Integer = 0 To tb_foto_activo_fijo.Rows.Count - 1

            '== Obtenemos el id de registro DE PORTAL == ==
            Dim IdRegistroFoto_It As Integer = 0
            IdRegistroFoto_It = tb_foto_activo_fijo.Rows(i).Item("Id").ToString

            '== Verificamos si el id de foto de plataforma existe en la DB local == ==
            Dim tb_temp_foto_activo_fijo As DataTable
            tb_temp_foto_activo_fijo = tb_Recordset_MySQL_local("Select id from tb_almacen_existencias_series2 where id = '" & IdRegistroFoto_It & "'")

            Dim IdClienteLocal As Integer = 0
            If tb_temp_foto_activo_fijo.Rows.Count = 0 Then

                LogEventos.Escribir("ADVERTENCIA: EL reigstro de la foto con id: " & tb_foto_activo_fijo.Rows(i).Item("Id").ToString & " no existe en base de datos local, verifique.")

                If Not Update_Central("tb_almacen_existencias_series2", _
                                      "sinc = 0", _
                                      "Id", IdRegistroFoto_It) Then
                    LogEventos.Escribir("Error al sincronizar el dato en la base de datos tb_almacen_existencias_series2 la foto con el id: " & tb_foto_activo_fijo.Rows(i).Item("Id").ToString & "")
                End If

            Else

                '== PASO 1: Descargar imagen desde FTP remoto a carpeta temporal == ==
                Dim NombreFotoActivoFijo As String = tb_foto_activo_fijo.Rows(i).Item("foto").ToString
                If NombreFotoActivoFijo <> "" Then
                    Try
                        '-- Ruta temporal local para guardar la imagen descargada --
                        Dim RutaTemporal As String = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Histomedic")
                        If Not System.IO.Directory.Exists(RutaTemporal) Then
                            System.IO.Directory.CreateDirectory(RutaTemporal)
                        End If
                        Dim ArchivoTemporal As String = System.IO.Path.Combine(RutaTemporal, NombreFotoActivoFijo)
                        Try
                            System.IO.File.Delete(ArchivoTemporal)
                        Catch ex As Exception
                        End Try

                        '-- Datos FTP remoto (histomedic.mx) --
                        Dim FTP_Remoto_Host As String = "ftp://histomedic.mx"
                        Dim FTP_Remoto_Usuario As String = "ftp_admin@histomedic.mx"
                        Dim FTP_Remoto_Password As String = "Jhytmd2cKb*5"
                        Dim FTP_Remoto_Ruta As String = FTP_Remoto_Host & "/portal.histomedic.mx/Assets/files/activofijo/" & NombreFotoActivoFijo

                        Try
                            My.Computer.Network.DownloadFile(FTP_Remoto_Ruta, ArchivoTemporal, FTP_Remoto_Usuario, FTP_Remoto_Password)
                            'LogEventos.Escribir("Imagen descargada desde FTP remoto: " & NombreFotoActivoFijo & " para el tb_almacen_existencias_series2.id: " & IdRegistroFoto_It.ToString())
                        Catch ex As Exception
                            LogEventos.Escribir("Error al descargar imagen desde FTP remoto para tb_almacen_existencias_series2.id: " & IdRegistroFoto_It.ToString() & " - " & ex.Message)
                        End Try

                        '== PASO 2: Subir imagen al FTP local (127.0.0.1) en carpeta FTP_ACTIVO_FIJO == ==
                        If System.IO.File.Exists(ArchivoTemporal) Then
                            Try
                                Dim FTP_Local_Host As String = IpServidor
                                Dim FTP_Local_Usuario As String = Me.FTP_USUARIO
                                Dim FTP_Local_Password As String = Me.FTP_PASSWORD
                                Dim FTP_Local_Ruta As String = "ftp://" & FTP_Local_Host & "/FTP_ACTIVO_FIJO/" & NombreFotoActivoFijo

                                Dim ftpRequest As System.Net.FtpWebRequest = CType(System.Net.WebRequest.Create(FTP_Local_Ruta), System.Net.FtpWebRequest)
                                ftpRequest.Method = System.Net.WebRequestMethods.Ftp.UploadFile
                                ftpRequest.Credentials = New System.Net.NetworkCredential(FTP_Local_Usuario, FTP_Local_Password)
                                ftpRequest.UseBinary = True
                                ftpRequest.UsePassive = True
                                ftpRequest.KeepAlive = False

                                Dim bytesArchivo() As Byte = System.IO.File.ReadAllBytes(ArchivoTemporal)
                                ftpRequest.ContentLength = bytesArchivo.Length

                                Using streamSalida As System.IO.Stream = ftpRequest.GetRequestStream()
                                    streamSalida.Write(bytesArchivo, 0, bytesArchivo.Length)
                                End Using

                                Using ftpResponse As System.Net.FtpWebResponse = CType(ftpRequest.GetResponse(), System.Net.FtpWebResponse)
                                    'LogEventos.Escribir("Imagen subida a FTP local (FTP_ACTIVO_FIJO): " & NombreFotoActivoFijo & " - Estado: " & ftpResponse.StatusDescription.Trim())
                                End Using

                            Catch ex As Exception
                                LogEventos.Escribir("Error al subir imagen al FTP local para tb_almacen_existencias_series2.id: " & IdRegistroFoto_It.ToString() & " - " & ex.Message)
                            End Try
                        End If

                    Catch ex As Exception
                        LogEventos.Escribir("Error general al procesar imagen FTP del tb_almacen_existencias_series2.id: " & IdRegistroFoto_It.ToString() & " - " & ex.Message)
                    End Try
                End If

                If Not Update_local("tb_almacen_existencias_series2", _
                                     "foto = '" & tb_foto_activo_fijo.Rows(i).Item("foto").ToString & "', " & _
                                     "sinc = 0", _
                                     "id", IdRegistroFoto_It) Then
                    LogEventos.Escribir("Error al sincronizar el dato en la base de datos tb_almacen_existencias_series2 del id: " & tb_foto_activo_fijo.Rows(i).Item("Id").ToString & "")

                Else
                    If Not Update_Central("tb_almacen_existencias_series2", _
                                    "sinc = 0", _
                                    "id", IdRegistroFoto_It) Then
                        LogEventos.Escribir("Error al actualizar el valor sinc = 0 de tb_almacen_existencias_series2 en el portal con id: " & tb_foto_activo_fijo.Rows(i).Item("Id").ToString & "")
                    End If
                End If

            End If

            Me.ProgressBarX1.Value = Me.ProgressBarX1.Value + 1
            Me.ProgressBarX1.Text = "ImportarFotoActivoFijo. " & Me.ProgressBarX1.Value & " de " & Me.ProgressBarX1.Maximum

        Next

        Me.ProgressBarX1.Value = 0


    End Sub


#End Region

  
End Class

Public Class LogEventos

    Private Shared ReadOnly RutaLog As String =
        Path.Combine(Application.StartupPath, "Eventos.log")

    Private Const LimiteMB As Long = 100

    Public Shared Sub Escribir(Mensaje As String)

        Try

            ' Verificar tamaño del log
            If File.Exists(RutaLog) Then

                Dim info As New FileInfo(RutaLog)
                Dim tamanoBytes As Long = info.Length

                If tamanoBytes >= (1 * 1024 * 1024) Then
                    '$"Eventos_{DateTime.Now:yyyyMMdd_HHmmss}.log")
                    Dim respaldo As String =
                        Path.Combine(
                            Application.StartupPath,
                            String.Format(
                                    "Eventos_{0:yyyyMMdd_HHmmss}.log",
                                    DateTime.Now))

                    File.Move(RutaLog, respaldo)

                    'File.Delete(RutaLog)

                End If

            End If

            Dim linea As String = String.Format(
                                    "{0:yyyy-MM-dd HH:mm:ss} - {1}",
                                    DateTime.Now,
                                    Mensaje)

            File.AppendAllText(RutaLog, linea & Environment.NewLine)

        Catch ex As Exception
            ' Ignorar errores del log
        End Try

    End Sub

End Class
