Imports MyAnimeScreen.App.Models

Namespace Services.Api
    Public Interface IAnimeApiClient
        Function SearchAsync(title As String, Optional page As Integer = 1, Optional maxRows As Integer = 25) As Task(Of AnimeSearchResult)
        Function GetByMalIdAsync(malId As Integer) As Task(Of Anime)
    End Interface
End Namespace
