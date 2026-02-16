Imports AnimeScreen.App.Models

Namespace Services.Api
    Public Class JikanApiClient
        Implements IAnimeApiClient

        Public Function SearchAsync(title As String, Optional maxRows As Integer = 25) As Task(Of IReadOnlyList(Of Anime)) Implements IAnimeApiClient.SearchAsync
            Throw New NotImplementedException("Integração da API será implementada na próxima etapa.")
        End Function

        Public Function GetByMalIdAsync(malId As Integer) As Task(Of Anime) Implements IAnimeApiClient.GetByMalIdAsync
            Throw New NotImplementedException("Integração da API será implementada na próxima etapa.")
        End Function
    End Class
End Namespace
