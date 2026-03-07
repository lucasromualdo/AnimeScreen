Imports System.Collections.Generic
Imports System.Net
Imports System.Net.Http
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports MyAnimeScreen.App.Models

Namespace Services.Api
    Public Class JikanApiClient
        Implements IAnimeApiClient

        Private Const BaseUrl As String = "https://api.jikan.moe/v4/"
        Private Const MaxRequestAttempts As Integer = 4
        Private Shared ReadOnly _jsonOptions As New JsonSerializerOptions With {
            .PropertyNameCaseInsensitive = True
        }
        Private ReadOnly _httpClient As HttpClient
        Private ReadOnly _delayAsync As Func(Of TimeSpan, Task)

        Public Sub New()
            Me.New(CreateHttpClient(), Function(delay) Task.Delay(delay))
        End Sub

        Public Sub New(httpClient As HttpClient, Optional delayAsync As Func(Of TimeSpan, Task) = Nothing)
            If httpClient Is Nothing Then
                Throw New ArgumentNullException(NameOf(httpClient))
            End If

            Dim defaultDelay As Func(Of TimeSpan, Task) = Function(delay) Task.Delay(delay)
            _httpClient = httpClient
            _delayAsync = If(delayAsync, defaultDelay)
        End Sub

        Public Async Function SearchAsync(title As String, Optional page As Integer = 1, Optional maxRows As Integer = 25) As Task(Of AnimeSearchResult) Implements IAnimeApiClient.SearchAsync
            If String.IsNullOrWhiteSpace(title) Then
                Return New AnimeSearchResult()
            End If

            Dim sanitizedPage = Math.Max(1, page)
            Dim sanitizedRows = Math.Max(1, Math.Min(maxRows, 25))
            Dim query = $"anime?q={Uri.EscapeDataString(title.Trim())}&page={sanitizedPage}&limit={sanitizedRows}"

            Dim payload = Await GetJsonAsync(Of JikanAnimeListResponse)(query).ConfigureAwait(False)
            If payload Is Nothing OrElse payload.Data Is Nothing Then
                Return New AnimeSearchResult With {
                    .Page = sanitizedPage,
                    .HasMore = False,
                    .Items = Array.Empty(Of Anime)()
                }
            End If

            Dim result = New List(Of Anime)
            For Each item In payload.Data
                result.Add(MapToAnime(item))
            Next

            Return New AnimeSearchResult With {
                .Page = sanitizedPage,
                .HasMore = payload.Pagination IsNot Nothing AndAlso payload.Pagination.HasNextPage,
                .Items = result
            }
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

        Private Async Function GetJsonAsync(Of T)(relativePath As String) As Task(Of T)
            For attempt = 1 To MaxRequestAttempts
                Dim retryDelay As TimeSpan? = Nothing
                Dim networkError As HttpRequestException = Nothing

                Try
                    Using response = Await _httpClient.GetAsync(relativePath).ConfigureAwait(False)
                        If response.IsSuccessStatusCode Then
                            Using stream = Await response.Content.ReadAsStreamAsync().ConfigureAwait(False)
                                Dim payload = Await JsonSerializer.DeserializeAsync(Of T)(
                                    stream,
                                    _jsonOptions
                                ).ConfigureAwait(False)

                                Return payload
                            End Using
                        End If

                        Dim statusCode = response.StatusCode
                        Dim reasonPhrase = NormalizeReasonPhrase(response.ReasonPhrase)

                        If IsRetriableStatusCode(statusCode) Then
                            If attempt < MaxRequestAttempts Then
                                retryDelay = GetRetryDelay(response, attempt)
                            Else
                                Throw New HttpRequestException(
                                    BuildTransientStatusErrorMessage(statusCode, reasonPhrase, attempt),
                                    Nothing,
                                    statusCode
                                )
                            End If
                        Else
                            Throw New HttpRequestException(
                                BuildDefinitiveStatusErrorMessage(statusCode, reasonPhrase),
                                Nothing,
                                statusCode
                            )
                        End If
                    End Using
                Catch ex As HttpRequestException When Not ex.StatusCode.HasValue
                    If attempt < MaxRequestAttempts Then
                        retryDelay = GetBackoffDelay(attempt)
                    Else
                        networkError = ex
                    End If
                Catch ex As TaskCanceledException When attempt < MaxRequestAttempts
                    retryDelay = GetBackoffDelay(attempt)
                Catch ex As TaskCanceledException
                    Throw New HttpRequestException(
                        $"Falha temporaria ao consultar Jikan: tempo limite apos {attempt.ToString()} tentativa(s).",
                        ex
                    )
                End Try

                If retryDelay.HasValue Then
                    Await _delayAsync(retryDelay.Value).ConfigureAwait(False)
                    Continue For
                End If

                If networkError IsNot Nothing Then
                    Throw New HttpRequestException(
                        $"Falha de rede ao consultar Jikan apos {attempt.ToString()} tentativa(s): {networkError.Message}",
                        networkError
                    )
                End If
            Next

            Throw New HttpRequestException("Falha temporaria ao consultar Jikan apos multiplas tentativas.")
        End Function

        Private Shared Function IsRetriableStatusCode(statusCode As HttpStatusCode) As Boolean
            Return statusCode = HttpStatusCode.TooManyRequests OrElse CInt(statusCode) >= 500
        End Function

        Private Shared Function GetRetryDelay(response As HttpResponseMessage, attempt As Integer) As TimeSpan
            Dim retryAfter = response.Headers.RetryAfter
            If retryAfter IsNot Nothing Then
                If retryAfter.Delta.HasValue Then
                    Return ClampRetryDelay(retryAfter.Delta.Value)
                End If

                If retryAfter.Date.HasValue Then
                    Dim waitTime = retryAfter.Date.Value - DateTimeOffset.UtcNow
                    If waitTime > TimeSpan.Zero Then
                        Return ClampRetryDelay(waitTime)
                    End If
                End If
            End If

            Return GetBackoffDelay(attempt)
        End Function

        Private Shared Function GetBackoffDelay(attempt As Integer) As TimeSpan
            Dim exponent = Math.Max(0, attempt - 1)
            Dim milliseconds = Math.Pow(2, exponent) * 500
            Return ClampRetryDelay(TimeSpan.FromMilliseconds(milliseconds))
        End Function

        Private Shared Function ClampRetryDelay(delay As TimeSpan) As TimeSpan
            Dim minDelay = TimeSpan.FromMilliseconds(200)
            Dim maxDelay = TimeSpan.FromSeconds(8)

            If delay < minDelay Then
                Return minDelay
            End If

            If delay > maxDelay Then
                Return maxDelay
            End If

            Return delay
        End Function

        Private Shared Function BuildTransientStatusErrorMessage(statusCode As HttpStatusCode, reasonPhrase As String, attempt As Integer) As String
            Return $"Falha temporaria na API Jikan apos {attempt.ToString()} tentativa(s): {(CInt(statusCode)).ToString()} ({reasonPhrase})."
        End Function

        Private Shared Function BuildDefinitiveStatusErrorMessage(statusCode As HttpStatusCode, reasonPhrase As String) As String
            Return $"Falha definitiva na API Jikan: {(CInt(statusCode)).ToString()} ({reasonPhrase})."
        End Function

        Private Shared Function NormalizeReasonPhrase(reasonPhrase As String) As String
            If String.IsNullOrWhiteSpace(reasonPhrase) Then
                Return "sem descricao"
            End If

            Return reasonPhrase.Trim()
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
                .Season = source.Season,
                .Genres = MapGenres(source.Genres)
            }
        End Function

        Private Shared Function MapGenres(source As IReadOnlyList(Of JikanGenreData)) As IReadOnlyList(Of Genre)
            If source Is Nothing OrElse source.Count = 0 Then
                Return Array.Empty(Of Genre)()
            End If

            Dim seenNames = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim mapped = New List(Of Genre)

            For Each item In source
                If item Is Nothing Then
                    Continue For
                End If

                Dim name = NormalizeGenreName(item.Name)
                If String.IsNullOrWhiteSpace(name) Then
                    Continue For
                End If

                If seenNames.Add(name) Then
                    mapped.Add(New Genre With {.Name = name})
                End If
            Next

            Return mapped
        End Function

        Private Shared Function NormalizeGenreName(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then
                Return String.Empty
            End If

            Return value.Trim()
        End Function

        Private Class JikanAnimeListResponse
            <JsonPropertyName("data")>
            Public Property Data As List(Of JikanAnimeData)

            <JsonPropertyName("pagination")>
            Public Property Pagination As JikanPaginationData
        End Class

        Private Class JikanPaginationData
            <JsonPropertyName("has_next_page")>
            Public Property HasNextPage As Boolean
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

            <JsonPropertyName("genres")>
            Public Property Genres As List(Of JikanGenreData)
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

        Private Class JikanGenreData
            <JsonPropertyName("name")>
            Public Property Name As String
        End Class
    End Class
End Namespace
