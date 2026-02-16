Imports Dapper
Imports MyAnimeScreen.App.Models

Namespace Services.Data
    Public Class UserAnimeRepository
        Private ReadOnly _connectionFactory As DbConnectionFactory

        Public Sub New(connectionFactory As DbConnectionFactory)
            _connectionFactory = connectionFactory
        End Sub

        Public Async Function UpsertAsync(entry As UserAnime) As Task(Of Long)
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
ORDER BY updated_at DESC;"

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

        Private Shared Function ToDatabaseStatus(status As AnimeStatus) As String
            Return status.ToString()
        End Function

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
    End Class
End Namespace
