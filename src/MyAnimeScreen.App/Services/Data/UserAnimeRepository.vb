Imports Dapper
Imports MyAnimeScreen.App.Models

Namespace Services.Data
    Public Class UserAnimeRepository
        Private ReadOnly _connectionFactory As DbConnectionFactory

        Public Sub New(connectionFactory As DbConnectionFactory)
            _connectionFactory = connectionFactory
        End Sub

        Public Async Function UpsertAsync(entry As UserAnime) As Task(Of Long)
            ValidateEntry(entry)

            Const sql As String =
"INSERT INTO user_anime (
    anime_id,
    status,
    current_episode,
    personal_score,
    notes,
    is_favorite,
    started_at,
    finished_at,
    updated_at
) VALUES (
    @AnimeId,
    @Status,
    @CurrentEpisode,
    @PersonalScore,
    @Notes,
    @IsFavorite,
    @StartedAt,
    @FinishedAt,
    datetime('now')
)
ON CONFLICT(anime_id) DO UPDATE SET
    status = excluded.status,
    current_episode = excluded.current_episode,
    personal_score = excluded.personal_score,
    notes = excluded.notes,
    is_favorite = excluded.is_favorite,
    started_at = excluded.started_at,
    finished_at = excluded.finished_at,
    updated_at = datetime('now')
RETURNING id;"

            Dim parameters = New With {
                .AnimeId = entry.AnimeId,
                .Status = ToDatabaseStatus(entry.Status),
                .CurrentEpisode = entry.CurrentEpisode,
                .PersonalScore = entry.PersonalScore,
                .Notes = entry.Notes,
                .IsFavorite = If(entry.IsFavorite, 1, 0),
                .StartedAt = entry.StartedAt,
                .FinishedAt = entry.FinishedAt
            }

            Using connection = Await _connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(False)
                Return Await connection.ExecuteScalarAsync(Of Long)(sql, parameters).ConfigureAwait(False)
            End Using
        End Function

        Public Async Function GetByAnimeIdAsync(animeId As Long) As Task(Of UserAnime)
            Const sql As String =
"SELECT
    id AS Id,
    anime_id AS AnimeId,
    status AS Status,
    current_episode AS CurrentEpisode,
    personal_score AS PersonalScore,
    notes AS Notes,
    is_favorite AS IsFavorite,
    started_at AS StartedAt,
    finished_at AS FinishedAt,
    updated_at AS UpdatedAt
FROM user_anime
WHERE anime_id = @AnimeId;"

            Using connection = Await _connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(False)
                Dim row = Await connection.QuerySingleOrDefaultAsync(Of UserAnimeRow)(
                    sql,
                    New With {.AnimeId = animeId}
                ).ConfigureAwait(False)

                Return If(row Is Nothing, Nothing, MapRow(row))
            End Using
        End Function

        Public Async Function ListByStatusAsync(status As AnimeStatus) As Task(Of IReadOnlyList(Of UserAnime))
            Const sql As String =
"SELECT
    id AS Id,
    anime_id AS AnimeId,
    status AS Status,
    current_episode AS CurrentEpisode,
    personal_score AS PersonalScore,
    notes AS Notes,
    is_favorite AS IsFavorite,
    started_at AS StartedAt,
    finished_at AS FinishedAt,
    updated_at AS UpdatedAt
FROM user_anime
WHERE status = @Status
ORDER BY
    is_favorite DESC,
    updated_at DESC;"

            Using connection = Await _connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(False)
                Dim rows = Await connection.QueryAsync(Of UserAnimeRow)(
                    sql,
                    New With {.Status = ToDatabaseStatus(status)}
                ).ConfigureAwait(False)

                Dim result = New List(Of UserAnime)
                For Each row In rows
                    result.Add(MapRow(row))
                Next

                Return result
            End Using
        End Function

        Public Async Function ListLibraryByStatusAsync(
            status As AnimeStatus?,
            Optional sortBy As LibrarySortBy = LibrarySortBy.UpdatedAtDesc,
            Optional genreName As String = Nothing
        ) As Task(Of IReadOnlyList(Of LibraryAnimeSnapshot))
            Const baseSql As String =
"SELECT
    ua.anime_id AS AnimeId,
    a.title AS Title,
    ua.status AS Status,
    ua.current_episode AS CurrentEpisode,
    ua.personal_score AS PersonalScore,
    ua.is_favorite AS IsFavorite
FROM user_anime ua
INNER JOIN animes a
    ON a.id = ua.anime_id
WHERE (@Status IS NULL OR ua.status = @Status)
  AND (
      @GenreName IS NULL
      OR EXISTS (
          SELECT 1
          FROM anime_genres ag
          INNER JOIN genres g
              ON g.id = ag.genre_id
          WHERE ag.anime_id = ua.anime_id
            AND g.name = @GenreName COLLATE NOCASE
      )
  )
ORDER BY {0};"

            Using connection = Await _connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(False)
                Dim databaseStatus As String = Nothing
                If status.HasValue Then
                    databaseStatus = ToDatabaseStatus(status.Value)
                End If

                Dim sql = String.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    baseSql,
                    GetLibraryOrderByClause(sortBy)
                )

                Dim normalizedGenreName = NormalizeGenreName(genreName)

                Dim rows = Await connection.QueryAsync(Of LibraryAnimeSnapshotRow)(
                    sql,
                    New With {
                        .Status = databaseStatus,
                        .GenreName = normalizedGenreName
                    }
                ).ConfigureAwait(False)

                Dim result = New List(Of LibraryAnimeSnapshot)
                For Each row In rows
                    result.Add(MapLibraryRow(row))
                Next

                Return result
            End Using
        End Function

        Public Async Function ListLibraryGenresAsync() As Task(Of IReadOnlyList(Of Genre))
            Const sql As String =
"SELECT DISTINCT
    g.id AS Id,
    g.name AS Name
FROM user_anime ua
INNER JOIN anime_genres ag
    ON ag.anime_id = ua.anime_id
INNER JOIN genres g
    ON g.id = ag.genre_id
ORDER BY g.name COLLATE NOCASE;"

            Using connection = Await _connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(False)
                Dim rows = Await connection.QueryAsync(Of Genre)(sql).ConfigureAwait(False)
                Return rows.AsList()
            End Using
        End Function

        Private Shared Function GetLibraryOrderByClause(sortBy As LibrarySortBy) As String
            Select Case sortBy
                Case LibrarySortBy.PersonalScoreDesc
                    Return "COALESCE(ua.personal_score, -1) DESC, ua.updated_at DESC, a.title COLLATE NOCASE ASC"
                Case LibrarySortBy.CurrentEpisodeDesc
                    Return "ua.current_episode DESC, ua.updated_at DESC, a.title COLLATE NOCASE ASC"
                Case LibrarySortBy.TitleAsc
                    Return "a.title COLLATE NOCASE ASC, ua.updated_at DESC, ua.is_favorite DESC"
                Case Else
                    Return "ua.updated_at DESC, ua.is_favorite DESC, a.title COLLATE NOCASE ASC"
            End Select
        End Function

        Private Shared Function NormalizeGenreName(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then
                Return Nothing
            End If

            Return value.Trim()
        End Function

        Public Async Function DeleteByAnimeIdAsync(animeId As Long) As Task(Of Integer)
            Const sql As String =
"DELETE FROM user_anime
WHERE anime_id = @AnimeId;"

            Using connection = Await _connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(False)
                Return Await connection.ExecuteAsync(
                    sql,
                    New With {.AnimeId = animeId}
                ).ConfigureAwait(False)
            End Using
        End Function

        Private Shared Function ToDatabaseStatus(status As AnimeStatus) As String
            Return status.ToString()
        End Function

        Private Shared Sub ValidateEntry(entry As UserAnime)
            If entry Is Nothing Then
                Throw New ArgumentNullException(NameOf(entry))
            End If

            If entry.AnimeId <= 0 Then
                Throw New ArgumentOutOfRangeException(NameOf(entry.AnimeId), "AnimeId must be greater than zero.")
            End If

            If entry.CurrentEpisode < 0 Then
                Throw New ArgumentOutOfRangeException(NameOf(entry.CurrentEpisode), "CurrentEpisode must be greater than or equal to zero.")
            End If

            If entry.PersonalScore.HasValue AndAlso (entry.PersonalScore.Value < 0 OrElse entry.PersonalScore.Value > 10) Then
                Throw New ArgumentOutOfRangeException(NameOf(entry.PersonalScore), "PersonalScore must be between 0 and 10.")
            End If
        End Sub

        Private Shared Function ParseDatabaseStatus(value As String) As AnimeStatus
            Dim parsed As AnimeStatus
            If [Enum].TryParse(value, ignoreCase:=True, result:=parsed) Then
                Return parsed
            End If

            Throw New InvalidOperationException($"Status inválido salvo no banco: '{value}'.")
        End Function

        Private Shared Function MapRow(row As UserAnimeRow) As UserAnime
            Return New UserAnime With {
                .Id = row.Id,
                .AnimeId = row.AnimeId,
                .Status = ParseDatabaseStatus(row.Status),
                .CurrentEpisode = row.CurrentEpisode,
                .PersonalScore = row.PersonalScore,
                .Notes = row.Notes,
                .IsFavorite = row.IsFavorite <> 0,
                .StartedAt = row.StartedAt,
                .FinishedAt = row.FinishedAt,
                .UpdatedAt = row.UpdatedAt
            }
        End Function

        Private Shared Function MapLibraryRow(row As LibraryAnimeSnapshotRow) As LibraryAnimeSnapshot
            Return New LibraryAnimeSnapshot With {
                .AnimeId = row.AnimeId,
                .Title = row.Title,
                .Status = ParseDatabaseStatus(row.Status),
                .CurrentEpisode = row.CurrentEpisode,
                .PersonalScore = row.PersonalScore,
                .IsFavorite = row.IsFavorite <> 0
            }
        End Function

        Public Class LibraryAnimeSnapshot
            Public Property AnimeId As Long
            Public Property Title As String = String.Empty
            Public Property Status As AnimeStatus
            Public Property CurrentEpisode As Integer
            Public Property PersonalScore As Double?
            Public Property IsFavorite As Boolean
        End Class

        Private Class UserAnimeRow
            Public Property Id As Long
            Public Property AnimeId As Long
            Public Property Status As String = String.Empty
            Public Property CurrentEpisode As Integer
            Public Property PersonalScore As Double?
            Public Property Notes As String
            Public Property IsFavorite As Integer
            Public Property StartedAt As DateTime?
            Public Property FinishedAt As DateTime?
            Public Property UpdatedAt As DateTime?
        End Class

        Private Class LibraryAnimeSnapshotRow
            Public Property AnimeId As Long
            Public Property Title As String = String.Empty
            Public Property Status As String = String.Empty
            Public Property CurrentEpisode As Integer
            Public Property PersonalScore As Double?
            Public Property IsFavorite As Integer
        End Class
    End Class
End Namespace
