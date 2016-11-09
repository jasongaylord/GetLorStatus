﻿Public Class MainForm
    Private Declare Function FindWindowEx Lib "user32" Alias "FindWindowExA" (ByVal hWnd1 As Integer, ByVal hWnd2 As Integer, ByVal lpsz1 As String, ByVal lpsz2 As String) As Integer
    Private Declare Ansi Function SendMessage Lib "user32.dll" Alias "SendMessageA" (ByVal hwnd As Integer, ByVal wMsg As Integer, ByVal wParam As Integer, ByVal lParam As String) As Integer
    Private Const WM_GETTEXT As Short = &HDS
    Private Const WM_GETTEXTLENGTH As Short = &HES


    Private Sub TestGetStatus_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load
        If EventLog.SourceExists("MyLightDisplay") = False Then
            EventLog.CreateEventSource("MyLightDisplay", "Application")
        End If

        ProcessLorStatusLog()
        Timer1.Start()
    End Sub

    Private Sub Timer1_Tick(sender As System.Object, e As System.EventArgs) Handles Timer1.Tick
        Timer1.Stop()
        ProcessLorStatusLog()
        Timer1.Start()
    End Sub

    Private Sub ProcessLorStatusLog()
        'Declare variables
        Dim tBuff As String
        Dim historyLines As String()
        Dim x As Integer

        Try
            'Get LOR Handle
            Dim hLor As Integer = FindWindowEx(0, 0, "ThunderRT6FormDC", "Light-O-Rama Status v3.1.4")

            'Get the Rich Text Box handle
            Dim hTb As Integer = FindWindowEx(hLor, 0, "RichTextWndClass", vbNullString)

            'Get the contents of the Rich Text
            Dim tLength As Long = SendMessage(hTb, WM_GETTEXTLENGTH, CInt(0), CInt(0)) + 1
            tBuff = Space(tLength)
            Dim tValue As Long = SendMessage(hTb, WM_GETTEXT, tLength, tBuff)
        Catch ex As Exception
            EventLog.WriteEntry("MyLightDisplay", "MainForm.ProcessLorStatusLog - Win32 Interop:" & vbCrLf & ex.Message, EventLogEntryType.Error)
            Timer1.Interval = 180000
            StatusLabel.Text = "MainForm.ProcessLorStatusLog - Win32 Interop:" & vbCrLf & ex.Message
            Exit Sub
        End Try

        Try
            'Split based on the return
            historyLines = tBuff.Split(ControlChars.CrLf)

            'Get the maximum number of lines to search
            x = historyLines.Length - 10
            If x < 0 Then
                x = 0
            End If
        Catch ex As Exception
            EventLog.WriteEntry("MyLightDisplay", "MainForm.ProcessLorStatusLog - History:" & vbCrLf & ex.Message, EventLogEntryType.Error)
            Timer1.Interval = 180000
            StatusLabel.Text = "MainForm.ProcessLorStatusLog - History:" & vbCrLf & ex.Message
            Exit Sub
        End Try
        
        Dim songTitle As String = ""
        Dim timeStarted As DateTime

        'Enumerate the history lines until we find the text "Starting Musical within the line"
        Dim y = historyLines.Length - 1
        While y >= x
            If historyLines(y).Contains("Starting Musical:") Then
                Dim lineDetails As String() = historyLines(y).Split(": ")
                timeStarted = DateTime.Parse(lineDetails(0) & ":" & lineDetails(1) & ":" & lineDetails(2))
                Dim sequenceParts As String() = lineDetails(5).Trim.Split("\")
                songTitle = sequenceParts(sequenceParts.Length - 1).Replace(".lms", "").Trim
                Exit While
            End If
            y = y - 1
        End While

        'If there is no song, an error occurred
        If songTitle = "" Then
            EventLog.WriteEntry("MyLightDisplay", "MainForm.ProcessLorStatusLog - No Song In Last 10 Lines", EventLogEntryType.Error)
            Timer1.Interval = 180000
            StatusLabel.Text = "MainForm.ProcessLorStatusLog - No Song In Last 10 Lines"
            Exit Sub
        End If

        'Grab MP3 properties
        Dim music As New Mp3
        Dim musicprops As MusicProperties = music.GetProperties(songTitle)

        'Display data
        StatusLabel.Text = "Song: " & musicprops.Title & vbCrLf & "Album: " & musicprops.Album & vbCrLf & "Artists: " & musicprops.Artist & vbCrLf & "Year: " & musicprops.Year.ToString() & vbCrLf & "Length: " & musicprops.Length.Minutes.ToString() & ":" & musicprops.Length.Seconds.ToString("00") & vbCrLf & "Started Playing At: " & timeStarted.ToLongTimeString()
        Dim interval As Integer = CInt(musicprops.Length.TotalMilliseconds - DateTime.Now.Subtract(timeStarted).TotalMilliseconds) + 2000
        If interval < 200 Then
            EventLog.WriteEntry("MyLightDisplay", "The song, " & musicprops.Title & ", has an interval of " & interval & " when playing at " & timeStarted & " and currently at " & DateTime.Now.ToLongTimeString())
            interval = 2000
        End If
        Timer1.Interval = interval
        StatusLabel.Text &= vbCrLf & "Interval in Milliseconds: " & interval & vbCrLf & "Next Polling Time Is: " & DateTime.Now.AddMilliseconds(Timer1.Interval).ToLongTimeString()

        If musicprops.Artist = Nothing Then
            musicprops.Artist = ""
        End If

        If musicprops.Album = Nothing Then
            musicprops.Artist = ""
        End If

        'Push Data
        Dim context As New orchard_mylightdisplayEntities
        Dim log As New MusicLog
        log.Artists = musicprops.Artist
        log.DateStarted = timeStarted
        log.Length = musicprops.Length.Minutes.ToString() & ":" & musicprops.Length.Seconds.ToString("00")
        log.Song = musicprops.Title
        log.Album = musicprops.Album
        log.Year = musicprops.Year
        log.SequenceType = musicprops.SequenceType
        context.AddToMusicLogs(log)
        context.SaveChanges()
    End Sub

    Private Sub Button1_Click(sender As System.Object, e As System.EventArgs) Handles Button1.Click
        Timer1.Stop()
        Timer1.Start()
    End Sub
End Class