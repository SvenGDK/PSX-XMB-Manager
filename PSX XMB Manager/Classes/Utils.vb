Imports System.Collections.Specialized
Imports System.Drawing
Imports System.IO
Imports System.Net
Imports System.Net.NetworkInformation
Imports System.Text.RegularExpressions

Public Class Utils

    Public Enum DiscType
        CD
        DVD
    End Enum

    Public Shared Function GetDiscType(ISOFile As String) As DiscType
        Dim ISOFileSize As Double = New FileInfo(ISOFile).Length / 1048576

        If ISOFileSize > 700 Then
            Return DiscType.DVD
        Else
            Return DiscType.CD
        End If
    End Function

    Public Shared Function IsNBDConnected(WNBDClientPath As String) As String
        Dim ProcessOutput As String()
        Dim NBDDriveName As String = ""

        'List connected clients
        If Not String.IsNullOrEmpty(WNBDClientPath) Then
            Using WNBDClient As New Process()
                WNBDClient.StartInfo.FileName = WNBDClientPath
                WNBDClient.StartInfo.Arguments = "list"
                WNBDClient.StartInfo.RedirectStandardOutput = True
                WNBDClient.StartInfo.UseShellExecute = False
                WNBDClient.StartInfo.CreateNoWindow = True
                WNBDClient.Start()
                WNBDClient.WaitForExit()

                Dim OutputReader As StreamReader = WNBDClient.StandardOutput
                ProcessOutput = OutputReader.ReadToEnd().Split({vbCrLf}, StringSplitOptions.None)
            End Using

            For Each ReturnedLine As String In ProcessOutput
                If ReturnedLine.Contains("wnbd-client") Then
                    NBDDriveName = ReturnedLine.Split(New String() {" "}, StringSplitOptions.RemoveEmptyEntries)(4).Trim()
                    Exit For
                End If
            Next
        End If

        If Not String.IsNullOrEmpty(NBDDriveName) Then
            Return NBDDriveName
        Else
            Return ""
        End If
    End Function

    Public Shared Function IsLocalHDDConnected() As String
        'Query the drives
        If File.Exists(My.Computer.FileSystem.CurrentDirectory + "\Tools\hdl_dump.exe") Then
            Using HDLDump As New Process()
                HDLDump.StartInfo.FileName = My.Computer.FileSystem.CurrentDirectory + "\Tools\hdl_dump.exe"
                HDLDump.StartInfo.Arguments = "query"
                HDLDump.StartInfo.RedirectStandardOutput = True
                HDLDump.StartInfo.UseShellExecute = False
                HDLDump.StartInfo.CreateNoWindow = True
                HDLDump.Start()

                'Read the output
                Dim OutputReader As StreamReader = HDLDump.StandardOutput
                Dim ProcessOutput As String() = OutputReader.ReadToEnd().Split({vbCrLf}, StringSplitOptions.None)

                Dim DriveHDLName As String = ""

                'Find the local drive
                For Each Line As String In ProcessOutput
                    If Not String.IsNullOrWhiteSpace(Line) Then
                        If Line.Contains("formatted Playstation 2 HDD") Then
                            'Set the found drive as mounted PSX drive
                            Dim DriveInfos As String() = Line.Split(New String() {" "}, StringSplitOptions.RemoveEmptyEntries)
                            If DriveInfos(0) IsNot Nothing Then
                                DriveHDLName = DriveInfos(0).Trim()
                                Exit For
                            End If
                        End If
                    End If
                Next

                If Not String.IsNullOrWhiteSpace(DriveHDLName) Then
                    Return DriveHDLName
                Else
                    Return ""
                End If

            End Using
        Else
            Return ""
        End If
    End Function

    Public Shared Function GetConnectedNBDIP(WNBDClientPath As String, NBDDriveName As String) As String
        'Get the connected IP address
        If Not String.IsNullOrEmpty(WNBDClientPath) Then
            Dim ProcessOutput As String()
            Dim NBDIP As String = ""

            Using WNBDClient As New Process()
                WNBDClient.StartInfo.FileName = WNBDClientPath
                WNBDClient.StartInfo.Arguments = "show " + NBDDriveName
                WNBDClient.StartInfo.RedirectStandardOutput = True
                WNBDClient.StartInfo.UseShellExecute = False
                WNBDClient.StartInfo.CreateNoWindow = True
                WNBDClient.Start()
                WNBDClient.WaitForExit()

                Dim OutputReader As StreamReader = WNBDClient.StandardOutput
                ProcessOutput = OutputReader.ReadToEnd().Split({vbCrLf}, StringSplitOptions.None)
            End Using

            For Each ReturnedLine As String In ProcessOutput
                If ReturnedLine.Contains("Hostname") Then
                    NBDIP = ReturnedLine.Split(":"c)(1).Trim()
                    Exit For
                End If
            Next

            Return NBDIP
        Else
            Return ""
        End If
    End Function

    Public Shared Function GetHDLDriveName() As String
        If File.Exists(My.Computer.FileSystem.CurrentDirectory + "\Tools\hdl_dump.exe") Then
            Dim HDLDriveName As String = ""

            'Query the drives
            Using HDLDump As New Process()
                HDLDump.StartInfo.FileName = My.Computer.FileSystem.CurrentDirectory + "\Tools\hdl_dump.exe"
                HDLDump.StartInfo.Arguments = "query"
                HDLDump.StartInfo.RedirectStandardOutput = True
                HDLDump.StartInfo.UseShellExecute = False
                HDLDump.StartInfo.CreateNoWindow = True
                HDLDump.Start()
                HDLDump.WaitForExit()

                'Read the output
                Dim OutputReader As StreamReader = HDLDump.StandardOutput
                Dim ProcessOutput As String() = OutputReader.ReadToEnd().Split({vbCrLf}, StringSplitOptions.None)

                'Find the drive
                For Each Line As String In ProcessOutput
                    If Not String.IsNullOrWhiteSpace(Line) Then
                        If Line.Contains("formatted Playstation 2 HDD") Then
                            'Set the found drive as mounted PSX drive
                            Dim DriveInfos As String() = Line.Split(New String() {" "}, StringSplitOptions.RemoveEmptyEntries)
                            HDLDriveName = DriveInfos(0).Trim()
                            Exit For
                        End If
                    End If
                Next
            End Using

            Return HDLDriveName
        Else
            Return ""
        End If
    End Function

    Public Shared Function GetHDDID() As String
        Dim DriveID As String = ""

        'Query the drives
        Using WMIC As New Process()
            WMIC.StartInfo.FileName = "wmic"
            WMIC.StartInfo.Arguments = "diskdrive get Caption,DeviceID"
            WMIC.StartInfo.RedirectStandardOutput = True
            WMIC.StartInfo.UseShellExecute = False
            WMIC.StartInfo.CreateNoWindow = True
            WMIC.Start()
            WMIC.WaitForExit()

            'Read the output
            Dim OutputReader As StreamReader = WMIC.StandardOutput
            Dim ProcessOutput As String() = OutputReader.ReadToEnd().Split({vbCrLf}, StringSplitOptions.None)

            'Find the drive
            For Each Line As String In ProcessOutput
                If Not String.IsNullOrWhiteSpace(Line) Then
                    If Line.Contains("WNBD WNBD_DISK SCSI Disk Device") Then
                        DriveID = Line.Split(New String() {" "}, StringSplitOptions.RemoveEmptyEntries)(5).Trim()
                        Exit For
                    ElseIf Line.Contains("Microsoft Virtual Disk") Then 'For testing with local VHD
                        DriveID = Line.Split(New String() {" "}, StringSplitOptions.RemoveEmptyEntries)(3).Trim()
                        Exit For
                    ElseIf Line.Contains("is not recognized") Then 'Windows 11 removed wmic, prompt for installation before continuing
                        DriveID = "WMIC_INSTALL_REQUIRED"
                        Exit For
                    End If
                End If
            Next
        End Using

        Return DriveID
    End Function

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
        Try
            If NetworkInterface.GetIsNetworkAvailable Then
                Dim Request As WebRequest = WebRequest.Create(ImageLocation)
                Dim Response As WebResponse = Request.GetResponse()
                Dim ResponseStream As Stream = Response.GetResponseStream()

                Dim OriginalBitmap As New Bitmap(ResponseStream)
                Dim ResizedBitmap As New Bitmap(OriginalBitmap, New Size(NewWidth, NewHeight))

                Return ResizedBitmap
            Else
                Return Nothing
            End If
        Catch Ex As Exception
            Return Nothing
        End Try
    End Function

    Public Shared Sub ConvertTo32bppAndDisposeOriginal(ByRef img As Bitmap)
        Try
            Dim bmp = New Bitmap(img.Width, img.Height, Imaging.PixelFormat.Format32bppArgb)

            Using gr = Graphics.FromImage(bmp)
                gr.DrawImage(img, New Rectangle(0, 0, 76, 108))
            End Using

            img.Dispose()
            img = bmp
        Catch ex As Exception
            img = Nothing
        End Try
    End Sub

    Public Shared Function IsURLValid(Url As String) As Boolean
        Try
            If NetworkInterface.GetIsNetworkAvailable Then
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
                    Else
                        Return False
                    End If
                End Using
            Else
                Return False
            End If
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
