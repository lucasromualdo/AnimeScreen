Imports System.Net
Imports System.Net.Http
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports MyAnimeScreen.App.Models

Namespace Services.Api
    Public Class JikanApiClient
        Implements IAnimeApiClient

        Private Const BaseUrl As String = "https://api.jikan.moe/v4/"
        Private Shared ReadOnly _jsonOptions As New JsonSerializerOptions With {
            .PropertyNameCaseInsensitive = True
        }
        Private Shared ReadOnly _httpClient As HttpClient = CreateHttpClient()

        Public Async Function SearchAsync(title As String, Optional maxRows As Integer = 25) As Task(Of IReadOnlyList(Of Anime)) Implements IAnimeApiClient.SearchAsync
            If String.IsNullOrWhiteSpace(title) Then
                Return Array.Empty(Of Anime)()
            End If

            Dim sanitizedRows = Math.Max(1, Math.Min(maxRows, 25))
            Dim query = $"anime?q={Uri.EscapeDataString(title.Trim())}&limit={sanitizedRows}"

            Dim payload = Await GetJsonAsync(Of JikanAnimeListResponse)(query).ConfigureAwait(False)
            If payload Is Nothing OrElse payload.Data Is Nothing Then
                Return Array.Empty(Of Anime)()
            End If

            Dim result = New List(Of Anime)
            For Each item In payload.Data
                result.Add(MapToAnime(item))
            Next

            Return result
        End Function

        Public Async Function GetByMalIdAsync(malId As Integer) As Task(Of Anime) Implements IAnimeApiClient.GetByMalIdAsync
            If malId <= 0 Then
                Throw New ArgumentOutOfRangeException(NameOf(malId), "malId deve ser maior que zero.")
            End If

            Try
                Dim payload = Await GetJsonAsync(Of JikanAnimeSingleResponse)($"anime/{malId}").ConfigureAwait(False)
                If payload Is Nothing OrElse payload.Data Is Nothing Then
                    Throw New InvalidOperationException($"Anime mal_id {malId} não encontrado.")
                End If

                Return MapToAnime(payload.Data)
            Catch ex As HttpRequestException When ex.StatusCode = HttpStatusCode.NotFound
                Throw New InvalidOperationException($"Anime mal_id {malId} não encontrado.", ex)
            End Try
        End Function

        Private Shared Function CreateHttpClient() As HttpClient
            Dim client = New HttpClient With {
                .BaseAddress = New Uri(BaseUrl),
                .Timeout = TimeSpan.FromSeconds(30)
            }

            client.DefaultRequestHeaders.UserAgent.ParseAdd("MyAnimeScreen/1.0")
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json")
            Return client
        End Function

        Private Shared Async Function GetJsonAsync(Of T)(relativePath As String) As Task(Of T)
            Using response = Await _httpClient.GetAsync(relativePath).ConfigureAwait(False)
                If Not response.IsSuccessStatusCode Then
                    Throw New HttpRequestException(
                        $"Jikan retornou {(CInt(response.StatusCode)).ToString()} ({response.ReasonPhrase}).",
                        Nothing,
                        response.StatusCode
                    )
                End If

                Using stream = Await response.Content.ReadAsStreamAsync().ConfigureAwait(False)
                    Dim payload = Await JsonSerializer.DeserializeAsync(Of T)(
                        stream,
                        _jsonOptions
                    ).ConfigureAwait(False)

                    Return payload
                End Using
            End Using
        End Function

        Private Shared Function MapToAnime(source As JikanAnimeData) As Anime
            Dim imageUrl As String = Nothing
            If source.Images IsNot Nothing Then
                If source.Images.Webp IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(source.Images.Webp.ImageUrl) Then
                    imageUrl = source.Images.Webp.ImageUrl
                ElseIf source.Images.Jpg IsNot Nothing Then
                    imageUrl = source.Images.Jpg.ImageUrl
                End If
            End If

            Return New Anime With {
                .MalId = source.MalId,
                .Title = If(source.Title, String.Empty),
                .TitleJp = source.TitleJapanese,
                .Synopsis = source.Synopsis,
                .ImageUrl = imageUrl,
                .EpisodesTotal = source.Episodes,
                .Score = source.Score,
                .Year = source.Year,
                .Season = source.Season
            }
        End Function

        Private Class JikanAnimeListResponse
            <JsonPropertyName("data")>
            Public Property Data As List(Of JikanAnimeData)
        End Class

        Private Class JikanAnimeSingleResponse
            <JsonPropertyName("data")>
            Public Property Data As JikanAnimeData
        End Class

        Private Class JikanAnimeData
            <JsonPropertyName("mal_id")>
            Public Property MalId As Integer

            <JsonPropertyName("title")>
            Public Property Title As String

            <JsonPropertyName("title_japanese")>
            Public Property TitleJapanese As String

            <JsonPropertyName("synopsis")>
            Public Property Synopsis As String

            <JsonPropertyName("episodes")>
            Public Property Episodes As Integer?

            <JsonPropertyName("score")>
            Public Property Score As Double?

            <JsonPropertyName("year")>
            Public Property Year As Integer?

            <JsonPropertyName("season")>
            Public Property Season As String

            <JsonPropertyName("images")>
            Public Property Images As JikanImages
        End Class

        Private Class JikanImages
            <JsonPropertyName("jpg")>
            Public Property Jpg As JikanImageSet

            <JsonPropertyName("webp")>
            Public Property Webp As JikanImageSet
        End Class

        Private Class JikanImageSet
            <JsonPropertyName("image_url")>
            Public Property ImageUrl As String
        End Class
    End Class
End Namespace
