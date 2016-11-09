Imports System.Configuration

Public Class Mp3

    Public Function GetProperties(Song As String) As MusicProperties
        Dim props As New MusicProperties

        Try
            props.Length = New TimeSpan(0)

            If Song.Contains(" - Animated") Then
                props.SequenceType = 2
                Song = Song.Replace(" - Animated", "")
            ElseIf Song.Contains(" - Offline") Then
                props.SequenceType = 0
                Song = Song.Replace(" - Offline", "")
            ElseIf Song.Trim.Length > 2 Then
                props.SequenceType = 1
            Else
                props.SequenceType = 0
            End If

            Dim filePath As String = ConfigurationManager.AppSettings("MusicFolder") & Song & ".mp3"
            Dim file As TagLib.File = TagLib.File.Create(filePath)

            Using file
                props.Artist = file.Tag.JoinedAlbumArtists
                If file.Tag.Title = "" Then
                    props.Title = Song
                Else
                    props.Title = file.Tag.Title
                End If
                props.Album = file.Tag.Album
                props.Year = file.Tag.Year
                props.Length = New TimeSpan(file.Properties.Duration.Ticks)
            End Using
        Catch ex As Exception
            EventLog.WriteEntry("MyLightDisplay", "Mp3.GetProperties Exception:" & vbCrLf & ex.Message)
        End Try

        Return props
    End Function
End Class

Public Class MusicProperties
    Public Property Title As String
    Public Property Artist As String
    Public Property Album As String
    Public Property Year As Integer
    Public Property Length As TimeSpan
    Public Property SequenceType As Integer
End Class