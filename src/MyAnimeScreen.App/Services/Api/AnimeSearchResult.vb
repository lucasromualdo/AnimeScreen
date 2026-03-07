Imports MyAnimeScreen.App.Models

Namespace Services.Api
    Public Class AnimeSearchResult
        Public Property Items As IReadOnlyList(Of Anime) = Array.Empty(Of Anime)()
        Public Property Page As Integer = 1
        Public Property HasMore As Boolean
    End Class
End Namespace
