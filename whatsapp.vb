Imports System
Imports System.Net
Imports System.IO
Imports System.Text

Public Class whatsapp

    Shared Function EnviarMensajeWhatsAppAPI(params As Parameters) As Boolean

        ' URL de facebook para usar las plantillas 
        Dim url As String = String.Format("https://graph.facebook.com/v22.0/{0}/messages", params.PhoneNumberID)

        ' Crear cuerpo de la peticion POST (En esta funcion se llenan las variables del cuerpo de la plantilla de WhatsApp) 
        Dim requestData As String = BuildJsonRequest(params)

        ' Consumir la API (Hacer peticion POST) ya con los datos cargados (Toda esta parte de abajo es fija, no necesita modificar nada aqui)
        'Console.WriteLine(vbCrLf & "URL de destino:")
        'Console.WriteLine(url)

        'Console.WriteLine(vbCrLf & "JSON a enviar:")
        'Console.WriteLine(requestData)

        Try
            Dim request As HttpWebRequest = DirectCast(WebRequest.Create(url), HttpWebRequest)
            request.Method = "POST"
            request.Headers.Add("Authorization", "Bearer " & params.AccessToken)
            'request.ContentType = "application/json; charset=UTF-8"
            request.ContentType = "application/json"

            ' Escribir los datos
            Dim byteArray As Byte() = Encoding.UTF8.GetBytes(requestData)
            request.ContentLength = byteArray.Length

            Using dataStream As Stream = request.GetRequestStream()
                dataStream.Write(byteArray, 0, byteArray.Length)
            End Using

            ' Obtener respuesta
            'Console.WriteLine(vbCrLf & "Enviando petición a WhatsApp API...")
            Using response As HttpWebResponse = DirectCast(request.GetResponse(), HttpWebResponse)
                Using reader As New StreamReader(response.GetResponseStream())
                    reader.ReadToEnd()
                    'Console.WriteLine(vbCrLf & "Respuesta recibida:")
                    'Console.WriteLine($"Código de estado: {response.StatusCode}")
                    'Console.WriteLine(responseText)
                End Using
            End Using

            Return True
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
            Return False
        Catch ex As Exception

            'Console.WriteLine(vbCrLf & "ERROR general:")
            'Console.WriteLine(ex.Message)
            Return False
        End Try

        Return False

    End Function


    Shared Function BuildJsonRequest(params As Parameters) As String

        ' Datos de ejemplo - IMPORTAMTE: Aqui si se modifica la estructura de acuerdo a la cantidad de variables de la plantilla
        ' IMPORTANTE en caso de que la plantilla no lleve botones se deben ELIMINAR ambos (desde la linea 104 hasta la 112)



        If params.TemplateName = "confrimacion_cita" Then

            ' == == == PROBADO Y VERFICADO == == == ==

            ' Estimado(a) *{{1}}*.

            'Su *Cita Médica* ha sido registrada con los siguientes datos:

            '🏨 Unidad Médica *{{2}}* .
            '✔️ Folio Cita: *{{3}}*.
            '📅 Fecha: *{{4}}*.
            '🕒 Hora: *{{5}}*.
            '🩺 Medico: *{{6}}*.
            '🚩 Consultorio *{{7}}*.

            'Para cualquier consulta o aclaración puede contactarnos al teléfono: *{{8}}*

            '_Favor de presentarse *{{9}}* minutos antes de su cita para la toma de signos vitales, en caso de aplicar._

            Return "{""messaging_product"":""whatsapp""," & _
                       """to"":""" & params.ToNumber & """," & _
                       """type"":""template""," & _
                       """template"":{""name"":""" & params.TemplateName & """," & _
                       """language"":{""code"":""" & params.LanguageCode & """}," & _
                       """components"":[ " & _
                       "{""type"":""header"",""parameters"":[" &
                          "{""type"":""image"",""image"":{""link"":""" & params.HeaderImageURL & """}}" &
                          "]}," &
                       "{""type"":""body"",""parameters"":[" & _
                           "{""type"":""text"",""text"":""" & params.UserName & """}," & _
                           "{""type"":""text"",""text"":""" & params.MedicalUnit & """}," & _
                           "{""type"":""text"",""text"":""" & params.FolioCitaOI & """}," & _
                           "{""type"":""text"",""text"":""" & params.AppointmentDate & """}," & _
                           "{""type"":""text"",""text"":""" & params.AppointmentHour & """}," & _
                           "{""type"":""text"",""text"":""" & params.DoctorName & """}," & _
                           "{""type"":""text"",""text"":""" & params.Office & """}," & _
                           "{""type"":""text"",""text"":""" & params.MedicalPhone & """}," & _
                           "{""type"":""text"",""text"":""" & params.TiempoPresentarseAntesIngreso & """}]} " & _
                   "]}}"




        ElseIf params.TemplateName = "cancelacion_cita" Then

            ' === Probado y Veriifcado == == ==

            ' Estimado(a) *{{1}}*.
            'Le notificamos que su cita en *{{2}}* ha sido *cancelada*.
            '*Datos de Cita Cancelada:*
            '🔹Fecha: *{{3}}*.
            '🔹Hora: *{{4}}*.
            '🔹Medico: *{{5}}*.
            '🔹Consultorio *{{6}}*
            'Para cualquier consulta o aclaración puede contactarnos al teléfono: *{{7}}*

            Return "{""messaging_product"":""whatsapp""," & _
                   """to"":""" & params.ToNumber & """," & _
                   """type"":""template""," & _
                   """template"":{""name"":""" & params.TemplateName & """," & _
                   """language"":{""code"":""" & params.LanguageCode & """}," & _
                   """components"":[{""type"":""body""," & _
                   """parameters"":[" & _
                       "{""type"":""text"",""text"":""" & params.UserName & """}," & _
                       "{""type"":""text"",""text"":""" & params.MedicalUnit & """}," & _
                       "{""type"":""text"",""text"":""" & params.AppointmentDate & """}," & _
                       "{""type"":""text"",""text"":""" & params.AppointmentHour & """}," & _
                       "{""type"":""text"",""text"":""" & params.DoctorName & """}," & _
                       "{""type"":""text"",""text"":""" & params.Office & """}," & _
                       "{""type"":""text"",""text"":""" & params.MedicalPhone & """}]} " & _
                   "]}}"


            'Return "{""messaging_product"":""whatsapp""," & _
            '       """to"":""" & params.ToNumber & """," & _
            '       """type"":""template""," & _
            '       """template"":{""name"":""" & params.TemplateName & """," & _
            '       """language"":{""code"":""" & params.LanguageCode & """}," & _
            '       """components"":[{""type"":""body""," & _
            '       """parameters"":[" & _
            '           "{""type"":""text"",""text"":""" & params.UserName & """}," & _
            '           "{""type"":""text"",""text"":""" & params.MedicalUnit & """}," & _
            '           "{""type"":""text"",""text"":""" & params.AppointmentDate & """}," & _
            '           "{""type"":""text"",""text"":""" & params.AppointmentHour & """}," & _
            '           "{""type"":""text"",""text"":""" & params.DoctorName & """}," & _
            '           "{""type"":""text"",""text"":""" & params.Office & """}]}," & _
            '           "{""type"":""button"",""sub_type"":""url""," & _
            '           """index"":0,""parameters"":[" & _
            '           "{""type"":""text"",""text"":""" & params.URLBotonUno & """}]}" & _
            '       "]}}"



        ElseIf params.TemplateName = "recordatorio_cita" Then

            ' Estimado(a) *{{1}}*, su cita para el día *{{2}}* a las *{{3}}* ha sido confirmada exitosamente.
            '_Recuerde llegar 5 minutos antes de su cita._
            '_Por favor, no responder este mensaje._

            Return "{""messaging_product"":""whatsapp""," & _
                   """to"":""" & params.ToNumber & """," & _
                   """type"":""template""," & _
                   """template"":{""name"":""" & params.TemplateName & """," & _
                   """language"":{""code"":""" & params.LanguageCode & """}," & _
                   """components"":[{""type"":""body""," & _
                   """parameters"":[" & _
                       "{""type"":""text"",""text"":""" & params.UserName & """}," & _
                       "{""type"":""text"",""text"":""" & params.AppointmentDate & """}," & _
                       "{""type"":""text"",""text"":""" & params.AppointmentHour & """}]}" & _
                   "]}}"


        ElseIf params.TemplateName = "reagendar_cita" Then

            ' Estimado(a) *{{1}}*, su cita para el día *{{2}}* a las *{{3}}* ha sido reagendada exitosamente para el día *{{4}}* a las *{{5}}*.
            '_Recuerde llegar 5 minutos antes de su cita._
            '_Por favor, no responder este mensaje._

            Return "{""messaging_product"":""whatsapp""," & _
                   """to"":""" & params.ToNumber & """," & _
                   """type"":""template""," & _
                   """template"":{""name"":""" & params.TemplateName & """," & _
                   """language"":{""code"":""" & params.LanguageCode & """}," & _
                   """components"":[{""type"":""body""," & _
                   """parameters"":[" & _
                       "{""type"":""text"",""text"":""" & params.UserName & """}," & _
                       "{""type"":""text"",""text"":""" & params.AppointmentDate & """}," & _
                       "{""type"":""text"",""text"":""" & params.AppointmentHour & """}," & _
                       "{""type"":""text"",""text"":""" & params.AppointmentNewDate & """}," & _
                       "{""type"":""text"",""text"":""" & params.AppointmentNewHour & """}]}" & _
                   "]}}"


        ElseIf params.TemplateName = "orden_internamiento_tratamiento" Then

            'Estimado(a) *{{1}}*.

            'Se ha programado su ingreso para *{{2}}*

            '*Lugar:*
            '🏨 Unidad Médica: *{{3}}*.
            '🚩 Domicilio: *{{4}}*.
            '📞 Teléfono: *{{5}}*.

            '*Datos de Ingreso:*
            '📌 Orden Internamiento: *{{6}}*
            '📅 Fecha: *{{7}}*.
            '🕒 Hora: *{{8}}*.
            '      Motivo: *{{9}}*.
            '      Indicaciones previas al ingreso: *{{10}}*.

            '_Favor de presentarse en recepción *{{11}}* minutos antes para su registro._

            Return "{""messaging_product"":""whatsapp""," & _
                       """to"":""" & params.ToNumber & """," & _
                       """type"":""template""," & _
                       """template"":{""name"":""" & params.TemplateName & """," & _
                       """language"":{""code"":""" & params.LanguageCode & """}," & _
                       """components"":[ " & _
                       "{""type"":""header"",""parameters"":[" &
                          "{""type"":""image"",""image"":{""link"":""" & params.HeaderImageURL & """}}" &
                          "]}," &
                       "{""type"":""body"",""parameters"":[" & _
                           "{""type"":""text"",""text"":""" & params.UserName & """}," & _
                           "{""type"":""text"",""text"":""" & params.TipoIngreso & """}," & _
                           "{""type"":""text"",""text"":""" & params.MedicalUnit & """}," & _
                           "{""type"":""text"",""text"":""" & params.MedicalAdress & """}," & _
                           "{""type"":""text"",""text"":""" & params.MedicalPhone & """}," & _
                           "{""type"":""text"",""text"":""" & params.FolioCitaOI & """}," & _
                           "{""type"":""text"",""text"":""" & params.AppointmentDate & """}," & _
                           "{""type"":""text"",""text"":""" & params.AppointmentHour & """}," & _
                           "{""type"":""text"",""text"":""" & params.AppointmentIssue & """}," & _
                           "{""type"":""text"",""text"":""" & params.IndicacionesPacienteOI & """}," & _
                           "{""type"":""text"",""text"":""" & params.TiempoPresentarseAntesIngreso & """}]}" & _
                      "]}}"


        ElseIf params.TemplateName = "ingresos_clinica" Then

            'Validado y probado

            'Estimado(a) *{{1}}*.

            'Se ha programado su ingreso para *{{2}}*

            '*Lugar de la Cirugía:*
            '🏨 Unidad Médica *{{3}}*.
            '🚩 Domicilio: *{{4}}*.
            '📞 Teléfono: *{{5}}*.

            '*Datos de la Cirugía Programada:*
            '📌 Orden Internamiento: *{{6}}*.
            '📅 Fecha: *{{7}}*.
            '🕒 Hora: *{{8}}*.
            ' Procedimiento: *{{9}}*.
            ' Indicaciones previas al ingreso: *{{10}}*.

            '_Favor de presentarse en recepción *{{11}}* hora(s) antes para su registro e ingreso._

            Return "{""messaging_product"":""whatsapp""," & _
                       """to"":""" & params.ToNumber & """," & _
                       """type"":""template""," & _
                       """template"":{""name"":""" & params.TemplateName & """," & _
                       """language"":{""code"":""" & params.LanguageCode & """}," & _
                       """components"":[ " & _
                       "{""type"":""header"",""parameters"":[" &
                          "{""type"":""image"",""image"":{""link"":""" & params.HeaderImageURL & """}}" &
                          "]}," &
                       "{""type"":""body"",""parameters"":[" & _
                           "{""type"":""text"",""text"":""" & params.UserName & """}," & _
                           "{""type"":""text"",""text"":""" & params.TipoIngreso & """}," & _
                           "{""type"":""text"",""text"":""" & params.MedicalUnit & """}," & _
                           "{""type"":""text"",""text"":""" & params.MedicalAdress & """}," & _
                           "{""type"":""text"",""text"":""" & params.MedicalPhone & """}," & _
                           "{""type"":""text"",""text"":""" & params.FolioCitaOI & """}," & _
                           "{""type"":""text"",""text"":""" & params.AppointmentDate & """}," & _
                           "{""type"":""text"",""text"":""" & params.AppointmentHour & """}," & _
                           "{""type"":""text"",""text"":""" & params.AppointmentIssue & """}," & _
                           "{""type"":""text"",""text"":""" & params.IndicacionesPacienteOI & """}," & _
                           "{""type"":""text"",""text"":""" & params.TiempoPresentarseAntesIngreso & """}]}" & _
                      "]}}"

        End If

    End Function

End Class

Public Class Parameters

    Public Property AccessToken As String
    Public Property PhoneNumberID As String
    Public Property ToNumber As String
    Public Property TemplateName As String
    Public Property LanguageCode As String
    Public Property UserName As String
    Public Property MedicalUnit As String
    Public Property AppointmentDate As String
    Public Property AppointmentHour As String
    Public Property DoctorName As String
    Public Property Office As String
    Public Property URLBotonUno As String
    Public Property URLBotonDos As String
    Public Property AppointmentNewDate As String
    Public Property AppointmentNewHour As String
    Public Property TipoIngreso As String
    Public Property TiempoPresentarseAntesIngreso As String
    Public Property IndicacionesPacienteOI As String
    Public Property HeaderImageURL As String
    Public Property MedicalAdress As String
    Public Property MedicalPhone As String
    Public Property AppointmentIssue As String
    Public Property FolioCitaOI As String

End Class
