Imports System.ComponentModel
Imports System.IO
Imports System.Net
Imports System.Windows.Forms
Imports nQuant

Public Class GamePartitionManager

    Public AssociatedDriveLetter As String
    Public AssociatedPartition As String
    Dim Unmounted As Boolean = False

    Private TextboxChangesDictionary As New Dictionary(Of TextBox, String)
    Private CheckboxChangesDictionary As New Dictionary(Of CheckBox, Boolean?)
    Private ImageChangesDictionary As New Dictionary(Of Image, ImageSource)

    Dim WithEvents PSXDatacenterBrowser As New WebBrowser()
    Dim WithEvents NewLoadingWindow As New SyncWindow() With {.Title = "Loading files", .ShowActivated = True, .WindowStartupLocation = WindowStartupLocation.CenterScreen}
    Dim WithEvents PartitionLoaderWorker As New BackgroundWorker() With {.WorkerReportsProgress = True}
    Dim WithEvents ContentDownloader As New WebClient()

    Private Sub GamePartitionManager_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If Unmounted = False Then
            If MsgBox("Do you want to keep the game partition mounted ? This will speed up reading the partition again.", MsgBoxStyle.YesNo, "Keep mounted ?") = MsgBoxResult.No Then
                UnMountPartition(AssociatedDriveLetter.ToUpper)
                Utils.RemoveMountedDriveLetter(AssociatedDriveLetter)
            End If
        End If
    End Sub

    Private Sub UnMountPartition(VolumeLabel As String)
        Using DokanCTL As New Process()
            DokanCTL.StartInfo.FileName = My.Computer.FileSystem.SpecialDirectories.ProgramFiles + "\Dokan\DokanLibrary-2.0.6\dokanctl.exe"
            DokanCTL.StartInfo.Arguments = "/u " + VolumeLabel
            DokanCTL.StartInfo.UseShellExecute = False
            DokanCTL.StartInfo.CreateNoWindow = True
            DokanCTL.Start()
        End Using
    End Sub

    Private Sub GamePartitionManager_ContentRendered(sender As Object, e As EventArgs) Handles Me.ContentRendered
        PartitionLoaderWorker.RunWorkerAsync()
    End Sub

    Private Sub GamePartitionManager_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        NewLoadingWindow = New SyncWindow() With {.Title = "Loading game partition", .ShowActivated = True, .WindowStartupLocation = WindowStartupLocation.CenterScreen, .Topmost = True}
        NewLoadingWindow.LoadProgressBar.IsIndeterminate = True
        NewLoadingWindow.LoadStatusTextBlock.Text = "Loading game info, please wait"
        NewLoadingWindow.Show()
    End Sub

    Private Sub PartitionLoaderWorker_DoWork(sender As Object, e As DoWorkEventArgs) Handles PartitionLoaderWorker.DoWork
        Dim DrivePath As String = AssociatedDriveLetter.ToUpper + ":\"
        Dim ResPath As String = AssociatedDriveLetter.ToUpper + ":\res"

        If File.Exists(ResPath + "\jkt_001.png") Then

            CoverPictureBox.Dispatcher.BeginInvoke(Sub()
                                                       Dim CoverImage As New BitmapImage()
                                                       CoverImage.BeginInit()
                                                       CoverImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache
                                                       CoverImage.CacheOption = BitmapCacheOption.OnLoad
                                                       CoverImage.UriSource = New Uri(ResPath + "\jkt_001.png")
                                                       CoverImage.EndInit()
                                                       CoverPictureBox.Source = CoverImage
                                                       CoverPictureBox.Tag = ResPath + "\jkt_001.png"
                                                   End Sub)
        End If

        '\res\info.sys
        If File.Exists(ResPath + "\info.sys") Then
            Dim GameInfos As String() = File.ReadAllLines(ResPath + "\info.sys")

            GameTitleTextBox.Dispatcher.BeginInvoke(Sub() GameTitleTextBox.Text = GameInfos(0).Split("="c)(1).Trim())
            GameIDTextBox.Dispatcher.BeginInvoke(Sub() GameIDTextBox.Text = GameInfos(1).Split("="c)(1).Replace("_", "-").Replace(".", "").Trim())

            If Not GameInfos(2).Split("="c)(1).Trim() = "0" Then
                ShowGameIDCheckBox.Dispatcher.BeginInvoke(Sub() ShowGameIDCheckBox.IsChecked = True)
            End If

            GameReleaseDateTextBox.Dispatcher.BeginInvoke(Sub() GameReleaseDateTextBox.Text = GameInfos(3).Split("="c)(1).Trim())
            GameDeveloperTextBox.Dispatcher.BeginInvoke(Sub() GameDeveloperTextBox.Text = GameInfos(4).Split("="c)(1).Trim())
            PublisherTextBox.Dispatcher.BeginInvoke(Sub() PublisherTextBox.Text = GameInfos(5).Split("="c)(1).Trim())
            GameNoteTextBox.Dispatcher.BeginInvoke(Sub() GameNoteTextBox.Text = GameInfos(6).Split("="c)(1).Trim())
            GameWebsiteTextBox.Dispatcher.BeginInvoke(Sub() GameWebsiteTextBox.Text = GameInfos(7).Split("="c)(1).Trim())

            If Not GameInfos(13).Split("="c)(1).Trim() = "0" Then
                ShowCopyrightCheckBox.Dispatcher.BeginInvoke(Sub() ShowCopyrightCheckBox.IsChecked = True)
            End If

            GameGenreTextBox.Dispatcher.BeginInvoke(Sub() GameGenreTextBox.Text = GameInfos(14).Split("="c)(1).Trim())
            RegionTextBox.Dispatcher.BeginInvoke(Sub() RegionTextBox.Text = GameInfos(18).Split("="c)(1).Trim())
        End If

        '\icon.sys
        If File.Exists(DrivePath + "\icon.sys") Then
            Dim Infos As String() = File.ReadAllLines(ResPath + "\info.sys")
            UninstallMsgTextBox.Dispatcher.BeginInvoke(Sub() UninstallMsgTextBox.Text = Infos(15).Split("="c)(1).Trim())
        End If
    End Sub

    Private Sub PartitionLoaderWorker_RunWorkerCompleted(sender As Object, e As RunWorkerCompletedEventArgs) Handles PartitionLoaderWorker.RunWorkerCompleted
        NewLoadingWindow.Close()

        For Each Img In GamePartitionManagerGrid.Children.OfType(Of Image)
            ImageChangesDictionary.Add(Img, Img.Source)
        Next
    End Sub

    Private Sub CloseButton_Click(sender As Object, e As RoutedEventArgs) Handles CloseButton.Click
        UnMountPartition(AssociatedDriveLetter.ToUpper)
        Utils.RemoveMountedDriveLetter(AssociatedDriveLetter)
        Unmounted = True
        Close()
    End Sub

    Private Sub CoverPictureBox_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs) Handles CoverPictureBox.MouseLeftButtonDown
        Dim OFD As New Forms.OpenFileDialog() With {.Title = "Choose your .png file.", .Filter = "png files (*.png)|*.png"}

        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            CoverPictureBox.Source = New BitmapImage(New Uri(OFD.FileName))
            CoverPictureBox.Tag = OFD.FileName
        End If
    End Sub

    Private Sub SaveButton_Click(sender As Object, e As RoutedEventArgs) Handles SaveButton.Click

        Mouse.SetCursor(Input.Cursors.Wait)

        'Unmount the partition from pc
        UnMountPartition(AssociatedDriveLetter.ToUpper)
        Utils.RemoveMountedDriveLetter(AssociatedDriveLetter)
        Unmounted = True

        'Create a temporary directory to upload the changes
        Dim TempDirectory As String = My.Computer.FileSystem.CurrentDirectory + "\Temp"
        If Not Directory.Exists(TempDirectory) Then
            Directory.CreateDirectory(TempDirectory)
        End If
        If Not Directory.Exists(TempDirectory + "\res") Then
            Directory.CreateDirectory(TempDirectory + "\res")
            Directory.CreateDirectory(TempDirectory + "\res\image")
        End If

        'PNG compressor
        Dim Quantizer As New WuQuantizer()
        'Save selected cover and pictures as compressed PNG if modified
        For Each Img In GamePartitionManagerGrid.Children.OfType(Of Image)()
            Dim oldValue = (From kp As KeyValuePair(Of Image, ImageSource) In ImageChangesDictionary
                            Where kp.Key Is Img
                            Select kp.Value).First()
            If oldValue IsNot Img.Source Then
                If Img.Name = "CoverPictureBox" Then
                    If CoverPictureBox.Tag IsNot Nothing Then
                        Dim Cover1Bitmap As System.Drawing.Bitmap = Utils.GetResizedBitmap(CoverPictureBox.Tag.ToString, 140, 200)
                        Dim Cover2Bitmap As System.Drawing.Bitmap = Utils.GetResizedBitmap(CoverPictureBox.Tag.ToString, 74, 108)

                        If Cover1Bitmap.PixelFormat <> System.Drawing.Imaging.PixelFormat.Format32bppArgb Then
                            Utils.ConvertTo32bppAndDisposeOriginal(Cover1Bitmap)
                        End If
                        If Cover2Bitmap.PixelFormat <> System.Drawing.Imaging.PixelFormat.Format32bppArgb Then
                            Utils.ConvertTo32bppAndDisposeOriginal(Cover2Bitmap)
                        End If

                        Try
                            Using CompressedImage = Quantizer.QuantizeImage(Cover1Bitmap)
                                CompressedImage.Save(TempDirectory + "\res\jkt_001.png", System.Drawing.Imaging.ImageFormat.Png)
                            End Using
                            Using CompressedImage = Quantizer.QuantizeImage(Cover2Bitmap)
                                CompressedImage.Save(TempDirectory + "\res\jkt_002.png", System.Drawing.Imaging.ImageFormat.Png)
                            End Using
                        Catch ex As Exception
                            MsgBox("Could not compress PNG." + vbCrLf + ex.Message, MsgBoxStyle.Exclamation)
                        Finally
                            Cover1Bitmap.Dispose()
                        End Try
                    End If
                End If
                If Img.Name = "BackgroundImagePictureBox" Then
                    If Not BackgroundImagePictureBox.Tag IsNot Nothing Then
                        Dim BackgroundImageBitmap As System.Drawing.Bitmap = Utils.GetResizedBitmap(BackgroundImagePictureBox.Tag.ToString, 640, 350)

                        If BackgroundImageBitmap.PixelFormat <> System.Drawing.Imaging.PixelFormat.Format32bppArgb Then
                            Utils.ConvertTo32bppAndDisposeOriginal(BackgroundImageBitmap)
                        End If

                        Try
                            Using CompressedImage = Quantizer.QuantizeImage(BackgroundImageBitmap)
                                CompressedImage.Save(TempDirectory + "\res\image\0.png", System.Drawing.Imaging.ImageFormat.Png)
                            End Using
                        Catch ex As Exception
                            MsgBox("Could not compress PNG." + vbCrLf + ex.Message, MsgBoxStyle.Exclamation)
                        Finally
                            BackgroundImageBitmap.Dispose()
                        End Try
                    End If
                End If
                If Img.Name = "ScreenshotImage1PictureBox" Then
                    If ScreenshotImage1PictureBox.Tag IsNot Nothing Then
                        Dim ScreenshotImageBitmap As System.Drawing.Bitmap = Utils.GetResizedBitmap(ScreenshotImage1PictureBox.Tag.ToString, 640, 350)

                        If ScreenshotImageBitmap.PixelFormat <> System.Drawing.Imaging.PixelFormat.Format32bppArgb Then
                            Utils.ConvertTo32bppAndDisposeOriginal(ScreenshotImageBitmap)
                        End If

                        Try
                            Using CompressedImage = Quantizer.QuantizeImage(ScreenshotImageBitmap)
                                CompressedImage.Save(TempDirectory + "\res\image\1.png", System.Drawing.Imaging.ImageFormat.Png)
                            End Using
                        Catch ex As Exception
                            MsgBox("Could not compress PNG." + vbCrLf + ex.Message, MsgBoxStyle.Exclamation)
                        Finally
                            ScreenshotImageBitmap.Dispose()
                        End Try
                    End If
                End If
                If Img.Name = "ScreenshotImage2PictureBox" Then
                    If ScreenshotImage2PictureBox.Tag IsNot Nothing Then
                        Dim ScreenshotImageBitmap As System.Drawing.Bitmap = Utils.GetResizedBitmap(ScreenshotImage2PictureBox.Tag.ToString, 640, 350)


                        If ScreenshotImageBitmap.PixelFormat <> System.Drawing.Imaging.PixelFormat.Format32bppArgb Then
                            Utils.ConvertTo32bppAndDisposeOriginal(ScreenshotImageBitmap)
                        End If

                        Try
                            Using CompressedImage = Quantizer.QuantizeImage(ScreenshotImageBitmap)
                                CompressedImage.Save(TempDirectory + "\res\image\2.png", System.Drawing.Imaging.ImageFormat.Png)
                            End Using
                        Catch ex As Exception
                            MsgBox("Could not compress PNG." + vbCrLf + ex.Message, MsgBoxStyle.Exclamation)
                        Finally
                            ScreenshotImageBitmap.Dispose()
                        End Try
                    End If
                End If
            End If
        Next

        'Write info.sys to res directory
        Using SYSWriter As New StreamWriter(TempDirectory + "\res\info.sys", False)
            SYSWriter.WriteLine("title = " + GameTitleTextBox.Text)
            SYSWriter.WriteLine("title_id = " + GameIDTextBox.Text)

            If ShowGameIDCheckBox.IsChecked Then
                SYSWriter.WriteLine("title_sub_id = 1")
            Else
                SYSWriter.WriteLine("title_sub_id = 0")
            End If

            SYSWriter.WriteLine("release_date = " + GameReleaseDateTextBox.Text)
            SYSWriter.WriteLine("developer_id = " + GameDeveloperTextBox.Text)
            SYSWriter.WriteLine("publisher_id = " + PublisherTextBox.Text)
            SYSWriter.WriteLine("note = " + GameNoteTextBox.Text)
            SYSWriter.WriteLine("content_web = " + GameWebsiteTextBox.Text)
            SYSWriter.WriteLine("image_topviewflag = 0")
            SYSWriter.WriteLine("image_type = 0")
            SYSWriter.WriteLine("image_count = 1")
            SYSWriter.WriteLine("image_viewsec = 600")

            If ShowCopyrightCheckBox.IsChecked Then
                SYSWriter.WriteLine("copyright_viewflag = 1")
            Else
                SYSWriter.WriteLine("copyright_viewflag = 0")
            End If

            SYSWriter.WriteLine("copyright_imgcount = 1")
            SYSWriter.WriteLine("genre = " + GameGenreTextBox.Text)
            SYSWriter.WriteLine("parental_lock = 1")
            SYSWriter.WriteLine("effective_date = 0")
            SYSWriter.WriteLine("expire_date = 0")
            SYSWriter.WriteLine("area = " + RegionTextBox.Text)
            SYSWriter.WriteLine("violence_flag = 0")
            SYSWriter.WriteLine("content_type = 255")
            SYSWriter.WriteLine("content_subtype = 0")
        End Using

        'Create man.xml (manual)
        Using MANWriter As New StreamWriter(TempDirectory + "\res\man.xml", False)
            MANWriter.WriteLine("<?xml version=""1.0"" encoding=""UTF-8""?>")
            MANWriter.WriteLine("")
            MANWriter.WriteLine("<MANUAL version=""1.0"">")
            MANWriter.WriteLine("")
            MANWriter.WriteLine("<IMG id=""bg"" src=""./image/0.png"" />") 'This is the background image
            MANWriter.WriteLine("")
            MANWriter.WriteLine("<MENUGROUP id=""TOP"">")
            MANWriter.WriteLine("<TITLE id=""TOP-TITLE"" label=""" + GameTitleTextBox.Text + """ />")
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

        'Write icon.sys to temp directory
        Using CNFWriter As New StreamWriter(TempDirectory + "\icon.sys", False)
            CNFWriter.WriteLine("PS2X")
            CNFWriter.WriteLine("title0=" + GameTitleTextBox.Text)
            CNFWriter.WriteLine("title1=" + GameIDTextBox.Text.Replace("-", "_").Insert(8, "."))
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
            CNFWriter.WriteLine("uninstallmes0=" + UninstallMsgTextBox.Text)
            CNFWriter.WriteLine("uninstallmes1=")
            CNFWriter.WriteLine("uninstallmes2=")
        End Using

        'Create a copy of hdl_dump in the temp directory
        File.Copy(My.Computer.FileSystem.CurrentDirectory + "\Tools\hdl_dump.exe", TempDirectory + "\hdl_dump.exe", True)

        'Switch to temp directory and inject the files
        Directory.SetCurrentDirectory(TempDirectory)

        'Modify the partition header (icon.sys)
        Dim HDLDumpOutput As String = ""
        Using HDLDump As New Process()
            HDLDump.StartInfo.FileName = "hdl_dump.exe"
            HDLDump.StartInfo.Arguments = "modify_header " + NewMainWindow.MountedDrive.HDLDriveName + " " + AssociatedPartition
            HDLDump.StartInfo.RedirectStandardOutput = True
            HDLDump.StartInfo.UseShellExecute = False
            HDLDump.StartInfo.CreateNoWindow = True
            HDLDump.Start()

            Dim OutputReader As StreamReader = HDLDump.StandardOutput
            HDLDumpOutput = HDLDump.StandardOutput.ReadToEnd()
        End Using

        'Update the files on the partition
        If Not HDLDumpOutput.Contains("partition not found:") Then
            'Set the mkdir & put commands
            Using CommandFileWriter As New StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "Tools\cmdlist\push.txt", False)
                CommandFileWriter.WriteLine("device " + NewMainWindow.MountedDrive.DriveID)
                CommandFileWriter.WriteLine("mount " + AssociatedPartition)
                CommandFileWriter.WriteLine("mkdir res") 'continues if it already exists - useful if not present yet
                CommandFileWriter.WriteLine("cd res")

                If File.Exists("res\info.sys") Then
                    CommandFileWriter.WriteLine("rm info.sys")
                    CommandFileWriter.WriteLine("put res\info.sys")
                    CommandFileWriter.WriteLine("rename res\info.sys info.sys")
                End If
                If File.Exists("res\jkt_001.png") Then
                    CommandFileWriter.WriteLine("rm jkt_001.png")
                    CommandFileWriter.WriteLine("put res\jkt_001.png")
                    CommandFileWriter.WriteLine("rename res\jkt_001.png jkt_001.png")
                End If
                If File.Exists("res\jkt_002.png") Then
                    CommandFileWriter.WriteLine("rm jkt_002.png")
                    CommandFileWriter.WriteLine("put res\jkt_002.png")
                    CommandFileWriter.WriteLine("rename res\jkt_002.png jkt_002.png")
                End If
                If File.Exists("res\jkt_cp.png") Then
                    CommandFileWriter.WriteLine("rm jkt_cp.png")
                    CommandFileWriter.WriteLine("put res\jkt_cp.png")
                    CommandFileWriter.WriteLine("rename res\jkt_cp.png jkt_cp.png")
                End If
                If File.Exists("res\man.xml") Then
                    CommandFileWriter.WriteLine("rm man.xml")
                    CommandFileWriter.WriteLine("put res\man.xml")
                    CommandFileWriter.WriteLine("rename res\man.xml man.xml")
                End If
                If File.Exists("res\notice.jpg") Then
                    CommandFileWriter.WriteLine("rm notice.jpg")
                    CommandFileWriter.WriteLine("put res\notice.jpg")
                    CommandFileWriter.WriteLine("rename res\notice.jpg notice.jpg")
                End If

                If Directory.Exists("res\image") Then
                    CommandFileWriter.WriteLine("mkdir image")
                    CommandFileWriter.WriteLine("cd image")

                    If File.Exists("res\image\0.png") Then
                        CommandFileWriter.WriteLine("rm 0.png")
                        CommandFileWriter.WriteLine("put res\image\0.png")
                        CommandFileWriter.WriteLine("rename res\image\0.png 0.png")
                    End If
                    If File.Exists("res\image\1.png") Then
                        CommandFileWriter.WriteLine("rm 1.png")
                        CommandFileWriter.WriteLine("put res\image\1.png")
                        CommandFileWriter.WriteLine("rename res\image\1.png 1.png")
                    End If
                    If File.Exists("res\image\2.png") Then
                        CommandFileWriter.WriteLine("rm 2.png")
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
                PFSShellProcess.StartInfo.UseShellExecute = False
                PFSShellProcess.StartInfo.CreateNoWindow = True
                PFSShellProcess.Start()
                PFSShellProcess.WaitForExit()
            End Using
        Else
            MsgBox("There was an error while modifying the partition, please check if you have enough space and report the next error.", MsgBoxStyle.Exclamation, "Error installing game")
            MsgBox(HDLDumpOutput)

            'Set the current directory back
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory)

            'Remove the temporary folder
            If Directory.Exists(TempDirectory) Then
                Directory.Delete(TempDirectory, True)
            End If

            Exit Sub
        End If

        'Set the current directory back
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory)

        'Remove the temporary folder
        If Directory.Exists(TempDirectory) Then
            Directory.Delete(TempDirectory, True)
        End If

        Mouse.SetCursor(Input.Cursors.Arrow)
        MsgBox("Done ! This window will be closed, please re-mount the game partition to load the changes.", MsgBoxStyle.Information)
        Close()
    End Sub

    Private Sub LoadFromPSXButton_Click(sender As Object, e As RoutedEventArgs) Handles LoadFromPSXButton.Click
        Try
            If Not String.IsNullOrWhiteSpace(GameIDTextBox.Text) Then
                PSXDatacenterBrowser.Navigate("https://psxdatacenter.com/psx2/games2/" + GameIDTextBox.Text + ".html")
            Else
                MsgBox("Please enter a valid game ID (SLUS-12345) to perform a search.", MsgBoxStyle.Exclamation)
            End If
        Catch ex As Exception
            MsgBox("Could not load game images and information, please check your game ID.", MsgBoxStyle.Exclamation, "No information found for this game ID")
        End Try
    End Sub

    Private Sub PSXDatacenterBrowser_DocumentCompleted(sender As Object, e As WebBrowserDocumentCompletedEventArgs) Handles PSXDatacenterBrowser.DocumentCompleted
        Try
            'Get the game infos
            Dim infoTable As HtmlElement = PSXDatacenterBrowser.Document.GetElementById("table4")
            Dim infoRows As HtmlElementCollection = PSXDatacenterBrowser.Document.GetElementsByTagName("tr")

            'Game Title
            If infoRows.Item(4).InnerText IsNot Nothing Then
                GameTitleTextBox.Text = infoRows.Item(4).InnerText.Split(New String() {"OFFICIAL TITLE "}, StringSplitOptions.RemoveEmptyEntries)(0)
            End If

            'Game ID
            If infoRows.Item(6).InnerText IsNot Nothing Then
                GameIDTextBox.Text = infoRows.Item(6).InnerText.Split(New String() {"SERIAL NUMBER(S) "}, StringSplitOptions.RemoveEmptyEntries)(0)
            End If

            'Region
            If infoRows.Item(7).InnerText IsNot Nothing Then
                Dim Region As String = infoRows.Item(7).InnerText.Split(New String() {"REGION "}, StringSplitOptions.RemoveEmptyEntries)(0)
                Select Case Region
                    Case "PAL"
                        RegionTextBox.Text = "E"
                    Case "NTSC-U"
                        RegionTextBox.Text = "U"
                    Case "NTSC-J"
                        RegionTextBox.Text = "J"
                End Select
            End If

            'Genre
            If infoRows.Item(8).InnerText IsNot Nothing Then
                GameGenreTextBox.Text = infoRows.Item(8).InnerText.Split(New String() {"GENRE / STYLE "}, StringSplitOptions.RemoveEmptyEntries)(0)
            End If

            'Developer
            If infoRows.Item(9).InnerText IsNot Nothing Then
                GameDeveloperTextBox.Text = infoRows.Item(9).InnerText.Split(New String() {"DEVELOPER "}, StringSplitOptions.RemoveEmptyEntries)(0)
            End If

            'Publisher
            If infoRows.Item(10).InnerText IsNot Nothing Then
                PublisherTextBox.Text = infoRows.Item(10).InnerText.Split(New String() {"PUBLISHER "}, StringSplitOptions.RemoveEmptyEntries)(0)
            End If

            'Release Date
            If infoRows.Item(11).InnerText IsNot Nothing Then
                GameReleaseDateTextBox.Text = infoRows.Item(11).InnerText.Split(New String() {"DATE RELEASED "}, StringSplitOptions.RemoveEmptyEntries)(0)
            End If

            'Publisher
            If infoRows.Item(10).InnerText IsNot Nothing Then
                Dim ReturnPublisherWebsite As String = infoRows.Item(10).InnerText.Split(New String() {"PUBLISHER "}, StringSplitOptions.RemoveEmptyEntries)(0)

                If ReturnPublisherWebsite.Contains("2K") Then
                    GameWebsiteTextBox.Text = "https://2k.com/"
                ElseIf ReturnPublisherWebsite.Contains("Activision") Then
                    GameWebsiteTextBox.Text = "https://www.activision.com/"
                ElseIf ReturnPublisherWebsite.Contains("Bandai") Then
                    GameWebsiteTextBox.Text = "http://www.bandai.com/"
                ElseIf ReturnPublisherWebsite.Contains("Capcom") Then
                    GameWebsiteTextBox.Text = "http://www.capcom.com/"
                ElseIf ReturnPublisherWebsite.Contains("Electronic Arts") Then
                    GameWebsiteTextBox.Text = "http://ea.com/"
                ElseIf ReturnPublisherWebsite.Contains("EA Sports") Then
                    GameWebsiteTextBox.Text = "https://www.easports.com/"
                ElseIf ReturnPublisherWebsite.Contains("Konami") Then
                    GameWebsiteTextBox.Text = "https://www.konami.com/"
                ElseIf ReturnPublisherWebsite.Contains("Rockstar Games") Then
                    GameWebsiteTextBox.Text = "https://www.rockstargames.com/"
                ElseIf ReturnPublisherWebsite.Contains("Sega") Then
                    GameWebsiteTextBox.Text = "http://sega.com/"
                ElseIf ReturnPublisherWebsite.Contains("Sony Computer Entertainment") Then
                    GameWebsiteTextBox.Text = "https://www.sie.com/en/index.html"
                ElseIf ReturnPublisherWebsite.Contains("THQ") Then
                    GameWebsiteTextBox.Text = "https://www.thqnordic.com/"
                ElseIf ReturnPublisherWebsite.Contains("Ubisoft") Then
                    GameWebsiteTextBox.Text = "https://www.ubisoft.com/"
                End If

            End If

            'Get the game cover
            If Not String.IsNullOrWhiteSpace(PSXDatacenterBrowser.Document.GetElementById("table2").GetElementsByTagName("img")(1).GetAttribute("src")) Then
                CoverPictureBox.Source = New BitmapImage(New Uri(PSXDatacenterBrowser.Document.GetElementById("table2").GetElementsByTagName("img")(1).GetAttribute("src")))
                CoverPictureBox.Tag = PSXDatacenterBrowser.Document.GetElementById("table2").GetElementsByTagName("img")(1).GetAttribute("src")
            End If

            'Get a background image (currently a screenshot too)
            If Not String.IsNullOrWhiteSpace(PSXDatacenterBrowser.Document.GetElementById("table22").GetElementsByTagName("img")(0).GetAttribute("src")) Then
                BackgroundImagePictureBox.Source = New BitmapImage(New Uri(PSXDatacenterBrowser.Document.GetElementById("table22").GetElementsByTagName("img")(0).GetAttribute("src")))
                BackgroundImagePictureBox.Tag = PSXDatacenterBrowser.Document.GetElementById("table22").GetElementsByTagName("img")(0).GetAttribute("src")
            End If

            'Get some screenshots
            If Not String.IsNullOrWhiteSpace(PSXDatacenterBrowser.Document.GetElementById("table22").GetElementsByTagName("img")(1).GetAttribute("src")) Then
                ScreenshotImage1PictureBox.Source = New BitmapImage(New Uri(PSXDatacenterBrowser.Document.GetElementById("table22").GetElementsByTagName("img")(1).GetAttribute("src")))
                ScreenshotImage1PictureBox.Tag = PSXDatacenterBrowser.Document.GetElementById("table22").GetElementsByTagName("img")(1).GetAttribute("src")
            End If
            If Not String.IsNullOrWhiteSpace(PSXDatacenterBrowser.Document.GetElementById("table22").GetElementsByTagName("img")(2).GetAttribute("src")) Then
                ScreenshotImage2PictureBox.Source = New BitmapImage(New Uri(PSXDatacenterBrowser.Document.GetElementById("table22").GetElementsByTagName("img")(2).GetAttribute("src")))
                ScreenshotImage2PictureBox.Tag = PSXDatacenterBrowser.Document.GetElementById("table22").GetElementsByTagName("img")(2).GetAttribute("src")
            End If

        Catch ex As Exception
            MsgBox(ex.Message)
        End Try
    End Sub

    Private Sub UpdateExecuteKELFButton_Click(sender As Object, e As RoutedEventArgs) Handles UpdateExecuteKELFButton.Click

        'Unmount the partition from pc
        UnMountPartition(AssociatedDriveLetter.ToUpper)
        Utils.RemoveMountedDriveLetter(AssociatedDriveLetter)

        'Create a temporary directory to upload the changes
        Dim TempDirectory As String = My.Computer.FileSystem.CurrentDirectory + "\Temp"
        If Not Directory.Exists(TempDirectory) Then
            Directory.CreateDirectory(TempDirectory)
        End If

        'Download latest OPL-Launcher
        ContentDownloader.DownloadFile("https://github.com/ps2homebrew/OPL-Launcher/releases/download/latest/OPL-Launcher.elf", My.Computer.FileSystem.CurrentDirectory + "\Tools\OPL-Launcher.elf")

        'Wrap OPL-Launcher as EXECUTE.KELF
        Dim WrapProcess As New Process()
        WrapProcess.StartInfo.FileName = My.Computer.FileSystem.CurrentDirectory + "\Tools\SCEDoormat_NoME.exe"
        WrapProcess.StartInfo.Arguments = """" + My.Computer.FileSystem.CurrentDirectory + "\Tools\OPL-Launcher.elf"" """ + TempDirectory + "\EXECUTE.KELF"""
        WrapProcess.StartInfo.CreateNoWindow = True
        WrapProcess.Start()
        WrapProcess.WaitForExit()

        'Create a copy of hdl_dump in the temp directory
        File.Copy(My.Computer.FileSystem.CurrentDirectory + "\Tools\hdl_dump.exe", TempDirectory + "\hdl_dump.exe", True)

        'Switch to temp directory and inject the new EXECUTE.KELF
        Directory.SetCurrentDirectory(TempDirectory)

        'Modify the partition header
        Dim HDLDumpOutput As String = ""
        Using HDLDump As New Process()
            HDLDump.StartInfo.FileName = "hdl_dump.exe"
            HDLDump.StartInfo.Arguments = "modify_header " + NewMainWindow.MountedDrive.HDLDriveName + " " + AssociatedPartition
            HDLDump.StartInfo.RedirectStandardOutput = True
            HDLDump.StartInfo.UseShellExecute = False
            HDLDump.StartInfo.CreateNoWindow = True
            HDLDump.Start()

            Dim OutputReader As StreamReader = HDLDump.StandardOutput
            HDLDumpOutput = HDLDump.StandardOutput.ReadToEnd()
        End Using

        'Update the files on the partition
        If Not HDLDumpOutput.Contains("partition not found:") Then
            MsgBox("Done")
        End If

        'Set the current directory back
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory)
        Mouse.SetCursor(Input.Cursors.Arrow)

        'Remove the temporary folder
        If Directory.Exists(TempDirectory) Then
            Directory.Delete(TempDirectory, True)
        End If

    End Sub

    Private Sub LoadAdditionalImagesButton_Click(sender As Object, e As RoutedEventArgs) Handles LoadAdditionalImagesButton.Click
        Dim DrivePath As String = AssociatedDriveLetter.ToUpper + ":\"
        Dim ResPath As String = AssociatedDriveLetter.ToUpper + ":\res"

        If MsgBox("Do you really want to load the background & screenshot images ? This can take some time.", MsgBoxStyle.YesNo, "Loading required") = MsgBoxResult.Yes Then
            If File.Exists(ResPath + "\image\0.png") Then
                Dim BGImage As New BitmapImage()
                BGImage.BeginInit()
                BGImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache
                BGImage.CacheOption = BitmapCacheOption.OnLoad
                BGImage.UriSource = New Uri(ResPath + "\image\0.png")
                BGImage.EndInit()

                BackgroundImagePictureBox.Source = BGImage
                BackgroundImagePictureBox.Tag = ResPath + "\image\0.png"
            End If
            If File.Exists(ResPath + "\image\1.png") Then
                Dim Screenshot1Image As New BitmapImage()
                Screenshot1Image.BeginInit()
                Screenshot1Image.CreateOptions = BitmapCreateOptions.IgnoreImageCache
                Screenshot1Image.CacheOption = BitmapCacheOption.OnLoad
                Screenshot1Image.UriSource = New Uri(ResPath + "\image\1.png")
                Screenshot1Image.EndInit()

                ScreenshotImage1PictureBox.Source = Screenshot1Image
                ScreenshotImage1PictureBox.Tag = ResPath + "\image\1.png"
            End If
            If File.Exists(ResPath + "\image\2.png") Then
                Dim Screenshot2Image As New BitmapImage()
                Screenshot2Image.BeginInit()
                Screenshot2Image.CreateOptions = BitmapCreateOptions.IgnoreImageCache
                Screenshot2Image.CacheOption = BitmapCacheOption.OnLoad
                Screenshot2Image.UriSource = New Uri(ResPath + "\image\2.png")
                Screenshot2Image.EndInit()

                ScreenshotImage2PictureBox.Source = Screenshot2Image
                ScreenshotImage2PictureBox.Tag = ResPath + "\image\2.png"
            End If
        End If

    End Sub

End Class
