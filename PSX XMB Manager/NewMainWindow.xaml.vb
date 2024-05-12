Imports System.ComponentModel
Imports System.IO
Imports System.Net
Imports System.Text.RegularExpressions
Imports System.Windows.Media.Animation
Imports System.Windows.Threading
Imports PSX_XMB_Manager.Structs

Public Class NewMainWindow

    Private MountedDrive As MountedPSXDrive

    Private WNBDClientPath As String = ""
    Private HDLGameID As String = ""
    Private CurrentProjectDirectory As String = ""

    Private WithEvents HDL_Dump As New Process()
    Private WithEvents ConnectDelay As New DispatcherTimer With {.Interval = TimeSpan.FromSeconds(2)}
    Private WithEvents ContentDownloader As New WebClient()

    Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Title = String.Format("PSX XMB Manager - {0}.{1}.{2}", My.Application.Info.Version.Major, My.Application.Info.Version.Minor, My.Application.Info.Version.Build)

        If Not Directory.Exists(My.Computer.FileSystem.CurrentDirectory + "\Projects") Then
            'Set up a projects directory to save all created projects
            Directory.CreateDirectory(My.Computer.FileSystem.CurrentDirectory + "\Projects")
        Else
            'Load saved projects
            For Each SavedProject In Directory.GetFiles(My.Computer.FileSystem.CurrentDirectory + "\Projects", "*.CFG")
                Dim NewCBProjectItem As New ComboBoxProjectItem() With {.ProjectFile = Path.GetFullPath(SavedProject), .ProjectName = Path.GetFileNameWithoutExtension(SavedProject)}
                Dim ProjectState As String = File.ReadAllLines(SavedProject)(5).Split("="c)(1)

                If ProjectState = "FALSE" Then
                    ProjectListComboBox.Items.Add(NewCBProjectItem)
                Else
                    ProjectListComboBox.Items.Add(NewCBProjectItem)
                    PreparedProjectsComboBox.Items.Add(NewCBProjectItem)
                End If
            Next
        End If

        'Set DisplayMemberPath
        ProjectListComboBox.DisplayMemberPath = "ProjectName"
        PreparedProjectsComboBox.DisplayMemberPath = "ProjectName"

        'Set wnbd-client
        If File.Exists(My.Computer.FileSystem.SpecialDirectories.ProgramFiles + "\Ceph\bin\wnbd-client.exe") Then
            WNBDClientPath = My.Computer.FileSystem.SpecialDirectories.ProgramFiles + "\Ceph\bin\wnbd-client.exe"
        ElseIf File.Exists(My.Computer.FileSystem.CurrentDirectory + "\Tools\wnbd-client.exe") Then
            WNBDClientPath = My.Computer.FileSystem.CurrentDirectory + "\Tools\wnbd-client.exe"
        End If

        'Check if NBD driver is installed
        If Not String.IsNullOrEmpty(WNBDClientPath) Then
            Using WNBDClient As New Process()
                WNBDClient.StartInfo.FileName = WNBDClientPath
                WNBDClient.StartInfo.Arguments = "-v"
                WNBDClient.StartInfo.RedirectStandardOutput = True
                WNBDClient.StartInfo.UseShellExecute = False
                WNBDClient.StartInfo.CreateNoWindow = True
                WNBDClient.Start()

                Dim OutputReader As StreamReader = WNBDClient.StandardOutput
                Dim ProcessOutput As String = OutputReader.ReadToEnd()
                Dim SplittedOutput As String() = ProcessOutput.Split({vbCrLf}, StringSplitOptions.None)

                Dim NBDDriverVersion As String

                If Not SplittedOutput(2).Trim() = "" Then
                    NBDDriverVersion = SplittedOutput(2).Trim().Split(":"c)(1).Trim()
                    NBDDriverVersionLabel.Text = NBDDriverVersion
                    NBDDriverVersionLabel.Foreground = Brushes.Green
                Else
                    NBDDriverVersionLabel.Text = "Not installed"
                    NBDDriverVersionLabel.Foreground = Brushes.Red
                End If
            End Using

            'Check if NBD is connected and if the drive is already mounted
            If IsNBDConnected() Then
                InstallProjectButton.IsEnabled = True
                NBDConnectionLabel.Text = "Connected"
                NBDConnectionLabel.Foreground = Brushes.Green
                ConnectButton.Content = "Disconnect"

            ElseIf IsLocalHDDConnected() Then
                MountedDrive.DriveID = GetHDDID()

                InstallProjectButton.IsEnabled = True
                NBDConnectionStatusLabel.Text = "Local Connection:"
                NBDConnectionLabel.Text = "Connected"

                EnterIPLabel.Text = "Local PS2/PSX HDD connected."
                EnterIPLabel.TextAlignment = TextAlignment.Center
                ConnectButton.Visibility = Visibility.Hidden
                PSXIPTextBox.Visibility = Visibility.Hidden

                NBDConnectionLabel.Foreground = Brushes.Green
                ConnectButton.Content = "Disconnect"
            End If
        End If

        'Check if Dokan driver is installed
        If Directory.Exists(My.Computer.FileSystem.SpecialDirectories.ProgramFiles + "\Dokan") Then
            Dim DokanLibraryFolder As String = ""
            For Each Folder In Directory.GetDirectories(My.Computer.FileSystem.SpecialDirectories.ProgramFiles + "\Dokan")
                Dim FolderInfo As New DirectoryInfo(Folder)
                If FolderInfo.Name.Contains("DokanLibrary") Or FolderInfo.Name.Contains("Dokan Library") Then
                    DokanLibraryFolder = Folder
                    Exit For
                End If
            Next
            If Not String.IsNullOrEmpty(DokanLibraryFolder) Then
                'Check if NBD driver is installed
                Using DokanCTL As New Process()
                    DokanCTL.StartInfo.FileName = DokanLibraryFolder + "\dokanctl.exe"
                    DokanCTL.StartInfo.Arguments = "/v"
                    DokanCTL.StartInfo.RedirectStandardOutput = True
                    DokanCTL.StartInfo.UseShellExecute = False
                    DokanCTL.StartInfo.CreateNoWindow = True
                    DokanCTL.Start()

                    Dim OutputReader As StreamReader = DokanCTL.StandardOutput
                    Dim ProcessOutput As String = OutputReader.ReadToEnd()
                    Dim SplittedOutput As String() = ProcessOutput.Split({vbCrLf}, StringSplitOptions.None)

                    Dim DokanVersion As String = ""
                    Dim DokanDriverVersion As String = ""

                    If Not SplittedOutput(2).Trim() = "" Then
                        DokanVersion = SplittedOutput(2).Trim().Split(":"c)(1).Trim()
                        DokanDriverVersion = SplittedOutput(3).Trim().Split(":"c)(1).Trim()

                        DokanDriverVersionLabel.Text = "Library: " + DokanVersion + " - Driver: " + DokanDriverVersion
                        DokanDriverVersionLabel.Foreground = Brushes.Green
                    End If
                End Using
            End If
        End If

    End Sub

    Private Sub NewMainWindow_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        Windows.Application.Current.Shutdown()
    End Sub

    Private Sub ConnectButton_Click(sender As Object, e As RoutedEventArgs) Handles ConnectButton.Click
        If ConnectButton.Content.ToString = "Connect" Then

            Using WNBDConnectClient As New Process()
                If File.Exists(My.Computer.FileSystem.SpecialDirectories.ProgramFiles + "\Ceph\bin\wnbd-client.exe") Then
                    WNBDConnectClient.StartInfo.FileName = My.Computer.FileSystem.SpecialDirectories.ProgramFiles + "\Ceph\bin\wnbd-client.exe"
                Else
                    WNBDConnectClient.StartInfo.FileName = My.Computer.FileSystem.CurrentDirectory + "\Tools\wnbd-client.exe"
                End If

                WNBDConnectClient.StartInfo.Arguments = "map PSXHDD " + PSXIPTextBox.Text
                WNBDConnectClient.StartInfo.UseShellExecute = False
                WNBDConnectClient.StartInfo.CreateNoWindow = True
                WNBDConnectClient.Start()
            End Using

            ConnectDelay.Start()

        ElseIf ConnectButton.Content.ToString = "Disconnect" Then

            Using WNBDProcess As New Process()
                If File.Exists(My.Computer.FileSystem.SpecialDirectories.ProgramFiles + "\Ceph\bin\wnbd-client.exe") Then
                    WNBDProcess.StartInfo.FileName = My.Computer.FileSystem.SpecialDirectories.ProgramFiles + "\Ceph\bin\wnbd-client.exe"
                Else
                    WNBDProcess.StartInfo.FileName = My.Computer.FileSystem.CurrentDirectory + "\Tools\wnbd-client.exe"
                End If

                WNBDProcess.StartInfo.Arguments = "unmap PSXHDD"
                WNBDProcess.StartInfo.CreateNoWindow = True
                WNBDProcess.Start()
            End Using

            InstallProjectButton.IsEnabled = True
            NBDConnectionLabel.Text = "Disconnected"
            NBDConnectionLabel.Foreground = Brushes.Red
            MountStatusLabel.Text = "Not mounted"
            MountStatusLabel.Foreground = Brushes.Orange
            ConnectButton.Content = "Connect"

            MsgBox("Your PSX HDD is now disconnected." + vbCrLf + "You can now safely close the NBD server.", MsgBoxStyle.Information)
        End If
    End Sub

    Public Enum DiscType
        CD
        DVD
    End Enum

    Private Function GetDiscType(ISOFile As String) As DiscType
        Dim ISOFileSize As Double = New FileInfo(ISOFile).Length / 1048576

        If ISOFileSize > 700 Then
            Return DiscType.DVD
        Else
            Return DiscType.CD
        End If
    End Function

    Private Function IsNBDConnected() As Boolean

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

            MountedDrive.NBDDriveName = NBDDriveName

            If PSXIPTextBox.Dispatcher.CheckAccess() = False Then
                PSXIPTextBox.Dispatcher.BeginInvoke(Sub()
                                                        PSXIPTextBox.Text = GetConnectedNBDIP(NBDDriveName)
                                                    End Sub)
            Else
                PSXIPTextBox.Text = GetConnectedNBDIP(NBDDriveName)
            End If

            MountedDrive.HDLDriveName = GetHDLDriveName()
            MountedDrive.DriveID = GetHDDID()

            Return True
        Else
            Return False
        End If

    End Function

    Private Function IsLocalHDDConnected() As Boolean
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
                    MountStatusLabel.Text = "on " + DriveHDLName
                    MountStatusLabel.Foreground = Brushes.Green
                    MountedDrive.HDLDriveName = DriveHDLName
                    Return True
                Else
                    Return False
                End If

            End Using
        Else
            Return False
        End If
    End Function

    Private Function GetConnectedNBDIP(NBDDriveName As String) As String
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

    Private Function GetHDLDriveName() As String
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

            'Update UI
            If Not String.IsNullOrWhiteSpace(HDLDriveName) Then
                If MountStatusLabel.CheckAccess() = False Then
                    MountStatusLabel.Dispatcher.BeginInvoke(Sub()
                                                                MountStatusLabel.Text = "On " + HDLDriveName
                                                                MountStatusLabel.Foreground = Brushes.Green
                                                            End Sub)
                Else
                    MountStatusLabel.Text = "On " + HDLDriveName
                    MountStatusLabel.Foreground = Brushes.Green
                End If
            End If

            Return HDLDriveName
        Else
            Return ""
        End If
    End Function

    Private Function GetHDDID() As String
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
                    End If
                End If
            Next
        End Using

        Return DriveID
    End Function

    Public Sub ReloadProjects()
        ProjectListComboBox.Items.Clear()
        PreparedProjectsComboBox.Items.Clear()

        If Directory.Exists(My.Computer.FileSystem.CurrentDirectory + "\Projects") Then
            'Load saved projects
            For Each SavedProject In Directory.GetFiles(My.Computer.FileSystem.CurrentDirectory + "\Projects", "*.CFG")
                Dim NewCBProjectItem As New ComboBoxProjectItem() With {.ProjectFile = SavedProject, .ProjectName = Path.GetFileNameWithoutExtension(SavedProject)}
                Dim ProjectState As String = File.ReadAllLines(SavedProject)(5).Split("="c)(1)

                If ProjectState = "FALSE" Then
                    ProjectListComboBox.Items.Add(NewCBProjectItem)
                Else
                    ProjectListComboBox.Items.Add(NewCBProjectItem)
                    PreparedProjectsComboBox.Items.Add(NewCBProjectItem)
                End If
            Next
        Else
            MsgBox("Could not find the Projects directory at " + My.Computer.FileSystem.CurrentDirectory + "\Projects", MsgBoxStyle.Critical, "Error")
        End If
    End Sub

#Region "Menu"

    Private Sub StartMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles StartMenuItem.Click
        'Switch to the StartGrid
        StartMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF004671"), Color))
        ProjectsMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))
        PartitionManagerMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))
        GameLibraryMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))
        XMBToolsMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))
        NBDDriverMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))

        Dim ProjectsGridAnimation As New DoubleAnimation With {.From = 1, .To = 0, .Duration = New Duration(TimeSpan.FromMilliseconds(300))}
        Dim StartGridAnimation As New DoubleAnimation With {.From = 0, .To = 1, .Duration = New Duration(TimeSpan.FromMilliseconds(300))}

        StartGrid.Visibility = Visibility.Visible

        ProjectsGrid.BeginAnimation(OpacityProperty, ProjectsGridAnimation)
        StartGrid.BeginAnimation(OpacityProperty, StartGridAnimation)

        ProjectsGrid.Visibility = Visibility.Hidden
    End Sub

    Private Sub ProjectsMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles ProjectsMenuItem.Click
        'Switch to the ProjectsGrid
        StartMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))
        ProjectsMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF004671"), Color))
        PartitionManagerMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))
        GameLibraryMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))
        XMBToolsMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))
        NBDDriverMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))

        Dim ProjectsGridAnimation As New DoubleAnimation With {.From = 0, .To = 1, .Duration = New Duration(TimeSpan.FromMilliseconds(300))}
        Dim StartGridAnimation As New DoubleAnimation With {.From = 1, .To = 0, .Duration = New Duration(TimeSpan.FromMilliseconds(300))}

        ProjectsGrid.Visibility = Visibility.Visible

        StartGrid.BeginAnimation(OpacityProperty, StartGridAnimation)
        ProjectsGrid.BeginAnimation(OpacityProperty, ProjectsGridAnimation)

        StartGrid.Visibility = Visibility.Hidden
    End Sub

    Private Sub PartitionManagerMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles PartitionManagerMenuItem.Click
        If MountedDrive.HDLDriveName = "" Then
            MsgBox("Please connect to the NBD server first.", MsgBoxStyle.Information)
        Else
            Dim NewPartitionManager As New PartitionManager() With {.ShowActivated = True, .MountedDrive = MountedDrive}
            NewPartitionManager.Show()
        End If
    End Sub

    Private Sub GameLibraryMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles GameLibraryMenuItem.Click
        Dim NewGameLibrary As New GameLibrary() With {.ShowActivated = True, .MountedDrive = MountedDrive}
        NewGameLibrary.Show()
    End Sub

    Private Sub XMBToolsMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles XMBToolsMenuItem.Click
        MsgBox("XMB Tools are not available yet in this release.", MsgBoxStyle.Information)
    End Sub

    Private Sub NBDDriverMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles NBDDriverMenuItem.Click
        Process.Start(New ProcessStartInfo("https://cloudbase.it/ceph-for-windows/") With {.UseShellExecute = True})
    End Sub

    Private Sub DokanDriverMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles DokanDriverMenuItem.Click
        Process.Start(New ProcessStartInfo("https://github.com/dokan-dev/dokany/releases") With {.UseShellExecute = True})
    End Sub

#End Region

#Region "Projects"

    Private Sub NewHomebrewProjectMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles NewHomebrewProjectButton.Click
        Dim NewHomebrewProjectWindow As New NewAppProject() With {.ShowActivated = True}
        NewHomebrewProjectWindow.Show()
    End Sub

    Private Sub NewGameProjectMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles NewGameProjectButton.Click
        Dim NewGameProjectWindow As New NewGameProject() With {.ShowActivated = True}
        NewGameProjectWindow.Show()
    End Sub

    Private Sub EditProjectButton_Click(sender As Object, e As RoutedEventArgs) Handles EditProjectButton.Click
        If ProjectListComboBox.SelectedItem IsNot Nothing Then
            'Get project infos
            Dim SelectedProject As ComboBoxProjectItem = CType(ProjectListComboBox.SelectedItem, ComboBoxProjectItem)
            Dim ProjectInfos As String() = File.ReadAllLines(SelectedProject.ProjectFile)
            Dim ProjectName As String = ProjectInfos(0).Split("="c)(1)
            Dim ProjectSubtitle As String = ProjectInfos(1).Split("="c)(1)
            Dim ProjectDirectory As String = ProjectInfos(2).Split("="c)(1)
            Dim ProjectFile As String = ProjectInfos(3).Split("="c)(1)
            Dim ProjectType As String = ProjectInfos(4).Split("="c)(1)

            If ProjectType = "APP" Then
                Dim HomebrewInfos As String() = File.ReadAllLines(ProjectDirectory + "\icon.sys")
                Dim HomebrewProjectEditor As New NewAppProject() With {.Title = "Editing project " + ProjectName + " - " + ProjectDirectory}

                HomebrewProjectEditor.ProjectNameTextBox.Text = ProjectName
                HomebrewProjectEditor.ProjectDirectoryTextBox.Text = ProjectDirectory
                HomebrewProjectEditor.ProjectTitleTextBox.Text = HomebrewInfos(1).Split("="c)(1)
                HomebrewProjectEditor.ProjectSubTitleTextBox.Text = ProjectSubtitle
                HomebrewProjectEditor.ProjectSubTitleTextBox.Text = HomebrewInfos(2).Split("="c)(1)
                HomebrewProjectEditor.ProjectUninstallMsgTextBox.Text = HomebrewInfos(15).Split("="c)(1)
                HomebrewProjectEditor.ProjectELFFileTextBox.Text = ProjectFile

                If File.Exists(ProjectDirectory + "\list.ico") Then
                    HomebrewProjectEditor.ProjectIconPathTextBox.Text = ProjectDirectory + "\list.ico"
                End If

                HomebrewProjectEditor.Show()
            ElseIf ProjectType = "GAME" Then
                Dim GameInfos As String() = File.ReadAllLines(ProjectDirectory + "\icon.sys")
                Dim GameProjectEditor As New NewGameProject() With {.Title = "Editing project " + ProjectName + " - " + ProjectDirectory}

                GameProjectEditor.ProjectNameTextBox.Text = ProjectName
                GameProjectEditor.ProjectDirectoryTextBox.Text = ProjectDirectory
                GameProjectEditor.ProjectTitleTextBox.Text = GameInfos(1).Split("="c)(1)
                GameProjectEditor.ProjectIDTextBox.Text = ProjectSubtitle
                GameProjectEditor.ProjectIDTextBox.Text = GameInfos(2).Split("="c)(1)
                GameProjectEditor.ProjectUninstallMsgTextBox.Text = GameInfos(15).Split("="c)(1)
                GameProjectEditor.ProjectISOFileTextBox.Text = ProjectFile

                If File.Exists(ProjectDirectory + "\list.ico") Then
                    GameProjectEditor.ProjectIconPathTextBox.Text = ProjectDirectory + "\list.ico"
                End If

                GameProjectEditor.Show()
            End If
        End If
    End Sub

    Private Sub PrepareProjectButton_Click(sender As Object, e As RoutedEventArgs) Handles PrepareProjectButton.Click
        If ProjectListComboBox.SelectedItem IsNot Nothing Then
            Dim SelectedProject As ComboBoxProjectItem = CType(ProjectListComboBox.SelectedItem, ComboBoxProjectItem)
            Dim ProjectDIR As String = File.ReadAllLines(SelectedProject.ProjectFile)(2).Split("="c)(1)
            Dim SignedStatus As String = File.ReadAllLines(SelectedProject.ProjectFile)(5).Split("="c)(1)
            Dim SignedELF As Boolean = False

            'Check if KELF already exists
            If File.Exists(ProjectDIR + "\EXECUTE.KELF") Or File.Exists(ProjectDIR + "\boot.elf") Or File.Exists(ProjectDIR + "\boot.kelf") Then SignedELF = True

            If SignedStatus = "TRUE" AndAlso SignedELF = True Then
                MsgBox("Your Project doesn't need to be prepared again.", MsgBoxStyle.Information)
            Else
                Dim ProjectELForISO As String = File.ReadAllLines(SelectedProject.ProjectFile)(3).Split("="c)(1)
                Dim ProjectTYPE As String = File.ReadAllLines(SelectedProject.ProjectFile)(4).Split("="c)(1)

                If ProjectTYPE = "APP" Then
                    'Wrap the application ELF as EXECUTE.KELF
                    Dim WrapProcess As New Process()
                    WrapProcess.StartInfo.FileName = My.Computer.FileSystem.CurrentDirectory + "\Tools\SCEDoormat_NoME.exe"
                    WrapProcess.StartInfo.Arguments = """" + ProjectELForISO + """ " + ProjectDIR + "\EXECUTE.KELF"
                    WrapProcess.StartInfo.CreateNoWindow = True
                    WrapProcess.Start()
                    WrapProcess.WaitForExit()

                    Dim ProjectConfigFileLines() As String = File.ReadAllLines(SelectedProject.ProjectFile)
                    ProjectConfigFileLines(5) = "SIGNED=TRUE"
                    File.WriteAllLines(SelectedProject.ProjectFile, ProjectConfigFileLines)

                    MsgBox("Homebrew Project prepared with success !" + vbCrLf + "You can now proceed with the installation on the PSX.", MsgBoxStyle.Information, "Success")
                Else
                    If File.Exists(My.Computer.FileSystem.CurrentDirectory + "\Tools\EXECUTE.KELF") Then
                        'Copy included OPL-Launcher to project folder
                        File.Copy(My.Computer.FileSystem.CurrentDirectory + "\Tools\EXECUTE.KELF", ProjectDIR + "\EXECUTE.KELF", True)
                    Else
                        'OPL-Launcher not found...
                        Dim HomebrewELF As String = ""

                        HomebrewELF = InputBox("OPL-Launcher has been deleted from the Tools folder." + vbCrLf + "Please enter the full path to the .elf file or leave the URL to download OPL-Launcher.",
                                                   "Missing file",
                                                   "https://github.com/ps2homebrew/OPL-Launcher/releases/download/latest/OPL-Launcher.elf")

                        If Not String.IsNullOrEmpty(HomebrewELF) Then
                            If HomebrewELF = "https://github.com/ps2homebrew/OPL-Launcher/releases/download/latest/OPL-Launcher.elf" Then
                                'Download latest OPL-Launcher
                                ContentDownloader.DownloadFile("https://github.com/ps2homebrew/OPL-Launcher/releases/download/latest/OPL-Launcher.elf", My.Computer.FileSystem.CurrentDirectory + "\Tools\OPL-Launcher.elf")
                            End If
                        Else
                            MsgBox("Not valid file provided, aborting ...", MsgBoxStyle.Exclamation, "Aborting")
                            Exit Sub
                        End If

                        'Wrap OPL-Launcher as EXECUTE.KELF
                        Dim WrapProcess As New Process()
                        WrapProcess.StartInfo.FileName = My.Computer.FileSystem.CurrentDirectory + "\Tools\SCEDoormat_NoME.exe"
                        WrapProcess.StartInfo.Arguments = """" + My.Computer.FileSystem.CurrentDirectory + "\Tools\OPL-Launcher.elf"" """ + ProjectDIR + "\EXECUTE.KELF"""
                        WrapProcess.StartInfo.CreateNoWindow = True
                        WrapProcess.Start()
                        WrapProcess.WaitForExit()
                    End If

                    Dim ProjectConfigFileLines() As String = File.ReadAllLines(SelectedProject.ProjectFile)
                    ProjectConfigFileLines(5) = "SIGNED=TRUE"
                    File.WriteAllLines(SelectedProject.ProjectFile, ProjectConfigFileLines)

                    MsgBox("Game Project is now prepared !" + vbCrLf + "You can now proceed with the installation on the PSX.", MsgBoxStyle.Information, "Success")
                End If
            End If

            ReloadProjects()
        End If
    End Sub

    Private Sub InstallProjectButton_Click(sender As Object, e As RoutedEventArgs) Handles InstallProjectButton.Click
        If PreparedProjectsComboBox.SelectedItem IsNot Nothing Then
            Dim SelectedProject As ComboBoxProjectItem = CType(PreparedProjectsComboBox.SelectedItem, ComboBoxProjectItem)

            If File.Exists(SelectedProject.ProjectFile) Then
                Dim ProjectTitle As String = File.ReadAllLines(SelectedProject.ProjectFile)(0).Split("="c)(1)
                If MsgBox("Do you really want to install " + ProjectTitle + " on your PSX ?", MsgBoxStyle.YesNo, "Please confirm") = MsgBoxResult.Yes Then

                    'Identify project type
                    Dim ProjectType As String = File.ReadAllLines(SelectedProject.ProjectFile)(4).Split("="c)(1)

                    'Show progress
                    ProgressGrid.Visibility = Visibility.Visible

                    If ProjectType = "APP" Then
                        StatusLabel.Text = "Installing Homebrew, please wait..."
                        InstallApp()
                    ElseIf ProjectType = "GAME" Then
                        StatusLabel.Text = "Installing Game, please wait..."
                        InstallGame()
                    End If

                Else
                    MsgBox("Installation aborted.", MsgBoxStyle.Information, "Aborted")
                End If
            Else
                MsgBox("Could not find the selected project: " + SelectedProject.ProjectFile, MsgBoxStyle.Critical, "Error")
            End If

        Else
            MsgBox("No project selected.", MsgBoxStyle.Critical, "Error")
        End If
    End Sub

#End Region

    Public Sub LockUI()
        If MainMenu.IsEnabled Then
            MainMenu.IsEnabled = False
            ProjectsGrid.IsEnabled = False
        Else
            MainMenu.IsEnabled = True
            ProjectsGrid.IsEnabled = True
        End If
    End Sub

    Private Sub InstallApp()
        'Check if drive is already identified, if not get the drive name
        If String.IsNullOrEmpty(MountedDrive.HDLDriveName) Then
            MountedDrive.HDLDriveName = GetHDLDriveName()
            'Retry
            InstallApp()
        Else
            If PreparedProjectsComboBox.SelectedItem IsNot Nothing Then
                'Proceed to installation on HDD
                Dim SelectedProject As ComboBoxProjectItem = CType(PreparedProjectsComboBox.SelectedItem, ComboBoxProjectItem)
                Dim HomebrewTitle As String = File.ReadAllLines(SelectedProject.ProjectFile)(0).Split("="c)(1)
                Dim HomebrewELF As String = File.ReadAllLines(SelectedProject.ProjectFile)(3).Split("="c)(1)
                Dim HomebrewPartition As String

                CurrentProjectDirectory = File.ReadAllLines(SelectedProject.ProjectFile)(2).Split("="c)(1)

                If HomebrewTitle.Contains("Open PS2 Loader") Or HomebrewTitle.Contains("OPL") Then
                    HomebrewPartition = "PP.APPS-00001..OPL"
                ElseIf HomebrewTitle.Contains("LaunchELF") Or HomebrewTitle.Contains("uLE") Or HomebrewTitle.Contains("wLE") Then
                    HomebrewPartition = "PP.APPS-00002..WLE"
                ElseIf HomebrewTitle.Contains("hdl_srv") Or HomebrewTitle.Contains("hdl_server") Or HomebrewTitle.Contains("hdl server") Then
                    HomebrewPartition = "PP.APPS-00003..HDL"
                ElseIf HomebrewTitle.Contains("SMS") Or HomebrewTitle.Contains("Simple Media System") Then
                    HomebrewPartition = "PP.APPS-00004..SMS"
                ElseIf HomebrewTitle.Contains("GSM") Then
                    HomebrewPartition = "PP.APPS-00005..GSM"
                Else
                    HomebrewPartition = InputBox("Please enter a valid partition name:", "Could not determine partition for this homebrew.", "PP.APPS-00001..TITLE")
                End If

                StatusLabel.Text = "Creating partition, please wait..."

                If Not String.IsNullOrEmpty(HomebrewPartition) Then
                    LockUI()
                    CreateHomebrewPartition(HomebrewPartition)
                Else
                    MsgBox("Partition name cannot be empty! Please try again.", MsgBoxStyle.Exclamation, "Error")
                    Exit Sub
                End If
            End If
        End If
    End Sub

    Private Sub InstallGame()
        'Check if drive is already identified, if not get the drive name
        If MountedDrive.HDLDriveName = "" Then
            MountedDrive.HDLDriveName = GetHDLDriveName()
            'Retry
            InstallGame()
        Else
            If PreparedProjectsComboBox.SelectedItem IsNot Nothing Then
                'Proceed to installation on HDD
                Dim SelectedProject As ComboBoxProjectItem = CType(PreparedProjectsComboBox.SelectedItem, ComboBoxProjectItem)
                Dim GameTitle As String = File.ReadAllLines(SelectedProject.ProjectFile)(0).Split("="c)(1)
                Dim GameID As String = File.ReadAllLines(SelectedProject.ProjectFile)(1).Split("="c)(1)
                Dim GameISO As String = File.ReadAllLines(SelectedProject.ProjectFile)(3).Split("="c)(1)

                HDLGameID = File.ReadAllLines(SelectedProject.ProjectFile)(1).Split("="c)(1).Replace("_", "-").Replace(".", "").Trim()
                CurrentProjectDirectory = File.ReadAllLines(SelectedProject.ProjectFile)(2).Split("="c)(1)

                HDL_Dump = New Process()
                HDL_Dump.StartInfo.FileName = My.Computer.FileSystem.CurrentDirectory + "\Tools\hdl_dump.exe"
                HDL_Dump.StartInfo.RedirectStandardOutput = True
                AddHandler HDL_Dump.OutputDataReceived, AddressOf HDLDumpOutputDataHandler
                HDL_Dump.StartInfo.UseShellExecute = False
                HDL_Dump.StartInfo.CreateNoWindow = True
                HDL_Dump.EnableRaisingEvents = True

                'Check if CD or DVD
                If GetDiscType(GameISO) = DiscType.DVD Then
                    LockUI() 'Disable UI controls

                    HDL_Dump.StartInfo.Arguments = "inject_dvd " + MountedDrive.HDLDriveName + " """ + GameTitle + """ """ + GameISO + """ " + GameID + " *u4 -hide"
                    HDL_Dump.Start()
                    HDL_Dump.BeginOutputReadLine()
                Else
                    LockUI()

                    HDL_Dump.StartInfo.Arguments = "inject_cd " + MountedDrive.HDLDriveName + " """ + GameTitle + """ """ + GameISO + """ " + GameID + " *u4 -hide"
                    HDL_Dump.Start()
                    HDL_Dump.BeginOutputReadLine()
                End If
            End If
        End If
    End Sub

    Private Sub CreateGamePartition()
        Dim CreatedGamePartition As String = ""

        'Get the created partition
        '1. List partitions
        Dim QueryOutput As String()
        Using HDLDump As New Process()
            HDLDump.StartInfo.FileName = My.Computer.FileSystem.CurrentDirectory + "\Tools\hdl_dump.exe"
            HDLDump.StartInfo.Arguments = "toc " + MountedDrive.HDLDriveName
            HDLDump.StartInfo.RedirectStandardOutput = True
            HDLDump.StartInfo.UseShellExecute = False
            HDLDump.StartInfo.CreateNoWindow = True
            HDLDump.Start()

            Dim OutputReader As StreamReader = HDLDump.StandardOutput
            QueryOutput = OutputReader.ReadToEnd().Split({vbCrLf}, StringSplitOptions.None)
        End Using

        '2. Search for the created hidden partition
        For Each HDDPartition As String In QueryOutput
            If Not String.IsNullOrEmpty(HDDPartition) Then
                If HDDPartition.Split(New String() {" "}, StringSplitOptions.RemoveEmptyEntries).Count >= 3 Then
                    HDDPartition = HDDPartition.Split(New String() {" "}, StringSplitOptions.RemoveEmptyEntries)(4)
                    If HDDPartition.Trim().StartsWith("__." + HDLGameID) Then 'The created hidden partition
                        CreatedGamePartition = HDDPartition.Trim()
                        Exit For
                    End If
                End If
            End If
        Next

        MsgBox(CreatedGamePartition.Replace("__.", "PP."))

        '3. Set mkpart command for the PP partition
        Using CommandFileWriter As New StreamWriter(My.Computer.FileSystem.CurrentDirectory + "\Tools\cmdlist\mkpart.txt", False)
            CommandFileWriter.WriteLine("device " + MountedDrive.DriveID)
            CommandFileWriter.WriteLine("mkpart " + CreatedGamePartition.Replace("__.", "PP.") + " 128M PFS")
            CommandFileWriter.WriteLine("exit")
        End Using

        '4. Proceed to partition creation
        Dim PFSShellOutput As String
        Using PFSShellProcess As New Process()
            PFSShellProcess.StartInfo.FileName = "cmd"
            PFSShellProcess.StartInfo.Arguments = """/c type """ + My.Computer.FileSystem.CurrentDirectory + "\Tools\cmdlist\mkpart.txt"" | """ + My.Computer.FileSystem.CurrentDirectory + "\Tools\pfsshell.exe"" 2>&1"

            PFSShellProcess.StartInfo.RedirectStandardOutput = True
            PFSShellProcess.StartInfo.UseShellExecute = False
            PFSShellProcess.StartInfo.CreateNoWindow = True

            PFSShellProcess.Start()

            Dim ShellReader As StreamReader = PFSShellProcess.StandardOutput
            Dim ProcessOutput As String = ShellReader.ReadToEnd()

            ShellReader.Close()
            PFSShellOutput = ProcessOutput
        End Using

        '5. Read partition creation output
        If PFSShellOutput.Contains("Main partition of 128M created.") Then

            If StatusLabel.Dispatcher.CheckAccess() = False Then
                StatusLabel.Dispatcher.BeginInvoke(Sub() StatusLabel.Text = "Partition created, modifying header...")
            Else
                StatusLabel.Text = "Partition created, modifying header..."
            End If

            '6. Modify the created partition
            ModifyPartitionHeader(CreatedGamePartition.Replace("__.", "PP."))
        Else
            MsgBox("There was an error in creating the game's PP partition, please check if the name doesn't already exists of if you have enough space.", MsgBoxStyle.Exclamation, "Error installing game")

            If Dispatcher.CheckAccess() = False Then
                Dispatcher.BeginInvoke(Sub()
                                           LockUI()
                                           StatusLabel.Text = ""
                                           ProgressGrid.Visibility = Visibility.Hidden
                                           PreparedProjectsComboBox.SelectedItem = Nothing
                                       End Sub)
            Else
                LockUI()
                StatusLabel.Text = ""
                ProgressGrid.Visibility = Visibility.Hidden
                PreparedProjectsComboBox.SelectedItem = Nothing
            End If

            Exit Sub
        End If
    End Sub

    Private Sub CreateHomebrewPartition(PartitionName As String)
        If PreparedProjectsComboBox.SelectedItem IsNot Nothing Then
            Dim SelectedProject As ComboBoxProjectItem = CType(PreparedProjectsComboBox.SelectedItem, ComboBoxProjectItem)
            Dim ProjectDirectory As String = File.ReadAllLines(SelectedProject.ProjectFile)(2).Split("="c)(1)

            '1. Set mkpart command for the PP partition
            Using CommandFileWriter As New StreamWriter(My.Computer.FileSystem.CurrentDirectory + "\Tools\cmdlist\mkpart.txt", False)
                CommandFileWriter.WriteLine("device " + MountedDrive.DriveID)
                CommandFileWriter.WriteLine("mkpart " + PartitionName + " 128M PFS")
                CommandFileWriter.WriteLine("exit")
            End Using

            '2. Proceed to partition creation
            Dim PFSShellOutput As String
            Using PFSShellProcess As New Process()
                PFSShellProcess.StartInfo.FileName = "cmd"
                PFSShellProcess.StartInfo.Arguments = """/c type """ + My.Computer.FileSystem.CurrentDirectory + "\Tools\cmdlist\mkpart.txt"" | """ + My.Computer.FileSystem.CurrentDirectory + "\Tools\pfsshell.exe"" 2>&1"

                PFSShellProcess.StartInfo.RedirectStandardOutput = True
                PFSShellProcess.StartInfo.UseShellExecute = False

                PFSShellProcess.Start()

                Dim ShellReader As StreamReader = PFSShellProcess.StandardOutput
                Dim ProcessOutput As String = ShellReader.ReadToEnd()

                ShellReader.Close()
                PFSShellOutput = ProcessOutput
            End Using

            '3. Read partition creation output
            If PFSShellOutput.Contains("Main partition of 128M created.") Then
                StatusLabel.Text = "Partition created, modifying header..."

                '4. Modify the created partition
                ModifyPartitionHeader(PartitionName)
            Else
                MsgBox("There was an error in creating the homebrew's PP partition." + vbCrLf + "Please check if the partition name '" + PartitionName + "' does not already exists of if HDD space is sufficient.", MsgBoxStyle.Exclamation, "Error while installing homebrew")
                Exit Sub
            End If
        End If
    End Sub

    Private Sub ModifyPartitionHeader(PartitionName As String)
        '1. Create a copy of hdl_dump in the project directory
        File.Copy(My.Computer.FileSystem.CurrentDirectory + "\Tools\hdl_dump.exe", CurrentProjectDirectory + "\hdl_dump.exe", True)

        '2. Switch to project directory and inject the files
        Directory.SetCurrentDirectory(CurrentProjectDirectory)

        '3. Modify the partition header using hdl_dump
        Dim HDLDumpOutput As String = ""
        Using HDLDump As New Process()
            HDLDump.StartInfo.FileName = "hdl_dump.exe"
            HDLDump.StartInfo.Arguments = "modify_header " + MountedDrive.HDLDriveName + " " + PartitionName
            HDLDump.StartInfo.RedirectStandardOutput = True
            HDLDump.StartInfo.UseShellExecute = False
            HDLDump.StartInfo.CreateNoWindow = True
            HDLDump.Start()

            HDLDumpOutput = HDLDump.StandardOutput.ReadToEnd()
        End Using

        '4. Read hdl_dump output
        If Not HDLDumpOutput.Contains("partition not found:") Then

            If StatusLabel.Dispatcher.CheckAccess() = False Then
                StatusLabel.Dispatcher.BeginInvoke(Sub() StatusLabel.Text = "Partition header modified, adding files...")
            Else
                StatusLabel.Text = "Partition header modified, adding files..."
            End If

            '5. Add files to the partition
            AddFilesToPartition(PartitionName)
        Else
            MsgBox("There was an error while modifying the partition, please check if you have enough space and report the next error." + vbCrLf + HDLDumpOutput, MsgBoxStyle.Exclamation, "Error installing game")
            'Set the current directory back
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory)
            Exit Sub
        End If
    End Sub

    Private Sub AddFilesToPartition(PartitionName As String)
        'Now put the "res" folder and EXECUTE.KELF file into the partition
        Dim PFSShellOutput As String

        'Set the mkdir & put commands
        Using CommandFileWriter As New StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "Tools\cmdlist\push.txt", False)
            CommandFileWriter.WriteLine("device " + MountedDrive.DriveID)
            CommandFileWriter.WriteLine("mount " + PartitionName)
            CommandFileWriter.WriteLine("put EXECUTE.KELF")
            CommandFileWriter.WriteLine("mkdir res")
            CommandFileWriter.WriteLine("cd res")

            If File.Exists("res\info.sys") Then
                CommandFileWriter.WriteLine("put res\info.sys")
                CommandFileWriter.WriteLine("rename res\info.sys info.sys")
            End If
            If File.Exists("res\jkt_001.png") Then
                CommandFileWriter.WriteLine("put res\jkt_001.png")
                CommandFileWriter.WriteLine("rename res\jkt_001.png jkt_001.png")
            End If
            If File.Exists("res\jkt_002.png") Then
                CommandFileWriter.WriteLine("put res\jkt_002.png")
                CommandFileWriter.WriteLine("rename res\jkt_002.png jkt_002.png")
            End If
            If File.Exists("res\jkt_cp.png") Then
                CommandFileWriter.WriteLine("put res\jkt_cp.png")
                CommandFileWriter.WriteLine("rename res\jkt_cp.png jkt_cp.png")
            End If
            If File.Exists("res\man.xml") Then
                CommandFileWriter.WriteLine("put res\man.xml")
                CommandFileWriter.WriteLine("rename res\man.xml man.xml")
            End If
            If File.Exists("res\notice.jpg") Then
                CommandFileWriter.WriteLine("put res\notice.jpg")
                CommandFileWriter.WriteLine("rename res\notice.jpg notice.jpg")
            End If

            If Directory.Exists("res\image") Then
                CommandFileWriter.WriteLine("mkdir image")
                CommandFileWriter.WriteLine("cd image")

                If File.Exists("res\image\0.png") Then
                    CommandFileWriter.WriteLine("put res\image\0.png")
                    CommandFileWriter.WriteLine("rename res\image\0.png 0.png")
                End If
                If File.Exists("res\image\1.png") Then
                    CommandFileWriter.WriteLine("put res\image\1.png")
                    CommandFileWriter.WriteLine("rename res\image\1.png 1.png")
                End If
                If File.Exists("res\image\2.png") Then
                    CommandFileWriter.WriteLine("put res\image\2.png")
                    CommandFileWriter.WriteLine("rename res\image\2.png 2.png")
                End If
            End If

            CommandFileWriter.WriteLine("umount")
            CommandFileWriter.WriteLine("exit")
        End Using

        'Put all detected files to the partition
        Using PFSShellProcess As New Process()
            PFSShellProcess.StartInfo.FileName = "cmd"
            PFSShellProcess.StartInfo.Arguments = """/c type """ + AppDomain.CurrentDomain.BaseDirectory + "Tools\cmdlist\push.txt"" | """ + AppDomain.CurrentDomain.BaseDirectory + "Tools\pfsshell.exe"" 2>&1"
            PFSShellProcess.StartInfo.RedirectStandardOutput = True
            PFSShellProcess.StartInfo.UseShellExecute = False
            PFSShellProcess.StartInfo.CreateNoWindow = True

            PFSShellProcess.Start()
            PFSShellProcess.WaitForExit()

            Dim PFSShellReader As StreamReader = PFSShellProcess.StandardOutput
            Dim ProcessOutput As String = PFSShellReader.ReadToEnd()

            PFSShellReader.Close()
            PFSShellOutput = ProcessOutput
        End Using

        'Update UI
        If Dispatcher.CheckAccess() = False Then
            Dispatcher.BeginInvoke(Sub()
                                       LockUI()
                                       StatusLabel.Text = ""
                                       ProgressGrid.Visibility = Visibility.Hidden
                                       PreparedProjectsComboBox.SelectedItem = Nothing
                                   End Sub)
        Else
            LockUI()
            StatusLabel.Text = ""
            ProgressGrid.Visibility = Visibility.Hidden
            PreparedProjectsComboBox.SelectedItem = Nothing
        End If

        'Set the current directory back
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory)

        MsgBox("Installation completed!", MsgBoxStyle.Information, "Success")
    End Sub

    Public Sub HDLDumpOutputDataHandler(sender As Object, e As DataReceivedEventArgs)
        If Not String.IsNullOrEmpty(e.Data) Then

            If StatusLabel.CheckAccess() = False Then
                StatusLabel.Dispatcher.BeginInvoke(Sub() StatusLabel.Text = e.Data)
            Else
                StatusLabel.Text = e.Data
            End If

            Dim ProgressPercentage As Double = 0

            If Regex.Match(e.Data, "\d\d[%]+").Success Then
                If Double.TryParse(Regex.Match(e.Data, "\d\d[%]+").Value.Replace("%", ""), ProgressPercentage) = True Then
                    If StatusProgressBar.CheckAccess() = False Then
                        StatusProgressBar.Dispatcher.BeginInvoke(Sub() StatusProgressBar.Value = ProgressPercentage)
                    Else
                        StatusProgressBar.Value = ProgressPercentage
                    End If
                End If
            End If

        End If
    End Sub

    Private Sub HDL_Dump_Exited(sender As Object, e As EventArgs) Handles HDL_Dump.Exited
        HDL_Dump.CancelOutputRead()
        HDL_Dump.Dispose()

        'Proceed to game partition
        If StatusLabel.CheckAccess() = False Then
            StatusLabel.Dispatcher.BeginInvoke(Sub() StatusLabel.Text = "Creating game PP partition ...")
        Else
            StatusLabel.Text = "Creating game PP partition ..."
        End If

        CreateGamePartition()
    End Sub

    Private Sub ConnectDelay_Tick(sender As Object, e As EventArgs) Handles ConnectDelay.Tick
        'Get drive properties after the connect delay
        If IsNBDConnected() Then

            If InstallProjectButton.CheckAccess() = False Then
                InstallProjectButton.Dispatcher.BeginInvoke(Sub() InstallProjectButton.IsEnabled = True)
            Else
                InstallProjectButton.IsEnabled = True
            End If

            If NBDConnectionLabel.CheckAccess() = False Then
                NBDConnectionLabel.Dispatcher.BeginInvoke(Sub()
                                                              NBDConnectionLabel.Text = "Connected"
                                                              NBDConnectionLabel.Foreground = Brushes.Green
                                                          End Sub)
            Else
                NBDConnectionLabel.Text = "Connected"
                NBDConnectionLabel.Foreground = Brushes.Green
            End If

            If ConnectButton.CheckAccess() = False Then
                ConnectButton.Dispatcher.BeginInvoke(Sub() ConnectButton.Content = "Disconnect")
            Else
                ConnectButton.Content = "Disconnect"
            End If

            MsgBox("PSX HDD is now connected." + vbCrLf + "You can now install a project on the PSX.", MsgBoxStyle.Information, "Success")
        Else
            MsgBox("Could not connect to the PSX." + vbCrLf + "Please check the IP address.", MsgBoxStyle.Critical, "Error")
        End If

        ConnectDelay.Stop()
    End Sub

End Class
