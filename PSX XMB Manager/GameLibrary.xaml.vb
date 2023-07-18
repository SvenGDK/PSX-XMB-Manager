Imports System.ComponentModel
Imports System.IO
Imports System.Windows.Forms
Imports System.Windows.Threading
Imports PSX_XMB_Manager.PartitionManager

Public Class GameLibrary

    Dim WithEvents HDLDump As New Process()
    Dim WithEvents HDLDump2 As New Process()

    Dim WithEvents GameLoaderWorker As New BackgroundWorker() With {.WorkerReportsProgress = True}
    Dim WithEvents PSXDatacenterBrowser As New WebBrowser() With {.ScriptErrorsSuppressed = True}
    Dim WithEvents NewLoadingWindow As New SyncWindow() With {.Title = "Loading PS2 files", .ShowActivated = True, .WindowStartupLocation = WindowStartupLocation.CenterScreen}

    'Used for game infos and covers
    Dim URLs As New List(Of String)
    Dim CurrentURL As Integer = 0

    Dim ISOCount As Integer = 0
    Dim GamePartitionsCount As Integer = 0
    Dim Partitions As New List(Of Partition)
    Dim GamePartitions As New List(Of GamePartition)

    Public SelectedGameToModify As PS2Game
    Dim NewDriveLetter As String
    Dim WithEvents MountDelay As New DispatcherTimer With {.Interval = TimeSpan.FromSeconds(1)}

    Dim ProcessOutputCommand As String = ""

    'Selected game context menu (PC)
    Dim WithEvents PCPS2GamesContextMenu As New Controls.ContextMenu()
    Dim WithEvents CreateProjectMenuItem As New Controls.MenuItem() With {.Header = "Create a game project", .Icon = New Controls.Image() With {.Source = New BitmapImage(New Uri("/Images/copy-icon.png", UriKind.Relative))}}

    'Selected game context menu (PSX)
    Dim WithEvents PSXPS2GamesContextMenu As New Controls.ContextMenu()
    Dim WithEvents ModifyPartitionMenuItem As New Controls.MenuItem() With {.Header = "Modify game partition", .Icon = New Controls.Image() With {.Source = New BitmapImage(New Uri("/Images/send-icon.png", UriKind.Relative))}}
    'Dim WithEvents DumpGameMenuItem As New Controls.MenuItem() With {.Header = "Dump game into ISO file", .Icon = New Controls.Image() With {.Source = New BitmapImage(New Uri("/Images/send-icon.png", UriKind.Relative))}}
    Dim WithEvents RemoveMenuItem As New Controls.MenuItem() With {.Header = "Remove", .Icon = New Controls.Image() With {.Source = New BitmapImage(New Uri("/Images/send-icon.png", UriKind.Relative))}}

    Private Sub GameLibrary_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        'ContextMenu for games on stored on PC
        PCPS2GamesContextMenu.Items.Add(CreateProjectMenuItem)

        'ContextMenu for games on PSX HDD
        PSXPS2GamesContextMenu.Items.Add(ModifyPartitionMenuItem)
        PSXPS2GamesContextMenu.Items.Add(RemoveMenuItem)
    End Sub

#Region "Game Loader"

    Public Function GetGameID(GameISO As String) As String
        Dim GameID As String = ""

        Using SevenZip As New Process()
            SevenZip.StartInfo.FileName = My.Computer.FileSystem.CurrentDirectory + "\Tools\7z.exe"
            SevenZip.StartInfo.Arguments = "l -ba """ + GameISO + """"
            SevenZip.StartInfo.RedirectStandardOutput = True
            SevenZip.StartInfo.UseShellExecute = False
            SevenZip.StartInfo.CreateNoWindow = True
            SevenZip.Start()

            'Read the output
            Dim OutputReader As StreamReader = SevenZip.StandardOutput
            Dim ProcessOutput As String() = OutputReader.ReadToEnd().Split(New String() {vbCrLf}, StringSplitOptions.None)

            For Each Line As String In ProcessOutput
                If Line.Contains("SLES_") Or Line.Contains("SLUS_") Or Line.Contains("SCES_") Or Line.Contains("SCUS_") Then
                    If Line.Contains("Volume:") Then 'ID found in the ISO Header
                        GameID = Line.Split(New String() {"Volume: "}, StringSplitOptions.RemoveEmptyEntries)(1)
                        Exit For
                    Else 'ID found in the ISO files
                        GameID = String.Join(" ", Line.Split(New Char() {}, StringSplitOptions.RemoveEmptyEntries)).Split(" "c)(5).Trim()
                        Exit For
                    End If
                End If
            Next
        End Using

        If GameID = "" Then
            Return "ID not found"
        Else
            Return GameID
        End If
    End Function

    Private Sub GameLoaderWorker_DoWork(sender As Object, e As DoWorkEventArgs) Handles GameLoaderWorker.DoWork

        'PS2 ISOs
        For Each GameISO In Directory.GetFiles(e.Argument.ToString, "*.iso", SearchOption.AllDirectories)

            Dim NewPS2Game As New PS2Game()
            Dim GameID As String = GetGameID(GameISO)
            Dim PS2ISOFileInfo As New FileInfo(GameISO)

            If GameID = "ID not found" Then

                NewPS2Game.GameTitle = "Unknown PS2 game"
                NewPS2Game.GameFilePath = GameISO
                NewPS2Game.GameSize = FormatNumber(PS2ISOFileInfo.Length / 1073741824, 2) + " GB"
                NewPS2Game.GameID = "Unknown ID"

                'Update progress
                Dispatcher.BeginInvoke(Sub() NewLoadingWindow.LoadProgressBar.Value += 1)
                Dispatcher.BeginInvoke(Sub() NewLoadingWindow.LoadStatusTextBlock.Text = "Loading ISO " + NewLoadingWindow.LoadProgressBar.Value.ToString() + " of " + ISOCount.ToString())

                'Add to the ListView
                If GamesListView.Dispatcher.CheckAccess() = False Then
                    GamesListView.Dispatcher.BeginInvoke(Sub() GamesListView.Items.Add(NewPS2Game))
                Else
                    GamesListView.Items.Add(NewPS2Game)
                End If

            Else
                GameID = GameID.Replace(".", "").Replace("_", "-").Trim()

                NewPS2Game.GameSize = FormatNumber(PS2ISOFileInfo.Length / 1073741824, 2) + " GB"
                NewPS2Game.GameID = GameID
                NewPS2Game.GameFilePath = GameISO

                'Update progress
                Dispatcher.BeginInvoke(Sub() NewLoadingWindow.LoadProgressBar.Value += 1)
                Dispatcher.BeginInvoke(Sub() NewLoadingWindow.LoadStatusTextBlock.Text = "Loading ISO " + NewLoadingWindow.LoadProgressBar.Value.ToString() + " of " + ISOCount.ToString())

                If Utils.IsURLValid("https://psxdatacenter.com/psx2/games2/" + GameID + ".html") Then
                    URLs.Add("https://psxdatacenter.com/psx2/games2/" + GameID + ".html")
                Else
                    Dispatcher.BeginInvoke(Sub()
                                               NewPS2Game.GameCoverSource = New BitmapImage(New Uri("/Images/blankcover.png", UriKind.RelativeOrAbsolute))
                                           End Sub)
                    NewPS2Game.GameTitle = GetGameTitleFromDatabaseList(GameID)
                End If

                'Add to the ListView
                If GamesListView.Dispatcher.CheckAccess() = False Then
                    GamesListView.Dispatcher.BeginInvoke(Sub() GamesListView.Items.Add(NewPS2Game))
                Else
                    GamesListView.Items.Add(NewPS2Game)
                End If
            End If
        Next

    End Sub

    Private Sub GameLoaderWorker_RunWorkerCompleted(sender As Object, e As RunWorkerCompletedEventArgs) Handles GameLoaderWorker.RunWorkerCompleted
        NewLoadingWindow.LoadStatusTextBlock.Text = "Getting " + URLs.Count.ToString() + " available infos with covers"
        NewLoadingWindow.LoadProgressBar.Value = 0
        NewLoadingWindow.LoadProgressBar.Maximum = URLs.Count

        GetGameCovers()
    End Sub

    Private Sub GetGameCovers()
        PSXDatacenterBrowser.Navigate(URLs.Item(0))
    End Sub

    Private Sub PSXDatacenterBrowser_DocumentCompleted(sender As Object, e As WebBrowserDocumentCompletedEventArgs) Handles PSXDatacenterBrowser.DocumentCompleted
        On Error Resume Next
        RemoveHandler PSXDatacenterBrowser.DocumentCompleted, AddressOf PSXDatacenterBrowser_DocumentCompleted

        Dim GameTitle As String = ""
        Dim GameID As String = ""
        Dim GameRegion As String = ""
        Dim GameGenre As String = ""
        Dim GameDeveloper As String = ""
        Dim GamePublisher As String = ""
        Dim GameReleaseDate As String = ""
        Dim GameDescription As String = ""
        Dim GameCoverURL As String = ""
        Dim GameCoverImage As ImageSource = Nothing
        Dim GamePublisherWebsite As String = ""

        'Get game infos
        Dim infoTable As HtmlElement = PSXDatacenterBrowser.Document.GetElementById("table4")
        Dim infoRows As HtmlElementCollection = PSXDatacenterBrowser.Document.GetElementsByTagName("tr")

        'Game Title
        If infoRows.Item(4).InnerText IsNot Nothing Then
            GameTitle = infoRows.Item(4).InnerText.Split(New String() {"OFFICIAL TITLE "}, StringSplitOptions.RemoveEmptyEntries)(0)
        End If

        'Game ID
        If infoRows.Item(6).InnerText IsNot Nothing Then
            GameID = infoRows.Item(6).InnerText.Split(New String() {"SERIAL NUMBER(S) "}, StringSplitOptions.RemoveEmptyEntries)(0)
        End If

        'Region
        If infoRows.Item(7).InnerText IsNot Nothing Then
            Dim Region As String = infoRows.Item(7).InnerText.Split(New String() {"REGION "}, StringSplitOptions.RemoveEmptyEntries)(0)
            Select Case Region
                Case "PAL"
                    GameRegion = "Europe"
                Case "NTSC-U"
                    GameRegion = "US"
                Case "NTSC-J"
                    GameRegion = "Japan"
            End Select
        End If

        'Genre
        If infoRows.Item(8).InnerText IsNot Nothing Then
            GameGenre = infoRows.Item(8).InnerText.Split(New String() {"GENRE / STYLE "}, StringSplitOptions.RemoveEmptyEntries)(0)
        End If

        'Developer
        If infoRows.Item(9).InnerText IsNot Nothing Then
            GameDeveloper = infoRows.Item(9).InnerText.Split(New String() {"DEVELOPER "}, StringSplitOptions.RemoveEmptyEntries)(0)
        End If

        'Publisher
        If infoRows.Item(10).InnerText IsNot Nothing Then
            GamePublisher = infoRows.Item(10).InnerText.Split(New String() {"PUBLISHER "}, StringSplitOptions.RemoveEmptyEntries)(0)

            If GamePublisher.Contains("2K") Then
                GamePublisherWebsite = "https://2k.com/"
            ElseIf GamePublisher.Contains("Activision") Then
                GamePublisherWebsite = "https://www.activision.com/"
            ElseIf GamePublisher.Contains("Bandai") Then
                GamePublisherWebsite = "http://www.bandai.com/"
            ElseIf GamePublisher.Contains("Capcom") Then
                GamePublisherWebsite = "http://www.capcom.com/"
            ElseIf GamePublisher.Contains("Electronic Arts") Then
                GamePublisherWebsite = "http://ea.com/"
            ElseIf GamePublisher.Contains("EA Sports") Then
                GamePublisherWebsite = "https://www.easports.com/"
            ElseIf GamePublisher.Contains("Konami") Then
                GamePublisherWebsite = "https://www.konami.com/"
            ElseIf GamePublisher.Contains("Rockstar Games") Then
                GamePublisherWebsite = "https://www.rockstargames.com/"
            ElseIf GamePublisher.Contains("Sega") Then
                GamePublisherWebsite = "http://sega.com/"
            ElseIf GamePublisher.Contains("Sony Computer Entertainment") Then
                GamePublisherWebsite = "https://www.sie.com/en/index.html"
            ElseIf GamePublisher.Contains("THQ") Then
                GamePublisherWebsite = "https://www.thqnordic.com/"
            ElseIf GamePublisher.Contains("Ubisoft") Then
                GamePublisherWebsite = "https://www.ubisoft.com/"
            End If
        End If

        'Release Date
        If infoRows.Item(11).InnerText IsNot Nothing Then
            GameReleaseDate = infoRows.Item(11).InnerText.Split(New String() {"DATE RELEASED "}, StringSplitOptions.RemoveEmptyEntries)(0)
        End If

        'Get the game cover
        If PSXDatacenterBrowser.Document.GetElementById("table2") IsNot Nothing Then
            GameCoverURL = PSXDatacenterBrowser.Document.GetElementById("table2").GetElementsByTagName("img")(1).GetAttribute("src")
            GameCoverImage = New BitmapImage(New Uri(PSXDatacenterBrowser.Document.GetElementById("table2").GetElementsByTagName("img")(1).GetAttribute("src")))
        End If

        'Get the game description
        If PSXDatacenterBrowser.Document.GetElementById("table16") IsNot Nothing Then
            GameDescription = PSXDatacenterBrowser.Document.GetElementById("table16").GetElementsByTagName("tr")(0).InnerText
        End If

        'Add the infos to the game
        If Not GameID = "" Then
            For Each Game In GamesListView.Items
                Dim FoundGame As PS2Game = CType(Game, PS2Game)

                If FoundGame.GameID.Contains(GameID) Or FoundGame.GameID = GameID Then
                    FoundGame.GameTitle = GameTitle
                    FoundGame.GameRegion = GameRegion
                    FoundGame.GameDescription = GameDescription
                    FoundGame.GameGenre = GameGenre
                    FoundGame.GameDeveloper = GameDeveloper
                    FoundGame.GamePublisher = GamePublisher
                    FoundGame.GameWebsite = GamePublisherWebsite
                    FoundGame.GameReleaseDate = GameReleaseDate
                    FoundGame.GameCoverURL = GameCoverURL
                    FoundGame.GameCoverSource = GameCoverImage
                    Exit For
                End If
            Next
        End If

        'Continue
        AddHandler PSXDatacenterBrowser.DocumentCompleted, AddressOf PSXDatacenterBrowser_DocumentCompleted

        If CurrentURL < URLs.Count Then
            PSXDatacenterBrowser.Navigate(URLs.Item(CurrentURL))
            CurrentURL += 1
            NewLoadingWindow.LoadProgressBar.Value = CurrentURL
        Else
            CurrentURL = 0
            URLs.Clear()
            NewLoadingWindow.Close()
            GamesListView.Items.Refresh()
        End If

    End Sub

    Private Sub LoadGamePartitions()
        'HDL TOC of the PSX HDD
        HDLDump.StartInfo.FileName = My.Computer.FileSystem.CurrentDirectory + "\Tools\hdl_dump.exe"
        HDLDump.StartInfo.Arguments = "hdl_toc " + NewMainWindow.MountedDrive.HDLDriveName
        HDLDump.StartInfo.RedirectStandardOutput = True
        AddHandler HDLDump.OutputDataReceived, AddressOf OutputDataHandler
        HDLDump.StartInfo.UseShellExecute = False
        HDLDump.StartInfo.CreateNoWindow = True
        HDLDump.Start()
        HDLDump.BeginOutputReadLine()
    End Sub

    Private Sub LoadPartitions()
        'TOC of the PSX HDD
        HDLDump2.StartInfo.FileName = My.Computer.FileSystem.CurrentDirectory + "\Tools\hdl_dump.exe"
        HDLDump2.StartInfo.Arguments = "toc " + NewMainWindow.MountedDrive.HDLDriveName
        HDLDump2.StartInfo.RedirectStandardOutput = True
        AddHandler HDLDump2.OutputDataReceived, AddressOf FullOutputDataHandler
        HDLDump2.StartInfo.UseShellExecute = False
        HDLDump2.StartInfo.CreateNoWindow = True
        HDLDump2.EnableRaisingEvents = True
        HDLDump2.Start()
        HDLDump2.BeginOutputReadLine()
    End Sub

    Public Function GetGameTitleFromDatabaseList(GameID As String) As String
        Dim FoundGameTitle As String = ""
        GameID = GameID.Replace("-", "")

        For Each GameTitle As String In File.ReadLines(My.Computer.FileSystem.CurrentDirectory + "\Tools\ids.txt")
            If GameTitle.Contains(GameID) Then
                FoundGameTitle = GameTitle.Split(";"c)(1)
                Exit For
            End If
        Next

        If String.IsNullOrEmpty(FoundGameTitle) Then
            Return "Unknown PS2 game"
        Else
            Return FoundGameTitle
        End If
    End Function

#End Region

#Region "Partition Mounting"

    Private Sub MountPartition(PartitionName As String, DriveID As String, VolumeName As String)
        'Get a free drive letter
        Dim NewDriveLetter As String = Utils.FindNextAvailableDriveLetter()

        'Mount the drive using pfsfuse
        Using PFSFuse As New Process()
            PFSFuse.StartInfo.FileName = My.Computer.FileSystem.CurrentDirectory + "\Tools\pfsfuse.exe"
            PFSFuse.StartInfo.Arguments = "--partition=" + PartitionName + " " + DriveID + " " + NewDriveLetter + " -o volname=""" + VolumeName + """"
            PFSFuse.StartInfo.UseShellExecute = False
            PFSFuse.StartInfo.CreateNoWindow = True
            PFSFuse.Start()
        End Using

        'Assign values to the game
        SelectedGameToModify.AssignedPartitionDriveLetter = NewDriveLetter
        SelectedGameToModify.PartitionName = PartitionName
    End Sub

    Private Sub HDLDump2_Exited(sender As Object, e As EventArgs) Handles HDLDump2.Exited
        If ProcessOutputCommand = "ModifyPartition" Then
            If NewLoadingWindow.Dispatcher.CheckAccess() = False Then
                NewLoadingWindow.Dispatcher.BeginInvoke(Sub()
                                                            NewLoadingWindow.LoadStatusTextBlock.Text = "Mounting partition with pfsfuse"
                                                            NewLoadingWindow.LoadProgressBar.IsIndeterminate = False
                                                            NewLoadingWindow.LoadProgressBar.Value = 0
                                                            NewLoadingWindow.LoadProgressBar.Maximum = 7
                                                        End Sub)
            Else
                NewLoadingWindow.LoadStatusTextBlock.Text = "Mounting partition with pfsfuse"
                NewLoadingWindow.LoadProgressBar.IsIndeterminate = False
                NewLoadingWindow.LoadProgressBar.Value = 0
                NewLoadingWindow.LoadProgressBar.Maximum = 7
            End If

            For Each Part As Partition In Partitions
                If Part.Name.StartsWith("PP." + SelectedGameToModify.GameID) Then
                    'Mount the partition as volume
                    MountPartition(Part.Name, NewMainWindow.MountedDrive.DriveID, SelectedGameToModify.GameTitle)
                    Exit For
                End If
            Next

            HDLDump2.CancelOutputRead()

            'Mounting with pfsfuse does have a little delay before the drive shows up in the explorer
            MountDelay.Start()

        ElseIf ProcessOutputCommand = "DeletePartition" Then

            Dim HiddenGamePartition As String = ""
            Dim GamePPPartition As String = ""

            For Each Part As Partition In Partitions
                If Part.Name.StartsWith("PP." + SelectedGameToModify.GameID) Then
                    GamePPPartition = Part.Name
                ElseIf Part.Name.StartsWith("__." + SelectedGameToModify.GameID) Then
                    HiddenGamePartition = Part.Name
                End If
            Next

            '
            'Partitions will be deleted separately in case there's no PP or hidden partition
            '
            'Delete the PP partition
            If Not String.IsNullOrEmpty(GamePPPartition) Then

                'Set rmpart command
                Using CommandFileWriter As New StreamWriter(My.Computer.FileSystem.CurrentDirectory + "\Tools\cmdlist\rmpart.txt", False)
                    CommandFileWriter.WriteLine("device " + NewMainWindow.MountedDrive.DriveID)
                    CommandFileWriter.WriteLine("rmpart " + GamePPPartition)
                    CommandFileWriter.WriteLine("exit")
                End Using

                'Proceed to partition deletion
                Dim PFSShellOutput As String
                Using PFSShellProcess As New Process()
                    PFSShellProcess.StartInfo.FileName = "cmd"
                    PFSShellProcess.StartInfo.Arguments = """/c type """ + My.Computer.FileSystem.CurrentDirectory + "\Tools\cmdlist\rmpart.txt"" | """ + My.Computer.FileSystem.CurrentDirectory + "\Tools\pfsshell.exe"" 2>&1"

                    PFSShellProcess.StartInfo.RedirectStandardOutput = True
                    PFSShellProcess.StartInfo.UseShellExecute = False
                    PFSShellProcess.StartInfo.CreateNoWindow = True

                    PFSShellProcess.Start()

                    Dim ShellReader As StreamReader = PFSShellProcess.StandardOutput
                    Dim ProcessOutput As String = ShellReader.ReadToEnd()

                    ShellReader.Close()
                    PFSShellOutput = ProcessOutput
                End Using

                If PFSShellOutput.Contains("No such file or directory") Then
                    MsgBox("There was an error while deleting the game partition. More details :" + vbCrLf + PFSShellOutput, MsgBoxStyle.Exclamation, "Error")
                End If
            End If

            'Delete the hidden partition
            If Not String.IsNullOrEmpty(HiddenGamePartition) Then

                'Set rmpart command
                Using CommandFileWriter As New StreamWriter(My.Computer.FileSystem.CurrentDirectory + "\Tools\cmdlist\rmpart.txt", False)
                    CommandFileWriter.WriteLine("device " + NewMainWindow.MountedDrive.DriveID)
                    CommandFileWriter.WriteLine("rmpart " + HiddenGamePartition)
                    CommandFileWriter.WriteLine("exit")
                End Using

                'Proceed to partition deletion
                Dim PFSShellOutput As String
                Using PFSShellProcess As New Process()
                    PFSShellProcess.StartInfo.FileName = "cmd"
                    PFSShellProcess.StartInfo.Arguments = """/c type """ + My.Computer.FileSystem.CurrentDirectory + "\Tools\cmdlist\rmpart.txt"" | """ + My.Computer.FileSystem.CurrentDirectory + "\Tools\pfsshell.exe"" 2>&1"

                    PFSShellProcess.StartInfo.RedirectStandardOutput = True
                    PFSShellProcess.StartInfo.UseShellExecute = False
                    PFSShellProcess.StartInfo.CreateNoWindow = True

                    PFSShellProcess.Start()

                    Dim ShellReader As StreamReader = PFSShellProcess.StandardOutput
                    Dim ProcessOutput As String = ShellReader.ReadToEnd()

                    ShellReader.Close()
                    PFSShellOutput = ProcessOutput
                End Using

                If PFSShellOutput.Contains("No such file or directory") Then
                    MsgBox("There was an error while deleting the game partition. More details :" + vbCrLf + PFSShellOutput, MsgBoxStyle.Exclamation, "Error")
                End If
            End If

            'Reload
            NewLoadingWindow.Close()
            LoadPSXGamesButton_Click(LoadPSXGamesButton, New RoutedEventArgs())
        End If
    End Sub

    Private Sub MountDelay_Tick(sender As Object, e As EventArgs) Handles MountDelay.Tick
        If NewLoadingWindow.Dispatcher.CheckAccess() = False Then
            NewLoadingWindow.Dispatcher.BeginInvoke(Sub()
                                                        NewLoadingWindow.LoadProgressBar.Value += 1
                                                    End Sub)
        Else
            NewLoadingWindow.LoadProgressBar.Value += 1
        End If

        If NewLoadingWindow.LoadProgressBar.Value = 7 Then
            MountDelay.Stop()
            NewLoadingWindow.Close()

            If String.IsNullOrEmpty(SelectedGameToModify.AssignedPartitionDriveLetter) Then
                MsgBox("Could not mount the selected game.", MsgBoxStyle.Exclamation, "Game partition not found.")
            Else
                Dim NewGamePartitionManager As New GamePartitionManager With {.ShowActivated = True, .AssociatedDriveLetter = SelectedGameToModify.AssignedPartitionDriveLetter, .AssociatedPartition = SelectedGameToModify.PartitionName}
                NewGamePartitionManager.Show()
            End If

        End If
    End Sub

    Public Sub RemoveDriveLetterFromGame(DriveLetter As String)
        For Each Game In GamesListView.Items
            Dim FoundGame As PS2Game = CType(Game, PS2Game)
            If FoundGame.AssignedPartitionDriveLetter = DriveLetter Then
                FoundGame.AssignedPartitionDriveLetter = ""
                Exit For
            End If
        Next
    End Sub

#End Region

    Private Sub LoadGamesOnPCButton_Click(sender As Object, e As RoutedEventArgs) Handles LoadGamesOnPCButton.Click
        Dim FBD As New FolderBrowserDialog() With {.Description = "Please select a folder containing PS2 ISOs.", .ShowNewFolderButton = False}

        GameTitleTextBlock.Text = ""
        ReleaseDateTextBlock.Text = ""
        DeveloperTextBlock.Text = ""
        PublisherTextBlock.Text = ""
        GameSizeTextBlock.Text = ""
        WebsiteTextBlock.Text = ""
        GenreTextBlock.Text = ""
        RegionTextBlock.Text = ""
        GameIDTextBlock.Text = ""

        If FBD.ShowDialog() = Forms.DialogResult.OK Then
            If Not String.IsNullOrEmpty(FBD.SelectedPath) Then

                CurrentDirectoryTextBlock.Text = FBD.SelectedPath

                LoadGamesOnPCButton.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF004671"), Color))
                LoadPSXGamesButton.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))
                ReloadButton.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))

                GamesListView.Items.Clear()
                URLs.Clear()
                ISOCount = Directory.GetFiles(FBD.SelectedPath, "*.iso", SearchOption.AllDirectories).Count
                TotalGamesTextBlock.Text = ISOCount.ToString() + " Games"

                GamesListView.ContextMenu = PCPS2GamesContextMenu

                NewLoadingWindow = New SyncWindow() With {.Title = "Loading PS2 files", .ShowActivated = True}
                NewLoadingWindow.LoadProgressBar.Maximum = ISOCount
                NewLoadingWindow.LoadStatusTextBlock.Text = "Loading file 1 of " + ISOCount.ToString()
                NewLoadingWindow.Show()

                GameLoaderWorker.RunWorkerAsync(FBD.SelectedPath)
            End If
        End If
    End Sub

    Private Sub LoadPSXGamesButton_Click(sender As Object, e As RoutedEventArgs) Handles LoadPSXGamesButton.Click

        GameTitleTextBlock.Text = ""
        ReleaseDateTextBlock.Text = ""
        DeveloperTextBlock.Text = ""
        PublisherTextBlock.Text = ""
        GameSizeTextBlock.Text = ""
        WebsiteTextBlock.Text = ""
        GenreTextBlock.Text = ""
        RegionTextBlock.Text = ""
        GameIDTextBlock.Text = ""
        GamePartitionsCount = 0

        If NewMainWindow.MountedDrive.HDLDriveName = "" Then
            MsgBox("Please connect to the NBD server first.", MsgBoxStyle.Information)
        Else
            CurrentDirectoryTextBlock.Text = "PSX HDD"

            LoadGamesOnPCButton.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))
            LoadPSXGamesButton.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF004671"), Color))
            ReloadButton.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FF00619C"), Color))

            GamesListView.Items.Clear()
            URLs.Clear()
            GamePartitions.Clear()

            GamesListView.ContextMenu = PSXPS2GamesContextMenu

            NewLoadingWindow = New SyncWindow() With {.Title = "Loading games on PSX HDD", .ShowActivated = True, .WindowStartupLocation = WindowStartupLocation.CenterScreen}
            NewLoadingWindow.LoadProgressBar.IsIndeterminate = True
            NewLoadingWindow.LoadStatusTextBlock.Text = "Please wait"
            NewLoadingWindow.Show()

            LoadGamePartitions()
        End If
    End Sub

    Private Sub GamesListView_PreviewMouseWheel(sender As Object, e As MouseWheelEventArgs) Handles GamesListView.PreviewMouseWheel
        'The mouse wheel only scrolls vertically, this code allows scrolling horizontally
        Dim GamesListViewScrollViewer As ScrollViewer = TryCast(Utils.GetScrollViewer(GamesListView), ScrollViewer)

        If GamesListViewScrollViewer IsNot Nothing Then
            If e.Delta > 0 Then
                GamesListViewScrollViewer.ScrollToHorizontalOffset(GamesListViewScrollViewer.HorizontalOffset - 3)
            End If

            If e.Delta < 0 Then
                GamesListViewScrollViewer.ScrollToHorizontalOffset(GamesListViewScrollViewer.HorizontalOffset + 3)
            End If
        End If
    End Sub

    Public Sub OutputDataHandler(sender As Object, e As DataReceivedEventArgs)
        If Not String.IsNullOrEmpty(e.Data) Then
            If e.Data.StartsWith("DVD") Or e.Data.StartsWith("CD") Then 'Game found
                Dim NewPS2Game As New PS2Game()
                Dim GameSize = e.Data.Split({" "}, StringSplitOptions.RemoveEmptyEntries)(1).Trim().Replace("KB", "")
                Dim GameSizeInMB = CInt(GameSize) / 1024
                Dim GamePart As New GamePartition() With {.Type = e.Data.Split({" "}, StringSplitOptions.RemoveEmptyEntries)(0),
                    .Size = FormatNumber(GameSizeInMB, 2) + " MB",
                    .Flags = e.Data.Split({" "}, StringSplitOptions.RemoveEmptyEntries)(2),
                    .DMA = e.Data.Split({" "}, StringSplitOptions.RemoveEmptyEntries)(3),
                    .Startup = e.Data.Split({" "}, StringSplitOptions.RemoveEmptyEntries)(4),
                    .Name = e.Data.Split({"  "}, StringSplitOptions.RemoveEmptyEntries)(2)}

                Dim GameID As String = GamePart.Startup.Replace("_", "-").Replace(".", "")

                GamePartitions.Add(GamePart)

                NewPS2Game.GameSize = GamePart.Size
                NewPS2Game.GameID = GameID

                If Utils.IsURLValid("https://psxdatacenter.com/psx2/games2/" + GameID + ".html") Then
                    URLs.Add("https://psxdatacenter.com/psx2/games2/" + GameID + ".html")
                Else
                    Dispatcher.BeginInvoke(Sub()
                                               NewPS2Game.GameCoverSource = New BitmapImage(New Uri("/Images/blankcover.png", UriKind.RelativeOrAbsolute))
                                           End Sub)
                    NewPS2Game.GameTitle = GetGameTitleFromDatabaseList(GameID)
                End If

                GamePartitionsCount += 1

                'Add to the ListView
                If GamesListView.Dispatcher.CheckAccess() = False Then
                    GamesListView.Dispatcher.BeginInvoke(Sub() GamesListView.Items.Add(NewPS2Game))
                Else
                    GamesListView.Items.Add(NewPS2Game)
                End If

            ElseIf e.Data.StartsWith("total") Then 'Last line of hdl_dump hdl_toc - Load covers after getting the game list
                Dim HDDSizes As String() = e.Data.Split({","}, StringSplitOptions.RemoveEmptyEntries)
                Dim AvailableSpaceInGB = Utils.GetIntOnly(HDDSizes(2)) / 1024

                If Dispatcher.CheckAccess() = False Then
                    Dispatcher.BeginInvoke(Sub()
                                               CurrentDirectoryTextBlock.Text = "PSX HDD" + " - Available: " + FormatNumber(AvailableSpaceInGB, 2) + " GB"

                                               'Left space indicator
                                               If AvailableSpaceInGB > 16 Then
                                                   CurrentDirectoryTextBlock.Foreground = Brushes.Green
                                               ElseIf AvailableSpaceInGB >= 10 Then
                                                   CurrentDirectoryTextBlock.Foreground = Brushes.Orange
                                               ElseIf AvailableSpaceInGB <= 9 Then
                                                   CurrentDirectoryTextBlock.Foreground = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#FFC12249"), Color))
                                               End If

                                               TotalGamesTextBlock.Text = GamePartitionsCount.ToString() + " Games"
                                               NewLoadingWindow.LoadStatusTextBlock.Text = "Getting " + URLs.Count.ToString() + " available infos with covers."
                                               NewLoadingWindow.LoadProgressBar.IsIndeterminate = False
                                               NewLoadingWindow.LoadProgressBar.Value = 0
                                               NewLoadingWindow.LoadProgressBar.Maximum = URLs.Count
                                               NewLoadingWindow.Show()
                                           End Sub)
                Else
                    TotalGamesTextBlock.Text = GamePartitionsCount.ToString() + " Games"
                    NewLoadingWindow.LoadStatusTextBlock.Text = "Getting " + URLs.Count.ToString() + " available infos with covers."
                    NewLoadingWindow.LoadProgressBar.IsIndeterminate = False
                    NewLoadingWindow.LoadProgressBar.Value = 0
                    NewLoadingWindow.LoadProgressBar.Maximum = URLs.Count
                    NewLoadingWindow.Show()
                End If

                HDLDump.CancelOutputRead()
                GetGameCovers()
            End If
        End If
    End Sub

    Public Sub FullOutputDataHandler(sender As Object, e As DataReceivedEventArgs)
        If Not String.IsNullOrEmpty(e.Data) Then
            If e.Data.StartsWith("0") Then

                Dim Part As New Partition() With {.Type = e.Data.Split({" "}, StringSplitOptions.RemoveEmptyEntries)(0),
                    .Start = e.Data.Split({" "}, StringSplitOptions.RemoveEmptyEntries)(1),
                    .Parts = e.Data.Split({" "}, StringSplitOptions.RemoveEmptyEntries)(2),
                    .Size = e.Data.Split({" "}, StringSplitOptions.RemoveEmptyEntries)(3),
                    .Name = e.Data.Split({" "}, StringSplitOptions.RemoveEmptyEntries)(4)}

                Partitions.Add(Part)
            End If
        End If
    End Sub

    Private Sub GamesListView_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles GamesListView.SelectionChanged
        If GamesListView.SelectedItem IsNot Nothing Then
            Dim SelectedPS2Game As PS2Game = CType(GamesListView.SelectedItem, PS2Game)

            GameTitleTextBlock.Text = SelectedPS2Game.GameTitle
            ReleaseDateTextBlock.Text = SelectedPS2Game.GameReleaseDate
            DeveloperTextBlock.Text = SelectedPS2Game.GameDeveloper
            PublisherTextBlock.Text = SelectedPS2Game.GamePublisher
            GameSizeTextBlock.Text = SelectedPS2Game.GameSize
            WebsiteTextBlock.Text = SelectedPS2Game.GameWebsite
            GenreTextBlock.Text = SelectedPS2Game.GameGenre
            RegionTextBlock.Text = SelectedPS2Game.GameRegion
            GameIDTextBlock.Text = SelectedPS2Game.GameID

            Select Case SelectedPS2Game.GameRegion
                Case "Europe"
                    GameRegionImage.Source = New BitmapImage(New Uri("/Images/eu.png", UriKind.RelativeOrAbsolute))
                Case "US"
                    GameRegionImage.Source = New BitmapImage(New Uri("/Images/us.png", UriKind.RelativeOrAbsolute))
                Case "Japan"
                    GameRegionImage.Source = New BitmapImage(New Uri("/Images/jp.png", UriKind.RelativeOrAbsolute))
                Case Else
                    GameRegionImage.Source = Nothing
            End Select

        End If
    End Sub

    Private Sub CreateProjectMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles CreateProjectMenuItem.Click
        If GamesListView.SelectedItem IsNot Nothing Then
            Dim SelectedPS2Game As PS2Game = CType(GamesListView.SelectedItem, PS2Game)
            Dim GameProjectDirectory As String = SelectedPS2Game.GameTitle + " [" + SelectedPS2Game.GameID + "]"
            Dim NewGameProjectDirectory As String = My.Computer.FileSystem.CurrentDirectory + "\Projects\" + SelectedPS2Game.GameTitle + " [" + SelectedPS2Game.GameID + "]"

            Dim NewGameProjectWindow As New NewGameProject() With {.ShowActivated = True}
            Dim NewGameEditor As New GameEditor() With {.ProjectDirectory = NewGameProjectDirectory, .Title = "Game Ressources Editor - " + NewGameProjectDirectory}

            'Set project information
            NewGameProjectWindow.ProjectISOFileTextBox.Text = SelectedPS2Game.GameFilePath
            NewGameProjectWindow.ProjectNameTextBox.Text = SelectedPS2Game.GameTitle
            NewGameProjectWindow.ProjectDirectoryTextBox.Text = NewGameProjectDirectory
            NewGameProjectWindow.ProjectTitleTextBox.Text = SelectedPS2Game.GameTitle
            NewGameProjectWindow.ProjectIDTextBox.Text = SelectedPS2Game.GameID.Replace("-", "_").Insert(8, ".")
            NewGameProjectWindow.ProjectUninstallMsgTextBox.Text = "Do you want to uninstall this game ?"

            'Create game project directory
            If Not Directory.Exists(NewGameProjectDirectory) Then
                Directory.CreateDirectory(NewGameProjectDirectory)
            End If

            'Write Project settings to .CFG
            Using ProjectWriter As New StreamWriter(".\Projects\" + SelectedPS2Game.GameTitle + ".CFG", False)
                ProjectWriter.WriteLine("TITLE=" + SelectedPS2Game.GameTitle)
                ProjectWriter.WriteLine("ID=" + SelectedPS2Game.GameID.Replace("-", "_").Insert(8, "."))
                ProjectWriter.WriteLine("DIR=" + NewGameProjectDirectory)
                ProjectWriter.WriteLine("ELForISO=" + SelectedPS2Game.GameFilePath)
                ProjectWriter.WriteLine("TYPE=GAME")
                ProjectWriter.WriteLine("SIGNED=FALSE")
            End Using

            'Write SYSTEM.CNF to project directory
            Using CNFWriter As New StreamWriter(NewGameProjectDirectory + "\SYSTEM.CNF", False)
                CNFWriter.WriteLine("BOOT2 = pfs:/EXECUTE.KELF") 'Loads EXECUTE.KELF
                CNFWriter.WriteLine("VER = 1.01")
                CNFWriter.WriteLine("VMODE = NTSC")
                CNFWriter.WriteLine("HDDUNITPOWER = NICHDD")
            End Using

            'Write icon.sys to project directory
            Using CNFWriter As New StreamWriter(NewGameProjectDirectory + "\icon.sys", False)
                CNFWriter.WriteLine("PS2X")
                CNFWriter.WriteLine("title0=" + SelectedPS2Game.GameTitle)
                CNFWriter.WriteLine("title1=" + SelectedPS2Game.GameID)
                CNFWriter.WriteLine("bgcola=0")
                CNFWriter.WriteLine("bgcol0=0,0,0")
                CNFWriter.WriteLine("bgcol1=0,0,0")
                CNFWriter.WriteLine("bgcol2=0,0,0")
                CNFWriter.WriteLine("bgcol3=0,0,0")
                CNFWriter.WriteLine("lightdir0=1.0,-1.0,1.0")
                CNFWriter.WriteLine("lightdir1=-1.0,1.0,-1.0")
                CNFWriter.WriteLine("lightdir2=0.0,0.0,0.0")
                CNFWriter.WriteLine("lightcolamb=64,64,64")
                CNFWriter.WriteLine("lightcol0=64,64,64")
                CNFWriter.WriteLine("lightcol1=16,16,16")
                CNFWriter.WriteLine("lightcol2=0,0,0")
                CNFWriter.WriteLine("uninstallmes0=Do you want to uninstall this game ?")
                CNFWriter.WriteLine("uninstallmes1=")
                CNFWriter.WriteLine("uninstallmes2=")
            End Using

            'Create game project res & image directory
            If Not Directory.Exists(NewGameProjectDirectory + "\res") Then
                Directory.CreateDirectory(NewGameProjectDirectory + "\res")
            End If
            If Not Directory.Exists(NewGameProjectDirectory + "\res\image") Then
                Directory.CreateDirectory(NewGameProjectDirectory + "\res\image")
            End If

            'Write info.sys to res directory
            Using SYSWriter As New StreamWriter(NewGameProjectDirectory + "\res\info.sys", False)
                SYSWriter.WriteLine("title = " + SelectedPS2Game.GameTitle)
                SYSWriter.WriteLine("title_id = " + SelectedPS2Game.GameID)
                SYSWriter.WriteLine("title_sub_id = 0")
                SYSWriter.WriteLine("release_date = " + SelectedPS2Game.GameReleaseDate)
                SYSWriter.WriteLine("developer_id = " + SelectedPS2Game.GameDeveloper)
                SYSWriter.WriteLine("publisher_id = " + SelectedPS2Game.GamePublisher)
                SYSWriter.WriteLine("note = ")
                SYSWriter.WriteLine("content_web = " + SelectedPS2Game.GameWebsite)
                SYSWriter.WriteLine("image_topviewflag = 0")
                SYSWriter.WriteLine("image_type = 0")
                SYSWriter.WriteLine("image_count = 1")
                SYSWriter.WriteLine("image_viewsec = 600")
                SYSWriter.WriteLine("copyright_viewflag = 0")
                SYSWriter.WriteLine("copyright_imgcount = 1")
                SYSWriter.WriteLine("genre = " + SelectedPS2Game.GameGenre)
                SYSWriter.WriteLine("parental_lock = 1")
                SYSWriter.WriteLine("effective_date = 0")
                SYSWriter.WriteLine("expire_date = 0")

                Select Case SelectedPS2Game.GameRegion
                    Case "Europe"
                        SYSWriter.WriteLine("area = E")
                    Case "US"
                        SYSWriter.WriteLine("area = U")
                    Case "Japan"
                        SYSWriter.WriteLine("area = J")
                    Case Else
                        SYSWriter.WriteLine("area = J")
                End Select

                SYSWriter.WriteLine("violence_flag = 0")
                SYSWriter.WriteLine("content_type = 255")
                SYSWriter.WriteLine("content_subtype = 0")
            End Using

            'Create man.xml
            Using MANWriter As New StreamWriter(NewGameProjectDirectory + "\res\man.xml")
                MANWriter.WriteLine("<?xml version=""1.0"" encoding=""UTF-8""?>")
                MANWriter.WriteLine("")
                MANWriter.WriteLine("<MANUAL version=""1.0"">")
                MANWriter.WriteLine("")
                MANWriter.WriteLine("<IMG id=""bg"" src=""./image/0.png"" />")
                MANWriter.WriteLine("")
                MANWriter.WriteLine("<MENUGROUP id=""TOP"">")
                MANWriter.WriteLine("<TITLE id=""TOP-TITLE"" label=""" + SelectedPS2Game.GameTitle + """ />")
                MANWriter.WriteLine("<ITEM id=""M00"" label=""Screenshots""	page=""PIC0000"" />")
                MANWriter.WriteLine("</MENUGROUP>")
                MANWriter.WriteLine("")
                MANWriter.WriteLine("<PAGEGROUP>")
                MANWriter.WriteLine("<PAGE id=""PIC0000"" src=""./image/1.png"" retitem=""M00"" retgroup=""TOP"" />")
                MANWriter.WriteLine("<PAGE id=""PIC0000"" src=""./image/2.png"" retitem=""M00"" retgroup=""TOP"" />")
                MANWriter.WriteLine("</PAGEGROUP>")
                MANWriter.WriteLine("</MANUAL>")
                MANWriter.WriteLine("")
            End Using

            NewGameProjectWindow.Show()

            NewGameEditor.Show()
            NewGameEditor.AutoSave = True
            NewGameEditor.PSXDatacenterBrowser.Navigate("https://psxdatacenter.com/psx2/games2/" + SelectedPS2Game.GameID + ".html")
        End If

    End Sub

    Private Sub ModifyPartitionMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles ModifyPartitionMenuItem.Click
        If GamesListView.SelectedItem IsNot Nothing Then
            Dim SelectedPS2Game As PS2Game = CType(GamesListView.SelectedItem, PS2Game)

            If String.IsNullOrEmpty(SelectedPS2Game.AssignedPartitionDriveLetter) Then 'Game partition not mounted yet
                SelectedGameToModify = SelectedPS2Game
                ProcessOutputCommand = "ModifyPartition"

                NewLoadingWindow = New SyncWindow() With {.Title = "Loading game partitions", .ShowActivated = True, .WindowStartupLocation = WindowStartupLocation.CenterScreen}
                NewLoadingWindow.LoadProgressBar.IsIndeterminate = True
                NewLoadingWindow.LoadStatusTextBlock.Text = "Loading partition"
                NewLoadingWindow.Show()

                'Get all partitions to find the game's PP partition
                LoadPartitions()
            Else
                Dim NewGamePartitionManager As New GamePartitionManager With {.ShowActivated = True,
                    .AssociatedDriveLetter = SelectedPS2Game.AssignedPartitionDriveLetter,
                    .AssociatedPartition = SelectedPS2Game.PartitionName}
                NewGamePartitionManager.Show()
            End If

        End If
    End Sub

    Private Sub ReloadButton_Click(sender As Object, e As RoutedEventArgs) Handles ReloadButton.Click
        If Not String.IsNullOrEmpty(CurrentDirectoryTextBlock.Text) Then
            If CurrentDirectoryTextBlock.Text.StartsWith("PSX") Then
                LoadPSXGamesButton_Click(LoadPSXGamesButton, New RoutedEventArgs())
            Else
                GamesListView.Items.Clear()
                URLs.Clear()

                ISOCount = Directory.GetFiles(CurrentDirectoryTextBlock.Text, "*.iso", SearchOption.AllDirectories).Count
                TotalGamesTextBlock.Text = ISOCount.ToString() + " Games"

                GamesListView.ContextMenu = PCPS2GamesContextMenu

                NewLoadingWindow = New SyncWindow() With {.Title = "Loading PS2 files", .ShowActivated = True}
                NewLoadingWindow.LoadProgressBar.Maximum = ISOCount
                NewLoadingWindow.LoadStatusTextBlock.Text = "Loading file 1 of " + ISOCount.ToString()
                NewLoadingWindow.Show()

                GameLoaderWorker.RunWorkerAsync(CurrentDirectoryTextBlock.Text)
            End If
        End If
    End Sub

    Private Sub RemoveMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles RemoveMenuItem.Click
        If GamesListView.SelectedItem IsNot Nothing Then
            Dim SelectedPS2Game As PS2Game = CType(GamesListView.SelectedItem, PS2Game)
            If String.IsNullOrEmpty(SelectedPS2Game.AssignedPartitionDriveLetter) Then 'Check if the game partition is not mounted
                If MsgBox("Do you really want to remoe the game " + SelectedPS2Game.GameTitle + " ?" + vbCrLf + "This operation could be destructive and should be made on the console !", MsgBoxStyle.YesNo, "Please confirm") = MsgBoxResult.Yes Then
                    SelectedGameToModify = SelectedPS2Game
                    ProcessOutputCommand = "DeletePartition"

                    NewLoadingWindow = New SyncWindow() With {.Title = "Removing " + SelectedPS2Game.GameTitle, .ShowActivated = True, .WindowStartupLocation = WindowStartupLocation.CenterScreen}
                    NewLoadingWindow.LoadProgressBar.IsIndeterminate = True
                    NewLoadingWindow.LoadStatusTextBlock.Text = "Deleting game, please wait"
                    NewLoadingWindow.Show()

                    'Get all partitions to find the PP & hidden partition of the game and delete them afterwards
                    LoadPartitions()
                End If
            End If
        End If
    End Sub

End Class
