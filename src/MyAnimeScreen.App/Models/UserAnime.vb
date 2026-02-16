Namespace Models
    Public Class UserAnime
        Public Property Id As Long
        Public Property AnimeId As Long
        Public Property Status As AnimeStatus
        Public Property CurrentEpisode As Integer
        Public Property PersonalScore As Double?
        Public Property Notes As String
        Public Property IsFavorite As Boolean
        Public Property StartedAt As DateTime?
        Public Property FinishedAt As DateTime?
        Public Property UpdatedAt As DateTime?
    End Class
End Namespace
