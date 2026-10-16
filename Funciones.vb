
Imports System.IO
Imports System.Net
Imports System.Net.NetworkInformation
Imports CrystalDecisions.Shared
Imports Newtonsoft.Json
Imports System.Text

Public Class Funciones

#Region "Funciones de Mensajeria"


    Shared Function Msj_Adv(ByVal Pregunta As String) As Boolean

        'If MsgBox(Pregunta, MsgBoxStyle.YesNo + MsgBoxStyle.Exclamation) = MsgBoxResult.Yes Then
        '    Return True
        'Else
        '    Return False
        'End If

    End Function

    Shared Sub Msj_AdvOnly(ByVal Texto As String)

        'MsgBox(Texto, MsgBoxStyle.Exclamation)

    End Sub

    Shared Sub Msj_Info(ByVal Texto As String)

        'MsgBox(Texto, MsgBoxStyle.Information)

    End Sub


    Shared Sub Msj_Err(ByVal Texto As String)

        'MsgBox(Texto, MsgBoxStyle.Critical)

    End Sub

    ''' <summary>
    ''' Devuelve la Ruta Donde se creo el archivo pdf
    ''' </summary>
    ''' <param name="ReporteOptions"></param>
    ''' <param name="NombreDocumento">Nombre con el que se guardará el archivo pdf temporal</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function CrearPDF(ByVal ReporteOptions As CrystalDecisions.Shared.ExportOptions, _
                              NombreDocumento As String) As String

        Try

            Dim RutaCarpeta As String = ""
            RutaCarpeta = System.IO.Path.GetTempPath() & "Histomedic\"

            Dim CrDiskFileDestinationOptions As New DiskFileDestinationOptions()
            Dim CrFormatTypeOptions As New PdfRtfWordFormatOptions()

            If Not Directory.Exists(RutaCarpeta) Then
                Directory.CreateDirectory(RutaCarpeta)
            End If

            Dim RutaCompleta As String = ""
            RutaCompleta = RutaCarpeta & NombreDocumento & ".pdf"
            CrDiskFileDestinationOptions.DiskFileName = RutaCompleta

            With ReporteOptions
                .ExportDestinationType = ExportDestinationType.DiskFile
                .ExportFormatType = ExportFormatType.PortableDocFormat
                .DestinationOptions = CrDiskFileDestinationOptions
                .FormatOptions = CrFormatTypeOptions
            End With

            Return RutaCompleta

        Catch ex As Exception
            Return ""
        End Try

    End Function

    Shared Function Calcula_FechaActual() As Date

        Dim tmp_table As DataTable

        tmp_table = frmInterface.tb_Recordset_MySQL_local("SELECT NOW()")
        Calcula_FechaActual = tmp_table.Rows(0).Item(0)

        Return Calcula_FechaActual

    End Function

    Shared Function Calcula_EdadAbs_Int(ByVal FechaNacimiento As Date) As Integer

        Dim meses As String
        Dim Edad As Integer
        Dim FechaActual As Date = Funciones.Calcula_FechaActual

        meses = DateDiff(DateInterval.Day, FechaNacimiento, FechaActual)
        Edad = CInt(Fix(Val(meses / 365)))
        Return CInt(Edad)

    End Function

    Shared Function Calcula_EdadActual_Str(ByVal FechaNacimiento As Date) As String

        Dim FechaActual As Date
        FechaActual = Funciones.Calcula_FechaActual

        Dim anoInicio As Integer
        anoInicio = CInt(Format(FechaNacimiento, "yyyy"))

        Dim anoFin As Integer
        anoFin = CInt(Format(FechaActual, "yyyy"))


        Dim mesInicio As Integer
        mesInicio = CInt(Format(FechaNacimiento, "MM"))

        Dim mesFin As Integer
        mesFin = CInt(Format(FechaActual, "MM"))

        Dim diaInicio As Integer
        diaInicio = CInt(Format(FechaNacimiento, "dd"))

        Dim diaFin As Integer
        diaFin = CInt(Format(FechaActual, "dd"))

        Dim intAniosTranscurridos As Integer
        Dim intMesesTranscurridos As Integer
        Dim intDiasTrancurridos As Integer
        Dim intNoDiaMes As Integer
        Dim intMesAnterior As Integer
        Dim strIntervalo As String

        intAniosTranscurridos = anoFin - anoInicio
        intMesesTranscurridos = mesFin - mesInicio

        If mesFin < mesInicio Then
            intAniosTranscurridos = intAniosTranscurridos - 1
        End If

        If intMesesTranscurridos < 0 Then
            intMesesTranscurridos = intMesesTranscurridos + 12
        End If

        intMesAnterior = mesFin - 1

        If intMesAnterior = 0 Then
            intMesAnterior = 12
        End If

        If (intMesAnterior = 1 Or intMesAnterior = 3 Or intMesAnterior = 5 Or intMesAnterior = 7 Or intMesAnterior = 8 Or intMesAnterior = 10 Or intMesAnterior = 12) Then
            intNoDiaMes = 31
        ElseIf (intMesAnterior = 4 Or intMesAnterior = 6 Or intMesAnterior = 9 Or intMesAnterior = 11) Then
            intNoDiaMes = 30
        ElseIf intMesAnterior = 2 Then
            If anoFin Mod 4 = 0 Then
                intNoDiaMes = 29
            Else
                intNoDiaMes = 28
            End If
        End If

        If diaFin < diaInicio Then
            intMesesTranscurridos = intMesesTranscurridos - 1
        End If

        If intMesesTranscurridos < 0 Then
            intAniosTranscurridos = intAniosTranscurridos - 1
            intMesesTranscurridos = intMesesTranscurridos + 12
        End If

        intDiasTrancurridos = diaFin - diaInicio

        If intDiasTrancurridos < 0 Then
            intDiasTrancurridos = intDiasTrancurridos + intNoDiaMes
            If intDiasTrancurridos < 0 Then
                intDiasTrancurridos = 0
            End If
        End If


        Dim strAniosTranscurridos As String
        If intAniosTranscurridos = 1 Then
            strAniosTranscurridos = intAniosTranscurridos & " año"
        Else
            strAniosTranscurridos = intAniosTranscurridos & " años"
        End If

        Dim strMesesTranscurridos As String
        If intMesesTranscurridos = 1 Then
            strMesesTranscurridos = intMesesTranscurridos & " mes"
        Else
            strMesesTranscurridos = intMesesTranscurridos & " meses"
        End If

        Dim strDiasTrancurridos As String
        If intDiasTrancurridos = 1 Then
            strDiasTrancurridos = intDiasTrancurridos & " dia"
        Else
            strDiasTrancurridos = intDiasTrancurridos & " dias"
        End If


        If intAniosTranscurridos >= 1 Then

            strIntervalo = strAniosTranscurridos & " y " & _
                           strMesesTranscurridos & " y " & _
                           strDiasTrancurridos
        Else

            strIntervalo = strMesesTranscurridos & " y " & _
                            strDiasTrancurridos

        End If

        ',2,'0') || '-' || LPAD(intMesesTranscurridos,2,'0') || '-' || LPAD(intDiasTrancurridos,2,'0');    
        'lpad('tech', 8, '0'); would return '0000tech' 

        Return strIntervalo

    End Function

#Region "ConexionInternet"

    Shared Function verificaConexionInternet() As Boolean

        ' ===== Verifica Actualización Disponible ==============================

        'Console.WriteLine("Verificando conexión a Internet...")
        Dim interfaces As NetworkInterface() = NetworkInterface.GetAllNetworkInterfaces()

        For Each interfaz As NetworkInterface In interfaces

            If interfaz.OperationalStatus = OperationalStatus.Up Then

                'Console.WriteLine("Interfaz: " & interfaz.Name & " - Estado: Disponible")
                ' Verificar conexión a internet con un Ping a Google
                If Funciones.IsInternetAvailable(interfaz) Then
                    'Console.WriteLine("  Conexión a internet: Disponible")
                    Return True

                Else
                    'Console.WriteLine("  Conexión a internet: No disponible")
                End If
            Else
                'Console.WriteLine("Interfaz: " & interfaz.Name & " - Estado: Desconectada")
            End If
        Next

        'Console.WriteLine("Verificación completada.")
        ' ===========================================================================
        Return False

    End Function

    ''' <summary>
    ''' Verificar si hay internet para checar la actulización del sistema
    ''' </summary>
    ''' <param name="networkInterface"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function IsInternetAvailable(ByVal networkInterface As NetworkInterface) As Boolean

        Dim ping As System.Net.NetworkInformation.Ping = New Ping()
        Dim reply As PingReply = Nothing
        Try
            reply = ping.Send("8.8.8.8", 1000) ' Intentar hacer Ping a Google (1 segundo de tiempo de espera)
        Catch ex As Exception
            'Console.WriteLine("Error durante el Ping: " & ex.Message)
            Return False
        End Try

        If reply IsNot Nothing AndAlso reply.Status = IPStatus.Success Then
            Return True
        Else
            Return False
        End If

    End Function

#End Region

    Shared Function getAccessToken() As String

        Dim token As String = ""
        Dim result As String = ""
        Dim url As String = ""

        ' ======= CONFIGURACIÓN DE PROTOCOLOS DE SEGURIDAD ========
        ' Forzar el uso de TLS 1.2, TLS 1.1 y TLS 1.0
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12

        '=========================================

        'Console.WriteLine("Configurando prueba...")

        ' URL de hsitomedic
        url = String.Format("https://admin.histomedic.mx/Whatsapp/getAccesToken")

        Try

            Dim request As HttpWebRequest = DirectCast(WebRequest.Create(url), HttpWebRequest)
            request.Method = "POST"
            request.ContentType = "application/json; charset=UTF-8"
            request.ContentLength = 0


            ' Obtener respuesta
            Using response As HttpWebResponse = DirectCast(request.GetResponse(), HttpWebResponse)
                Using reader As New StreamReader(response.GetResponseStream())
                    result = reader.ReadToEnd()
                End Using
            End Using
            result = Newtonsoft.Json.JsonConvert.DeserializeObject(result)
            token = result
            'Console.WriteLine(vbCrLf & "¡Mensaje enviado con éxito!")

        Catch ex As WebException

            'Console.WriteLine(vbCrLf & "ERROR en la petición:")
            If ex.Response IsNot Nothing Then
                Using reader As New StreamReader(ex.Response.GetResponseStream())
                    'Console.WriteLine(reader.ReadToEnd())
                End Using
            Else
                'Console.WriteLine(ex.Message)
            End If

        Catch ex As Exception
            'Console.WriteLine(vbCrLf & "ERROR general:")
            'Console.WriteLine(ex.Message)
        End Try

        Return token

        '========================================

    End Function

    Shared Function getPhoneNumberId() As String

        Dim phonenumberid As String = ""
        Dim result As String = ""
        Dim url As String = ""

        ' ======= CONFIGURACIÓN DE PROTOCOLOS DE SEGURIDAD ========
        ' Forzar el uso de TLS 1.2, TLS 1.1 y TLS 1.0
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12

        '=========================================

        'Console.WriteLine("Configurando prueba...")

        ' URL de hsitomedic
        url = String.Format("https://admin.histomedic.mx/Whatsapp/getPhoneNumberId")

        Try

            Dim request As HttpWebRequest = DirectCast(WebRequest.Create(url), HttpWebRequest)
            request.Method = "POST"
            request.ContentType = "application/json; charset=UTF-8"
            request.ContentLength = 0


            ' Obtener respuesta
            Using response As HttpWebResponse = DirectCast(request.GetResponse(), HttpWebResponse)
                Using reader As New StreamReader(response.GetResponseStream())
                    result = reader.ReadToEnd()
                End Using
            End Using

            result = Newtonsoft.Json.JsonConvert.DeserializeObject(result)
            phonenumberid = result
            'Console.WriteLine(vbCrLf & "¡Mensaje enviado con éxito!")

        Catch ex As WebException

            'Console.WriteLine(vbCrLf & "ERROR en la petición:")
            If ex.Response IsNot Nothing Then
                Using reader As New StreamReader(ex.Response.GetResponseStream())
                    'Console.WriteLine(reader.ReadToEnd())
                End Using
            Else
                'Console.WriteLine(ex.Message)
            End If

        Catch ex As Exception
            'Console.WriteLine(vbCrLf & "ERROR general:")
            'Console.WriteLine(ex.Message)
        End Try

        Return phonenumberid

        '========================================

    End Function

    Shared Function limpiarTextoMensajesWA(texto As String) As String

        Dim TextoReducido As String
        TextoReducido = texto.Trim
        TextoReducido = TextoReducido.Replace("%", " porc")
        TextoReducido = TextoReducido.Replace("\", "")
        TextoReducido = TextoReducido.Replace("/", "")
        TextoReducido = TextoReducido.Replace("         ", " ")
        TextoReducido = TextoReducido.Replace("        ", " ")
        TextoReducido = TextoReducido.Replace("       ", " ")
        TextoReducido = TextoReducido.Replace("      ", " ")
        TextoReducido = TextoReducido.Replace("     ", " ")
        TextoReducido = TextoReducido.Replace("    ", " ")
        TextoReducido = TextoReducido.Replace("   ", " ")
        TextoReducido = TextoReducido.Replace("  ", " ")

        TextoReducido = TextoReducido.Replace(vbCrLf, ". ")

        Return TextoReducido

    End Function

    ''' <summary>
    ''' Envio de Mensajes por WhatsApp para Orden de Internamiento.
    ''' </summary>
    ''' <param name="ToNumber">Numero Telefonico del Paciente</param>
    ''' <param name="UserName">Nombre del Paciente</param>
    ''' <param name="MedicalUnit">Nombre de la Unidad Medica que envía el mensaje</param>
    ''' <param name="AppointmentDate">Fecha de la Cita en Formato dd/MM/yyyy</param>
    ''' <param name="AppointmentHour">Hora de la Cita en Formato HH:mm</param>
    ''' <param name="DoctorName">Nombre del Medico con el que se agenda la cita.</param>
    ''' <param name="Office">Numero o Nombre del Consultorio en el que se agenda la cita.</param>
    ''' <param name="AppointmentNewDate">Nueva Fecha de la Cita Reagendada en Formato dd/MM/yyyy.</param>
    ''' <param name="AppointmentNewHour">Nueva Hora de la Cita Reagendada en Formato HH:mm.</param>
    ''' <param name="TipoIngresoOI">Tipo de Ingreso al Hospital.</param>
    ''' <param name="TiempoPresentarseAntesIngresoOI">Tiempo que debe presentarse antes del Ingreso.</param>
    ''' <param name="IndicacionesPacienteOI">Indicaciones al Paciente.</param>
    ''' <param name="MedicalAdress">Domicilio Unidad Medica.</param>
    ''' <param name="MedicalPhone">Telefono Unidad Medica.</param>
    ''' <param name="AppointmentIssue">Procedimeinto de Ingreso.</param>
    ''' <remarks></remarks>
    Shared Function WA_OrdenInternamientoCirugia(ByVal tb_DatosCliente As DataTable, ByVal ToNumber As String, ByVal UserName As String, ByVal MedicalUnit As String, _
                                ByVal AppointmentDate As String, ByVal AppointmentHour As String, _
                                ByVal DoctorName As String, ByVal Office As String, _
                                ByVal AppointmentNewDate As String, ByVal AppointmentNewHour As String, _
                                ByVal TipoIngresoOI As String, ByVal TiempoPresentarseAntesIngresoOI As String, _
                                ByVal IndicacionesPacienteOI As String, ByVal MedicalAdress As String, ByVal MedicalPhone As String, ByVal AppointmentIssue As String, _
                                ByVal FolioCitaOI As String) As Boolean


        Try

            If tb_DatosCliente.Rows.Count = 0 Then
                Return False
            End If

            If tb_DatosCliente.Rows(0).Item("permite_uso_WA").ToString <> "Activo" Then
                Return False
            End If

            If Len(ToNumber) <> 10 Then
                Return False
            End If

            ToNumber = "52" & ToNumber
            Dim TemplateName As String = "ingresos_clinica"
            Dim LanguageCode As String = "es_MX"
            Dim URLBotonUno As String = ""
            Dim URLBotonDos As String = ""
            Dim HeaderImageURL As String = tb_DatosCliente.Rows(0).Item("url_iMagen_WA").ToString

            UserName = Funciones.limpiarTextoMensajesWA(UserName)
            If IndicacionesPacienteOI.Length > 0 Then
                IndicacionesPacienteOI = Funciones.limpiarTextoMensajesWA(IndicacionesPacienteOI)
                If IndicacionesPacienteOI.Length > 450 Then
                    IndicacionesPacienteOI = IndicacionesPacienteOI.Substring(0, 450)
                End If
            End If

            MedicalAdress = Funciones.limpiarTextoMensajesWA(MedicalAdress)
            MedicalPhone = Funciones.limpiarTextoMensajesWA(MedicalPhone)
            MedicalUnit = Funciones.limpiarTextoMensajesWA(MedicalUnit)
            DoctorName = Funciones.limpiarTextoMensajesWA(DoctorName)
            Office = Funciones.limpiarTextoMensajesWA(Office)
            AppointmentIssue = Funciones.limpiarTextoMensajesWA(AppointmentIssue)

            ' ======= CONFIGURACIÓN DE PROTOCOLOS DE SEGURIDAD ========
            ' Forzar el uso de TLS 1.2, TLS 1.1 y TLS 1.0
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12

            'Console.WriteLine("Iniciando prueba de WhatsApp API")
            'Console.WriteLine("=================================" & vbCrLf)

            ' =======[DATOS NECESARIOS PARA CONSUMIR API DE WHATSAPP]==========
            ' ========[DATOS DEL CUERPO DE LA PLANTILLA DE EJEMPLO]=========

            If Funciones.verificaConexionInternet Then

                Dim AccessToken As String = Funciones.getAccessToken()
                Dim PhoneNumberID As String = Funciones.getPhoneNumberId()

                Dim Params As New Parameters With {
                        .AccessToken = AccessToken,
                        .PhoneNumberID = PhoneNumberID,
                        .ToNumber = ToNumber,
                        .TemplateName = TemplateName,
                        .LanguageCode = LanguageCode,
                        .UserName = UserName,
                        .MedicalUnit = MedicalUnit,
                        .AppointmentDate = AppointmentDate,
                        .AppointmentHour = AppointmentHour,
                        .DoctorName = DoctorName,
                        .Office = Office,
                        .URLBotonUno = URLBotonUno,
                        .URLBotonDos = URLBotonDos,
                        .AppointmentNewDate = AppointmentNewDate,
                        .AppointmentNewHour = AppointmentNewHour,
                        .TipoIngreso = TipoIngresoOI,
                        .TiempoPresentarseAntesIngreso = TiempoPresentarseAntesIngresoOI,
                        .IndicacionesPacienteOI = IndicacionesPacienteOI,
                        .HeaderImageURL = HeaderImageURL,
                        .MedicalAdress = MedicalAdress,
                        .MedicalPhone = MedicalPhone,
                        .AppointmentIssue = AppointmentIssue,
                        .FolioCitaOI = FolioCitaOI
                }

                If Not histomedic.whatsapp.EnviarMensajeWhatsAppAPI(Params) Then
                    Return False
                End If

            End If

            Return True

        Catch ex As Exception
            Return False
        End Try


    End Function

#End Region

#Region "Funciones de Financieros"

    Shared Function PacienteListaPrecios(ByVal ccvepaciente As String) As String

        If ccvepaciente = "" Then
            Return 1
        End If

        Dim tb As DataTable
        tb = frmInterface.tb_Recordset_MySQL_local("Select cat_factorsocial2.icvefactorsocial as lista_precios from tb_paciente " & _
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

    ''' <summary>
    ''' Precion de Publico General 
    ''' </summary>
    ''' <param name="ccvematerial"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function MaterialPrecioPublicoGeneral(ByVal ccvematerial As String) As Double

        Dim tb_materiales As DataTable
        tb_materiales = frmInterface.tb_Recordset_MySQL_local("Select iPrecioPublicoOficial " & _
                                                              "from tb_materiales where ccvematerial = '" & ccvematerial & "'")
        Try
            Return tb_materiales.Rows(0).Item(0).ToString
        Catch ex As Exception
            Return 0
        End Try

        Return 0

    End Function

    Shared Function Numero_Redondear(ByVal dNumero As Double, ByVal iDecimales As Integer) As Double

        Dim dRetorno As Double
        dRetorno = Math.Round(dNumero, iDecimales)
        Return dRetorno

    End Function

    ''' <summary>
    ''' Nuevo Modelo de Cobro para N Listas de Precios
    ''' </summary>
    ''' <param name="ccvematerial"></param>
    ''' <param name="Cantidad"></param>
    ''' <param name="ListaPrecios"></param>
    ''' <param name="TipoCambioValor"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function getImporteClasificacionNew(ByVal ccvematerial As String, ByVal Cantidad As Double, ByVal ListaPrecios As Integer, Optional TipoCambioValor As Double = 1) As Double

        Dim PrecioPublico As Double = 0
        Dim ImporteTicket As Double = 0


        Try

            Dim tb As DataTable
            tb = frmInterface.tb_Recordset_MySQL_local("Select iPrecio from tb_materiales_precios where " & _
                                                       "ccvematerial = '" & ccvematerial & "' and lista_precios_id = '" & ListaPrecios & "'")

            PrecioPublico = tb.Rows(0).Item("iPrecio").ToString
            PrecioPublico = PrecioPublico / TipoCambioValor

            ImporteTicket = PrecioPublico * Cantidad
            ImporteTicket = Math.Round(ImporteTicket, 2)
            Return ImporteTicket
        Catch ex As Exception
        End Try

        Return ImporteTicket

    End Function

    ''' <summary>
    ''' Multiplica por factor 1.16
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function IvaFactorAumenta() As Double

        Dim tb As DataTable
        tb = frmInterface.tb_Recordset_MySQL_local("Select iFactor from cat_impuestos where " & _
                                              "iVigente = '1'")
        Try
            Return tb.Rows(0).Item(0).ToString
        Catch ex As Exception
            Return 1
        End Try

        Return 1

    End Function

    Shared Function TicketEC_Id(ByVal NumTicket As String) As Integer


        Dim tb As Data.DataTable
        tb = frmInterface.tb_Recordset_MySQL_local("SELECT icveticketec FROM tb_ticketsec where " & _
                                           "cNumTicketec = '" & NumTicket & "'")

        Try
            Return tb.Rows(0).Item(0).ToString
        Catch ex As Exception
            Return 0
        End Try

        Return 0

    End Function

#End Region

#Region "Paquetes"


    ''' <summary>
    ''' > 0 indica que el paciente cuenta con un paquete activo registrado en el ingreso de admisión hospitalaria. = 0 indica que no cuenta con paquete activo.
    ''' </summary>
    ''' <param name="CvePaciente"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function getPaqueteId(ByVal CvePaciente As String) As Integer

        Try

            Dim tb_temp As DataTable
            tb_temp = frmInterface.tb_Recordset_MySQL_local("SELECT paquete_id FROM tb_paciente_paquete where ccvepaciente = '" & CvePaciente & "' and estatus = 0")
            If tb_temp.Rows.Count > 0 Then
                Return tb_temp.Rows(0).Item("paquete_id").ToString
            End If

        Catch ex As Exception
            Return 0
        End Try

        Return 0

    End Function

    Shared Function getPaquete(ByVal paquete_id As Integer) As DataTable

        Dim tb_temp As DataTable

        Try

            'DROP TABLE IF EXISTS `histoclin`.`cat_convenios`;
            'CREATE TABLE  `histoclin`.`cat_convenios` (
            '  `icveconvenio` int(10) unsigned NOT NULL AUTO_INCREMENT,
            '  `cdscconvenio` varchar(100) DEFAULT NULL,
            '  `fchregistro` datetime DEFAULT NULL,
            '  `ccveusuario` varchar(45) DEFAULT NULL,
            '  `cDomicilio` mediumtext,
            '  `cTelefono` varchar(45) DEFAULT NULL,
            '  `iActivo` int(10) unsigned DEFAULT '1',
            '  `ccveusuariosuspende` varchar(45) DEFAULT NULL,
            '  `fchregistrosuspende` datetime DEFAULT NULL,
            '  `email` varchar(95) DEFAULT NULL,
            '  `derechohabiencia_id` int(10) unsigned DEFAULT '0',
            '  `clasificacion_social_id` int(10) unsigned DEFAULT '0',
            '  PRIMARY KEY (`icveconvenio`),
            '  KEY `cdscconvenio` (`cdscconvenio`),
            '  KEY `derechohabiencia_id` (`derechohabiencia_id`),
            '  KEY `clasificacion_social_id` (`clasificacion_social_id`)
            ') ENGINE=MyISAM AUTO_INCREMENT=13 DEFAULT CHARSET=latin1;


            tb_temp = frmInterface.tb_Recordset_MySQL_local("SELECT p.*, cat_convenios.cdscconvenio as convenio, cat_convenios.cTelefono as telefono_notifica, cat_convenios.email as correo_notifica FROM " & _
                                                       "tb_paquete p " & _
                                                       "LEFT JOIN cat_convenios ON (cat_convenios.icveconvenio = p.convenio_id) " & _
                                                       "where p.id = '" & paquete_id & "'")

            Return tb_temp

        Catch ex As Exception
            Return tb_temp
        End Try

        Return tb_temp

    End Function

    Shared Function getProductoPaqueteId(ByVal ccvematerial As String) As Integer

        Try

            Dim tb_temp As DataTable
            tb_temp = frmInterface.tb_Recordset_MySQL_local("SELECT productos_paquete_id FROM tb_materiales where ccvematerial = '" & ccvematerial & "'")
            If tb_temp.Rows.Count > 0 Then
                If tb_temp.Rows(0).Item("productos_paquete_id").ToString = "" Then
                    Return 0
                Else
                    Return tb_temp.Rows(0).Item("productos_paquete_id").ToString
                End If
            End If

        Catch ex As Exception
            Return 0
        End Try

        Return 0

    End Function

    ''' <summary>
    ''' 0 = no aplica paquete, > 0 aplica paquete.
    ''' </summary>NumCuentaPaciente
    ''' <param name="CveMaterial"></param>
    ''' <param name="CvePaciente"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function CargarExcepcionesEC_Paquete(ByVal NumCuenta As String,
                                                ByVal Cantidad As String, _
                                                ByVal CveMaterial As String, _
                                                ByVal CvePaciente As String, _
                                                ByVal IsConsumoUnitario As Boolean,
                                                ByRef notificar_fuera_paquete As Boolean, _
                                                ByRef cantidad_div As Collection) As Integer


        Try

            notificar_fuera_paquete = False

            Dim paquete_id As Integer = 0
            paquete_id = getPaqueteId(CvePaciente)

            ' Si no tiene un paquete activo en el ingreso actual, se envia 0, (no aplica paquete)
            If paquete_id = 0 Then
                Return 0
            End If

            'Verifica Cantidad Base
            Dim equivalenciaDosis As Double = 1
            If IsConsumoUnitario Then
                Dim tb_materiales As DataTable
                tb_materiales = frmInterface.tb_Recordset_MySQL_local("select iClasifAdministra, equivalenciaSuministroDosis, ccveunidadSuministroDosis from tb_materiales where " & _
                                                                        "ccvematerial = '" & CveMaterial & "'")
                equivalenciaDosis = tb_materiales.Rows(0).Item("equivalenciaSuministroDosis").ToString
                Cantidad = Cantidad / equivalenciaDosis

            End If

            If Funciones.MaterialAplica_Paquete(NumCuenta, Cantidad, CveMaterial, paquete_id, cantidad_div, equivalenciaDosis) = 0 Then
                notificar_fuera_paquete = True
                Return 0
            Else
                If cantidad_div.Count > 0 Then
                    notificar_fuera_paquete = True
                End If
            End If

            Return paquete_id
        Catch ex As Exception
            Return 0
        End Try

        Return 0

    End Function

    Shared Function MaterialAplica_Paquete(ByVal NumCuenta As String, ByVal Cantidad As Double, ByVal ccvematerial As String, ByVal paquete_id As Integer, ByRef cantidad_div As Collection, ByRef equivalenciaDosis As Double) As Integer

        Try

            cantidad_div.Clear()

            Dim productos_paquete_id As Integer = 0
            productos_paquete_id = getProductoPaqueteId(ccvematerial)
            'If productos_paquete_id = 0 Then
            '    Return 0
            'End If



            '1) == VERIFICAMOS SI EL PRODCUTO O SERVICIO ESTA DENTRO DEL PAQUETE. == 

            Dim tbSP As DataTable

            'Verifica si el producto existe directo 
            tbSP = frmInterface.tb_Recordset_MySQL_local("SELECT id FROM tb_paquete_detalle " & _
                                                    "where ccvematerial = '" & ccvematerial & "' and paquete_id = '" & paquete_id & "'")

            If tbSP.Rows.Count > 0 Then
                'Return 1
                GoTo valida_cantidad_1
            End If

            'Verifica lso prodcutos asociados al prodcuto actual, para saber si lo incluye en el pquete. GROUP BY mat.ccvematerial?????
            tbSP = frmInterface.tb_Recordset_MySQL_local("SELECT p.id, mat.ccvematerial, p.paquete_id, mat.cDescripcion " & _
                                                    "FROM tb_materiales mat " & _
                                                    "INNER JOIN tb_paquete_detalle p ON (p.productos_paquete_id = mat.productos_paquete_id) " & _
                                                    "where " & _
                                                    "p.paquete_id = '" & paquete_id & "' and " & _
                                                    "p.productos_paquete_id = " & productos_paquete_id & " GROUP BY mat.ccvematerial ")

            If tbSP.Rows.Count = 0 Then
                Return 0
            End If

            If tbSP.Rows.Count > 0 Then
                'Return 1
                GoTo valida_cantidad_2
            End If


            '2) == VERIFICAMOS SI LA CANTIDAD DEL PRODCUTO O SERVICIO ESTA DENTRO DEL PAQUETE. == 

valida_cantidad_1:

            Dim cant_aut As Double = 0
            Dim cant_reg As Double = 0

            Dim tb_sp2 As DataTable
            'tb_sp2 = frmInterface.tb_Recordset_MySQL_local("SELECT sum(iCantidad) as cantidad FROM tb_paquete_detalle " & _
            '                                          "where ccvematerial = '" & ccvematerial & "' and paquete_id = '" & paquete_id & "'")

            If productos_paquete_id > 0 Then

                tb_sp2 = frmInterface.tb_Recordset_MySQL_local("SELECT p.iCantidad as cantidad, mat.ccvematerial " & _
                                                          "FROM tb_materiales mat " & _
                                                          "INNER JOIN tb_paquete_detalle p ON (p.productos_paquete_id = mat.productos_paquete_id) " & _
                                                          "where " & _
                                                          "p.paquete_id = '" & paquete_id & "' and " & _
                                                          "p.productos_paquete_id = " & productos_paquete_id & " GROUP BY mat.ccvematerial")

            Else

                tb_sp2 = frmInterface.tb_Recordset_MySQL_local("SELECT p.iCantidad as cantidad, mat.ccvematerial " & _
                                                          "FROM tb_materiales mat " & _
                                                          "INNER JOIN tb_paquete_detalle p ON (p.ccvematerial = mat.ccvematerial) " & _
                                                          "where " & _
                                                          "p.ccvematerial = '" & ccvematerial & "' and " & _
                                                          "p.paquete_id = '" & paquete_id & "'")
            End If

            'If tb_sp2.Rows(0).Item("cantidad").ToString <> "" Then
            If tb_sp2.Rows.Count > 0 Then
                cant_aut = tb_sp2.Rows(0).Item("cantidad").ToString

                'Dim tb_cta As DataTable
                'tb_cta = frmInterface.tb_Recordset_MySQL_local("Select sum(iCantidad) as cantidad from tb_ticketsec_detalle " & _
                '                                          "where " & _
                '                                          "cNumTicketEC = '" & NumCuenta & "' and ccvematerial = '" & ccvematerial & "'")

                'If tb_cta.Rows(0).Item("cantidad").ToString <> "" Then
                '    cant_reg = tb_cta.Rows(0).Item("cantidad").ToString
                'End If

                Dim tb_cta As DataTable
                For i As Integer = 0 To tb_sp2.Rows.Count - 1

                    tb_cta = frmInterface.tb_Recordset_MySQL_local("Select sum(iCantidad) as cantidad from tb_ticketsec_detalle " & _
                                                              "where " & _
                                                              "cNumTicketEC = '" & NumCuenta & "' and ccvematerial = '" & tb_sp2.Rows(i).Item("ccvematerial").ToString & "'")
                    If tb_cta.Rows(0).Item("cantidad").ToString <> "" Then
                        cant_reg = cant_reg + (tb_cta.Rows(0).Item("cantidad").ToString / equivalenciaDosis)
                    End If

                Next

                '== Algoritmo especial =======================================================================
                '       3         0             2 
                '       2         2             3 
                If (Cantidad + cant_reg) <= cant_aut Then
                    Return 1
                Else

                    If cant_reg > cant_aut Then
                        Return 0
                    End If

                    '== Total a cobrar ====
                    Dim cant_cobrar As Double = 0
                    '     1            3              0                  2
                    '     1            2              2                  3
                    cant_cobrar = (Cantidad + cant_reg) - cant_aut
                    cantidad_div.Add(cant_cobrar)

                    '== Total a agergar a control interno de paquee ====
                    Dim cant_pqt As Double = 0
                    '    2         3           1
                    '    1         2           1
                    cant_pqt = Cantidad - cant_cobrar
                    If cant_pqt > 0 Then
                        cantidad_div.Add(cant_pqt)
                    End If

                    Return 1

                End If
                '== Algoritmo especial Fin =======================================================================

            End If

valida_cantidad_2:

            cant_aut = 0
            cant_reg = 0

            'Verifica lso prodcutos asociados al prodcuto actual, para saber si lo incluye en el pquete.
            tb_sp2 = frmInterface.tb_Recordset_MySQL_local("SELECT p.iCantidad as cantidad, mat.ccvematerial " & _
                                                    "FROM tb_materiales mat " & _
                                                    "INNER JOIN tb_paquete_detalle p ON (p.productos_paquete_id = mat.productos_paquete_id) " & _
                                                    "where " & _
                                                    "p.paquete_id = '" & paquete_id & "' and " & _
                                                    "p.productos_paquete_id = " & productos_paquete_id & " GROUP BY mat.ccvematerial ")

            If tb_sp2.Rows.Count > 0 Then
                cant_aut = tb_sp2.Rows(0).Item("cantidad").ToString

                Dim tb_cta As DataTable
                For i As Integer = 0 To tb_sp2.Rows.Count - 1

                    tb_cta = frmInterface.tb_Recordset_MySQL_local("Select sum(iCantidad) as cantidad from tb_ticketsec_detalle " & _
                                                              "where " & _
                                                              "cNumTicketEC = '" & NumCuenta & "' and ccvematerial = '" & tb_sp2.Rows(i).Item("ccvematerial").ToString & "'")
                    If tb_cta.Rows(0).Item("cantidad").ToString <> "" Then
                        cant_reg = cant_reg + (tb_cta.Rows(0).Item("cantidad").ToString / equivalenciaDosis)
                    End If

                Next

                'cant_reg = Cantidad + cant_reg
                'If cant_reg <= cant_aut Then
                '    Return 1
                'End If

                '== Algoritmo especial =======================================================================
                '       3         0             2 
                '       2         2             3 
                If (Cantidad + cant_reg) <= cant_aut Then
                    Return 1
                Else

                    If cant_reg > cant_aut Then
                        Return 0
                    End If

                    '== Total a cobrar ====
                    Dim cant_cobrar As Double = 0
                    '     1            3              0                  2
                    '     1            2              2                  3
                    cant_cobrar = (Cantidad + cant_reg) - cant_aut
                    cantidad_div.Add(cant_cobrar)

                    '== Total a agergar a control interno de paquee ====
                    Dim cant_pqt As Double = 0
                    '    2         3           1
                    '    1         2           1
                    cant_pqt = Cantidad - cant_cobrar
                    If cant_pqt > 0 Then
                        cantidad_div.Add(cant_pqt)
                    End If

                    Return 1

                End If
                '== Algoritmo especial Fin =======================================================================

            End If

        Catch ex As Exception
            Return 0
        End Try

        Return 0

    End Function




    ''' <summary>
    ''' Valida Precio de Venta y Envia correo de alerta de precios si el precio es mayor al costo.
    ''' </summary>
    ''' <param name="CvePaciente"></param>
    ''' <param name="CveMaterial"></param>
    ''' <param name="Cantidad_Unitario"></param>
    ''' <param name="CveUnidad_Unitario"></param>
    ''' <param name="Descripcion"></param>
    ''' <remarks></remarks>
    Shared Sub notificarAlertaProductoCantidadFueraPaquete(ByVal CvePaciente As String, ByVal CveMaterial As String, ByVal Cantidad_Unitario As Double, ByVal CveUnidad_Unitario As String, ByVal Descripcion As String)

        Try

            Dim paquete_id As Integer = 0
            paquete_id = Funciones.getPaqueteId(CvePaciente)
            If paquete_id = 0 Then
                Exit Sub
            End If

            Try
                frmInterface.Insert_local("tb_paquetes_notificaciones", _
                                     "clues = '" & frmInterface.CLUES & "', " & _
                                     "paquete_id = '" & paquete_id & "', " & _
                                     "ccvepaciente = '" & CvePaciente & "', " & _
                                     "ccvematerial = '" & CveMaterial & "', " & _
                                     "descripcion = '" & Descripcion & "', " & _
                                     "cantidad_unitario = '" & Cantidad_Unitario & "', " & _
                                     "ccveunidad_unitario = '" & CveUnidad_Unitario & "', " & _
                                     "enviado = 0")
            Catch ex As Exception
            End Try

            'Funciones.Msj_AdvOnly("A V I S O   D E   P R O D U C T O   O   C A N T I D A D   F U E R A   D E   P A Q U E T E" & vbCrLf & vbCrLf & _
            '                      "Producto: " & vbCrLf & Descripcion & vbCrLf & vbCrLf & _
            '                      "Debe informar a recepcion que la cantidad o producto no está cubierto para este PAQUETE")

        Catch ex As Exception
        End Try

    End Sub

#End Region

#Region "Paciente"

    Shared Function PacienteClasificacionSocial(ByVal ccvepaciente As String) As String

        
        Dim tb As DataTable
        tb = frmInterface.tb_Recordset_MySQL_local("Select cClasificacion from tb_paciente where " & _
                                                   "ccvepaciente = '" & ccvepaciente & "'")

        If tb.Rows.Count = 0 Then Return "NO CLASIFICADO"
        Try
            If tb.Rows(0).Item(0).ToString = "" Then
                Return "NO CLASIFICADO"
            End If
            Return tb.Rows(0).Item(0).ToString
        Catch ex As Exception
            Return "NO CLASIFICADO"
        End Try
        Return tb.Rows(0).Item(0).ToString

    End Function

    ''' <summary>
    ''' Devuelve el nombre del servicio activo del paciente, sin importar cómo se haya logueado el usuario.
    ''' </summary>
    ''' <param name="ClavePaciente"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function Get_Recepcion_ServicioActivo(ByVal ClavePaciente As String) As String

        Try
            Dim tbRecepcionVigente As DataTable
            tbRecepcionVigente = frmInterface.tb_Recordset_MySQL_local("SELECT Id, cdscareaafectada, cClasificacionAccidente, cStatus " & _
                                                                   "FROM tb_urg_recepcion WHERE " & _
                                                                   "ccvepaciente = '" & ClavePaciente & "' AND " & _
                                                                   "iEgresado = 0 " & _
                                                                   "ORDER BY Id DESC")
            If tbRecepcionVigente.Rows.Count = 0 Then
                Return ""
            Else
                Return tbRecepcionVigente.Rows(0).Item(1).ToString
            End If
            Return ""
        Catch ex As Exception
            Return ""
        End Try

        Return ""

    End Function

    Shared Function Get_Recepcion_IdRecepcion(ByVal ClavePaciente As String, Servicio As String) As Integer


        If ClavePaciente = "" Then
            Return 0
        End If

        Try
            Dim tbRecepcionVigente As DataTable
            Dim IdRecepcion As Integer

            tbRecepcionVigente = frmInterface.tb_Recordset_MySQL_local("SELECT Id, icvecama, cClasificacionAccidente, cStatus " & _
                                                                   "FROM tb_urg_recepcion WHERE " & _
                                                                   "ccvepaciente = '" & ClavePaciente & "' and " & _
                                                                   "cdscareaafectada = '" & Servicio & "' and " & _
                                                                   "iEgresado = 0 " & _
                                                                   "ORDER BY fchHoraRecepcion desc")

            If tbRecepcionVigente.Rows.Count = 0 Then
                tbRecepcionVigente = frmInterface.tb_Recordset_MySQL_local("SELECT Id, icvecama, cClasificacionAccidente, cStatus " & _
                                                                      "FROM tb_urg_recepcion WHERE " & _
                                                                      "ccvepaciente = '" & ClavePaciente & "' and " & _
                                                                      "iEgresado = 0 " & _
                                                                      "ORDER BY fchHoraRecepcion desc")
                If tbRecepcionVigente.Rows.Count = 0 Then
                    Return 0
                Else
                    IdRecepcion = tbRecepcionVigente.Rows(0).Item(0).ToString
                End If
            Else
                IdRecepcion = tbRecepcionVigente.Rows(0).Item(0).ToString
            End If

            Return IdRecepcion

        Catch ex As Exception
            Return 0
        End Try

        Return 0

    End Function

    Shared Function Get_Recepcion_icvecama(ByVal IdRecepcion As Integer, ByVal ClavePaciente As String) As Integer

        If IdRecepcion = 0 Then
            Return 0
        End If

        Dim Cama As Integer = 0

        Try
            Dim tbRecepcionVigente As DataTable
            tbRecepcionVigente = frmInterface.tb_Recordset_MySQL_local("SELECT Id, icvecama, cClasificacionAccidente, cStatus " & _
                                                                   "FROM tb_urg_recepcion WHERE " & _
                                                                   "Id = '" & IdRecepcion & "' and " & _
                                                                   "ccvepaciente = '" & ClavePaciente & "'")
            If tbRecepcionVigente.Rows.Count = 0 Then
                Return Cama
            Else
                Cama = tbRecepcionVigente.Rows(0).Item(1).ToString
            End If

            Return Cama

        Catch ex As Exception
            Return Cama
        End Try

        Return Cama

    End Function

#End Region

#Region "Productos"

    ''' <summary>
    ''' Muestra la Descripcion y Presentacion del material concatenados.
    ''' </summary>
    ''' <param name="ccvematerial"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function MaterialDescripcion(ByVal ccvematerial As String) As String

        Dim proveedor As DataTable
        proveedor = frmInterface.tb_Recordset_MySQL_local("Select cDescripcion  " & _
                                                     "from tb_materiales where ccvematerial = '" & ccvematerial & "'")
        Try
            Return Trim(proveedor.Rows(0).Item(0).ToString)
            'Return Trim(Desc)
        Catch ex As Exception
            Return ""
        End Try

        Return ""

    End Function

    Shared Function MaterialUnidad(ByVal ccvematerial As String) As String

        Dim proveedor As DataTable
        proveedor = frmInterface.tb_Recordset_MySQL_local("Select ccveunidad " & _
                                                     "from tb_materiales where ccvematerial = '" & ccvematerial & "'")
        Try
            Return proveedor.Rows(0).Item(0).ToString
        Catch ex As Exception
            Return "ser"
        End Try

        Return "ser"

    End Function

    Shared Function MaterialicveClave(ByVal ccvematerial As String) As Integer

        Dim tb_materiales As DataTable
        tb_materiales = frmInterface.tb_Recordset_MySQL_local("Select icveclave " & _
                                                  "from tb_materiales where ccvematerial = '" & ccvematerial & "'")
        Try
            Return tb_materiales.Rows(0).Item(0).ToString
        Catch ex As Exception
            Return 0
        End Try

        Return 0

    End Function

    Shared Function Material_ExentoIVA(ByVal ccvematerial As String) As String

        Dim tb_materiales As DataTable
        tb_materiales = frmInterface.tb_Recordset_MySQL_local("Select iExcentoIVA " & _
                                                  "from tb_materiales where ccvematerial = '" & ccvematerial & "'")
        Try
            Return tb_materiales.Rows(0).Item(0).ToString
        Catch ex As Exception
            Return "X"
        End Try

        Return "X"

    End Function

    Shared Function Material_CveAnterior(ByVal ccvematerial As String) As String

        Dim tb As DataTable

        tb = frmInterface.tb_Recordset_MySQL_local("Select ccveanterior from tb_materiales where " & _
                                           "ccvematerial = '" & ccvematerial & "'")
        Try
            Return tb.Rows(0).Item(0).ToString
        Catch ex As Exception
            Return ""
        End Try

        Return ""

    End Function

    Shared Function Material_SujetoCostoOficial(ByVal ccvematerial As String) As Integer

        Dim tb As DataTable
        tb = frmInterface.tb_Recordset_MySQL_local("Select iSujetoCatalogoOficial from tb_materiales where " & _
                                           "ccvematerial = '" & ccvematerial & "'")
        Try
            Return tb.Rows(0).Item(0).ToString
        Catch ex As Exception
            Return 0
        End Try

        Return 0

    End Function

    ''' <summary>
    ''' True = Causa IVA
    ''' </summary>
    ''' <param name="ccvematerial"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function MaterialCausaIVA(ByVal ccvematerial As String) As Boolean

        Dim tb_materiales As DataTable
        tb_materiales = frmInterface.tb_Recordset_MySQL_local("Select iExcentoIVA  " & _
                                                 "from tb_materiales where ccvematerial = '" & ccvematerial & "'")
        Try
            If tb_materiales.Rows(0).Item(0).ToString = "I" Then
                Return True
            End If
            Return False
        Catch ex As Exception
            Return False
        End Try

        Return False

    End Function

#End Region

#Region "Otras Funciones"

    ''' <summary>
    ''' Configuarciones Varioas de Operación. Devielve el valor configurado en Datos de Unidad
    ''' </summary>
    ''' <param name="concepto"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function getValorConfiguracion(ByVal concepto As String) As String

        Try

            Dim tb_valor As DataTable
            tb_valor = frmInterface.tb_Recordset_MySQL_local("SELECT ccvematerial as valor from cat_materiales_configuracion where cConcepto = '" & concepto & "'")
            If tb_valor.Rows.Count = 0 Then
                Return "0"
            End If
            Return tb_valor.Rows(0).Item("valor").ToString
        Catch ex As Exception
            Return "0"
        End Try

        Return "0"

    End Function

    Shared Function Get_FechaHoraIngresoHospitalario(ByVal ClavePaciente As String, ByVal IdRecepcion As Integer) As Date

        'fchhorarecepcion, fchhoraegreso
        Dim tbegre As DataTable
        tbegre = frmInterface.tb_Recordset_MySQL_local("Select id, fchEgresa " & _
                                                     "from tb_notamedica_egreso where " & _
                                                     "ccvepaciente = '" & ClavePaciente & "' and " & _
                                                     "id_recepcion = '" & IdRecepcion & "' " & _
                                                     "order by fchregistro desc")
        If tbegre.Rows.Count > 0 Then
            Try
                Return CDate(Format(tbegre.Rows(0).Item(1), "dd/MM/yyyy HH:mm"))
            Catch ex As Exception
                Return Funciones.Calcula_FechaActual
            End Try
        End If

        Dim tb_ingreso As DataTable
        tb_ingreso = frmInterface.tb_Recordset_MySQL_local("Select tb_urg_recepcion.id, tb_urg_recepcion.fchregistro, tb_urg_recepcion.fchhorarecepcion, tb_urg_recepcion.fchregistro_Egr, tb_urg_recepcion.iHora_Egr " & _
                                                      "from tb_urg_recepcion " & _
                                                      "INNER JOIN cat_servicios ON (cat_servicios.cdscservicio = tb_urg_recepcion.cdscareaafectada) " & _
                                                      "where " & _
                                                      "(cat_servicios.cdscareaafectada = 'HOSPITALIZACION' OR cat_servicios.cdscareaafectada = 'URGENCIAS') AND " & _
                                                      "tb_urg_recepcion.ccvepaciente = '" & ClavePaciente & "' " & _
                                                      "order by tb_urg_recepcion.fchhorarecepcion desc")
        If tb_ingreso.Rows.Count = 0 Then
            Return Funciones.Calcula_FechaActual
        End If

        Dim i As Integer = 0

        If tb_ingreso.Rows.Count = 1 Then
            Return CDate(Format(tb_ingreso.Rows(0).Item(2), "dd/MM/yyyy HH:mm"))
        End If

        For i = 0 To tb_ingreso.Rows.Count - 1
            Try

                Dim fechaegr As Date
                Dim fechaegr_str As String
                Dim hora_str As String

                fechaegr_str = Format(CDate(tb_ingreso.Rows(i + 1).Item(3)), "dd/MM/yyyy")
                hora_str = tb_ingreso.Rows(i + 1).Item(4).ToString
                fechaegr = CDate(fechaegr_str & " " & hora_str)

                Dim horas As Integer
                horas = DateDiff(DateInterval.Hour, fechaegr, CDate(tb_ingreso.Rows(i).Item(2)))
                If horas >= 6 Then
                    Return CDate(Format(tb_ingreso.Rows(i).Item(2), "dd/MM/yyyy HH:mm"))
                End If
            Catch ex As Exception
                Return CDate(Format(tb_ingreso.Rows(i).Item(2), "dd/MM/yyyy HH:mm"))
            End Try
        Next

        Return Funciones.Calcula_FechaActual

    End Function

#End Region



End Class

Public Class FacturacionEnviarOrdenInternamiento

    Shared tb As DataTable

    Shared email As String
    Shared Property pty_email() As String
        Get
            Return email
        End Get
        Set(value As String)
            email = value
        End Set
    End Property

    Shared Nom_Destinatario As String
    Shared Property pty_Nom_Destinatario() As String
        Get
            Return Nom_Destinatario
        End Get
        Set(value As String)
            Nom_Destinatario = value
        End Set
    End Property

    Shared asunto As String
    Shared Property pty_asunto() As String
        Get
            Return asunto
        End Get
        Set(value As String)
            asunto = value
        End Set
    End Property

    Shared mensaje As String
    Shared Property pty_mensaje() As String
        Get
            Return mensaje
        End Get
        Set(value As String)
            mensaje = value
        End Set
    End Property

    Shared valido As String
    Shared Property pty_valido() As String
        Get
            Return valido
        End Get
        Set(value As String)
            valido = value
        End Set
    End Property

    Shared IdOrdenInternamiento As Integer
    Shared Property pty_IdOrdenInternamiento() As Integer
        Get
            Return IdOrdenInternamiento
        End Get
        Set(value As Integer)
            IdOrdenInternamiento = value
        End Set
    End Property

#Region "Enviar Email OI"

    ''' <summary>
    ''' Valida los datos del Destinatario
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function ValidaDatosDest() As Boolean

        If pty_Nom_Destinatario = "" Then
            'Funciones.Msj_Err("Debe indicar el nombre del destinatario")
            'pty_valido = False
            Return False
        End If

        If pty_email = "" Then
            'Funciones.Msj_Err("Debe indicar la dirección de correo electrónico")
            'pty_valido = False
            Return False
        End If

        If validaDireccionCorreo(pty_email) = False Then
            pty_valido = False
            Return False
        End If

        Return True

    End Function

    ''' <summary>
    ''' Valida los datos del contendio del correo.
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function ValidaDatosCorreo() As Boolean

        If pty_asunto = "" Then
            'If Funciones.Msj_Adv("No se ha especificado en el correo el asunto,  ¿Deseas continuar y enviarlo de todos modos?") = False Then
            '    pty_valido = False
            '    Return False
            'End If
            Return False
        End If

        If pty_mensaje = "" Then
            'If Funciones.Msj_Adv("No se ha especificado en el correo ningún mensaje,  ¿Deseas continuar y enviarlo de todos modos?") = False Then
            '    pty_valido = False
            '    Return False
            'End If
            Return False
        End If

        Return True

    End Function

    ''' <summary>
    ''' Función para el envío de la orden de internamiento
    ''' </summary>
    ''' <remarks></remarks>
    Shared Function EnviarOrdenInternamiento(Nom_Destinatario As String, email As String, asunto As String, mensaje As String, IdOrdenInternamiento As Integer, Optional MensajeConfirma As Boolean = False) As Boolean

        If email = "" Then
            Exit Function
        End If

        pty_Nom_Destinatario = Nom_Destinatario
        pty_email = email
        pty_asunto = asunto
        pty_mensaje = mensaje
        pty_IdOrdenInternamiento = IdOrdenInternamiento

        'Me.lblMensajeEspera.Text = "Validando Datos del Destinatario...."
        If ValidaDatosDest() = False Then
            Exit Function
        End If

        'Me.lblMensajeEspera.Text = "Validando Datos del Correo...."
        If ValidaDatosCorreo() = False Then
            Exit Function
        End If

        'Me.lblMensajeEspera.Text = "Creando Archivo PDF...."
        If CrearPDF(pty_IdOrdenInternamiento) = False Then
            Exit Function
        End If

        If EnviaEmail(pty_Nom_Destinatario, pty_email, pty_asunto, pty_mensaje, MensajeConfirma) Then

            If frmInterface.Update_local("tb_cirugiascontrol", _
                                         "correoenviado = '1', " & _
                                         "fechaenviocorreo = current_timestamp, " & _
                                         "ccveusuarioenviocorreo = 'histomedic_portal'", _
                                         "Id", pty_IdOrdenInternamiento) Then

                'Funciones.Msj_Info("Información Actualizada Exitosamente")
            Else
                'Funciones.Msj_Err("Error al Guardar, Consulte al Adminstrador")
                Return False
            End If

        Else
            Return False
        End If

        EliminarArchivoTemporal(pty_IdOrdenInternamiento & ".pdf")

        Return True

    End Function

    ''' <summary>
    ''' Genera la carpeta HistoMedic en los archivos temporales para guardar ahi el xml y pdf temporales
    ''' Retorna la ruta completa de onde se alojarán los arhcivos temporales
    ''' </summary>
    ''' <remarks></remarks>
    Shared Function Get_RutaTemporal() As String

        Return System.IO.Path.Combine(System.IO.Path.GetTempPath() & "Histomedic\")

        'If Not Directory.Exists(System.IO.Path.GetTempPath & "HistoMedic") Then
        '    Directory.CreateDirectory(System.IO.Path.GetTempPath & "HistoMedic")
        'End If

        'Return System.IO.Path.GetTempPath & "HistoMedic\"

    End Function

    ''' <summary>
    ''' Elimina los archivos pdf temporales retorna true si la operacion fue completada
    ''' </summary>
    ''' <param name="nombreArchivo">Nombre del archivo a eliminar</param>
    ''' <remarks></remarks>
    Shared Function EliminarArchivoTemporal(ByVal nombreArchivo As String) As Boolean

        Try
            My.Computer.FileSystem.DeleteFile(Get_RutaTemporal() & nombreArchivo, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                              Microsoft.VisualBasic.FileIO.RecycleOption.DeletePermanently,
                                              Microsoft.VisualBasic.FileIO.UICancelOption.DoNothing)
        Catch ex As Exception
            Return False
        End Try

        Return True

    End Function

    ''' <summary>
    ''' Crea y envia el correo electronico. Retorna true si el correo se creó y fue enviado correctamente.
    ''' </summary>
    ''' <param name="nombreDest">Nombre del detinatario</param>
    ''' <param name="direcEmail">Dirección de correo electronico </param>
    ''' <param name="asuntoCorreo">Asunto</param>
    ''' <param name="mensajeCorreo">Mensaje</param>
    ''' <remarks></remarks>
    Shared Function EnviaEmail(ByVal nombreDest As String, ByVal direcEmail As String, ByVal asuntoCorreo As String, ByVal mensajeCorreo As String, Optional MensajeConfirma As Boolean = False) As Boolean

        ' Create a mailman object for sending email.
        Dim mailman As New Chilkat.MailMan()

        ' Any string passed to UnlockComponent automatically begins a 30-day trial.
        Dim success As Boolean
        success = mailman.UnlockComponent("MAIL87654321_3C7B9122j163")
        If (success <> True) Then
            MsgBox(mailman.LastErrorText)
            Return False
        End If

        'DROP TABLE IF EXISTS `histomedic`.`cat_confserversmtp`;
        'CREATE TABLE  `histomedic`.`cat_confserversmtp` (
        '  `icveconf` int(10) NOT NULL AUTO_INCREMENT,
        '  `cpuerto` varchar(9) DEFAULT NULL,
        '  `cdireccion` varchar(250) DEFAULT NULL,
        '  `cclave` varchar(50) DEFAULT NULL,
        '  `chost` varchar(100) DEFAULT NULL,
        '  `cremitente` varchar(250) DEFAULT NULL,
        '  `ccorreoremitente` varchar(200) DEFAULT NULL,
        '  `iActivo` int(10) unsigned DEFAULT '1',
        '  `fchregistro` datetime DEFAULT NULL,
        '  `ccveusuario` varchar(45) DEFAULT NULL,
        '  `fchregistrosuspende` datetime DEFAULT NULL,
        '  `ccveusuariosuspende` varchar(45) DEFAULT NULL,
        '  PRIMARY KEY (`icveconf`),
        '  KEY `chost` (`chost`),
        '  KEY `iActivo` (`iActivo`),
        '  KEY `ccveusuario` (`ccveusuario`),
        '  KEY `ccveusuariosuspende` (`ccveusuariosuspende`)
        ') ENGINE=MyISAM AUTO_INCREMENT=2 DEFAULT CHARSET=latin1;

        Dim icveconf As Integer = 2
        tb = frmInterface.tb_Recordset_MySQL_local("Select cpuerto, cdireccion, cclave, " & _
                                                  "chost, cremitente, ccorreoremitente " & _
                                                  "from cat_confserversmtp where iActivo = 1 and icveconf = " & icveconf & "")
        If tb.Rows.Count = 0 Then
            Return False
        End If

        mailman.SmtpSsl = False
        mailman.SmtpPort = tb.Rows(0).Item(0).ToString
        mailman.SmtpHost = tb.Rows(0).Item(3).ToString
        mailman.SmtpUsername = tb.Rows(0).Item(1).ToString
        mailman.SmtpPassword = tb.Rows(0).Item(2).ToString

        ' Create a simple email.
        Dim email As New Chilkat.Email()
        'email.Body = mensajeCorreo
        email.AddHtmlAlternativeBody(mensajeCorreo)
        email.Subject = asuntoCorreo
        email.AddTo(nombreDest, direcEmail)
        email.From = tb.Rows(0).Item(4).ToString & " " & tb.Rows(0).Item(1).ToString
        email.AddFileAttachment2(Get_RutaTemporal() & "Orden de Internamiento.pdf", "pdf")

        ' Send mail.
        success = mailman.SendEmail(email)
        If success Then
            'If MensajeConfirma Then
            '    Funciones.Msj_Info("Correo electrónico enviado exitosamente")
            'End If
        Else
            'Funciones.Msj_Err("Error al enviar el correo de AVISO DE ORDEN DE INTERNAMIENTO: " & mailman.LastErrorText & vbCrLf & _
            '                  "¡¡¡IMPORTANTE!!!: " & vbCrLf & _
            '                  "1) Intente Guardar Nuevamente." & _
            '                  "2) En caso de persistir el error, Debe imprimir la Orden de Internamiento y Enviarla por otro medio al paciente y al medico tratante.")
            Return False
        End If
        '  Some SMTP servers do not actually send the email until
        '  the connection is closed.  In these cases, it is necessary to
        '  call CloseSmtpConnection for the mail to be  sent.
        '  Most SMTP servers send the email immediately, and it is
        '  not required to close the connection.  We'll close it here
        '  for the example:
        success = mailman.CloseSmtpConnection()
        If (success <> True) Then
            'Funciones.Msj_Err("Conexión del servidor SMTP no fue cerrada adecuadamente")
        End If

        Return True

    End Function

    ''' <summary>
    ''' Crea el archivo PDF de la orden de internamiento a enviar
    ''' </summary>
    ''' <param name="IdOrdenInternamiento">Folio de Orden de Internamiento</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function CrearPDF(ByVal IdOrdenInternamiento As Integer) As Boolean

        Try

            '-Codificación Previa --------------------------------------------------


            '-Asignacion de Variables --------------------------------------------------
            Dim Path As String = System.AppDomain.CurrentDomain.BaseDirectory
            Dim CrystalFile As String = PacienteFTP_Reportes("OrdenInternamiento.rpt")
            Dim reporte As New CrystalDecisions.CrystalReports.Engine.ReportDocument
            reporte.Load(CrystalFile)
            reporte.SetDatabaseLogon("UserReport", "UserG20")

            '-Asignacion de Filtro ----------------------------------------------------
            Dim Filtro As String
            Filtro = "{tb_cirugiascontrol.Id} = " & IdOrdenInternamiento

            reporte.RecordSelectionFormula = Filtro
            reporte.Refresh()

            '-Creación de PDF ----------------------------------------------------------
            Dim CrExportOptionsReporte As CrystalDecisions.Shared.ExportOptions
            CrExportOptionsReporte = reporte.ExportOptions
            Dim RutaTemp As String = ""
            RutaTemp = Funciones.CrearPDF(CrExportOptionsReporte, "Orden de Internamiento")
            If RutaTemp = "" Then
                Return False
            Else
                Try
                    reporte.Export()
                Catch ex As Exception
                    Return False
                End Try
            End If

            ''-Auditoria de Impresion ----------------------------------------------------------


            '-Cerrar Conexión de Reporte a Base de Datos--------------------------------------------------------
            reporte.Close()

            Return True

        Catch ex As Exception
            Return False
        End Try

        Return True

    End Function

    ''' <summary>
    ''' Valida que la dirección de correo del destinatario sea válida
    ''' </summary>
    ''' <param name="direccionEmail"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function validaDireccionCorreo(ByRef direccionEmail As String) As Boolean

        Try
            'Creamos nuestro objeto, el constructor recive la cadena como parametro
            Dim mail As New System.Net.Mail.MailAddress(direccionEmail)
            'Al crear nuestro objeto evalua la cadena, y si es correcta no se produce
            'ningun error
            Return True
        Catch ex As Exception
            'En caso de que el formato de la cadena sea incorrecto nos produce una exepcion
            'del tipo FormatException, ni necesidad tenemos que escribir el mensaje de error
            'simplemente lo obtenemos de la exepcion
            ' '' ''Funciones.Msj_Err("Dirección de correo No Valida" + ex.Message)
            Return False
        End Try

    End Function

#End Region

#Region "FTP"

    ''' <summary>
    ''' Devuelve la ruta donde se descarga el archivo de crystal
    ''' </summary>
    ''' <param name="NombreArchivoReporte">Nombre del Reporte Incluyendo la Extención</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function PacienteFTP_Reportes(NombreArchivoReporte As String) As String

        Try

            Dim RutaTemp As String = System.IO.Path.Combine(System.IO.Path.GetTempPath() & "Histomedic\")
            RutaTemp = System.IO.Path.Combine(RutaTemp & NombreArchivoReporte)
            Try
                System.IO.File.Delete(RutaTemp)
            Catch ex As Exception
            End Try

            Dim adress As String = frmInterface.FTP_IP & GetRutaFTP(CARPETAS_FTP.REPORTES)
            adress = adress & "/" & NombreArchivoReporte
            Dim destinationFilename As String = RutaTemp
            Dim usuario As String = frmInterface.FTP_USUARIO
            Dim pwd As String = frmInterface.FTP_PASSWORD
            Try
                My.Computer.Network.DownloadFile(adress, destinationFilename, usuario, pwd)
                Return destinationFilename
            Catch ex As Exception
            End Try

        Catch ex As Exception
            Return ""
        End Try

        Return ""

    End Function

#Region "FTP"

    Shared Function GetRutaFTP(Carpetaftp As CARPETAS_FTP)

        Select Case Carpetaftp

            'Case CARPETAS_FTP.TB_ESTDIOSAPOYORESVIDEO
            '    Return "TB_ESTDIOSAPOYORESVIDEO"
            'ok

            Case CARPETAS_FTP.REPORTES
                Return "Reportes"

            Case CARPETAS_FTP.TB_ESTUDIOSAPOYORESESCRITO
                Return "TB_ESTUDIOSAPOYORESESCRITO"
                'ok

            Case CARPETAS_FTP.TB_FOTOSPADECIMIENTO
                Return "TB_FOTOSPADECIMIENTO"
                'ok

            Case CARPETAS_FTP.TB_PACIENTE
                Return "TB_PACIENTE"
                'ok

            Case CARPETAS_FTP.TB_PACIENTE_HISTORICO_IDE
                Return "TB_PACIENTE_HISTORICO_IDE"
                'ok

            Case CARPETAS_FTP.TB_TS_DOCTOS
                Return "TB_TS_DOCTOS"
                'ok

            Case CARPETAS_FTP.TB_ECE_HISTORICO
                Return "TB_ECE_HISTORICO"
                'ok

            Case CARPETAS_FTP.TB_TS_PASEVISITA
                Return "TB_TS_PASEVISITA"
                'ok

            Case CARPETAS_FTP.TB_EXPLESPESQUEMA
                Return "TB_EXPLESPESQUEMA"
                'ok

            Case CARPETAS_FTP.TB_VIDEOSPADECIMIENTO
                Return "TB_VIDEOSPADECIMIENTO"
                'ok

            Case CARPETAS_FTP.TB_HOJA_VIOLENCIALESION
                Return "TB_HOJA_VIOLENCIALESION"
                'ok

            Case CARPETAS_FTP.TB_GUIASCLINICAS
                Return "TB_GUIASCLINICAS"
                'ok

            Case CARPETAS_FTP.TB_MATERIALES
                Return "TB_MATERIALES"
                'ok

            Case CARPETAS_FTP.TB_TS_ESTUDIOSOCIOECONOMICO
                Return "TB_TS_ESTUDIOSOCIOECONOMICO"
                'ok

            Case CARPETAS_FTP.TB_ESP_END_GRAFICOS
                Return "TB_ESP_END_GRAFICOS"
                'ok

            Case CARPETAS_FTP.TB_FOTOSPADECIMIENTO_PDF
                Return "TB_FOTOSPADECIMIENTO_PDF"
                'ok

            Case CARPETAS_FTP.TB_ESP_ODONTO_ODONTOGRAMA
                Return "TB_ESP_ODONTO_ODONTOGRAMA"
                'ok

            Case CARPETAS_FTP.TB_ESP_GINECO_NOTAPOSPARTO
                Return "TB_ESP_GINECO_NOTAPOSPARTO"

            Case CARPETAS_FTP.TB_NOTAMEDICA_POSTOPERATORIA
                Return "TB_NOTAMEDICA_POSTOPERATORIA"

            Case CARPETAS_FTP.TB_ESP_GINECO_VIGILA_2MITADEMB_ESQUEMA
                Return "TB_ESP_GINECO_VIGILA_2MITADEMB_ESQUEMA"

            Case CARPETAS_FTP.TB_EXPLORACIONGENRAL
                Return "TB_EXPLORACIONGENRAL"

            Case CARPETAS_FTP.TB_NOTAMEDICA_PREOPERATORIA
                Return "TB_NOTAMEDICA_PREOPERATORIA"

            Case CARPETAS_FTP.TB_ESP_GINECO_CRECUTERINO
                Return "TB_ESP_GINECO_CRECUTERINO"

            Case CARPETAS_FTP.TB_ESP_GINECO_INDICEGANANCIA
                Return "TB_ESP_GINECO_INDICEGANANCIA"

            Case CARPETAS_FTP.TB_ESTUDIOSAPOYORESIMAGEN
                Return "TB_ESTUDIOSAPOYORESIMAGEN"

            Case CARPETAS_FTP.TB_ESTUDIOSAPOYORESDICOM
                Return "TB_ESTUDIOSAPOYORESDICOM"

        End Select

        Return ""

    End Function

    Public Enum CARPETAS_FTP

        'TB_ESTDIOSAPOYORESVIDEO
        TB_ESTUDIOSAPOYORESESCRITO
        TB_PACIENTE
        TB_PACIENTE_HISTORICO_IDE
        TB_TS_DOCTOS
        TB_ECE_HISTORICO
        TB_TS_PASEVISITA
        TB_GUIASCLINICAS
        TB_ESP_END_GRAFICOS
        'TB_CIRUGIAS

        TB_ESP_ODONTO_ODONTOGRAMA

        TB_EXPLESPESQUEMA

        TB_FOTOSPADECIMIENTO
        TB_FOTOSPADECIMIENTO_PDF
        TB_HOJA_VIOLENCIALESION
        TB_MATERIALES
        TB_TS_ESTUDIOSOCIOECONOMICO
        TB_VIDEOSPADECIMIENTO
        REPORTES
        TB_ESP_GINECO_NOTAPOSPARTO
        TB_NOTAMEDICA_POSTOPERATORIA
        TB_ESP_GINECO_VIGILA_2MITADEMB_ESQUEMA
        TB_EXPLORACIONGENRAL
        TB_NOTAMEDICA_PREOPERATORIA
        TB_ESP_GINECO_CRECUTERINO
        TB_ESP_GINECO_INDICEGANANCIA

        TB_ESTUDIOSAPOYORESIMAGEN
        TB_ESTUDIOSAPOYORESDICOM

    End Enum

#End Region

#End Region

End Class

Public Class EnviarCorreos

    Shared IdTabla As Integer
    Shared ccvepaciente As String
    Shared tb_enviar As DataTable
    Shared cadXML As String


    Shared email As String
    Shared Property pty_email() As String
        Get
            Return email
        End Get
        Set(value As String)
            email = value
        End Set
    End Property

    Shared Nom_Destinatario As String
    Shared Property pty_Nom_Destinatario() As String
        Get
            Return Nom_Destinatario
        End Get
        Set(value As String)
            Nom_Destinatario = value
        End Set
    End Property


    Shared asunto As String
    Shared Property pty_asunto() As String
        Get
            Return asunto
        End Get
        Set(value As String)
            asunto = value
        End Set
    End Property


    Shared mensaje As String
    Shared Property pty_mensaje() As String
        Get
            Return mensaje
        End Get
        Set(value As String)
            mensaje = value
        End Set
    End Property

    Shared valido As String
    Shared Property pty_valido() As String
        Get
            Return valido
        End Get
        Set(value As String)
            valido = value
        End Set
    End Property

    ''' <summary>
    ''' Guardar un nuevo registro o Actualizar los datos del registro seleccionado
    ''' </summary>
    ''' <remarks></remarks>
    Shared Function EnviarNotificacionGeneral(Nom_Destinatario As String, email As String, asunto As String, mensaje As String) As Boolean

        Try

            pty_Nom_Destinatario = Nom_Destinatario
            pty_email = email
            pty_asunto = asunto
            pty_mensaje = mensaje


            'Me.lblMensajeEspera.Text = "Validando Datos del Destinatario...."
            If ValidaDatosDest() = False Then
                Exit Function
            End If

            'Me.lblMensajeEspera.Text = "Validando Datos del Correo...."
            If ValidaDatosCorreo() = False Then
                Exit Function
            End If


            '== Rutina para varios correos.== == == == 
            Dim correos As String() = pty_email.Split(New [Char]() {","c})

            For i As Integer = 0 To correos.Length - 1
                If validaDireccionCorreo(correos(i).Trim) Then
                    EnviaEmail(pty_Nom_Destinatario, correos(i).Trim, pty_asunto, pty_mensaje)
                End If
            Next

            Return True

        Catch ex As Exception
        End Try

        Return False

    End Function


#Region "Funciones"

    ''' <summary>
    ''' Valida los datos del Destinatario
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function ValidaDatosDest() As Boolean

        If pty_Nom_Destinatario = "" Then
            'Funciones.Msj_Err("Debe indicar el nombre del destinatario")
            pty_valido = False
            Return False
        End If

        If pty_email = "" Then
            'Funciones.Msj_Err("Debe indicar la dirección de correo electrónico")
            pty_valido = False
            Return False
        End If

        'If validaDireccionCorreo(pty_email) = False Then
        '    pty_valido = False
        '    Return False
        'End If

        Return True

    End Function

    ''' <summary>
    ''' Valida los datos del contendio del correo.
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function ValidaDatosCorreo() As Boolean

        If pty_asunto = "" Then
            If Funciones.Msj_Adv("No se ha especificado en el correo el asunto,  ¿Deseas continuar y enviarlo de todos modos?") = False Then
                pty_valido = False
                Return False
            End If
        End If

        If pty_mensaje = "" Then
            If Funciones.Msj_Adv("No se ha especificado en el correo ningún mensaje,  ¿Deseas continuar y enviarlo de todos modos?") = False Then
                pty_valido = False
                Return False
            End If
        End If

        Return True

    End Function

    ''' <summary>
    ''' Valida que la dirección de correo del destinatario sea válida
    ''' </summary>
    ''' <param name="direccionEmail"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Shared Function validaDireccionCorreo(ByRef direccionEmail As String) As Boolean

        Try
            'Creamos nuestro objeto, el constructor recive la cadena como parametro
            Dim mail As New System.Net.Mail.MailAddress(direccionEmail)
            'Al crear nuestro objeto evalua la cadena, y si es correcta no se produce
            'ningun error
            Return True
        Catch ex As Exception
            'En caso de que el formato de la cadena sea incorrecto nos produce una exepcion
            'del tipo FormatException, ni necesidad tenemos que escribir el mensaje de error
            'simplemente lo obtenemos de la exepcion
            'Funciones.Msj_Err("Dirección de correo " & direccionEmail & " No Valida. Error: " + ex.Message)
            Return False
        End Try

    End Function


    ''' <summary>
    ''' Crea y envia el correo electronico. Retorna true si el correo se creó y fue enviado correctamente.
    ''' </summary>
    ''' <param name="nombreDest">Nombre del detinatario</param>
    ''' <param name="direcEmail">Dirección de correo electronico </param>
    ''' <param name="asuntoCorreo">Asunto</param>
    ''' <param name="mensajeCorreo">Mensaje</param>
    ''' <remarks></remarks>
    Shared Function EnviaEmail(ByVal nombreDest As String, ByVal direcEmail As String, ByVal asuntoCorreo As String, ByVal mensajeCorreo As String) As Boolean


        ' Create a mailman object for sending email.
        Dim mailman As New Chilkat.MailMan()

        ' Any string passed to UnlockComponent automatically begins a 30-day trial.
        Dim success As Boolean
        success = mailman.UnlockComponent("MAIL87654321_3C7B9122j163")
        If (success <> True) Then
            MsgBox(mailman.LastErrorText)
            Return False
        End If

        tb_enviar = frmInterface.tb_Recordset_MySQL_local("Select cpuerto, cdireccion, cclave, " & _
                                                          "chost, cremitente, ccorreoremitente " & _
                                                          "from cat_confserversmtp where iActivo = 1")
        If tb_enviar.Rows.Count = 0 Then
            Return False
        End If

        mailman.SmtpSsl = False
        mailman.SmtpPort = tb_enviar.Rows(0).Item(0).ToString
        mailman.SmtpHost = tb_enviar.Rows(0).Item(3).ToString
        mailman.SmtpUsername = tb_enviar.Rows(0).Item(1).ToString
        mailman.SmtpPassword = tb_enviar.Rows(0).Item(2).ToString

        ' Create a simple email.
        Dim email As New Chilkat.Email()
        'email.Body = mensajeCorreo
        email.AddHtmlAlternativeBody(mensajeCorreo)
        email.Subject = asuntoCorreo
        email.AddTo(nombreDest, direcEmail)
        email.From = tb_enviar.Rows(0).Item(4).ToString & " " & tb_enviar.Rows(0).Item(1).ToString
        'email.From = frm_Login.cat_consultorio.pty_cNombreAbreviado & " " & tb_enviar.Rows(0).Item(1).ToString

        ' Send mail.
        success = mailman.SendEmail(email)
        If success Then
            'Funciones.Msj_Info("Correo electrónico enviado exitosamente")
        Else
            'Funciones.Msj_Err("Error al enviar el correo: " & mailman.LastErrorText)
            Return False
        End If

        '  Some SMTP servers do not actually send the email until
        '  the connection is closed.  In these cases, it is necessary to
        '  call CloseSmtpConnection for the mail to be  sent.
        '  Most SMTP servers send the email immediately, and it is
        '  not required to close the connection.  We'll close it here
        '  for the example:
        success = mailman.CloseSmtpConnection()
        If (success <> True) Then
            'Funciones.Msj_Err("Conexión del servidor SMTP no fue cerrada adecuadamente")
        End If

        Return True

    End Function



#End Region

End Class
