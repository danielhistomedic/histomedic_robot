Option Explicit On
Option Strict On

Imports System
Imports System.Data
Imports System.Collections.Generic

' Simulador exclusivo de pruebas: no crea conexiones y no pertenece al proyecto del Robot.
Module CargosCamaTests
    Private aprobadas As Integer

    Private Sub Comprobar(ByVal condicion As Boolean, ByVal nombre As String)
        If Not condicion Then Throw New Exception("FALLÓ: " & nombre)
        aprobadas += 1
    End Sub

    Public Sub Main()
        Dim ingreso As New DateTime(2026, 10, 1, 18, 47, 0)
        Comprobar(HisPeriodosCama.Total(ingreso, ingreso) = 1, "cargo anticipado")
        Comprobar(HisPeriodosCama.Total(ingreso, ingreso.AddHours(12)) = 1, "12 horas")
        Comprobar(HisPeriodosCama.Total(ingreso, ingreso.AddDays(1).AddTicks(-1)) = 1, "instante anterior a 24 horas")
        Comprobar(HisPeriodosCama.Total(ingreso, ingreso.AddDays(1)) = 2, "24 horas exactas")
        Comprobar(HisPeriodosCama.Total(ingreso, ingreso.AddDays(2)) = 3, "48 horas exactas")
        Comprobar(HisPeriodosCama.Total(ingreso, ingreso.AddDays(3)) = 4, "72 horas exactas")
        Comprobar(HisPeriodosCama.Total(ingreso, ingreso.AddTicks(-1)) = 0, "referencia futura")
        Comprobar(Not HisPeriodosCama.Completo(New Integer() {1}, 2), "fragmento faltante")
        Comprobar(Not HisPeriodosCama.Completo(New Integer() {1, 1}, 2), "fragmento repetido")
        Comprobar(HisPeriodosCama.Completo(New Integer() {2, 1}, 2), "fragmentos confirmados en otro orden")

        Dim caso As New Escenario(72)
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 4 AndAlso caso.Periodos.Rows.Count = 4, "recuperación de cuatro periodos")
        Comprobar(caso.Completos() = 4, "todos los periodos confirmados")
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 4, "reinicio y repetición sin duplicar")
        caso.Material = "OTRA-CAMA"
        caso.Recepcion = 11
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 4, "cambio de cama y recepción conserva identidad original")

        caso = New Escenario(2)
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 1, "primer cargo con dos horas")
        Comprobar(CDbl(caso.Cargos.Rows(0)("publico")) = 150, "precio público del material")
        Comprobar(CDbl(caso.Cargos.Rows(0)("iPrecioVenta")) = 100, "precio con IVA separado")
        caso = New Escenario(72)
        caso.SembrarPeriodo(1)
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 4, "reponer solamente los tres periodos faltantes")

        caso = New Escenario(2) With {.Abierta = False}
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 0 AndAlso caso.Periodos.Rows.Count = 0, "cuenta cerrada")
        caso = New Escenario(2) With {.Activo = False}
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 0, "paciente egresado")
        caso = New Escenario(2) With {.CambiarCama = True}
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 0 AndAlso caso.Periodos.Rows.Count = 0, "cambio de cama durante validación")
        caso = New Escenario(2) With {.CambiarAlInsertar = True}
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 0 AndAlso caso.Periodos.Rows.Count = 1, "INSERT comprueba vigencia y conserva el periodo en proceso")
        caso.CambiarAlInsertar = False
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 0, "periodo en proceso sin cargo no se reintenta automáticamente")

        caso = New Escenario(2) With {.Tablas = False}
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 0 AndAlso caso.BusquedasCuenta = 0, "sin tablas auxiliares no buscar cuenta ni escribir")
        caso = New Escenario(2) With {.Indices = False}
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 0 AndAlso caso.BusquedasCuenta = 0, "sin índices únicos no escribir")
        caso = New Escenario(2) With {.FallaLectura = True}
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 0 AndAlso caso.Periodos.Rows.Count = 0, "error de lectura no equivale a ausencia")
        caso = New Escenario(2) With {.FaltaPrecio = True}
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 0 AndAlso caso.Periodos.Rows.Count = 0, "precio ausente no se inventa")
        caso = New Escenario(2) With {.ReferenciaInvalida = True}
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 0, "fecha de respaldo no verificable")
        caso = New Escenario(2) With {.Paquete = True, .FallaPaquete = True}
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 0, "error de paquete no equivale a sin cobertura")

        caso = New Escenario(2)
        caso.AgregarCargoManual("servidor_his")
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 1 AndAlso caso.Periodos.Rows.Count = 0, "cargos heredados requieren conciliación")
        caso = New Escenario(2)
        caso.AgregarCargoManual("manual")
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 2, "cargo manual no cuenta como periodo automático")

        caso = New Escenario(2) With {.Paquete = True, .Autorizado = 2}
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 1 AndAlso CInt(caso.Cargos.Rows(0)("iCubreSP")) = 7, "cobertura completa por paquete")
        caso = New Escenario(2) With {.Paquete = True, .Autorizado = 0.5}
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 2 AndAlso caso.Detalles.Rows.Count = 2 AndAlso caso.Completos() = 1, "dos fragmentos de un periodo")
        Comprobar(CDbl(caso.Cargos.Rows(0)("iCantidad")) = 0.5 AndAlso CInt(caso.Cargos.Rows(0)("iCubreSP")) = 0 AndAlso
                   CDbl(caso.Cargos.Rows(1)("iCantidad")) = 0.5 AndAlso CInt(caso.Cargos.Rows(1)("iCubreSP")) = 7, "excedente y cantidad cubierta")
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 2 AndAlso caso.Notificaciones = 1, "repetición no duplica fragmentos ni notificación")

        caso = New Escenario(2) With {.InicioIncierto = True}
        caso.Correr()
        caso.InicioIncierto = False
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 0 AndAlso caso.Periodos.Rows.Count = 1, "corte después de registrar el inicio y antes de planificar")
        caso = New Escenario(2) With {.Paquete = True, .Autorizado = 0.5, .FallaPlanParte = 2}
        caso.Correr()
        caso.FallaPlanParte = 0
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 0 AndAlso caso.Detalles.Rows.Count = 1, "corte durante plan no produce cobros")
        caso = New Escenario(2) With {.Paquete = True, .Autorizado = 0.5, .FallaCargoNumero = 2}
        caso.Correr()
        caso.FallaCargoNumero = 0
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 1 AndAlso caso.Completos() = 0, "reinicio entre cargos no vuelve a cobrar")
        caso = New Escenario(2) With {.CargoIncierto = True}
        caso.Correr()
        caso.CargoIncierto = False
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 1 AndAlso caso.Detalles.Rows(0).IsNull("id_cargo"), "respuesta perdida después del cargo no duplica")
        caso = New Escenario(2) With {.FallaVinculo = True}
        caso.Correr()
        caso.FallaVinculo = False
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 1 AndAlso caso.Completos() = 0, "error al vincular mantiene el periodo en proceso sin recobro")
        caso = New Escenario(2) With {.FallaConfirmar = True}
        caso.Correr()
        caso.FallaConfirmar = False
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 1 AndAlso caso.Completos() = 1, "confirmar vínculos completos tras reinicio sin insertar")
        caso = New Escenario(2) With {.ConfirmacionIncierta = True}
        caso.Correr()
        caso.ConfirmacionIncierta = False
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 1 AndAlso caso.Completos() = 1, "confirmación persistida con respuesta perdida")

        caso = New Escenario(2)
        caso.Correr()
        caso.Cargos.Rows.Clear()
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 0, "cargo vinculado eliminado no se recrea")
        caso = New Escenario(2)
        caso.Correr()
        caso.Cargos.Rows(0)("iPrecioVenta") = 999
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 1, "cargo modificado no se vuelve a cobrar")
        caso = New Escenario(2)
        caso.Correr()
        caso.Periodos.Rows(0)("id_ingreso") = 999
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 1, "control de otro ingreso exige conciliación")
        caso = New Escenario(2)
        caso.Correr()
        caso.Ingreso = caso.Ingreso.AddMinutes(1)
        caso.Correr()
        Comprobar(caso.Cargos.Rows.Count = 1, "referencia fija modificada no reinicia los periodos")

        Console.WriteLine("Pruebas aprobadas: " & aprobadas.ToString())
    End Sub

    Private Function Tabla(ByVal columnas As String, ParamArray fila() As Object) As DataTable
        Dim resultado As New DataTable()
        For Each columna As String In columnas.Split(","c)
            resultado.Columns.Add(columna, GetType(Object))
        Next
        If fila.Length > 0 Then resultado.Rows.Add(fila)
        Return resultado
    End Function

    Private Class Escenario
        Public Ingreso As DateTime
        Public ReadOnly Periodos As DataTable = Tabla("id,id_ingreso,id_cuenta,periodo,fecha_ingreso,numero_cuenta,id_cama,material,partes,estado")
        Public ReadOnly Detalles As DataTable = Tabla("id_periodo,parte,cantidad,precio,paquete,id_cargo")
        Public ReadOnly Cargos As DataTable = Tabla("icveticketecdetalle,icveticketec,cNumTicketEC,icvecama,ccvematerial,iCantidad,iPrecioVenta,iCubreSP,ccveusuario,publico")
        Public Material As String = "CAMA"
        Public Recepcion As Integer = 10
        Public Activo As Boolean = True
        Public Abierta As Boolean = True
        Public Tablas As Boolean = True
        Public Indices As Boolean = True
        Public Paquete As Boolean
        Public Autorizado As Double
        Public FallaLectura As Boolean
        Public FaltaPrecio As Boolean
        Public FallaPaquete As Boolean
        Public ReferenciaInvalida As Boolean
        Public CambiarCama As Boolean
        Public CambiarAlInsertar As Boolean
        Public InicioIncierto As Boolean
        Public CargoIncierto As Boolean
        Public FallaVinculo As Boolean
        Public FallaConfirmar As Boolean
        Public ConfirmacionIncierta As Boolean
        Public FallaPlanParte As Integer
        Public FallaCargoNumero As Integer
        Public BusquedasCuenta As Integer
        Public Notificaciones As Integer
        Private intentosCargo As Integer
        Private siguienteCargo As Long = 100

        Public Sub New(ByVal horas As Double)
            Dim ahora As DateTime = DateTime.Now.AddHours(-horas)
            Ingreso = ahora.AddTicks(-(ahora.Ticks Mod TimeSpan.TicksPerMinute))
        End Sub

        Public Sub SembrarPeriodo(ByVal periodo As Integer)
            ' Cargo de una ejecución anterior; la fecha fija no se altera para simular el paso del tiempo.
            Dim id As Long = Periodos.Rows.Count + 1
            Periodos.Rows.Add(id, 10, 20, periodo, Ingreso, "EC-H-TEST", 30, Material, 1, "COMPLETO")
            Detalles.Rows.Add(id, 1, 1, 100, 0, siguienteCargo)
            Cargos.Rows.Add(siguienteCargo, 20, "EC-H-TEST", 30, Material, 1, 100, 0, "servidor_his", 150)
            siguienteCargo += 1
        End Sub

        Public Sub AgregarCargoManual(ByVal usuario As String)
            Cargos.Rows.Add(siguienteCargo, 20, "EC-H-TEST", 30, Material, 1, 100, 0, usuario, 150)
            siguienteCargo += 1
        End Sub

        Public Function Completos() As Integer
            Dim cuenta As Integer = 0
            For Each fila As DataRow In Periodos.Rows
                If CStr(fila("estado")) = "COMPLETO" Then cuenta += 1
            Next
            Return cuenta
        End Function

        Public Sub Correr()
            ' Nueva instancia en cada llamada; solo sobrevive el estado simulado de las tres tablas.
            Dim servicio As New HisCargosHospitalizacionAutomaticos(AddressOf Consultar, AddressOf Insertar, AddressOf Escribir, "PRUEBA",
                Function(paciente, cama)
                    BusquedasCuenta += 1
                    Return "EC-H-TEST"
                End Function,
                Function(paciente, recepcion) If(ReferenciaInvalida, Ingreso.AddMinutes(1), Ingreso),
                Sub(mensaje) System.Diagnostics.Trace.WriteLine(mensaje))
            Try
                servicio.Ejecutar()
            Catch ex As InvalidOperationException
                If Tablas AndAlso Indices Then Throw
            End Try
        End Sub

        Private Function Consultar(ByVal sql As String, ByVal p As IDictionary(Of String, Object)) As DataTable
            If sql.Contains("WHERE 1=0") Then
                If Not Tablas Then Throw New InvalidOperationException("Tablas ausentes de prueba.")
                Return Tabla("id")
            End If
            If sql.Contains("information_schema.STATISTICS") Then
                Dim tablaIndices As DataTable = Tabla("TABLE_NAME,INDEX_NAME,SEQ_IN_INDEX,COLUMN_NAME,SUB_PART")
                If Not Indices Then Return tablaIndices
                tablaIndices.Rows.Add("robot_camas_periodos", "ux_periodo", 1, "id_ingreso", DBNull.Value)
                tablaIndices.Rows.Add("robot_camas_periodos", "ux_periodo", 2, "id_cuenta", DBNull.Value)
                tablaIndices.Rows.Add("robot_camas_periodos", "ux_periodo", 3, "periodo", DBNull.Value)
                tablaIndices.Rows.Add("robot_camas_detalles", "PRIMARY", 1, "id_periodo", DBNull.Value)
                tablaIndices.Rows.Add("robot_camas_detalles", "PRIMARY", 2, "parte", DBNull.Value)
                tablaIndices.Rows.Add("robot_camas_detalles", "ux_cargo", 1, "id_cargo", DBNull.Value)
                Return tablaIndices
            End If
            If sql.StartsWith("SELECT r.Id,r.ccvepaciente") Then
                If Not Activo Then Return Tabla("Id,ccvepaciente,cdscareaafectada,fchregistro,icvecama,material_cama")
                Return Tabla("Id,ccvepaciente,cdscareaafectada,fchregistro,icvecama,material_cama", Recepcion, "TEST", "HOSPITALIZACION", Ingreso, 30, Material)
            End If
            If sql.StartsWith("SELECT ccvematerial,cDescripcion") Then Return Tabla(
                "ccvematerial,cDescripcion,icveclave,ccveunidad,ccveanterior,iPrecioPublicoOficial,iSujetoCatalogoOficial,iExcentoIVA,productos_paquete_id",
                Material, "Cama de prueba", 1, "ser", "", 150, 0, "I", 0)
            If sql.StartsWith("SELECT icveticketec,cEstatus") Then Return Tabla("icveticketec,cEstatus", 20, If(Abierta, "ABIERTO", "CERRADO"))
            If sql.StartsWith("SELECT r.Id,r.fchhorarecepcion") Then Return Tabla("Id,fchhorarecepcion", 10, Ingreso)
            If sql.Contains("FROM tb_notamedica_egreso") Then Return Tabla("id")
            If sql.StartsWith("SELECT id,id_ingreso") Then
                If FallaLectura Then Throw New InvalidOperationException("Error de lectura simulado.")
                Return Periodos.Copy()
            End If
            If sql.StartsWith("SELECT a.parte") Then Return Vinculos(CLng(p("@id")))
            If sql.StartsWith("SELECT d.icveticketecdetalle") Then
                For Each cargo As DataRow In Cargos.Rows
                    Dim vinculado As Boolean = False
                    For Each detalle As DataRow In Detalles.Rows
                        If Not detalle.IsNull("id_cargo") AndAlso CLng(detalle("id_cargo")) = CLng(cargo("icveticketecdetalle")) Then vinculado = True
                    Next
                    If CStr(cargo("ccveusuario")) = "servidor_his" AndAlso Not vinculado Then Return Tabla("icveticketecdetalle", cargo("icveticketecdetalle"))
                Next
                Return Tabla("icveticketecdetalle")
            End If
            If sql.StartsWith("SELECT f.icvefactorsocial") Then Return Tabla("icvefactorsocial", 1)
            If sql.StartsWith("SELECT iPrecio FROM") Then
                If FaltaPrecio Then Return Tabla("iPrecio")
                Return Tabla("iPrecio", 116)
            End If
            If sql.StartsWith("SELECT paquete_id") Then
                If FallaPaquete Then Throw New InvalidOperationException("Fallo de paquete simulado.")
                If Paquete Then Return Tabla("paquete_id", 7)
                Return Tabla("paquete_id")
            End If
            If sql.StartsWith("SELECT iFactor") Then Return Tabla("iFactor", 1.16)
            If sql.StartsWith("SELECT id FROM tb_paquete_detalle") Then Return Tabla("id", 1)
            If sql.StartsWith("SELECT p.iCantidad") Then Return Tabla("cantidad,ccvematerial", Autorizado, Material)
            If sql.StartsWith("SELECT COALESCE(SUM") Then
                Dim suma As Double = 0
                For Each cargo As DataRow In Cargos.Rows
                    suma += CDbl(cargo("iCantidad"))
                Next
                Return Tabla("cantidad", suma)
            End If
            If sql.StartsWith("SELECT r.Id FROM") Then
                If CambiarCama OrElse Not Abierta OrElse Not Activo Then Return Tabla("Id")
                Return Tabla("Id", Recepcion)
            End If
            If sql.StartsWith("SELECT id FROM robot_camas_periodos") Then
                For Each periodo As DataRow In Periodos.Rows
                    If CInt(periodo("id_ingreso")) = CInt(p("@ingreso")) AndAlso CInt(periodo("id_cuenta")) = CInt(p("@cuenta")) AndAlso
                        CInt(periodo("periodo")) = CInt(p("@periodo")) Then Return Tabla("id", periodo("id"))
                Next
                Return Tabla("id")
            End If
            If sql.StartsWith("SELECT icvdiagnostico") Then Return Tabla("icvdiagnostico")
            If sql.StartsWith("SELECT e.fchTicketec") Then Return Tabla("fchTicketec,tiene_detalle", Ingreso.Date, If(Cargos.Rows.Count > 0, 1, 0))
            If sql.StartsWith("SELECT id,id_cuenta") Then
                Dim resultado As DataTable = Periodos.Clone()
                For Each fila As DataRow In Periodos.Rows
                    If CLng(fila("id")) = CLng(p("@id")) Then resultado.ImportRow(fila)
                Next
                Return resultado
            End If
            Throw New Exception("Consulta sin simular: " & sql)
        End Function

        Private Function Vinculos(ByVal id As Long) As DataTable
            Dim tablaVinculos As DataTable = Tabla("parte,cantidad,precio,paquete,id_cargo,icveticketecdetalle,icveticketec,cNumTicketEC,icvecama,ccvematerial,iCantidad,iPrecioVenta,iCubreSP,ccveusuario")
            For Each detalle As DataRow In Detalles.Rows
                If CLng(detalle("id_periodo")) <> id Then Continue For
                Dim fila As DataRow = tablaVinculos.NewRow()
                For Each nombre As String In New String() {"parte", "cantidad", "precio", "paquete", "id_cargo"}
                    fila(nombre) = detalle(nombre)
                Next
                If Not detalle.IsNull("id_cargo") Then
                    For Each cargo As DataRow In Cargos.Rows
                        If CLng(cargo("icveticketecdetalle")) <> CLng(detalle("id_cargo")) Then Continue For
                        For Each columna As DataColumn In Cargos.Columns
                            If tablaVinculos.Columns.Contains(columna.ColumnName) Then fila(columna.ColumnName) = cargo(columna.ColumnName)
                        Next
                    Next
                End If
                tablaVinculos.Rows.Add(fila)
            Next
            Return tablaVinculos
        End Function

        Private Function Insertar(ByVal sql As String, ByVal p As IDictionary(Of String, Object)) As Long
            If sql.StartsWith("INSERT INTO robot_camas_periodos") Then
                For Each periodo As DataRow In Periodos.Rows
                    If CInt(periodo("id_ingreso")) = CInt(p("@ingreso")) AndAlso CInt(periodo("id_cuenta")) = CInt(p("@cuenta")) AndAlso
                        CInt(periodo("periodo")) = CInt(p("@periodo")) Then Throw New Exception("Inicio de periodo duplicado en prueba.")
                Next
                Dim id As Long = Periodos.Rows.Count + 1
                Periodos.Rows.Add(id, p("@ingreso"), p("@cuenta"), p("@periodo"), p("@fecha"), p("@numero"), p("@cama"), p("@material"), p("@partes"), "EN_PROCESO")
                If InicioIncierto Then Throw New InvalidOperationException("Respuesta de inicio de periodo perdida.")
                Return id
            End If
            If Not sql.StartsWith("INSERT INTO tb_ticketsec_detalle") Then Throw New Exception("Inserción sin simular: " & sql)
            intentosCargo += 1
            If intentosCargo = FallaCargoNumero Then Throw New InvalidOperationException("Fallo de cargo simulado.")
            If CambiarAlInsertar OrElse Not Activo OrElse Not Abierta Then Return 0
            Dim nuevoId As Long = siguienteCargo
            siguienteCargo += 1
            Cargos.Rows.Add(nuevoId, p("@id"), p("@cuenta"), p("@cama"), p("@material"), p("@cantidad"), p("@precio"), p("@paquete"), "servidor_his", p("@publico"))
            If CargoIncierto Then Throw New InvalidOperationException("Respuesta del cargo perdida.")
            Return nuevoId
        End Function

        Private Function Escribir(ByVal sql As String, ByVal p As IDictionary(Of String, Object)) As Integer
            If sql.StartsWith("UPDATE tb_ticketsec SET") Then Return 1
            If sql.StartsWith("INSERT INTO robot_camas_detalles") Then
                If CInt(p("@parte")) = FallaPlanParte Then Throw New InvalidOperationException("Fallo de planificación simulado.")
                For Each detalle As DataRow In Detalles.Rows
                    If CLng(detalle("id_periodo")) = CLng(p("@id")) AndAlso CInt(detalle("parte")) = CInt(p("@parte")) Then
                        Throw New Exception("Fragmento de periodo duplicado en prueba.")
                    End If
                Next
                Detalles.Rows.Add(p("@id"), p("@parte"), p("@cantidad"), p("@precio"), p("@paquete"), DBNull.Value)
                Return 1
            End If
            If sql.StartsWith("UPDATE robot_camas_detalles") Then
                If FallaVinculo Then Throw New InvalidOperationException("Fallo al vincular simulado.")
                For Each detalle As DataRow In Detalles.Rows
                    If Not detalle.IsNull("id_cargo") AndAlso CLng(detalle("id_cargo")) = CLng(p("@cargo")) Then
                        Throw New Exception("Cargo vinculado a más de un fragmento en prueba.")
                    End If
                Next
                For Each fila As DataRow In Detalles.Rows
                    If CLng(fila("id_periodo")) = CLng(p("@id")) AndAlso CInt(fila("parte")) = CInt(p("@parte")) AndAlso fila.IsNull("id_cargo") Then
                        fila("id_cargo") = p("@cargo")
                        Return 1
                    End If
                Next
                Return 0
            End If
            If sql.StartsWith("UPDATE robot_camas_periodos") Then
                If FallaConfirmar Then Throw New InvalidOperationException("Fallo al confirmar simulado.")
                For Each fila As DataRow In Periodos.Rows
                    If CLng(fila("id")) = CLng(p("@id")) AndAlso CStr(fila("estado")) = "EN_PROCESO" Then
                        fila("estado") = "COMPLETO"
                        If ConfirmacionIncierta Then Throw New InvalidOperationException("Respuesta de confirmación perdida.")
                        Return 1
                    End If
                Next
                Return 0
            End If
            If sql.StartsWith("INSERT INTO tb_paquetes_notificaciones") Then
                Notificaciones += 1
                Return 1
            End If
            Throw New Exception("Escritura sin simular: " & sql)
        End Function
    End Class
End Module
