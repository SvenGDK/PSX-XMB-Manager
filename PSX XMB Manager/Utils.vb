Public Class Utils

    Public Shared Sub ReloadProjects()
        For Each Win In Windows.Application.Current.Windows()
            If Win.ToString = "PSX_XMB_Manager.MainWindow" Then
                CType(Win, MainWindow).ReloadProjects()
                Exit For
            End If
        Next
    End Sub

End Class
