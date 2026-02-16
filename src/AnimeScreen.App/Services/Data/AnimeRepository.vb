Imports Dapper
Imports AnimeScreen.App.Models

Namespace Services.Data
    Public Class AnimeRepository
        Private ReadOnly _connectionFactory As DbConnectionFactory

        Public Sub New(connectionFactory As DbConnectionFactory)
            _connectionFactory = connectionFactory
        End Sub

        Public Async Function UpsertAsync(anime As Anime) As Task(Of Long)
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
                Dim animeId = Await connection.ExecuteScalarAsync(Of Long)(sql, anime).ConfigureAwait(False)
                Return animeId
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
                Return Await connection.QuerySingleOrDefaultAsync(Of Anime)(
                    sql,
                    New With {.MalId = malId}
                ).ConfigureAwait(False)
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

                Return rows.AsList()
            End Using
        End Function
    End Class
End Namespace
