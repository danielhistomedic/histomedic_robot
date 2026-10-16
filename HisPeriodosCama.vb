Option Explicit On
Option Strict On

Imports System.Collections.Generic

''' <summary>Cálculo de periodos anticipados y comprobación de fragmentos de un cargo por cama.</summary>
Friend NotInheritable Class HisPeriodosCama
#Region "Cálculo de periodos"
    Private Sub New()
    End Sub

    Friend Shared Function Total(ByVal ingreso As DateTime, ByVal ahora As DateTime) As Integer
        If ingreso = DateTime.MinValue Then Throw New InvalidOperationException("Falta la fecha de ingreso.")
        If ahora < ingreso Then Return 0
        Return CInt((ahora.Ticks - ingreso.Ticks) \ TimeSpan.TicksPerDay + 1L)
    End Function

    Friend Shared Function Completo(ByVal partes As IEnumerable(Of Integer), ByVal esperadas As Integer) As Boolean
        If esperadas < 1 OrElse esperadas > 2 Then Return False
        Dim encontrados As New HashSet(Of Integer)()
        For Each parte As Integer In partes
            If parte < 1 OrElse parte > esperadas OrElse Not encontrados.Add(parte) Then Return False
        Next
        Return encontrados.Count = esperadas
    End Function
#End Region
End Class
