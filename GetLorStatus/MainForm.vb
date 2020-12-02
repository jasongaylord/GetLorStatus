Imports System.Configuration

Public Class MainForm
    Private Declare Function FindWindowEx Lib "user32" Alias "FindWindowExA" (ByVal hWnd1 As Integer, ByVal hWnd2 As Integer, ByVal lpsz1 As String, ByVal lpsz2 As String) As Integer
    Private Declare Ansi Function SendMessage Lib "user32.dll" Alias "SendMessageA" (ByVal hwnd As Integer, ByVal wMsg As Integer, ByVal wParam As Integer, ByVal lParam As String) As Integer
    Private Const WM_GETTEXT As Short = &HDS
    Private Const WM_GETTEXTLENGTH As Short = &HES

    Private LORVersionString As String = ""

    Private Sub TestGetStatus_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load
        If EventLog.SourceExists(ConfigurationManager.AppSettings("LogName")) = False Then
            EventLog.CreateEventSource(ConfigurationManager.AppSettings("LogName"), "Application")
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
            If String.IsNullOrWhiteSpace(LORVersionString) Then
                LORVersionString = ConfigurationManager.AppSettings("LORVersion")
            End If

            'Get LOR Handle
            Dim hLor As Integer = FindWindowEx(0, 0, "ThunderRT6FormDC", LORVersionString)

            'Get the Picture Box Control
            Dim hPic As Integer = FindWindowEx(hLor, 0, "ThunderRT6PictureBoxDC", vbNullString)

            'Get Tab
            Dim hTab As Integer = FindWindowEx(hPic, 0, "ThunderRT6UserControl", vbNullString)

            'Get the Rich Text Box handle
            Dim hEditor As Integer = FindWindowEx(hTab, 0, "RICHEDIT50W", vbNullString)

            'Get the contents of the Rich Text
            Dim tLength As Long = SendMessage(hEditor, WM_GETTEXTLENGTH, CInt(0), CInt(0)) + 1
            tBuff = Space(tLength)
            Dim tValue As Long = SendMessage(hEditor, WM_GETTEXT, tLength, tBuff)
        Catch ex As Exception
            EventLog.WriteEntry(ConfigurationManager.AppSettings("LogName"), "MainForm.ProcessLorStatusLog - Win32 Interop:" & vbCrLf & ex.Message, EventLogEntryType.Error)
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
            EventLog.WriteEntry(ConfigurationManager.AppSettings("LogName"), "MainForm.ProcessLorStatusLog - History:" & vbCrLf & ex.Message, EventLogEntryType.Error)
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
                songTitle = sequenceParts(sequenceParts.Length - 1).Replace(".lms.lcs", "").Replace(".lms", "").Replace(".play", "").Trim
                Exit While
            End If
            y = y - 1
        End While

        'If there is no song, an error occurred
        If songTitle = "" Then
            EventLog.WriteEntry(ConfigurationManager.AppSettings("LogName"), "MainForm.ProcessLorStatusLog - No Song In Last 10 Lines", EventLogEntryType.Error)
            Timer1.Interval = 180000
            StatusLabel.Text = "MainForm.ProcessLorStatusLog - No Song In Last 10 Lines"
            Exit Sub
        End If

        'Grab MP3 properties
        Dim music As New Mp3
        Dim musicprops As MusicProperties = music.GetProperties(songTitle)

        ''If the MP3 cannot be found, set default values
        'If String.IsNullOrWhiteSpace(musicprops.Title) Then
        '    musicprops.Title = "Unknown"
        '    musicprops.Album = "Unknown"
        '    musicprops.Artist = "Unknown"
        '    musicprops.Length = TimeSpan.FromSeconds(3)
        '    musicprops.SequenceType = 0
        '    musicprops.Year = 1900
        'End If

        'Display data
        StatusLabel.Text = "Song: " & musicprops.Title & vbCrLf & "Album: " & musicprops.Album & vbCrLf & "Artists: " & musicprops.Artist & vbCrLf & "Year: " & musicprops.Year.ToString() & vbCrLf & "Length: " & musicprops.Length.Minutes.ToString() & ":" & musicprops.Length.Seconds.ToString("00") & vbCrLf & "Started Playing At: " & timeStarted.ToLongTimeString()
        Dim interval As Integer = CInt(musicprops.Length.TotalMilliseconds - DateTime.Now.Subtract(timeStarted).TotalMilliseconds) + 2000
        If interval < 200 Then
            EventLog.WriteEntry(ConfigurationManager.AppSettings("LogName"), "The song, " & musicprops.Title & ", has an interval of " & interval & " when playing at " & timeStarted & " and currently at " & DateTime.Now.ToLongTimeString())
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
        Try
            Dim context As New mylightdisplayEntities
            Dim log As New MusicLogEntry
            log.Artists = musicprops.Artist
            log.DateStarted = timeStarted
            log.Length = musicprops.Length.Minutes.ToString() & ":" & musicprops.Length.Seconds.ToString("00")
            log.Song = musicprops.Title
            log.Album = musicprops.Album
            log.Year = musicprops.Year
            log.SequenceType = musicprops.SequenceType

            If (log.Song.Length > 0) Then
                context.MusicLogEntries.Add(log)
                context.SaveChanges()
            End If
        Catch ex As Exception
            StatusLabel.Text = "Could not save database changes. (" & ex.Message & ")"
        End Try
    End Sub

    Private Sub Button1_Click(sender As System.Object, e As System.EventArgs) Handles Button1.Click
        Timer1.Stop()
        Timer1.Start()
    End Sub
End Class