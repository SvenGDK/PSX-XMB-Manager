Imports System.ComponentModel
Imports System.IO
Imports System.Windows.Forms

Public Class PS1GameLibrary

    Dim WithEvents GameLoaderWorker As New BackgroundWorker() With {.WorkerReportsProgress = True}
    Dim WithEvents PSXDatacenterBrowser As New WebBrowser()
    Dim WithEvents NewLoadingWindow As New SyncWindow() With {.Title = "Loading PS1 files", .ShowActivated = True}

    Dim WithEvents PS1GamesContextMenu As New Controls.ContextMenu()
    Dim WithEvents CreateProjectMenuItem As New Controls.MenuItem() With {.Header = "Create a game project", .Icon = New Controls.Image() With {.Source = New BitmapImage(New Uri("/Images/copy-icon.png", UriKind.Relative))}}

    Dim URLs As New List(Of String)()
    Dim CurrentKeyCount As Integer = 0

    Dim BINCount As Integer = 0
    Dim VCDCount As Integer = 0

    Private Sub PS1GameLibrary_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        'ContextMenu for games on stored on PC
        PS1GamesContextMenu.Items.Add(CreateProjectMenuItem)
        GamesListView.ContextMenu = PS1GamesContextMenu
    End Sub

    Private Sub LoadGamesOnPCButton_Click(sender As Object, e As RoutedEventArgs) Handles LoadGamesOnPCButton.Click
        Dim FBD As New FolderBrowserDialog() With {.Description = "Please select a folder containing PS1 BINs and/or VCDs.", .ShowNewFolderButton = False}

        GameTitleTextBlock.Text = ""
        ReleaseDateTextBlock.Text = ""
        DeveloperTextBlock.Text = ""
        PublisherTextBlock.Text = ""
        GameSizeTextBlock.Text = ""
        GenreTextBlock.Text = ""
        RegionTextBlock.Text = ""
        GameIDTextBlock.Text = ""

        If FBD.ShowDialog() = Forms.DialogResult.OK Then
            If Not String.IsNullOrEmpty(FBD.SelectedPath) Then

                CurrentDirectoryTextBlock.Text = FBD.SelectedPath

                GamesListView.Items.Clear()
                URLs.Clear()
                BINCount = 0
                VCDCount = 0

                For Each GameBIN In Directory.GetFiles(FBD.SelectedPath, "*.bin", SearchOption.AllDirectories)
                    Dim GameInfo As New FileInfo(GameBIN)
                    If GameInfo.Name.ToLower().Contains("track") Then
                        If Not GameBIN.ToLower().Contains("(track 1).bin") OrElse Not GameInfo.Name.ToLower().Contains("(track 01).bin") Then
                            'Skip
                            Continue For
                        Else
                            BINCount += 1
                        End If
                    Else
                        BINCount += 1
                    End If
                Next

                VCDCount = Directory.GetFiles(FBD.SelectedPath, "*.VCD", SearchOption.AllDirectories).Count

                TotalGamesTextBlock.Text = (BINCount + VCDCount).ToString() + " Games"

                NewLoadingWindow = New SyncWindow() With {.Title = "Loading PS1 games", .ShowActivated = True}
                NewLoadingWindow.LoadProgressBar.Maximum = BINCount + VCDCount
                NewLoadingWindow.LoadStatusTextBlock.Text = "Loading file 1 of " + (BINCount + VCDCount).ToString()
                NewLoadingWindow.Show()

                GameLoaderWorker.RunWorkerAsync(FBD.SelectedPath)
            End If
        End If
    End Sub

    Private Sub GameLoaderWorker_DoWork(sender As Object, e As DoWorkEventArgs) Handles GameLoaderWorker.DoWork

        Dim GamesList As New List(Of String)()
        Dim FailList As New List(Of String)()

        Dim FoundGames As IEnumerable(Of String) = Directory.EnumerateFiles(e.Argument.ToString, "*.*", SearchOption.AllDirectories).Where(Function(s) s.EndsWith(".bin") OrElse s.EndsWith(".BIN") OrElse s.EndsWith(".VCD"))

        For Each Game In FoundGames

            Dim GameInfo As New FileInfo(Game)

            'Skip multiple "Track" files and only read the first one
            If GameInfo.Name.ToLower().Contains("track") Then
                If Not Game.ToLower().Contains("(track 1).bin") OrElse Not GameInfo.Name.ToLower().Contains("(track 01).bin") Then
                    'Skip
                    Continue For
                End If
            End If

            Dim GameStartLetter As String = GameInfo.Name.Substring(0, 1) 'Take the first letter of the file name (required to browse PSXDatacenter)
            Dim NewPS1Game As New PS1Game With {.GameFilePath = Game, .GameSize = FormatNumber(GameInfo.Length / 1048576, 2) + " MB"}

            'Search for the game ID within the first 7MB with strings & findstr
            'We could also use StreamReader & BinaryReader but there are many methods, this is an easy way and also used in PS Mac Tools
            Using WindowsCMD As New Process()
                WindowsCMD.StartInfo.FileName = "cmd"
                WindowsCMD.StartInfo.Arguments = "/c strings.exe /accepteula -nobanner -b 7340032 """ + Game + """ | findstr BOOT"
                WindowsCMD.StartInfo.RedirectStandardOutput = True
                WindowsCMD.StartInfo.UseShellExecute = False
                WindowsCMD.StartInfo.CreateNoWindow = True
                WindowsCMD.Start()
                WindowsCMD.WaitForExit()

                Dim OutputReader As StreamReader = WindowsCMD.StandardOutput
                Dim ProcessOutput As String() = OutputReader.ReadToEnd().Split(New String() {vbCrLf}, StringSplitOptions.RemoveEmptyEntries)
                Dim GameIDFound As Boolean = False

                If ProcessOutput.Length > 0 Then
                    For Each OutputLine In ProcessOutput
                        If OutputLine.Contains("BOOT =") Or OutputLine.Contains("BOOT=") Then
                            GameIDFound = True
                            Dim GameID As String = OutputLine.Replace("BOOT = cdrom:\", "").Replace("BOOT=cdrom:\", "").Replace("BOOT = cdrom:", "").Replace(";1", "").Replace("_", "-").Replace(".", "").Replace("MGS\", "").Trim()
                            Dim RegionCharacter As String = PS1Game.GetRegionChar(GameID)

                            'Set known values
                            NewPS1Game.GameID = UCase(GameID)

                            'Check game id length & if the generated url is valid
                            If GameID.Length = 10 Then
                                If Utils.IsURLValid("https://raw.githubusercontent.com/SvenGDK/PSMT-Covers/main/PS1/" + GameID + ".jpg") Then
                                    If Dispatcher.CheckAccess() = False Then
                                        Dispatcher.BeginInvoke(Sub()
                                                                   Dim TempBitmapImage = New BitmapImage()
                                                                   TempBitmapImage.BeginInit()
                                                                   TempBitmapImage.CacheOption = BitmapCacheOption.OnLoad
                                                                   TempBitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache
                                                                   TempBitmapImage.UriSource = New Uri("https://raw.githubusercontent.com/SvenGDK/PSMT-Covers/main/PS1/" + GameID + ".jpg", UriKind.RelativeOrAbsolute)
                                                                   TempBitmapImage.EndInit()
                                                                   NewPS1Game.GameCoverSource = TempBitmapImage
                                                               End Sub)
                                    Else
                                        Dim TempBitmapImage = New BitmapImage()
                                        TempBitmapImage.BeginInit()
                                        TempBitmapImage.CacheOption = BitmapCacheOption.OnLoad
                                        TempBitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache
                                        TempBitmapImage.UriSource = New Uri("https://raw.githubusercontent.com/SvenGDK/PSMT-Covers/main/PS1/" + GameID + ".jpg", UriKind.RelativeOrAbsolute)
                                        TempBitmapImage.EndInit()
                                        NewPS1Game.GameCoverSource = TempBitmapImage
                                    End If

                                    If Utils.IsURLValid("https://psxdatacenter.com/games/" + RegionCharacter + "/" + GameStartLetter + "/" + GameID + ".html") Then
                                        URLs.Add("https://psxdatacenter.com/games/" + RegionCharacter + "/" + GameStartLetter + "/" + GameID + ".html")
                                    Else
                                        NewPS1Game.GameTitle = GetPS1GameTitleFromDatabaseList(UCase(GameID).Trim())
                                    End If
                                Else
                                    If Utils.IsURLValid("https://psxdatacenter.com/games/" + RegionCharacter + "/" + GameStartLetter + "/" + GameID + ".html") Then
                                        URLs.Add("https://psxdatacenter.com/games/" + RegionCharacter + "/" + GameStartLetter + "/" + GameID + ".html")
                                    Else
                                        NewPS1Game.GameTitle = GetPS1GameTitleFromDatabaseList(UCase(GameID).Trim())
                                    End If
                                End If
                            End If

                            GameIDFound = True
                            Exit For
                        Else
                            NewPS1Game.GameTitle = GameInfo.Name
                        End If
                    Next
                Else
                    NewPS1Game.GameTitle = GameInfo.Name
                End If

                If GameIDFound = False Then
                    FailList.Add(Game)
                Else
                    'Update progress
                    Dispatcher.BeginInvoke(Sub()
                                               NewLoadingWindow.LoadProgressBar.Value += 1
                                               NewLoadingWindow.LoadStatusTextBlock.Text = "Loading bin " + NewLoadingWindow.LoadProgressBar.Value.ToString + " of " + (BINCount + VCDCount).ToString()
                                           End Sub)

                    'Add to the ListView
                    If GamesListView.Dispatcher.CheckAccess() = False Then
                        GamesListView.Dispatcher.BeginInvoke(Sub() GamesListView.Items.Add(NewPS1Game))
                    Else
                        GamesListView.Items.Add(NewPS1Game)
                    End If
                End If

            End Using
        Next

        If FailList.Count > 0 Then 'Ask for an extended search
            If MsgBox("Some Game IDs could not be found quickly, do you want to extend the search ? This requires about 1-5min for each game (depending on your hardware).", MsgBoxStyle.YesNo, "Not all Game IDs could be found") = MsgBoxResult.Yes Then

                'Update progress
                Dispatcher.BeginInvoke(Sub()
                                           NewLoadingWindow.LoadProgressBar.Value = 0
                                           NewLoadingWindow.LoadProgressBar.Maximum = FailList.Count
                                           NewLoadingWindow.LoadStatusTextBlock.Text = "Loading bin 1 of " + FailList.Count.ToString()
                                       End Sub)

                For Each Game In FailList

                    Dim GameInfo As New FileInfo(Game)

                    'Skip multiple "Track" files and only read the first one
                    If GameInfo.Name.ToLower().Contains("track") Then
                        If Not Game.ToLower().Contains("(track 1).bin") OrElse Not GameInfo.Name.ToLower().Contains("(track 01).bin") Then
                            'Skip
                            Continue For
                        End If
                    End If

                    Dim GameStartLetter As String = GameInfo.Name.Substring(0, 1) 'Take the first letter of the file name (required to browse PSXDatacenter)
                    Dim NewPS1Game As New PS1Game With {.GameFilePath = Game, .GameSize = FormatNumber(GameInfo.Length / 1048576, 2) + " MB"}
                    Dim GameFileSizeAsString As String = GameInfo.Length.ToString()

                    'Search for the game ID within the first 7MB with strings & findstr
                    'We could also use StreamReader & BinaryReader but there are many methods, this is an easy way and also used in PS Mac Tools
                    Using WindowsCMD As New Process()
                        WindowsCMD.StartInfo.FileName = "cmd"
                        WindowsCMD.StartInfo.Arguments = "/c strings -nobanner -b " + GameFileSizeAsString + " """ + Game + """ | findstr BOOT"
                        WindowsCMD.StartInfo.RedirectStandardOutput = True
                        WindowsCMD.StartInfo.UseShellExecute = False
                        WindowsCMD.StartInfo.CreateNoWindow = True
                        WindowsCMD.Start()
                        WindowsCMD.WaitForExit()

                        Dim OutputReader As StreamReader = WindowsCMD.StandardOutput
                        Dim ProcessOutput As String() = OutputReader.ReadToEnd().Split(New String() {vbCrLf}, StringSplitOptions.RemoveEmptyEntries)

                        If ProcessOutput.Length > 0 Then 'Game ID found
                            For Each OutputLine In ProcessOutput
                                If OutputLine.Contains("BOOT =") Or OutputLine.Contains("BOOT=") Then
                                    Dim GameID As String = OutputLine.Replace("BOOT = cdrom:\", "").Replace("BOOT=cdrom:\", "").Replace("BOOT = cdrom:", "").Replace(";1", "").Replace("_", "-").Replace(".", "").Replace("MGS\", "").Trim()
                                    Dim RegionCharacter As String = PS1Game.GetRegionChar(GameID)

                                    'Set known values
                                    NewPS1Game.GameID = UCase(GameID)

                                    'Check game id length & if the generated url is valid
                                    If GameID.Length = 10 Then
                                        If Utils.IsURLValid("https://raw.githubusercontent.com/SvenGDK/PSMT-Covers/main/PS1/" + GameID + ".jpg") Then
                                            If Dispatcher.CheckAccess() = False Then
                                                Dispatcher.BeginInvoke(Sub()
                                                                           Dim TempBitmapImage = New BitmapImage()
                                                                           TempBitmapImage.BeginInit()
                                                                           TempBitmapImage.CacheOption = BitmapCacheOption.OnLoad
                                                                           TempBitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache
                                                                           TempBitmapImage.UriSource = New Uri("https://raw.githubusercontent.com/SvenGDK/PSMT-Covers/main/PS1/" + GameID + ".jpg", UriKind.RelativeOrAbsolute)
                                                                           TempBitmapImage.EndInit()
                                                                           NewPS1Game.GameCoverSource = TempBitmapImage
                                                                       End Sub)
                                            Else
                                                Dim TempBitmapImage = New BitmapImage()
                                                TempBitmapImage.BeginInit()
                                                TempBitmapImage.CacheOption = BitmapCacheOption.OnLoad
                                                TempBitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache
                                                TempBitmapImage.UriSource = New Uri("https://raw.githubusercontent.com/SvenGDK/PSMT-Covers/main/PS1/" + GameID + ".jpg", UriKind.RelativeOrAbsolute)
                                                TempBitmapImage.EndInit()
                                                NewPS1Game.GameCoverSource = TempBitmapImage
                                            End If

                                            If Utils.IsURLValid("https://psxdatacenter.com/games/" + RegionCharacter + "/" + GameStartLetter + "/" + GameID + ".html") Then
                                                URLs.Add("https://psxdatacenter.com/games/" + RegionCharacter + "/" + GameStartLetter + "/" + GameID + ".html")
                                            Else
                                                NewPS1Game.GameTitle = GetPS1GameTitleFromDatabaseList(UCase(GameID).Trim())
                                            End If
                                        Else
                                            If Utils.IsURLValid("https://psxdatacenter.com/games/" + RegionCharacter + "/" + GameStartLetter + "/" + GameID + ".html") Then
                                                URLs.Add("https://psxdatacenter.com/games/" + RegionCharacter + "/" + GameStartLetter + "/" + GameID + ".html")
                                            Else
                                                NewPS1Game.GameTitle = GetPS1GameTitleFromDatabaseList(UCase(GameID).Trim())
                                            End If
                                        End If
                                    End If

                                    Exit For
                                Else
                                    NewPS1Game.GameTitle = GameInfo.Name
                                End If
                            Next
                        Else
                            NewPS1Game.GameTitle = GameInfo.Name
                        End If

                    End Using

                    'Update progress
                    Dispatcher.BeginInvoke(Sub()
                                               NewLoadingWindow.LoadProgressBar.Value += 1
                                               NewLoadingWindow.LoadStatusTextBlock.Text = "Loading bin " + NewLoadingWindow.LoadProgressBar.Value.ToString + " of " + FailList.Count.ToString()
                                           End Sub)

                    'Add to the ListView
                    If GamesListView.Dispatcher.CheckAccess() = False Then
                        GamesListView.Dispatcher.BeginInvoke(Sub()
                                                                 GamesListView.Items.Add(NewPS1Game)
                                                                 GamesListView.Items.Refresh()
                                                             End Sub)
                    Else
                        GamesListView.Items.Add(NewPS1Game)
                        GamesListView.Items.Refresh()
                    End If
                Next

            End If
        End If

    End Sub

    Private Sub GameLoaderWorker_RunWorkerCompleted(sender As Object, e As RunWorkerCompletedEventArgs) Handles GameLoaderWorker.RunWorkerCompleted
        If URLs.Count > 0 Then
            NewLoadingWindow.LoadStatusTextBlock.Text = "Getting " + URLs.Count.ToString() + " available game infos and missing covers."
            NewLoadingWindow.LoadProgressBar.Value = 0
            NewLoadingWindow.LoadProgressBar.Maximum = URLs.Count

            GetGameInfos()
        Else
            NewLoadingWindow.Close()
            GamesListView.Items.Refresh()
        End If
    End Sub

    Private Sub GetGameInfos()
        PSXDatacenterBrowser.Navigate(URLs.Item(0))
    End Sub

    Private Sub PSXDatacenterBrowser_DocumentCompleted(sender As Object, e As WebBrowserDocumentCompletedEventArgs) Handles PSXDatacenterBrowser.DocumentCompleted
        RemoveHandler PSXDatacenterBrowser.DocumentCompleted, AddressOf PSXDatacenterBrowser_DocumentCompleted

        Dim GameTitle As String = ""
        Dim GameCode As String = ""
        Dim GameRegion As String = ""
        Dim GameCoverSource As String = ""
        Dim GameGenre As String = ""
        Dim GameDeveloper As String = ""
        Dim GamePublisher As String = ""
        Dim GameReleaseDate As String = ""
        Dim GameDescription As String = ""

        'Get game infos
        Dim infoTable As HtmlElementCollection = Nothing
        If PSXDatacenterBrowser.Document.GetElementById("table4") IsNot Nothing AndAlso PSXDatacenterBrowser.Document.GetElementById("table4").GetElementsByTagName("tr").Count > 0 Then
            infoTable = PSXDatacenterBrowser.Document.GetElementById("table4").GetElementsByTagName("tr")
        End If
        Dim coverTableRows As HtmlElementCollection = Nothing
        If PSXDatacenterBrowser.Document.GetElementById("table2") IsNot Nothing AndAlso PSXDatacenterBrowser.Document.GetElementById("table2").GetElementsByTagName("tr").Count > 0 Then
            coverTableRows = PSXDatacenterBrowser.Document.GetElementById("table2").GetElementsByTagName("tr")
        End If

        If infoTable.Count >= 7 Then
            'Game Title
            If infoTable.Item(0).Children.Count >= 1 Then
                GameTitle = infoTable.Item(0).Children(1).InnerText.Trim()
            End If

            'GameCode
            If infoTable.Item(2).Children.Count >= 1 Then
                GameCode = infoTable.Item(2).Children(1).InnerText.Trim()
            End If

            'Region
            If infoTable.Item(3).Children.Count >= 1 Then
                Dim Region As String = infoTable.Item(3).Children(1).InnerText.Trim()
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
            If infoTable.Item(4).Children.Count >= 1 Then
                GameGenre = infoTable.Item(4).Children(1).InnerText.Trim()
            End If

            'Developer
            If infoTable.Item(5).Children.Count >= 1 Then
                GameDeveloper = infoTable.Item(5).Children(1).InnerText.Trim()
            End If

            'Publisher
            If infoTable.Item(6).Children.Count >= 1 Then
                GamePublisher = infoTable.Item(6).Children(1).InnerText.Trim()
            End If

            'Release Date
            If infoTable.Item(7).Children.Count >= 1 Then
                GameReleaseDate = infoTable.Item(7).Children(1).InnerText.Trim()
            End If
        End If

        'Get the game description
        If PSXDatacenterBrowser.Document.GetElementById("table16") IsNot Nothing AndAlso PSXDatacenterBrowser.Document.GetElementById("table16").GetElementsByTagName("tr").Count >= 0 Then
            GameDescription = PSXDatacenterBrowser.Document.GetElementById("table16").GetElementsByTagName("tr")(0).InnerText
        End If

        If coverTableRows.Count >= 2 Then
            If coverTableRows.Item(2) IsNot Nothing AndAlso coverTableRows.Item(2).GetElementsByTagName("img").Count > 0 Then
                GameCoverSource = coverTableRows.Item(2).GetElementsByTagName("img")(0).GetAttribute("src").Trim()
            End If
        End If

        If Not String.IsNullOrEmpty(GameCode) Then
            For Each Game In GamesListView.Items
                Dim FoundGame As PS1Game = CType(Game, PS1Game)
                If Not String.IsNullOrEmpty(FoundGame.GameTitle) Then
                    If FoundGame.GameTitle.Contains(GameCode) Or FoundGame.GameTitle = GameCode Then
                        FoundGame.GameRegion = GameRegion
                        FoundGame.GameGenre = GameGenre
                        FoundGame.GameDeveloper = GameDeveloper
                        FoundGame.GamePublisher = GamePublisher
                        FoundGame.GameReleaseDate = GameReleaseDate
                        FoundGame.GameDescription = GameDescription

                        If FoundGame.GameCoverSource Is Nothing Then
                            If Dispatcher.CheckAccess() = False Then
                                Dispatcher.BeginInvoke(Sub()
                                                           Dim TempBitmapImage = New BitmapImage()
                                                           TempBitmapImage.BeginInit()
                                                           TempBitmapImage.CacheOption = BitmapCacheOption.OnLoad
                                                           TempBitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache
                                                           TempBitmapImage.UriSource = New Uri(GameCoverSource, UriKind.RelativeOrAbsolute)
                                                           TempBitmapImage.EndInit()
                                                           FoundGame.GameCoverSource = TempBitmapImage
                                                       End Sub)
                            Else
                                Dim TempBitmapImage = New BitmapImage()
                                TempBitmapImage.BeginInit()
                                TempBitmapImage.CacheOption = BitmapCacheOption.OnLoad
                                TempBitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache
                                TempBitmapImage.UriSource = New Uri(GameCoverSource, UriKind.RelativeOrAbsolute)
                                TempBitmapImage.EndInit()
                                FoundGame.GameCoverSource = TempBitmapImage
                            End If
                        End If
                    End If
                ElseIf Not String.IsNullOrEmpty(FoundGame.GameID) Then
                    If FoundGame.GameID.Contains(GameCode) Or FoundGame.GameID = GameCode Then
                        FoundGame.GameTitle = GameTitle
                        FoundGame.GameRegion = GameRegion
                        FoundGame.GameGenre = GameGenre
                        FoundGame.GameDeveloper = GameDeveloper
                        FoundGame.GamePublisher = GamePublisher
                        FoundGame.GameReleaseDate = GameReleaseDate
                        FoundGame.GameDescription = GameDescription

                        If FoundGame.GameCoverSource Is Nothing Then
                            If Dispatcher.CheckAccess() = False Then
                                Dispatcher.BeginInvoke(Sub()
                                                           Dim TempBitmapImage = New BitmapImage()
                                                           TempBitmapImage.BeginInit()
                                                           TempBitmapImage.CacheOption = BitmapCacheOption.OnLoad
                                                           TempBitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache
                                                           TempBitmapImage.UriSource = New Uri(GameCoverSource, UriKind.RelativeOrAbsolute)
                                                           TempBitmapImage.EndInit()
                                                           FoundGame.GameCoverSource = TempBitmapImage
                                                       End Sub)
                            Else
                                Dim TempBitmapImage = New BitmapImage()
                                TempBitmapImage.BeginInit()
                                TempBitmapImage.CacheOption = BitmapCacheOption.OnLoad
                                TempBitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache
                                TempBitmapImage.UriSource = New Uri(GameCoverSource, UriKind.RelativeOrAbsolute)
                                TempBitmapImage.EndInit()
                                FoundGame.GameCoverSource = TempBitmapImage
                            End If
                        End If
                    End If
                End If
            Next
        End If

        AddHandler PSXDatacenterBrowser.DocumentCompleted, AddressOf PSXDatacenterBrowser_DocumentCompleted

        If CurrentKeyCount < URLs.Count Then
            PSXDatacenterBrowser.Navigate(URLs.Item(CurrentKeyCount))
            CurrentKeyCount += 1
            NewLoadingWindow.LoadProgressBar.Value = CurrentKeyCount
        Else
            CurrentKeyCount = 0
            URLs.Clear()
            NewLoadingWindow.Close()
            GamesListView.Items.Refresh()
        End If
    End Sub

    Public Function GetPS1GameTitleFromDatabaseList(GameID As String) As String
        Dim FoundGameTitle As String = ""

        For Each GameTitle As String In File.ReadLines(My.Computer.FileSystem.CurrentDirectory + "\Tools\ps1ids.txt")
            If GameTitle.Contains(GameID) Then
                If GameTitle.Split(";"c).Length > 1 Then
                    FoundGameTitle = GameTitle.Split(";"c)(1)
                    Exit For
                Else
                    FoundGameTitle = "Unknown PS2 game"
                    Exit For
                End If
            End If
        Next

        If String.IsNullOrEmpty(FoundGameTitle) Then
            Return ""
        Else
            Return FoundGameTitle
        End If
    End Function

    Private Sub ReloadButton_Click(sender As Object, e As RoutedEventArgs) Handles ReloadButton.Click
        If Not String.IsNullOrEmpty(CurrentDirectoryTextBlock.Text) Then

            GameTitleTextBlock.Text = ""
            ReleaseDateTextBlock.Text = ""
            DeveloperTextBlock.Text = ""
            PublisherTextBlock.Text = ""
            GameSizeTextBlock.Text = ""
            GenreTextBlock.Text = ""
            RegionTextBlock.Text = ""
            GameIDTextBlock.Text = ""

            GamesListView.Items.Clear()
            URLs.Clear()
            BINCount = 0
            VCDCount = 0

            For Each GameBIN In Directory.GetFiles(CurrentDirectoryTextBlock.Text, "*.bin", SearchOption.AllDirectories)
                Dim GameInfo As New FileInfo(GameBIN)
                If GameInfo.Name.ToLower().Contains("track") Then
                    If Not GameBIN.ToLower().Contains("(track 1).bin") OrElse Not GameInfo.Name.ToLower().Contains("(track 01).bin") Then
                        'Skip
                        Continue For
                    Else
                        BINCount += 1
                    End If
                Else
                    BINCount += 1
                End If
            Next

            VCDCount = Directory.GetFiles(CurrentDirectoryTextBlock.Text, "*.VCD", SearchOption.AllDirectories).Count

            TotalGamesTextBlock.Text = (BINCount + VCDCount).ToString() + " Games"

            NewLoadingWindow = New SyncWindow() With {.Title = "Loading PS1 games", .ShowActivated = True}
            NewLoadingWindow.LoadProgressBar.Maximum = BINCount + VCDCount
            NewLoadingWindow.LoadStatusTextBlock.Text = "Loading file 1 of " + (BINCount + VCDCount).ToString()
            NewLoadingWindow.Show()

            GameLoaderWorker.RunWorkerAsync(CurrentDirectoryTextBlock.Text)
        End If
    End Sub

    Private Sub CreateProjectMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles CreateProjectMenuItem.Click
        If GamesListView.SelectedItem IsNot Nothing Then
            Dim SelectedPS1Game As PS1Game = CType(GamesListView.SelectedItem, PS1Game)

            If Path.GetExtension(SelectedPS1Game.GameFilePath) = ".VCD" Then
                Dim GameProjectDirectory As String = SelectedPS1Game.GameTitle + " [" + SelectedPS1Game.GameID + "]"
                Dim NewGameProjectDirectory As String = My.Computer.FileSystem.CurrentDirectory + "\Projects\" + SelectedPS1Game.GameTitle + " [" + SelectedPS1Game.GameID + "]"

                Dim NewGameProjectWindow As New NewPS1GameProject() With {.ShowActivated = True}
                Dim NewGameEditor As New PS1GameEditor() With {.ProjectDirectory = NewGameProjectDirectory, .Title = "Game Ressources Editor - " + NewGameProjectDirectory}

                'Set project information
                NewGameProjectWindow.IMAGE0PathTextBox.Text = SelectedPS1Game.GameFilePath
                NewGameProjectWindow.ProjectNameTextBox.Text = SelectedPS1Game.GameTitle
                NewGameProjectWindow.ProjectDirectoryTextBox.Text = NewGameProjectDirectory
                NewGameProjectWindow.ProjectTitleTextBox.Text = SelectedPS1Game.GameTitle
                NewGameProjectWindow.ProjectIDTextBox.Text = SelectedPS1Game.GameID
                NewGameProjectWindow.ProjectUninstallMsgTextBox.Text = "Do you want to uninstall this game ?"

                'Create game project directory
                If Not Directory.Exists(NewGameProjectDirectory) Then
                    Directory.CreateDirectory(NewGameProjectDirectory)
                End If

                'Write Project settings to .CFG
                Using ProjectWriter As New StreamWriter(My.Computer.FileSystem.CurrentDirectory + "\Projects\" + SelectedPS1Game.GameTitle + ".CFG", False)
                    ProjectWriter.WriteLine("TITLE=" + SelectedPS1Game.GameTitle)
                    ProjectWriter.WriteLine("ID=" + SelectedPS1Game.GameID)
                    ProjectWriter.WriteLine("DIR=" + NewGameProjectDirectory)
                    ProjectWriter.WriteLine("ELForISO=" + SelectedPS1Game.GameFilePath)
                    ProjectWriter.WriteLine("TYPE=GAME")
                    ProjectWriter.WriteLine("SIGNED=FALSE")
                    ProjectWriter.WriteLine("GAMETYPE=PS1")
                End Using

                'Write SYSTEM.CNF to project directory
                Using CNFWriter As New StreamWriter(NewGameProjectDirectory + "\SYSTEM.CNF", False)
                    CNFWriter.WriteLine("BOOT2 = pfs:/EXECUTE.KELF")
                    CNFWriter.WriteLine("VER = 1.01")
                    CNFWriter.WriteLine("VMODE = NTSC")
                    CNFWriter.WriteLine("HDDUNITPOWER = NICHDD")
                End Using

                'Write icon.sys to project directory
                Using CNFWriter As New StreamWriter(NewGameProjectDirectory + "\icon.sys", False)
                    CNFWriter.WriteLine("PS2X")
                    CNFWriter.WriteLine("title0=" + SelectedPS1Game.GameTitle)
                    CNFWriter.WriteLine("title1=" + SelectedPS1Game.GameID)
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
                    SYSWriter.WriteLine("title = " + SelectedPS1Game.GameTitle)
                    SYSWriter.WriteLine("title_id = " + SelectedPS1Game.GameID)
                    SYSWriter.WriteLine("title_sub_id = 0")
                    SYSWriter.WriteLine("release_date = " + SelectedPS1Game.GameReleaseDate)
                    SYSWriter.WriteLine("developer_id = " + SelectedPS1Game.GameDeveloper)
                    SYSWriter.WriteLine("publisher_id = " + SelectedPS1Game.GamePublisher)
                    SYSWriter.WriteLine("note = ")
                    SYSWriter.WriteLine("content_web = ")
                    SYSWriter.WriteLine("image_topviewflag = 0")
                    SYSWriter.WriteLine("image_type = 0")
                    SYSWriter.WriteLine("image_count = 1")
                    SYSWriter.WriteLine("image_viewsec = 600")
                    SYSWriter.WriteLine("copyright_viewflag = 0")
                    SYSWriter.WriteLine("copyright_imgcount = 1")
                    SYSWriter.WriteLine("genre = " + SelectedPS1Game.GameGenre)
                    SYSWriter.WriteLine("parental_lock = 1")
                    SYSWriter.WriteLine("effective_date = 0")
                    SYSWriter.WriteLine("expire_date = 0")

                    Select Case SelectedPS1Game.GameRegion
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
                Using MANWriter As New StreamWriter(NewGameProjectDirectory + "\res\man.xml", False)
                    MANWriter.WriteLine("<?xml version=""1.0"" encoding=""UTF-8""?>")
                    MANWriter.WriteLine("")
                    MANWriter.WriteLine("<MANUAL version=""1.0"">")
                    MANWriter.WriteLine("")
                    MANWriter.WriteLine("<IMG id=""bg"" src=""./image/0.png"" />")
                    MANWriter.WriteLine("")
                    MANWriter.WriteLine("<MENUGROUP id=""TOP"">")
                    MANWriter.WriteLine("<TITLE id=""TOP-TITLE"" label=""" + SelectedPS1Game.GameTitle + """ />")
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

                'Open project settings window
                NewGameProjectWindow.Show()

                'Open the Game Editor (in case of additional changes)
                NewGameEditor.Show()
                NewGameEditor.AutoSave = True

                'Open the Game Editor and try to load values from PSXDatacenter
                Dim GameStartLetter As String = SelectedPS1Game.GameTitle.Substring(0, 1)
                Dim RegionCharacter As String = PS1Game.GetRegionChar(SelectedPS1Game.GameID)

                If Utils.IsURLValid("https://psxdatacenter.com/games/" + RegionCharacter + "/" + GameStartLetter + "/" + SelectedPS1Game.GameID + ".html") Then
                    NewGameEditor.PSXDatacenterBrowser.Navigate("https://psxdatacenter.com/games/" + RegionCharacter + "/" + GameStartLetter + "/" + SelectedPS1Game.GameID + ".html")
                Else
                    'Apply cover, title and region only if no data is available on PSXDatacenter
                    NewGameEditor.ApplyKnownValues(SelectedPS1Game.GameID, SelectedPS1Game.GameTitle)
                End If
            Else
                MsgBox("Games in BIN format cannot be installed directly on the HDD, please convert it with cue2pops using PS Multi Tools.", MsgBoxStyle.Information, "BIN files not supported")
            End If

        End If
    End Sub

    Private Sub GamesListView_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles GamesListView.SelectionChanged
        If GamesListView.SelectedItem IsNot Nothing Then
            Dim SelectedPS1Game As PS1Game = CType(GamesListView.SelectedItem, PS1Game)

            GameTitleTextBlock.Text = SelectedPS1Game.GameTitle
            ReleaseDateTextBlock.Text = SelectedPS1Game.GameReleaseDate
            DeveloperTextBlock.Text = SelectedPS1Game.GameDeveloper
            PublisherTextBlock.Text = SelectedPS1Game.GamePublisher
            GameSizeTextBlock.Text = SelectedPS1Game.GameSize
            GenreTextBlock.Text = SelectedPS1Game.GameGenre
            RegionTextBlock.Text = SelectedPS1Game.GameRegion
            GameIDTextBlock.Text = SelectedPS1Game.GameID

            Select Case SelectedPS1Game.GameRegion
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

End Class
