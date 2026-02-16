Imports MyAnimeScreen.App.Models

Namespace Services.Api
    Public Interface IAnimeApiClient
        Function SearchAsync(title As String, Optional maxRows As Integer = 25) As Task(Of IReadOnlyList(Of Anime))
        Function GetByMalIdAsync(malId As Integer) As Task(Of Anime)
    End Interface
End Namespace
