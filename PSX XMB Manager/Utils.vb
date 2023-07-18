Imports System.Collections.Specialized
Imports System.Drawing
Imports System.IO
Imports System.Net
Imports System.Text.RegularExpressions

Public Class Utils

    Public Shared Sub ReloadProjects()
        For Each Win In Windows.Application.Current.Windows()
            If Win.ToString = "PSX_XMB_Manager.NewMainWindow" Then
                CType(Win, NewMainWindow).ReloadProjects()
                Exit For
            End If
        Next
    End Sub

    Public Shared Sub ReloadPartitions()
        For Each Win In Windows.Application.Current.Windows()
            If Win.ToString = "PSX_XMB_Manager.PartitionManager" Then
                CType(Win, PartitionManager).ReloadPartitions()
                Exit For
            End If
        Next
    End Sub

    Public Shared Function GetIntOnly(Value As String) As Integer
        Dim ReturnValue As String = String.Empty
        Dim MatchCol As MatchCollection = Regex.Matches(Value, "\d+")
        For Each m As Match In MatchCol
            ReturnValue += m.ToString()
        Next
        Return Convert.ToInt32(ReturnValue)
    End Function

    Public Shared Function GetResizedBitmap(ImageLocation As String, NewWidth As Integer, NewHeight As Integer) As Bitmap
        Dim Request As WebRequest = WebRequest.Create(ImageLocation)
        Dim Response As WebResponse = Request.GetResponse()
        Dim ResponseStream As Stream = Response.GetResponseStream()

        Dim OriginalBitmap As New Bitmap(ResponseStream)
        Dim ResizedBitmap As New Bitmap(OriginalBitmap, New Size(NewWidth, NewHeight))

        Return ResizedBitmap
    End Function

    Public Shared Sub ConvertTo32bppAndDisposeOriginal(ByRef img As Bitmap)
        Dim bmp = New Bitmap(img.Width, img.Height, Imaging.PixelFormat.Format32bppArgb)

        Using gr = Graphics.FromImage(bmp)
            gr.DrawImage(img, New Rectangle(0, 0, 76, 108))
        End Using

        img.Dispose()
        img = bmp
    End Sub

    Public Shared Function IsURLValid(Url As String) As Boolean
        Try
            Dim request As HttpWebRequest = CType(WebRequest.Create(Url), HttpWebRequest)
            Using response As HttpWebResponse = CType(request.GetResponse(), HttpWebResponse)
                If response.StatusCode = HttpStatusCode.OK Then
                    Return True
                ElseIf response.StatusCode = HttpStatusCode.Found Then
                    Return True
                ElseIf response.StatusCode = HttpStatusCode.NotFound Then
                    Return False
                ElseIf response.StatusCode = HttpStatusCode.Unauthorized Then
                    Return False
                ElseIf response.StatusCode = HttpStatusCode.Forbidden Then
                    Return False
                ElseIf response.StatusCode = HttpStatusCode.BadGateway Then
                    Return False
                ElseIf response.StatusCode = HttpStatusCode.BadRequest Then
                    Return False
                End If
                Return False
            End Using
        Catch Ex As Exception
            Return False
        End Try
    End Function

    Public Shared Function GetScrollViewer(DepObj As DependencyObject) As DependencyObject
        If TypeOf DepObj Is ScrollViewer Then
            Return DepObj
        End If

        For i As Integer = 0 To VisualTreeHelper.GetChildrenCount(DepObj) - 1
            Dim Child = VisualTreeHelper.GetChild(DepObj, i)
            Dim Result = GetScrollViewer(Child)

            If Result Is Nothing Then
                Continue For
            Else
                Return Result
            End If
        Next

        Return Nothing
    End Function

    Public Shared Function FindNextAvailableDriveLetter() As String
        Dim AlphabetCollection As New StringCollection()
        Dim LowerBound As Integer = Convert.ToInt16("a"c)
        Dim UpperBound As Integer = Convert.ToInt16("z"c)

        For i As Integer = LowerBound To UpperBound - 1
            Dim DriveLetter As Char = ChrW(i)
            AlphabetCollection.Add(DriveLetter.ToString())
        Next

        Dim Drives As DriveInfo() = DriveInfo.GetDrives()
        For Each Drive As DriveInfo In Drives
            AlphabetCollection.Remove(Drive.Name.Substring(0, 1).ToLower())
        Next

        If AlphabetCollection.Count > 0 Then
            Return AlphabetCollection(0)
        Else
            Throw New ApplicationException("No drive letter available.")
        End If
    End Function

    Public Shared Sub RemoveMountedDriveLetter(DriveLetter As String)
        For Each Win In Windows.Application.Current.Windows()
            If Win.ToString = "PSX_XMB_Manager.GameLibrary" Then
                CType(Win, GameLibrary).RemoveDriveLetterFromGame(DriveLetter)
                Exit For
            End If
        Next
    End Sub

End Class
