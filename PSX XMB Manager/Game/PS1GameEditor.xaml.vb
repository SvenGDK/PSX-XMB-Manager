﻿Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports nQuant

Public Class PS1GameEditor

    Public ProjectDirectory As String
    Public WithEvents PSXDatacenterBrowser As New WebBrowser()
    Public AutoSave As Boolean = False

    Private Sub LoadFromPSXButton_Click(sender As Object, e As RoutedEventArgs) Handles LoadFromPSXButton.Click
        Try
            If Not String.IsNullOrWhiteSpace(GameTitleTextBox.Text) AndAlso Not String.IsNullOrWhiteSpace(GameIDTextBox.Text) Then
                Dim GameStartLetter As String = GameTitleTextBox.Text.Substring(0, 1) 'Take the first letter of the game title (required to browse PSXDatacenter)
                Dim RegionCharacter As String = PS1Game.GetRegionChar(GameIDTextBox.Text)

                If Utils.IsURLValid("https://psxdatacenter.com/games/" + RegionCharacter + "/" + GameStartLetter + "/" + GameIDTextBox.Text + ".html") Then
                    PSXDatacenterBrowser.Navigate("https://psxdatacenter.com/games/" + RegionCharacter + "/" + GameStartLetter + "/" + GameIDTextBox.Text + ".html")
                Else
                    MsgBox("Could not find any data for this game.", MsgBoxStyle.Information)
                End If
            Else
                MsgBox("Please enter a valid game title & ID (SLUS-12345) to perform a search.", MsgBoxStyle.Exclamation)
            End If
        Catch ex As Exception
            MsgBox("Could not load game images and information, please check your Game ID.", MsgBoxStyle.Exclamation, "No information found for this game ID")
        End Try
    End Sub

    Private Sub PSXDatacenterBrowser_DocumentCompleted(sender As Object, e As WebBrowserDocumentCompletedEventArgs) Handles PSXDatacenterBrowser.DocumentCompleted
        Try
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
                    GameTitleTextBox.Text = infoTable.Item(0).Children(1).InnerText.Trim()
                End If

                'Region
                If infoTable.Item(3).Children.Count >= 1 Then
                    Dim Region As String = infoTable.Item(3).Children(1).InnerText.Trim()
                    Select Case Region
                        Case "PAL"
                            GameRegionTextBox.Text = "E"
                        Case "NTSC-U"
                            GameRegionTextBox.Text = "U"
                        Case "NTSC-J"
                            GameRegionTextBox.Text = "J"
                    End Select
                End If

                'Genre
                If infoTable.Item(4).Children.Count >= 1 Then
                    GameGenreTextBox.Text = infoTable.Item(4).Children(1).InnerText.Trim()
                End If

                'Developer
                If infoTable.Item(5).Children.Count >= 1 Then
                    GameDeveloperTextBox.Text = infoTable.Item(5).Children(1).InnerText.Trim()
                End If

                'Publisher
                If infoTable.Item(6).Children.Count >= 1 Then
                    GamePublisherTextBox.Text = infoTable.Item(6).Children(1).InnerText.Trim()
                End If

                'Release Date
                If infoTable.Item(7).Children.Count >= 1 Then
                    GameReleaseDateTextBox.Text = infoTable.Item(7).Children(1).InnerText.Trim()
                End If
            End If

            'Set cover
            If coverTableRows.Count >= 2 Then
                If coverTableRows.Item(2) IsNot Nothing AndAlso coverTableRows.Item(2).GetElementsByTagName("img").Count > 0 Then
                    Dim GameCoverSource As String = coverTableRows.Item(2).GetElementsByTagName("img")(0).GetAttribute("src").Trim()
                    GameCoverImage.Tag = GameCoverSource
                    If Dispatcher.CheckAccess() = False Then
                        Dispatcher.BeginInvoke(Sub()
                                                   Dim TempBitmapImage = New BitmapImage()
                                                   TempBitmapImage.BeginInit()
                                                   TempBitmapImage.CacheOption = BitmapCacheOption.OnLoad
                                                   TempBitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache
                                                   TempBitmapImage.UriSource = New Uri(GameCoverSource, UriKind.RelativeOrAbsolute)
                                                   TempBitmapImage.EndInit()
                                                   GameCoverImage.Source = TempBitmapImage
                                               End Sub)
                    Else
                        Dim TempBitmapImage = New BitmapImage()
                        TempBitmapImage.BeginInit()
                        TempBitmapImage.CacheOption = BitmapCacheOption.OnLoad
                        TempBitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache
                        TempBitmapImage.UriSource = New Uri(GameCoverSource, UriKind.RelativeOrAbsolute)
                        TempBitmapImage.EndInit()
                        GameCoverImage.Source = TempBitmapImage
                    End If
                End If
            End If

            'Get some images
            If PSXDatacenterBrowser.Document.GetElementById("table22") IsNot Nothing Then
                BackgroundImagePictureBox.Source = New BitmapImage(New Uri(PSXDatacenterBrowser.Document.GetElementById("table22").GetElementsByTagName("img")(0).GetAttribute("src")))
                BackgroundImagePictureBox.Tag = PSXDatacenterBrowser.Document.GetElementById("table22").GetElementsByTagName("img")(0).GetAttribute("src")

                ScreenshotImage1PictureBox.Source = New BitmapImage(New Uri(PSXDatacenterBrowser.Document.GetElementById("table22").GetElementsByTagName("img")(1).GetAttribute("src")))
                ScreenshotImage1PictureBox.Tag = PSXDatacenterBrowser.Document.GetElementById("table22").GetElementsByTagName("img")(1).GetAttribute("src")

                ScreenshotImage2PictureBox.Source = New BitmapImage(New Uri(PSXDatacenterBrowser.Document.GetElementById("table22").GetElementsByTagName("img")(2).GetAttribute("src")))
                ScreenshotImage2PictureBox.Tag = PSXDatacenterBrowser.Document.GetElementById("table22").GetElementsByTagName("img")(2).GetAttribute("src")
            End If

            'Save automatically if project is created using the Game Library
            If AutoSave = True Then
                SaveButton_Click(SaveButton, New RoutedEventArgs())
            End If

        Catch ex As Exception
            MsgBox(ex.Message)
        End Try
    End Sub

    Private Sub GameCoverImage_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs) Handles GameCoverImage.MouseLeftButtonDown
        Dim OFD As New OpenFileDialog() With {.Title = "Choose your cover .png file.", .Filter = "png files (*.png)|*.png"}

        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            GameCoverImage.Source = New BitmapImage(New Uri(OFD.FileName))
            GameCoverImage.Tag = OFD.FileName
        End If
    End Sub

    Public Sub ApplyKnownValues(GameID As String, GameTitle As String)
        'Set Title, ID & Region
        GameTitleTextBox.Text = GameTitle
        GameIDTextBox.Text = GameID
        GameRegionTextBox.Text = PS1Game.GetRegionChar(GameID)

        'Set Cover
        If Utils.IsURLValid("https://raw.githubusercontent.com/SvenGDK/PSMT-Covers/main/PS1/" + GameID + ".jpg") Then

            'Set Tag
            GameCoverImage.Tag = "https://raw.githubusercontent.com/SvenGDK/PSMT-Covers/main/PS1/" + GameID + ".jpg"

            'Load the Cover
            Dispatcher.BeginInvoke(Sub()
                                       Dim TempBitmapImage = New BitmapImage()
                                       TempBitmapImage.BeginInit()
                                       TempBitmapImage.CacheOption = BitmapCacheOption.OnLoad
                                       TempBitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache
                                       TempBitmapImage.UriSource = New Uri("https://raw.githubusercontent.com/SvenGDK/PSMT-Covers/main/PS1/" + GameID + ".jpg", UriKind.RelativeOrAbsolute)
                                       TempBitmapImage.EndInit()
                                       GameCoverImage.Source = TempBitmapImage
                                   End Sub)
        End If

        'Save automatically if project is created using the Game Library
        If AutoSave = True Then
            SaveButton_Click(SaveButton, New RoutedEventArgs())
        End If
    End Sub

    Private Sub SaveButton_Click(sender As Object, e As RoutedEventArgs) Handles SaveButton.Click

        Dim Quantizer As New WuQuantizer()

        'Create the res\image directory
        If Not Directory.Exists(ProjectDirectory + "\res\image") Then
            Directory.CreateDirectory(ProjectDirectory + "\res\image")
        End If

        'Save selected XMB cover as compressed PNG
        'Skips now already saved art
        If GameCoverImage.Tag IsNot Nothing AndAlso Not File.Exists(ProjectDirectory + "\res\jkt_001.png") Then
            If TypeOf GameCoverImage.Tag Is String Then
                Dim Cover1Bitmap As Bitmap = Utils.GetResizedBitmap(GameCoverImage.Tag.ToString(), 190, 200)
                Dim Cover2Bitmap As Bitmap = Utils.GetResizedBitmap(GameCoverImage.Tag.ToString(), 102, 108)

                If Cover1Bitmap IsNot Nothing AndAlso Cover2Bitmap IsNot Nothing Then
                    If Cover1Bitmap.PixelFormat <> Imaging.PixelFormat.Format32bppArgb Then
                        Utils.ConvertTo32bppAndDisposeOriginal(Cover1Bitmap)
                    End If
                    If Cover2Bitmap.PixelFormat <> Imaging.PixelFormat.Format32bppArgb Then
                        Utils.ConvertTo32bppAndDisposeOriginal(Cover2Bitmap)
                    End If

                    Try
                        Using CompressedImage = Quantizer.QuantizeImage(Cover1Bitmap)
                            CompressedImage?.Save(ProjectDirectory + "\res\jkt_001.png", Imaging.ImageFormat.Png)
                        End Using
                        Using CompressedImage = Quantizer.QuantizeImage(Cover2Bitmap)
                            CompressedImage?.Save(ProjectDirectory + "\res\jkt_002.png", Imaging.ImageFormat.Png)
                        End Using
                    Catch ex As Exception
                        MsgBox("Could not resize the selected cover. Please save it manually in the project folder:" + vbCrLf +
                               """\res\jkt_001.png"" 140 x 200" + vbCrLf +
                               """\res\jkt_002.png"" 74 x 108", MsgBoxStyle.Exclamation, "Cover Warning")
                    Finally
                        Cover1Bitmap.Dispose()
                        Cover2Bitmap.Dispose()
                    End Try
                Else
                    MsgBox("Could not resize the selected cover. Please save it manually in the project folder:" + vbCrLf +
                               """\res\jkt_001.png"" 140 x 200" + vbCrLf +
                               """\res\jkt_002.png"" 74 x 108", MsgBoxStyle.Exclamation, "Cover Warning")
                End If
            End If
        End If

        'Background image
        If BackgroundImagePictureBox.Tag IsNot Nothing AndAlso Not File.Exists(ProjectDirectory + "\res\image\0.png") Then
            If TypeOf BackgroundImagePictureBox.Tag Is String Then
                Dim BackgroundImageBitmap As Bitmap = Utils.GetResizedBitmap(BackgroundImagePictureBox.Tag.ToString, 640, 350)
                If BackgroundImageBitmap IsNot Nothing Then
                    If BackgroundImageBitmap.PixelFormat <> Imaging.PixelFormat.Format32bppArgb Then
                        Utils.ConvertTo32bppAndDisposeOriginal(BackgroundImageBitmap)
                    End If

                    Try
                        Using CompressedImage = Quantizer.QuantizeImage(BackgroundImageBitmap)
                            CompressedImage?.Save(ProjectDirectory + "\res\image\0.png", Imaging.ImageFormat.Png)
                        End Using
                    Catch ex As Exception
                        MsgBox("Could not resize the selected background. Please save it manually in the project folder:" + vbCrLf +
                               """\res\image\0.png"" 640 x 350", MsgBoxStyle.Exclamation, "Background Warning")
                    Finally
                        BackgroundImageBitmap.Dispose()
                    End Try
                Else
                    MsgBox("Could not resize the selected background. Please save it manually in the project folder:" + vbCrLf +
                               """\res\image\0.png"" 640 x 350", MsgBoxStyle.Exclamation, "Background Warning")
                End If
            End If
        End If

        'Screenshots
        If ScreenshotImage1PictureBox.Tag IsNot Nothing AndAlso Not File.Exists(ProjectDirectory + "\res\image\1.png") Then
            If TypeOf ScreenshotImage1PictureBox.Tag Is String Then
                Dim ScreenshotImageBitmap As Bitmap = Utils.GetResizedBitmap(ScreenshotImage1PictureBox.Tag.ToString, 640, 350)
                If ScreenshotImageBitmap IsNot Nothing Then
                    If ScreenshotImageBitmap.PixelFormat <> Imaging.PixelFormat.Format32bppArgb Then
                        Utils.ConvertTo32bppAndDisposeOriginal(ScreenshotImageBitmap)
                    End If

                    Try
                        Using CompressedImage = Quantizer.QuantizeImage(ScreenshotImageBitmap)
                            CompressedImage?.Save(ProjectDirectory + "\res\image\1.png", Imaging.ImageFormat.Png)
                        End Using
                    Catch ex As Exception
                        MsgBox("Could not resize the selected screenshot 1. Please save it manually in the project folder:" + vbCrLf +
                               """\res\image\1.png"" 640 x 350", MsgBoxStyle.Exclamation, "Screenshot 1 Warning")
                    Finally
                        ScreenshotImageBitmap.Dispose()
                    End Try
                Else
                    MsgBox("Could not resize the selected screenshot 1. Please save it manually in the project folder:" + vbCrLf +
                               """\res\image\1.png"" 640 x 350", MsgBoxStyle.Exclamation, "Screenshot 1 Warning")
                End If
            End If
        End If
        If ScreenshotImage2PictureBox.Tag IsNot Nothing AndAlso Not File.Exists(ProjectDirectory + "\res\image\2.png") Then
            If TypeOf ScreenshotImage2PictureBox.Tag Is String Then
                Dim ScreenshotImageBitmap As Bitmap = Utils.GetResizedBitmap(ScreenshotImage2PictureBox.Tag.ToString, 640, 350)
                If ScreenshotImageBitmap IsNot Nothing Then
                    If ScreenshotImageBitmap.PixelFormat <> Imaging.PixelFormat.Format32bppArgb Then
                        Utils.ConvertTo32bppAndDisposeOriginal(ScreenshotImageBitmap)
                    End If

                    Try
                        Using CompressedImage = Quantizer.QuantizeImage(ScreenshotImageBitmap)
                            CompressedImage?.Save(ProjectDirectory + "\res\image\2.png", Imaging.ImageFormat.Png)
                        End Using
                    Catch ex As Exception
                        MsgBox("Could not compress PNG." + vbCrLf + ex.Message, MsgBoxStyle.Exclamation)
                    Finally
                        ScreenshotImageBitmap.Dispose()
                    End Try
                Else
                    MsgBox("Could not resize the selected screenshot 2. Please save it manually in the project folder:" + vbCrLf +
                               """\res\image\2.png"" 640 x 350", MsgBoxStyle.Exclamation, "Screenshot 2 Warning")
                End If
            End If
        End If

        'Write info.sys to res directory
        Using SYSWriter As New StreamWriter(ProjectDirectory + "\res\info.sys", False)
            SYSWriter.WriteLine("title = " + GameTitleTextBox.Text)
            SYSWriter.WriteLine("title_id = " + GameIDTextBox.Text)

            If ShowGameIDCheckBox.IsChecked Then
                SYSWriter.WriteLine("title_sub_id = 1")
            Else
                SYSWriter.WriteLine("title_sub_id = 0")
            End If

            SYSWriter.WriteLine("release_date = " + GameReleaseDateTextBox.Text)
            SYSWriter.WriteLine("developer_id = " + GameDeveloperTextBox.Text)
            SYSWriter.WriteLine("publisher_id = " + GamePublisherTextBox.Text)
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
            SYSWriter.WriteLine("area = " + GameRegionTextBox.Text)
            SYSWriter.WriteLine("violence_flag = 0")
            SYSWriter.WriteLine("content_type = 255")
            SYSWriter.WriteLine("content_subtype = 0")
        End Using

        'Create man.xml
        Using MANWriter As New StreamWriter(ProjectDirectory + "\res\man.xml", False)
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

        If MsgBox("Game resources saved! Close this window ?", MsgBoxStyle.YesNo, "Saved") = MsgBoxResult.Yes Then
            Close()
        End If
    End Sub

End Class
