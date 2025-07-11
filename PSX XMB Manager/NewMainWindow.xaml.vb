﻿Imports System.ComponentModel
Imports System.IO
Imports System.Net
Imports System.Windows.Media.Animation
Imports System.Windows.Threading
Imports PSX_XMB_Manager.Structs
Imports PSX_XMB_Manager.Utils

Public Class NewMainWindow

    Private MountedDrive As MountedPSXDrive = Nothing
    Private WNBDClientPath As String = ""
    Private DokanClientPath As String = ""

    Private WithEvents ConnectDelay As New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(1250)}
    Private WithEvents ContentDownloader As New WebClient()

    Private Sub NewMainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Title = String.Format("PSX XMB Manager - {0}.{1}.{2}", My.Application.Info.Version.Major, My.Application.Info.Version.Minor, My.Application.Info.Version.Build)

        If Not Directory.Exists(My.Computer.FileSystem.CurrentDirectory + "\Projects") Then
            'Set up a projects directory to save all created projects
            Directory.CreateDirectory(My.Computer.FileSystem.CurrentDirectory + "\Projects")
        Else
            'Load saved projects
            For Each SavedProject In Directory.GetFiles(My.Computer.FileSystem.CurrentDirectory + "\Projects", "*.CFG")

                Dim NewCBProjectItem As New ComboBoxProjectItem()
                If Not String.IsNullOrEmpty(Path.GetFullPath(SavedProject)) Then
                    NewCBProjectItem.ProjectFile = Path.GetFullPath(SavedProject)
                Else
                    MsgBox("A broken project has been detected: " + SavedProject + vbCrLf + vbCrLf + "It's recommended to remove this project and to recreate it.", MsgBoxStyle.Critical, "Error")
                End If
                If Not String.IsNullOrEmpty(Path.GetFileNameWithoutExtension(SavedProject)) Then
                    NewCBProjectItem.ProjectName = Path.GetFileNameWithoutExtension(SavedProject)
                Else
                    MsgBox("A broken project has been detected: " + SavedProject + vbCrLf + vbCrLf + "It's recommended to remove this project and to recreate it.", MsgBoxStyle.Critical, "Error")
                End If

                'Get project state of saved projects
                Dim ProjectState As String = ""
                If File.ReadAllLines(SavedProject).Length > 5 Then
                    If File.ReadAllLines(SavedProject)(5).Split("="c).Length > 1 Then
                        ProjectState = File.ReadAllLines(SavedProject)(5).Split("="c)(1)
                    Else
                        MsgBox("Cannot read the project state of: " + SavedProject + vbCrLf + vbCrLf + "It's recommended to remove this project and to recreate it.", MsgBoxStyle.Critical, "Error")
                    End If
                Else
                    MsgBox("Cannot find the project state of: " + SavedProject + vbCrLf + vbCrLf + "It's recommended to remove this project and to recreate it.", MsgBoxStyle.Critical, "Error")
                End If

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

        'Set wnbd-client path
        If File.Exists(My.Computer.FileSystem.SpecialDirectories.ProgramFiles + "\Ceph\bin\wnbd-client.exe") Then
            'Use installed wnbd client
            WNBDClientPath = My.Computer.FileSystem.SpecialDirectories.ProgramFiles + "\Ceph\bin\wnbd-client.exe"
        ElseIf File.Exists(My.Computer.FileSystem.CurrentDirectory + "\Tools\wnbd-client.exe") Then
            'Use included wnbd client
            WNBDClientPath = My.Computer.FileSystem.CurrentDirectory + "\Tools\wnbd-client.exe"
        End If
    End Sub

    Private Sub NewMainWindow_ContentRendered(sender As Object, e As EventArgs) Handles Me.ContentRendered
        If Not String.IsNullOrEmpty(WNBDClientPath) Then

            'Check if WNBD is installed
            Using WNBDClient As New Process()
                WNBDClient.StartInfo.FileName = WNBDClientPath
                WNBDClient.StartInfo.Arguments = "-v"
                WNBDClient.StartInfo.RedirectStandardOutput = True
                WNBDClient.StartInfo.UseShellExecute = False
                WNBDClient.StartInfo.CreateNoWindow = True
                WNBDClient.Start()

                Dim SplittedOutput As String() = WNBDClient.StandardOutput.ReadToEnd().Split({vbCrLf}, StringSplitOptions.None)

                Dim NBDDriverVersion As String
                If SplittedOutput.Length > 2 Then
                    If Not SplittedOutput(2).Trim() = "" Then
                        NBDDriverVersion = SplittedOutput(2).Trim().Split(":"c)(1).Trim()
                        NBDDriverVersionLabel.Text = NBDDriverVersion
                        NBDDriverVersionLabel.Foreground = Brushes.Green
                    Else
                        NBDDriverVersionLabel.Text = "Not installed"
                        NBDDriverVersionLabel.Foreground = Brushes.Red
                    End If
                End If
            End Using

            'Check if NBD is connected to the PSX
            Dim ConnectedNBDDriveName As String = IsNBDConnected(WNBDClientPath)
            If Not String.IsNullOrEmpty(ConnectedNBDDriveName) Then

                MountedDrive.NBDDriveName = ConnectedNBDDriveName

                Dim ConnectedIP As String = GetConnectedNBDIP(WNBDClientPath, ConnectedNBDDriveName)
                If Not String.IsNullOrEmpty(ConnectedIP) Then
                    PSXIPTextBox.Text = ConnectedIP
                    MountedDrive.ConnectedOnIP = ConnectedIP
                Else
                    MsgBox("Failed to retrieve the connected NBD IP", MsgBoxStyle.Critical)
                End If

                'Get HDL Drive Name
                Dim HDLDriveName As String = GetHDLDriveName()
                If Not String.IsNullOrEmpty(HDLDriveName) Then
                    MountStatusLabel.Text = "On " + HDLDriveName
                    MountStatusLabel.Foreground = Brushes.Green
                    MountedDrive.HDLDriveName = HDLDriveName
                End If

                'Get HDD path using wmic
                MountedDrive.DriveID = GetHDDID()
                If MountedDrive.DriveID = "WMIC_INSTALL_REQUIRED" Then
                    If MsgBox("WMIC is not installed on your system but is required." + vbCrLf +
                              "Please install it first from 'Optional features' in your Windows Settings before continuing." + vbCrLf +
                              "Restart PSX XMB Manager when done.", MsgBoxStyle.YesNo, "Warning") = MsgBoxResult.Yes Then
                        Process.Start("ms-settings:optionalfeatures")
                    End If
                End If

                InstallProjectButton.IsEnabled = True
                NBDConnectionLabel.Text = "Connected"
                NBDConnectionLabel.Foreground = Brushes.Green
                ConnectButton.Content = "Disconnect"
            Else
                'Check for local HDD
                Dim ConnectedLocalHDDDriveName As String = IsLocalHDDConnected()
                If Not String.IsNullOrEmpty(ConnectedLocalHDDDriveName) Then

                    MountStatusLabel.Text = "on " + ConnectedLocalHDDDriveName
                    MountStatusLabel.Foreground = Brushes.Green

                    MountedDrive.HDLDriveName = ConnectedLocalHDDDriveName
                    MountedDrive.DriveID = GetHDDID()

                    If MountedDrive.DriveID = "WMIC_INSTALL_REQUIRED" Then
                        If MsgBox("WMIC is not installed on your system but is required." + vbCrLf +
                                  "Please install it first from 'Optional features' in your Windows Settings before continuing." + vbCrLf +
                                  "Restart PSX XMB Manager when done.", MsgBoxStyle.YesNo, "Warning") = MsgBoxResult.Yes Then
                            Process.Start("ms-settings:optionalfeatures")
                        End If
                    End If

                    InstallProjectButton.IsEnabled = True
                    NBDConnectionStatusLabel.Text = "Local Connection:"
                    NBDConnectionLabel.Text = "Connected"

                    EnterIPLabel.Text = "Local PS2/PSX HDD detected & connected."
                    EnterIPLabel.TextAlignment = TextAlignment.Center
                    ConnectButton.IsEnabled = False
                    PSXIPTextBox.IsEnabled = False
                    PSXIPTextBox.Text = "Local Connection"
                    ConnectButton.Foreground = Brushes.Black

                    NBDConnectionLabel.Foreground = Brushes.Green
                    ConnectButton.Content = "Disabled"
                End If
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
                DokanClientPath = DokanLibraryFolder + "\dokanctl.exe"
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

    Private Sub ConnectDelay_Tick(sender As Object, e As EventArgs) Handles ConnectDelay.Tick
        'Get drive properties after the connect delay
        Dim ConnectedNBDDriveName As String = IsNBDConnected(WNBDClientPath)
        If Not String.IsNullOrEmpty(ConnectedNBDDriveName) Then
            MountedDrive.NBDDriveName = ConnectedNBDDriveName

            'Get HDL Drive Name
            Dim HDLDriveName As String = GetHDLDriveName()
            If Not String.IsNullOrEmpty(HDLDriveName) Then

                'Update UI
                If MountStatusLabel.CheckAccess() = False Then
                    MountStatusLabel.Dispatcher.BeginInvoke(Sub()
                                                                MountStatusLabel.Text = "On " + HDLDriveName
                                                                MountStatusLabel.Foreground = Brushes.Green
                                                            End Sub)
                Else
                    MountStatusLabel.Text = "On " + HDLDriveName
                    MountStatusLabel.Foreground = Brushes.Green
                End If

                MountedDrive.HDLDriveName = HDLDriveName
            End If

            'Get HDD drive path
            MountedDrive.DriveID = GetHDDID()
            If MountedDrive.DriveID = "WMIC_INSTALL_REQUIRED" Then
                If MsgBox("WMIC is not installed on your system but is required." + vbCrLf +
                              "Please install it first from 'Optional features' in your Windows Settings before continuing." + vbCrLf +
                              "Restart PSX XMB Manager when done.", MsgBoxStyle.YesNo, "Warning") = MsgBoxResult.Yes Then
                    Process.Start("ms-settings:optionalfeatures")
                End If
            End If

            If Dispatcher.CheckAccess() = False Then
                Dispatcher.BeginInvoke(Sub()
                                           InstallProjectButton.IsEnabled = True
                                           NBDConnectionLabel.Text = "Connected"
                                           NBDConnectionLabel.Foreground = Brushes.Green
                                           ConnectButton.Content = "Disconnect"
                                       End Sub)
            Else
                InstallProjectButton.IsEnabled = True
                NBDConnectionLabel.Text = "Connected"
                NBDConnectionLabel.Foreground = Brushes.Green
                ConnectButton.Content = "Disconnect"
            End If

            'Display warnings if HDD could not be determined
            If String.IsNullOrEmpty(MountedDrive.HDLDriveName) Then
                MsgBox("Could not determine the HDD drive name using 'hdl_dump'." + vbCrLf + "Make sure a PS2/PSX formatted HDD is connected.", MsgBoxStyle.Exclamation, "Warning")
            End If
            If String.IsNullOrEmpty(MountedDrive.DriveID) Then
                MsgBox("Could not determine the full HDD drive path using 'wmic'." + vbCrLf + "Make sure 'wmic' is installed on your PC and try again.", MsgBoxStyle.Exclamation, "Warning")
            End If

            MsgBox("PSX HDD is now connected." + vbCrLf + "You can now install a project on the PSX.", MsgBoxStyle.Information, "Success")
        Else
            'Check for local HDD
            Dim ConnectedLocalHDDDriveName As String = IsLocalHDDConnected()
            If Not String.IsNullOrEmpty(ConnectedLocalHDDDriveName) Then
                MountStatusLabel.Text = "on " + ConnectedLocalHDDDriveName
                MountStatusLabel.Foreground = Brushes.Green
                MountedDrive.HDLDriveName = ConnectedLocalHDDDriveName

                MountedDrive.DriveID = GetHDDID()
                If MountedDrive.DriveID = "WMIC_INSTALL_REQUIRED" Then
                    If MsgBox("WMIC is not installed on your system but is required." + vbCrLf +
                                  "Please install it first from 'Optional features' in your Windows Settings before continuing." + vbCrLf +
                                  "Restart PSX XMB Manager when done.", MsgBoxStyle.YesNo, "Warning") = MsgBoxResult.Yes Then
                        Process.Start("ms-settings:optionalfeatures")
                    End If
                End If

                'Display warnings if HDD could not be determined
                If String.IsNullOrEmpty(MountedDrive.HDLDriveName) Then
                    MsgBox("Could not determine the HDD drive name using 'hdl_dump'." + vbCrLf + "Make sure a PS2/PSX formatted HDD is connected.", MsgBoxStyle.Exclamation, "Warning")
                End If
                If String.IsNullOrEmpty(MountedDrive.DriveID) Then
                    MsgBox("Could not determine the full HDD drive path using 'wmic'." + vbCrLf + "Make sure 'wmic' is installed on your PC and try again.", MsgBoxStyle.Exclamation, "Warning")
                End If

                InstallProjectButton.IsEnabled = True
                NBDConnectionStatusLabel.Text = "Local Connection:"
                NBDConnectionLabel.Text = "Connected"

                EnterIPLabel.Text = "Local PS2/PSX HDD detected & connected."
                EnterIPLabel.TextAlignment = TextAlignment.Center
                ConnectButton.IsEnabled = False
                PSXIPTextBox.IsEnabled = False
                PSXIPTextBox.Text = "Local Connection"
                ConnectButton.Foreground = Brushes.Black

                NBDConnectionLabel.Foreground = Brushes.Green
                ConnectButton.Content = "Disabled"
            Else
                MsgBox("Could not connect to the PSX." + vbCrLf + "Please check the IP address.", MsgBoxStyle.Critical, "Error")
            End If
        End If

        ConnectDelay.Stop()
    End Sub

#Region "Menu"

    Private Sub StartMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles StartMenuItem.Click
        'Switch to the StartGrid
        StartMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF004671"), Color))
        ProjectsMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))
        PartitionManagerMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))
        PS1GameLibraryMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))
        PS2GameLibraryMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))
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
        PS1GameLibraryMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))
        PS2GameLibraryMenuItem.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))
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
            Dim NewPartitionManager As New PartitionManager() With {.ShowActivated = True, .MountedDrive = MountedDrive, .DokanCTLPath = DokanClientPath}
            NewPartitionManager.Show()
        End If
    End Sub

    Private Sub PS1GameLibraryMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles PS1GameLibraryMenuItem.Click
        Dim NewGameLibrary As New PS1GameLibrary() With {.ShowActivated = True}
        NewGameLibrary.Show()
    End Sub

    Private Sub PS2GameLibraryMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles PS2GameLibraryMenuItem.Click
        Dim NewGameLibrary As New GameLibrary() With {.ShowActivated = True, .MountedDrive = MountedDrive}
        NewGameLibrary.Show()
    End Sub

    Private Sub XMBToolsMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles XMBToolsMenuItem.Click
        Dim NewAssetsBrowser As New AssetsBrowser() With {.ShowActivated = True}
        NewAssetsBrowser.Show()
    End Sub

    Private Sub NBDDriverMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles NBDDriverMenuItem.Click
        Process.Start(New ProcessStartInfo("https://cloudbase.it/ceph-for-windows/") With {.UseShellExecute = True})
    End Sub

    Private Sub DokanDriverMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles DokanDriverMenuItem.Click
        Process.Start(New ProcessStartInfo("https://github.com/dokan-dev/dokany/releases") With {.UseShellExecute = True})
    End Sub

    Private Sub UtilitiesMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles UtilitiesMenuItem.Click
        Dim NewUtilities As New Utilities() With {.ShowActivated = True, .MountedDrive = MountedDrive}
        NewUtilities.Show()
    End Sub

#End Region

#Region "Projects"

    Public Sub ReloadProjects()
        ProjectListComboBox.Items.Clear()
        PreparedProjectsComboBox.Items.Clear()

        If Directory.Exists(My.Computer.FileSystem.CurrentDirectory + "\Projects") Then
            'Load saved projects
            For Each SavedProject In Directory.GetFiles(My.Computer.FileSystem.CurrentDirectory + "\Projects", "*.CFG")

                Dim NewCBProjectItem As New ComboBoxProjectItem()
                If Not String.IsNullOrEmpty(Path.GetFullPath(SavedProject)) Then
                    NewCBProjectItem.ProjectFile = Path.GetFullPath(SavedProject)
                Else
                    MsgBox("A broken project has been detected: " + SavedProject + vbCrLf + vbCrLf + "It's recommended to remove this project and to re-create it.", MsgBoxStyle.Critical, "Error")
                End If
                If Not String.IsNullOrEmpty(Path.GetFileNameWithoutExtension(SavedProject)) Then
                    NewCBProjectItem.ProjectName = Path.GetFileNameWithoutExtension(SavedProject)
                Else
                    MsgBox("A broken project has been detected: " + SavedProject + vbCrLf + vbCrLf + "It's recommended to remove this project and to re-create it.", MsgBoxStyle.Critical, "Error")
                End If

                'Get project state of saved projects
                Dim ProjectState As String = ""
                If File.ReadAllLines(SavedProject).Length > 5 Then
                    If File.ReadAllLines(SavedProject)(5).Split("="c).Length > 1 Then
                        ProjectState = File.ReadAllLines(SavedProject)(5).Split("="c)(1)
                    Else
                        MsgBox("Cannot read the project state of: " + SavedProject + vbCrLf + vbCrLf + "It's recommended to remove this project and to re-create it.", MsgBoxStyle.Critical, "Error")
                    End If
                Else
                    MsgBox("Cannot find the project state of: " + SavedProject + vbCrLf + vbCrLf + "It's recommended to remove this project and to re-create it.", MsgBoxStyle.Critical, "Error")
                End If

                If ProjectState = "FALSE" Then
                    ProjectListComboBox.Items.Add(NewCBProjectItem)
                Else
                    ProjectListComboBox.Items.Add(NewCBProjectItem)
                    PreparedProjectsComboBox.Items.Add(NewCBProjectItem)
                End If
            Next
        Else
            'Set up a projects directory to save all created projects
            Directory.CreateDirectory(My.Computer.FileSystem.CurrentDirectory + "\Projects")
        End If
    End Sub

    Private Sub NewHomebrewProjectMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles NewHomebrewProjectButton.Click
        Dim NewHomebrewProjectWindow As New NewAppProject() With {.ShowActivated = True}
        NewHomebrewProjectWindow.Show()
    End Sub

    Private Sub NewGameProjectMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles NewGameProjectButton.Click
        Dim NewGameProjectWindow As New NewGameProject() With {.ShowActivated = True}
        NewGameProjectWindow.Show()
    End Sub

    Private Sub NewPS1GameProjectButton_Click(sender As Object, e As RoutedEventArgs) Handles NewPS1GameProjectButton.Click
        Dim NewGameProjectWindow As New NewPS1GameProject() With {.ShowActivated = True}
        NewGameProjectWindow.Show()
    End Sub

    Private Sub EditProjectButton_Click(sender As Object, e As RoutedEventArgs) Handles EditProjectButton.Click
        If ProjectListComboBox.SelectedItem IsNot Nothing Then
            'Get project infos
            Dim SelectedProject As ComboBoxProjectItem = CType(ProjectListComboBox.SelectedItem, ComboBoxProjectItem)
            Dim ProjectInfos As String() = File.ReadAllLines(SelectedProject.ProjectFile)

            If ProjectInfos.Length > 4 Then
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
                    Dim GameType As String = ProjectInfos(6).Split("="c)(1)
                    Dim GameInfos As String() = File.ReadAllLines(ProjectDirectory + "\icon.sys")

                    Select Case GameType
                        Case "PS1"
                            Dim GameProjectEditor As New NewPS1GameProject() With {.Title = "Editing project " + ProjectName + " - " + ProjectDirectory}
                            GameProjectEditor.ProjectNameTextBox.Text = ProjectName
                            GameProjectEditor.ProjectDirectoryTextBox.Text = ProjectDirectory
                            GameProjectEditor.ProjectTitleTextBox.Text = GameInfos(1).Split("="c)(1)
                            GameProjectEditor.ProjectIDTextBox.Text = ProjectSubtitle
                            GameProjectEditor.ProjectIDTextBox.Text = GameInfos(2).Split("="c)(1)
                            GameProjectEditor.ProjectUninstallMsgTextBox.Text = GameInfos(15).Split("="c)(1)

                            GameProjectEditor.IMAGE0PathTextBox.Text = ProjectFile
                            GameProjectEditor.DISCSInfoTextBox.AppendText(Path.GetFileName(ProjectFile) + vbCrLf)

                            'Check for multiple images and tick MultiDiscCheckBox if we have at least 2 discs
                            For Each ProjectFileLine As String In ProjectInfos
                                If ProjectFileLine.StartsWith("IMAGE1=") Then
                                    GameProjectEditor.IMAGE1PathTextBox.Text = ProjectInfos(7).Split("="c)(1)
                                    GameProjectEditor.MultiDiscCheckBox.IsChecked = True
                                    GameProjectEditor.DISCSInfoTextBox.AppendText(Path.GetFileName(ProjectInfos(7).Split("="c)(1)) + vbCrLf)
                                End If
                                If ProjectFileLine.StartsWith("IMAGE2=") Then
                                    GameProjectEditor.IMAGE2PathTextBox.Text = ProjectInfos(8).Split("="c)(1)
                                    GameProjectEditor.DISCSInfoTextBox.AppendText(Path.GetFileName(ProjectInfos(8).Split("="c)(1)) + vbCrLf)
                                End If
                                If ProjectFileLine.StartsWith("IMAGE3=") Then
                                    GameProjectEditor.IMAGE3PathTextBox.Text = ProjectInfos(9).Split("="c)(1)
                                    GameProjectEditor.DISCSInfoTextBox.AppendText(Path.GetFileName(ProjectInfos(9).Split("="c)(1)))
                                End If
                            Next

                            If File.Exists(ProjectDirectory + "\list.ico") Then
                                GameProjectEditor.ProjectIconPathTextBox.Text = ProjectDirectory + "\list.ico"
                            End If

                            GameProjectEditor.Show()
                        Case "PS2"
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
                    End Select
                End If
            Else
                MsgBox("A broken project has been detected: " + SelectedProject.ProjectFile + vbCrLf + vbCrLf + "It's recommended to remove this project and to re-create it.", MsgBoxStyle.Critical, "Error")
            End If
        End If
    End Sub

    Private Sub DeleteProjectButton_Click(sender As Object, e As RoutedEventArgs) Handles DeleteProjectButton.Click
        If ProjectListComboBox.SelectedItem IsNot Nothing Then
            Dim SelectedProject As ComboBoxProjectItem = CType(ProjectListComboBox.SelectedItem, ComboBoxProjectItem)
            If File.Exists(SelectedProject.ProjectFile) Then
                File.Delete(SelectedProject.ProjectFile)
                ProjectListComboBox.Items.Remove(SelectedProject)
                ReloadProjects()
            End If
        End If
    End Sub

    Private Sub PrepareProjectButton_Click(sender As Object, e As RoutedEventArgs) Handles PrepareProjectButton.Click
        If ProjectListComboBox.SelectedItem IsNot Nothing Then
            Dim SelectedProject As ComboBoxProjectItem = CType(ProjectListComboBox.SelectedItem, ComboBoxProjectItem)
            If File.ReadAllLines(SelectedProject.ProjectFile).Length > 5 Then
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

                        'Mark project as SIGNED
                        Dim ProjectConfigFileLines() As String = File.ReadAllLines(SelectedProject.ProjectFile)
                        ProjectConfigFileLines(5) = "SIGNED=TRUE"
                        File.WriteAllLines(SelectedProject.ProjectFile, ProjectConfigFileLines)

                        MsgBox("Homebrew Project prepared with success !" + vbCrLf + "You can now proceed with the installation on the PSX.", MsgBoxStyle.Information, "Success")
                        Activate()
                    Else

                        'PS1 games get POPSTARTER and PS2 games get OPL-Launcher
                        Dim GameType As String = File.ReadAllLines(SelectedProject.ProjectFile)(6).Split("="c)(1)

                        Select Case GameType
                            Case "PS1"
                                'Copy included POPSTARTER to project folder
                                If File.Exists(My.Computer.FileSystem.CurrentDirectory + "\Tools\POPSTARTER.KELF") Then
                                    File.Copy(My.Computer.FileSystem.CurrentDirectory + "\Tools\POPSTARTER.KELF", ProjectDIR + "\EXECUTE.KELF", True) 'Save as EXECUTE.KELF
                                Else
                                    MsgBox("POPSTARTER.KELF is missing in the Tools directory.", MsgBoxStyle.Critical, "Error setting up the project")
                                End If
                            Case "PS2"
                                'Copy included OPL-Launcher to project folder
                                If File.Exists(My.Computer.FileSystem.CurrentDirectory + "\Tools\EXECUTE.KELF") Then
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
                        End Select

                        'Mark project as SIGNED
                        Dim ProjectConfigFileLines() As String = File.ReadAllLines(SelectedProject.ProjectFile)
                        ProjectConfigFileLines(5) = "SIGNED=TRUE"
                        File.WriteAllLines(SelectedProject.ProjectFile, ProjectConfigFileLines)

                        MsgBox("Game Project is now prepared !" + vbCrLf + "You can now proceed with the installation on the PSX.", MsgBoxStyle.Information, "Success")
                        Activate()
                    End If
                End If

                ReloadProjects()
            Else
                MsgBox("A broken project has been detected: " + SelectedProject.ProjectFile + vbCrLf + vbCrLf + "It's recommended to remove this project and to re-create it.", MsgBoxStyle.Critical, "Error")
            End If
        End If
    End Sub

    Private Sub InstallProjectButton_Click(sender As Object, e As RoutedEventArgs) Handles InstallProjectButton.Click
        If String.IsNullOrEmpty(MountedDrive.HDLDriveName) Then
            MsgBox("Please connect to the NBD server first.", MsgBoxStyle.Information)
        Else
            If PreparedProjectsComboBox.SelectedItem IsNot Nothing Then
                Dim SelectedProject As ComboBoxProjectItem = CType(PreparedProjectsComboBox.SelectedItem, ComboBoxProjectItem)
                If File.Exists(SelectedProject.ProjectFile) Then
                    If File.ReadAllLines(SelectedProject.ProjectFile).Length > 6 Then
                        Dim ProjectTitle As String = File.ReadAllLines(SelectedProject.ProjectFile)(0).Split("="c)(1)
                        If MsgBox("Do you really want to install " + ProjectTitle + " on your PSX ?", MsgBoxStyle.YesNo, "Please confirm") = MsgBoxResult.Yes Then

                            'Identify project type
                            Dim ProjectType As String = File.ReadAllLines(SelectedProject.ProjectFile)(4).Split("="c)(1)
                            Dim NewInstallWindow As New InstallWindow() With {.ProjectToInstall = SelectedProject, .MountedDrive = MountedDrive, .Title = "Installing " + ProjectTitle}

                            If ProjectType = "APP" Then
                                NewInstallWindow.InstallStatus = "Installing Homebrew, please wait..."
                                NewInstallWindow.InstallationProgressBar.IsIndeterminate = True
                                NewInstallWindow.ShowDialog()
                            ElseIf ProjectType = "GAME" Then
                                Dim GameType As String = File.ReadAllLines(SelectedProject.ProjectFile)(6).Split("="c)(1)

                                Select Case GameType
                                    Case "PS1"
                                        NewInstallWindow.InstallStatus = "Installing PS1 Game, do not close when it freezes or hangs."
                                        NewInstallWindow.InstallationProgressBar.IsIndeterminate = True
                                        NewInstallWindow.InstallForPS1 = True
                                    Case "PS2"
                                        NewInstallWindow.InstallStatus = "Installing PS2 Game, please wait..."
                                        NewInstallWindow.InstallForPS2 = True
                                End Select

                                NewInstallWindow.ShowDialog()
                            End If

                        Else
                            MsgBox("Installation aborted.", MsgBoxStyle.Information, "Aborted")
                        End If
                    Else
                        MsgBox("A broken project has been detected: " + SelectedProject.ProjectFile + vbCrLf + vbCrLf + "It's recommended to remove this project and to re-create it.", MsgBoxStyle.Critical, "Error")
                    End If
                Else
                    MsgBox("Could not find the selected project: " + SelectedProject.ProjectFile, MsgBoxStyle.Critical, "Error")
                End If
            Else
                MsgBox("No project selected.", MsgBoxStyle.Critical, "Error")
            End If
        End If
    End Sub

#End Region

End Class
