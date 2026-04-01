Imports System.Collections.Generic
Imports System.Data
Imports System.Diagnostics
Imports System.Linq
Imports Dapper
Imports MyAnimeScreen.App.Models

Namespace Services.Data
    Public Class AnimeRepository
        Private ReadOnly _connectionFactory As DbConnectionFactory

        Public Sub New(connectionFactory As DbConnectionFactory)
            _connectionFactory = connectionFactory
        End Sub

        Public Async Function UpsertAsync(anime As Anime) As Task(Of Long)
            ValidateAnime(anime)

            Const sql As String =
"INSERT INTO animes (
    mal_id,
    title,
    title_jp,
    synopsis,
    image_url,
    episodes_total,
    score,
    year,
    season,
    updated_at
) VALUES (
    @MalId,
    @Title,
    @TitleJp,
    @Synopsis,
    @ImageUrl,
    @EpisodesTotal,
    @Score,
    @Year,
    @Season,
    datetime('now')
)
ON CONFLICT(mal_id) DO UPDATE SET
    title = excluded.title,
    title_jp = excluded.title_jp,
    synopsis = excluded.synopsis,
    image_url = excluded.image_url,
    episodes_total = excluded.episodes_total,
    score = excluded.score,
    year = excluded.year,
    season = excluded.season,
    updated_at = datetime('now')
RETURNING id;"

            Using connection = Await _connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using transaction = connection.BeginTransaction()
                    Try
                        Dim animeId = Await connection.ExecuteScalarAsync(Of Long)(sql, anime, transaction).ConfigureAwait(False)
                        Await ReplaceGenresForAnimeAsync(connection, transaction, animeId, anime.Genres).ConfigureAwait(False)
                        transaction.Commit()
                        Return animeId
                    Catch ex As Exception
                        Try
                            transaction.Rollback()
                        Catch rollbackEx As Exception
                            Trace.TraceError($"AnimeRepository.UpsertAsync rollback failed for MalId={anime.MalId}: {rollbackEx}")
                            Throw
                        End Try

                        Trace.TraceError($"AnimeRepository.UpsertAsync failed for MalId={anime.MalId}: {ex}")
                        Throw
                    End Try
                End Using
            End Using
        End Function

        Public Async Function GetByMalIdAsync(malId As Integer) As Task(Of Anime)
            Const sql As String =
"SELECT
    id AS Id,
    mal_id AS MalId,
    title AS Title,
    title_jp AS TitleJp,
    synopsis AS Synopsis,
    image_url AS ImageUrl,
    episodes_total AS EpisodesTotal,
    score AS Score,
    year AS Year,
    season AS Season,
    created_at AS CreatedAt,
    updated_at AS UpdatedAt
FROM animes
WHERE mal_id = @MalId;"

            Using connection = Await _connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(False)
                Dim anime = Await connection.QuerySingleOrDefaultAsync(Of Anime)(
                    sql,
                    New With {.MalId = malId}
                ).ConfigureAwait(False)

                If anime Is Nothing Then
                    Return Nothing
                End If

                Dim genresByAnimeId = Await LoadGenresForAnimeIdsAsync(
                    connection,
                    New List(Of Long) From {anime.Id}
                ).ConfigureAwait(False)

                anime.Genres = GetGenresOrEmpty(genresByAnimeId, anime.Id)
                Return anime
            End Using
        End Function

        Public Async Function GetByIdAsync(id As Long) As Task(Of Anime)
            Const sql As String =
"SELECT
    id AS Id,
    mal_id AS MalId,
    title AS Title,
    title_jp AS TitleJp,
    synopsis AS Synopsis,
    image_url AS ImageUrl,
    episodes_total AS EpisodesTotal,
    score AS Score,
    year AS Year,
    season AS Season,
    created_at AS CreatedAt,
    updated_at AS UpdatedAt
FROM animes
WHERE id = @Id;"

            Using connection = Await _connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(False)
                Dim anime = Await connection.QuerySingleOrDefaultAsync(Of Anime)(
                    sql,
                    New With {.Id = id}
                ).ConfigureAwait(False)

                If anime Is Nothing Then
                    Return Nothing
                End If

                Dim genresByAnimeId = Await LoadGenresForAnimeIdsAsync(
                    connection,
                    New List(Of Long) From {anime.Id}
                ).ConfigureAwait(False)

                anime.Genres = GetGenresOrEmpty(genresByAnimeId, anime.Id)
                Return anime
            End Using
        End Function

        Public Async Function SearchByTitleAsync(title As String, Optional maxRows As Integer = 50) As Task(Of IReadOnlyList(Of Anime))
            Dim sanitizedRows = Math.Max(1, Math.Min(maxRows, 200))

            Const sql As String =
"SELECT
    id AS Id,
    mal_id AS MalId,
    title AS Title,
    title_jp AS TitleJp,
    synopsis AS Synopsis,
    image_url AS ImageUrl,
    episodes_total AS EpisodesTotal,
    score AS Score,
    year AS Year,
    season AS Season,
    created_at AS CreatedAt,
    updated_at AS UpdatedAt
FROM animes
WHERE title LIKE '%' || @Title || '%'
ORDER BY title
LIMIT @MaxRows;"

            Using connection = Await _connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(False)
                Dim rows = Await connection.QueryAsync(Of Anime)(
                    sql,
                    New With {
                        .Title = title,
                        .MaxRows = sanitizedRows
                    }
                ).ConfigureAwait(False)

                Dim result = rows.AsList()
                If result.Count = 0 Then
                    Return result
                End If

                Dim animeIds = result.
                    Select(Function(item) item.Id).
                    Distinct().
                    ToList()

                Dim genresByAnimeId = Await LoadGenresForAnimeIdsAsync(connection, animeIds).ConfigureAwait(False)
                For Each item In result
                    item.Genres = GetGenresOrEmpty(genresByAnimeId, item.Id)
                Next

                Return result
            End Using
        End Function

        Private Shared Async Function ReplaceGenresForAnimeAsync(
            connection As IDbConnection,
            transaction As IDbTransaction,
            animeId As Long,
            genres As IReadOnlyList(Of Genre)
        ) As Task
            Const deleteSql As String =
"DELETE FROM anime_genres
WHERE anime_id = @AnimeId;"

            Await connection.ExecuteAsync(
                deleteSql,
                New With {.AnimeId = animeId},
                transaction
            ).ConfigureAwait(False)

            Dim normalizedNames = NormalizeGenreNames(genres)
            If normalizedNames.Count = 0 Then
                Return
            End If

            Const upsertGenreSql As String =
"INSERT INTO genres (name)
VALUES (@Name)
ON CONFLICT(name) DO NOTHING;"

            Const linkGenreSql As String =
"INSERT INTO anime_genres (anime_id, genre_id)
SELECT @AnimeId, id
FROM genres
WHERE name = @Name
ON CONFLICT(anime_id, genre_id) DO NOTHING;"

            For Each genreName In normalizedNames
                Await connection.ExecuteAsync(
                    upsertGenreSql,
                    New With {.Name = genreName},
                    transaction
                ).ConfigureAwait(False)

                Await connection.ExecuteAsync(
                    linkGenreSql,
                    New With {
                        .AnimeId = animeId,
                        .Name = genreName
                    },
                    transaction
                ).ConfigureAwait(False)
            Next
        End Function

        Private Shared Function NormalizeGenreNames(genres As IReadOnlyList(Of Genre)) As IReadOnlyList(Of String)
            If genres Is Nothing OrElse genres.Count = 0 Then
                Return Array.Empty(Of String)()
            End If

            Dim seen = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim names = New List(Of String)

            For Each genre In genres
                If genre Is Nothing Then
                    Continue For
                End If

                Dim name = NormalizeGenreName(genre.Name)
                If String.IsNullOrWhiteSpace(name) Then
                    Continue For
                End If

                If seen.Add(name) Then
                    names.Add(name)
                End If
            Next

            Return names
        End Function

        Private Shared Function NormalizeGenreName(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then
                Return String.Empty
            End If

            Return value.Trim()
        End Function

        Private Shared Async Function LoadGenresForAnimeIdsAsync(
            connection As IDbConnection,
            animeIds As IReadOnlyList(Of Long)
        ) As Task(Of Dictionary(Of Long, IReadOnlyList(Of Genre)))
            Dim result = New Dictionary(Of Long, IReadOnlyList(Of Genre))
            If animeIds Is Nothing OrElse animeIds.Count = 0 Then
                Return result
            End If

            Const sql As String =
"SELECT
    ag.anime_id AS AnimeId,
    g.id AS GenreId,
    g.name AS GenreName
FROM anime_genres ag
INNER JOIN genres g
    ON g.id = ag.genre_id
WHERE ag.anime_id IN @AnimeIds
ORDER BY g.name COLLATE NOCASE;"

            Dim rows = Await connection.QueryAsync(Of AnimeGenreRow)(
                sql,
                New With {.AnimeIds = animeIds}
            ).ConfigureAwait(False)

            Dim grouped = New Dictionary(Of Long, List(Of Genre))

            For Each row In rows
                If Not grouped.ContainsKey(row.AnimeId) Then
                    grouped(row.AnimeId) = New List(Of Genre)
                End If

                grouped(row.AnimeId).Add(
                    New Genre With {
                        .Id = row.GenreId,
                        .Name = row.GenreName
                    }
                )
            Next

            For Each pair In grouped
                result(pair.Key) = pair.Value
            Next

            Return result
        End Function

        Private Shared Function GetGenresOrEmpty(
            genresByAnimeId As IReadOnlyDictionary(Of Long, IReadOnlyList(Of Genre)),
            animeId As Long
        ) As IReadOnlyList(Of Genre)
            If genresByAnimeId Is Nothing Then
                Return Array.Empty(Of Genre)()
            End If

            Dim genres As IReadOnlyList(Of Genre) = Nothing
            If genresByAnimeId.TryGetValue(animeId, genres) Then
                Return genres
            End If

            Return Array.Empty(Of Genre)()
        End Function

        Private Shared Sub ValidateAnime(anime As Anime)
            If anime Is Nothing Then
                Throw New ArgumentNullException(NameOf(anime))
            End If

            If anime.MalId <= 0 Then
                Throw New ArgumentOutOfRangeException(NameOf(anime.MalId), "MalId must be greater than zero.")
            End If

            If String.IsNullOrWhiteSpace(anime.Title) Then
                Throw New ArgumentException("Title must not be empty.", NameOf(anime.Title))
            End If
        End Sub

        Private Class AnimeGenreRow
            Public Property AnimeId As Long
            Public Property GenreId As Long
            Public Property GenreName As String = String.Empty
        End Class
    End Class
End Namespace
