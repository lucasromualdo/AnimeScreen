Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports Dapper
Imports Microsoft.Data.Sqlite
Imports Microsoft.VisualBasic.FileIO
Imports MyAnimeScreen.App.Models

Namespace Services.Data
    Public Class LibraryTransferService
        Private Const CsvGenreSeparator As Char = "|"c
        Private Shared ReadOnly CsvHeaderColumns As IReadOnlyList(Of String) = New List(Of String) From {
            "anime_id",
            "anime_mal_id",
            "title",
            "title_jp",
            "synopsis",
            "image_url",
            "episodes_total",
            "score",
            "year",
            "season",
            "genres",
            "user_status",
            "current_episode",
            "personal_score",
            "notes",
            "is_favorite",
            "started_at",
            "finished_at",
            "updated_at"
        }

        Private ReadOnly _connectionFactory As DbConnectionFactory

        Public Sub New(connectionFactory As DbConnectionFactory)
            If connectionFactory Is Nothing Then
                Throw New ArgumentNullException(NameOf(connectionFactory))
            End If

            _connectionFactory = connectionFactory
        End Sub

        Public Async Function ExportAsJsonAsync(filePath As String) As Task(Of Integer)
            ValidateOutputFilePath(filePath)

            Dim records = Await ListLibraryTransferRecordsAsync().ConfigureAwait(False)
            EnsureDirectoryForFilePath(filePath)

            Dim payload = New LibraryExportPayload With {
                .Version = 1,
                .ExportedAtUtc = DateTime.UtcNow,
                .Entries = records.ToList()
            }

            Dim options = New JsonSerializerOptions With {
                .WriteIndented = True
            }

            Dim json = JsonSerializer.Serialize(payload, options)
            Await File.WriteAllTextAsync(filePath, json, Encoding.UTF8).ConfigureAwait(False)
            Return records.Count
        End Function

        Public Async Function ExportAsCsvAsync(filePath As String) As Task(Of Integer)
            ValidateOutputFilePath(filePath)

            Dim records = Await ListLibraryTransferRecordsAsync().ConfigureAwait(False)
            EnsureDirectoryForFilePath(filePath)

            Dim builder = New StringBuilder()
            builder.AppendLine(String.Join(",", CsvHeaderColumns))

            For Each record In records
                Dim columns = New List(Of String) From {
                    ToCsvColumn(record.AnimeId.ToString(CultureInfo.InvariantCulture)),
                    ToCsvColumn(record.AnimeMalId.ToString(CultureInfo.InvariantCulture)),
                    ToCsvColumn(record.Title),
                    ToCsvColumn(record.TitleJp),
                    ToCsvColumn(record.Synopsis),
                    ToCsvColumn(record.ImageUrl),
                    ToCsvColumn(FormatNullableInteger(record.EpisodesTotal)),
                    ToCsvColumn(FormatNullableDouble(record.Score)),
                    ToCsvColumn(FormatNullableInteger(record.Year)),
                    ToCsvColumn(record.Season),
                    ToCsvColumn(String.Join(CsvGenreSeparator, record.Genres)),
                    ToCsvColumn(record.UserStatus),
                    ToCsvColumn(record.CurrentEpisode.ToString(CultureInfo.InvariantCulture)),
                    ToCsvColumn(FormatNullableDouble(record.PersonalScore)),
                    ToCsvColumn(record.Notes),
                    ToCsvColumn(If(record.IsFavorite, "1", "0")),
                    ToCsvColumn(FormatNullableDateTime(record.StartedAt)),
                    ToCsvColumn(FormatNullableDateTime(record.FinishedAt)),
                    ToCsvColumn(FormatNullableDateTime(record.UpdatedAt))
                }

                builder.AppendLine(String.Join(",", columns))
            Next

            Await File.WriteAllTextAsync(filePath, builder.ToString(), Encoding.UTF8).ConfigureAwait(False)
            Return records.Count
        End Function

        Public Async Function ImportAsync(filePath As String) As Task(Of LibraryImportSummary)
            ValidateInputFilePath(filePath)

            Dim parsed = Await ReadImportBatchAsync(filePath).ConfigureAwait(False)
            Dim summary = New LibraryImportSummary With {
                .InvalidEntries = parsed.InvalidRows
            }

            If parsed.Records.Count = 0 Then
                Return summary
            End If

            Using connection = Await _connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using transaction = connection.BeginTransaction()
                    Try
                        For Each record In parsed.Records
                            Dim normalized As NormalizedLibraryTransferRecord = Nothing
                            If Not TryNormalizeRecord(record, normalized) Then
                                summary.InvalidEntries += 1
                                Continue For
                            End If

                            Try
                                Dim mergeOutcome = Await MergeRecordAsync(connection, transaction, normalized).ConfigureAwait(False)
                                Select Case mergeOutcome
                                    Case MergeOutcome.Added
                                        summary.NewEntries += 1
                                    Case MergeOutcome.Updated
                                        summary.UpdatedEntries += 1
                                    Case Else
                                        summary.IgnoredEntries += 1
                                End Select
                            Catch ex As SqliteException
                                summary.InvalidEntries += 1
                            End Try
                        Next

                        transaction.Commit()
                    Catch
                        transaction.Rollback()
                        Throw
                    End Try
                End Using
            End Using

            Return summary
        End Function

        Private Async Function ListLibraryTransferRecordsAsync() As Task(Of IReadOnlyList(Of LibraryTransferRecord))
            Const userLibrarySql As String =
"SELECT
    ua.anime_id AS AnimeId,
    a.mal_id AS AnimeMalId,
    a.title AS Title,
    a.title_jp AS TitleJp,
    a.synopsis AS Synopsis,
    a.image_url AS ImageUrl,
    a.episodes_total AS EpisodesTotal,
    a.score AS Score,
    a.year AS Year,
    a.season AS Season,
    ua.status AS UserStatus,
    ua.current_episode AS CurrentEpisode,
    ua.personal_score AS PersonalScore,
    ua.notes AS Notes,
    ua.is_favorite AS IsFavorite,
    ua.started_at AS StartedAt,
    ua.finished_at AS FinishedAt,
    ua.updated_at AS UpdatedAt
FROM user_anime ua
INNER JOIN animes a
    ON a.id = ua.anime_id
ORDER BY ua.anime_id ASC;"

            Using connection = Await _connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(False)
                Dim rows = (Await connection.QueryAsync(Of LibraryTransferRow)(userLibrarySql).ConfigureAwait(False)).AsList()
                If rows.Count = 0 Then
                    Return Array.Empty(Of LibraryTransferRecord)()
                End If

                Dim animeIds = rows.Select(Function(item) item.AnimeId).Distinct().ToList()
                Dim genresByAnimeId = Await LoadGenreNamesByAnimeIdAsync(connection, animeIds).ConfigureAwait(False)
                Dim result = New List(Of LibraryTransferRecord)(rows.Count)

                For Each row In rows
                    result.Add(New LibraryTransferRecord With {
                        .AnimeId = row.AnimeId,
                        .AnimeMalId = row.AnimeMalId,
                        .Title = If(row.Title, String.Empty),
                        .TitleJp = row.TitleJp,
                        .Synopsis = row.Synopsis,
                        .ImageUrl = row.ImageUrl,
                        .EpisodesTotal = row.EpisodesTotal,
                        .Score = row.Score,
                        .Year = row.Year,
                        .Season = row.Season,
                        .Genres = GetGenreNamesOrEmpty(genresByAnimeId, row.AnimeId),
                        .UserStatus = If(row.UserStatus, String.Empty),
                        .CurrentEpisode = row.CurrentEpisode,
                        .PersonalScore = row.PersonalScore,
                        .Notes = row.Notes,
                        .IsFavorite = row.IsFavorite <> 0,
                        .StartedAt = row.StartedAt,
                        .FinishedAt = row.FinishedAt,
                        .UpdatedAt = row.UpdatedAt
                    })
                Next

                Return result
            End Using
        End Function

        Private Async Function LoadGenreNamesByAnimeIdAsync(
            connection As SqliteConnection,
            animeIds As IReadOnlyList(Of Long)
        ) As Task(Of Dictionary(Of Long, IReadOnlyList(Of String)))
            Dim result = New Dictionary(Of Long, IReadOnlyList(Of String))
            If animeIds Is Nothing OrElse animeIds.Count = 0 Then
                Return result
            End If

            Const genresSql As String =
"SELECT
    ag.anime_id AS AnimeId,
    g.name AS GenreName
FROM anime_genres ag
INNER JOIN genres g
    ON g.id = ag.genre_id
WHERE ag.anime_id IN @AnimeIds
ORDER BY g.name COLLATE NOCASE;"

            Dim rows = Await connection.QueryAsync(Of AnimeGenreRow)(
                genresSql,
                New With {.AnimeIds = animeIds}
            ).ConfigureAwait(False)

            Dim grouped = New Dictionary(Of Long, List(Of String))
            For Each row In rows
                If Not grouped.ContainsKey(row.AnimeId) Then
                    grouped(row.AnimeId) = New List(Of String)()
                End If

                grouped(row.AnimeId).Add(row.GenreName)
            Next

            For Each pair In grouped
                result(pair.Key) = pair.Value
            Next

            Return result
        End Function

        Private Shared Function GetGenreNamesOrEmpty(
            genresByAnimeId As IReadOnlyDictionary(Of Long, IReadOnlyList(Of String)),
            animeId As Long
        ) As IReadOnlyList(Of String)
            If genresByAnimeId Is Nothing Then
                Return Array.Empty(Of String)()
            End If

            Dim genres As IReadOnlyList(Of String) = Nothing
            If genresByAnimeId.TryGetValue(animeId, genres) Then
                Return genres
            End If

            Return Array.Empty(Of String)()
        End Function

        Private Async Function ReadImportBatchAsync(filePath As String) As Task(Of ParsedImportBatch)
            Dim extension = Path.GetExtension(filePath)
            If String.IsNullOrWhiteSpace(extension) Then
                Throw New InvalidDataException("Arquivo de importacao sem extensao suportada. Use .json ou .csv.")
            End If

            Select Case extension.Trim().ToLowerInvariant()
                Case ".json"
                    Return Await ReadImportBatchFromJsonAsync(filePath).ConfigureAwait(False)
                Case ".csv"
                    Return ReadImportBatchFromCsv(filePath)
                Case Else
                    Throw New InvalidDataException("Formato de importacao nao suportado. Use .json ou .csv.")
            End Select
        End Function

        Private Shared Async Function ReadImportBatchFromJsonAsync(filePath As String) As Task(Of ParsedImportBatch)
            Try
                Dim json = Await File.ReadAllTextAsync(filePath, Encoding.UTF8).ConfigureAwait(False)
                Dim options = New JsonSerializerOptions With {
                    .PropertyNameCaseInsensitive = True
                }

                Dim payload = JsonSerializer.Deserialize(Of LibraryExportPayload)(json, options)
                If payload Is Nothing OrElse payload.Entries Is Nothing Then
                    Throw New InvalidDataException("Arquivo JSON invalido: campo 'entries' ausente.")
                End If

                Dim records = payload.Entries.Where(Function(item) item IsNot Nothing).ToList()
                Dim invalidRows = payload.Entries.Count - records.Count

                Return New ParsedImportBatch With {
                    .Records = records,
                    .InvalidRows = Math.Max(0, invalidRows)
                }
            Catch ex As JsonException
                Throw New InvalidDataException("Arquivo JSON invalido para importacao.", ex)
            End Try
        End Function

        Private Shared Function ReadImportBatchFromCsv(filePath As String) As ParsedImportBatch
            Dim records = New List(Of LibraryTransferRecord)()
            Dim invalidRows = 0

            Using parser = New TextFieldParser(filePath, Encoding.UTF8)
                parser.TextFieldType = FieldType.Delimited
                parser.SetDelimiters(",")
                parser.HasFieldsEnclosedInQuotes = True

                If parser.EndOfData Then
                    Throw New InvalidDataException("Arquivo CSV vazio.")
                End If

                Dim headerFields = parser.ReadFields()
                Dim headerMap = BuildCsvHeaderMap(headerFields)

                While Not parser.EndOfData
                    Dim fields = parser.ReadFields()
                    If fields Is Nothing Then
                        invalidRows += 1
                        Continue While
                    End If

                    If fields.Length = 1 AndAlso String.IsNullOrWhiteSpace(fields(0)) Then
                        Continue While
                    End If

                    Dim parsedRecord As LibraryTransferRecord = Nothing
                    If TryParseCsvRecord(fields, headerMap, parsedRecord) Then
                        records.Add(parsedRecord)
                    Else
                        invalidRows += 1
                    End If
                End While
            End Using

            Return New ParsedImportBatch With {
                .Records = records,
                .InvalidRows = invalidRows
            }
        End Function

        Private Shared Function BuildCsvHeaderMap(fields As String()) As IReadOnlyDictionary(Of String, Integer)
            If fields Is Nothing OrElse fields.Length = 0 Then
                Throw New InvalidDataException("Cabecalho CSV ausente.")
            End If

            Dim indexByName = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
            For i = 0 To fields.Length - 1
                Dim headerName = NormalizeHeaderName(fields(i))
                If String.IsNullOrWhiteSpace(headerName) Then
                    Continue For
                End If

                If Not indexByName.ContainsKey(headerName) Then
                    indexByName(headerName) = i
                End If
            Next

            For Each requiredHeader In CsvHeaderColumns
                If Not indexByName.ContainsKey(requiredHeader) Then
                    Throw New InvalidDataException($"Cabecalho CSV invalido: coluna obrigatoria '{requiredHeader}' nao encontrada.")
                End If
            Next

            Return indexByName
        End Function

        Private Shared Function NormalizeHeaderName(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then
                Return String.Empty
            End If

            Return value.Trim()
        End Function

        Private Shared Function TryParseCsvRecord(
            fields As String(),
            headerMap As IReadOnlyDictionary(Of String, Integer),
            ByRef record As LibraryTransferRecord
        ) As Boolean
            record = Nothing
            If fields Is Nothing OrElse headerMap Is Nothing Then
                Return False
            End If

            Dim animeId As Long
            If Not TryParseRequiredLong(GetCsvValue(fields, headerMap, "anime_id"), animeId) Then
                Return False
            End If

            Dim animeMalId As Integer
            If Not TryParseRequiredInteger(GetCsvValue(fields, headerMap, "anime_mal_id"), animeMalId) Then
                Return False
            End If

            Dim episodesTotal As Integer?
            If Not TryParseOptionalInteger(GetCsvValue(fields, headerMap, "episodes_total"), episodesTotal) Then
                Return False
            End If

            Dim score As Double?
            If Not TryParseOptionalDouble(GetCsvValue(fields, headerMap, "score"), score) Then
                Return False
            End If

            Dim year As Integer?
            If Not TryParseOptionalInteger(GetCsvValue(fields, headerMap, "year"), year) Then
                Return False
            End If

            Dim currentEpisode As Integer
            If Not TryParseRequiredInteger(GetCsvValue(fields, headerMap, "current_episode"), currentEpisode) Then
                Return False
            End If

            Dim personalScore As Double?
            If Not TryParseOptionalDouble(GetCsvValue(fields, headerMap, "personal_score"), personalScore) Then
                Return False
            End If

            Dim isFavorite As Boolean
            If Not TryParseBoolean(GetCsvValue(fields, headerMap, "is_favorite"), isFavorite) Then
                Return False
            End If

            Dim startedAt As DateTime?
            If Not TryParseOptionalDateTime(GetCsvValue(fields, headerMap, "started_at"), startedAt) Then
                Return False
            End If

            Dim finishedAt As DateTime?
            If Not TryParseOptionalDateTime(GetCsvValue(fields, headerMap, "finished_at"), finishedAt) Then
                Return False
            End If

            Dim updatedAt As DateTime?
            If Not TryParseOptionalDateTime(GetCsvValue(fields, headerMap, "updated_at"), updatedAt) Then
                Return False
            End If

            record = New LibraryTransferRecord With {
                .AnimeId = animeId,
                .AnimeMalId = animeMalId,
                .Title = GetCsvValue(fields, headerMap, "title"),
                .TitleJp = GetCsvValue(fields, headerMap, "title_jp"),
                .Synopsis = GetCsvValue(fields, headerMap, "synopsis"),
                .ImageUrl = GetCsvValue(fields, headerMap, "image_url"),
                .EpisodesTotal = episodesTotal,
                .Score = score,
                .Year = year,
                .Season = GetCsvValue(fields, headerMap, "season"),
                .Genres = ParseGenresFromCsv(GetCsvValue(fields, headerMap, "genres")),
                .UserStatus = GetCsvValue(fields, headerMap, "user_status"),
                .CurrentEpisode = currentEpisode,
                .PersonalScore = personalScore,
                .Notes = GetCsvValue(fields, headerMap, "notes"),
                .IsFavorite = isFavorite,
                .StartedAt = startedAt,
                .FinishedAt = finishedAt,
                .UpdatedAt = updatedAt
            }

            Return True
        End Function

        Private Shared Function ParseGenresFromCsv(value As String) As IReadOnlyList(Of String)
            If String.IsNullOrWhiteSpace(value) Then
                Return Array.Empty(Of String)()
            End If

            Dim normalized = New List(Of String)()
            For Each genre In value.Split(CsvGenreSeparator)
                If String.IsNullOrWhiteSpace(genre) Then
                    Continue For
                End If

                normalized.Add(genre.Trim())
            Next

            Return normalized
        End Function

        Private Shared Function GetCsvValue(
            fields As String(),
            headerMap As IReadOnlyDictionary(Of String, Integer),
            headerName As String
        ) As String
            If fields Is Nothing OrElse headerMap Is Nothing Then
                Return String.Empty
            End If

            Dim index = 0
            If Not headerMap.TryGetValue(headerName, index) Then
                Return String.Empty
            End If

            If index < 0 OrElse index >= fields.Length Then
                Return String.Empty
            End If

            Return If(fields(index), String.Empty)
        End Function

        Private Shared Function TryParseRequiredLong(value As String, ByRef parsed As Long) As Boolean
            Return Long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                parsed)
        End Function

        Private Shared Function TryParseRequiredInteger(value As String, ByRef parsed As Integer) As Boolean
            Return Integer.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                parsed)
        End Function

        Private Shared Function TryParseOptionalInteger(value As String, ByRef parsed As Integer?) As Boolean
            parsed = Nothing
            If String.IsNullOrWhiteSpace(value) Then
                Return True
            End If

            Dim raw = 0
            If Not Integer.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, raw) Then
                Return False
            End If

            parsed = raw
            Return True
        End Function

        Private Shared Function TryParseOptionalDouble(value As String, ByRef parsed As Double?) As Boolean
            parsed = Nothing
            If String.IsNullOrWhiteSpace(value) Then
                Return True
            End If

            Dim raw = 0.0R
            If Not Double.TryParse(value, NumberStyles.Float Or NumberStyles.AllowThousands, CultureInfo.InvariantCulture, raw) Then
                Return False
            End If

            parsed = raw
            Return True
        End Function

        Private Shared Function TryParseBoolean(value As String, ByRef parsed As Boolean) As Boolean
            parsed = False
            If String.IsNullOrWhiteSpace(value) Then
                Return True
            End If

            Dim normalized = value.Trim()
            Select Case normalized.ToLowerInvariant()
                Case "1", "true"
                    parsed = True
                    Return True
                Case "0", "false"
                    parsed = False
                    Return True
                Case Else
                    Return False
            End Select
        End Function

        Private Shared Function TryParseOptionalDateTime(value As String, ByRef parsed As DateTime?) As Boolean
            parsed = Nothing
            If String.IsNullOrWhiteSpace(value) Then
                Return True
            End If

            Dim raw As DateTime
            If Not DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                raw) Then
                Return False
            End If

            parsed = raw
            Return True
        End Function

        Private Shared Function TryNormalizeRecord(
            source As LibraryTransferRecord,
            ByRef normalized As NormalizedLibraryTransferRecord
        ) As Boolean
            normalized = Nothing
            If source Is Nothing Then
                Return False
            End If

            If source.AnimeId <= 0 Then
                Return False
            End If

            If source.AnimeMalId <= 0 Then
                Return False
            End If

            Dim title = NormalizeRequiredText(source.Title)
            If String.IsNullOrWhiteSpace(title) Then
                Return False
            End If

            If source.CurrentEpisode < 0 Then
                Return False
            End If

            If source.PersonalScore.HasValue AndAlso (source.PersonalScore.Value < 0 OrElse source.PersonalScore.Value > 10) Then
                Return False
            End If

            Dim status As AnimeStatus
            If Not [Enum].TryParse(source.UserStatus, ignoreCase:=True, result:=status) Then
                Return False
            End If

            normalized = New NormalizedLibraryTransferRecord With {
                .AnimeId = source.AnimeId,
                .AnimeMalId = source.AnimeMalId,
                .Title = title,
                .TitleJp = NormalizeOptionalText(source.TitleJp),
                .Synopsis = NormalizeOptionalText(source.Synopsis),
                .ImageUrl = NormalizeOptionalText(source.ImageUrl),
                .EpisodesTotal = source.EpisodesTotal,
                .Score = source.Score,
                .Year = source.Year,
                .Season = NormalizeOptionalText(source.Season),
                .Genres = NormalizeGenres(source.Genres),
                .Status = status,
                .CurrentEpisode = source.CurrentEpisode,
                .PersonalScore = source.PersonalScore,
                .Notes = NormalizeOptionalText(source.Notes),
                .IsFavorite = source.IsFavorite,
                .StartedAt = source.StartedAt,
                .FinishedAt = source.FinishedAt
            }

            Return True
        End Function

        Private Shared Function NormalizeRequiredText(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then
                Return String.Empty
            End If

            Return value.Trim()
        End Function

        Private Shared Function NormalizeOptionalText(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then
                Return Nothing
            End If

            Return value.Trim()
        End Function

        Private Shared Function NormalizeGenres(genres As IReadOnlyList(Of String)) As IReadOnlyList(Of String)
            If genres Is Nothing OrElse genres.Count = 0 Then
                Return Array.Empty(Of String)()
            End If

            Dim seen = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim normalized = New List(Of String)()

            For Each genreName In genres
                If String.IsNullOrWhiteSpace(genreName) Then
                    Continue For
                End If

                Dim name = genreName.Trim()
                If seen.Add(name) Then
                    normalized.Add(name)
                End If
            Next

            Return normalized
        End Function

        Private Async Function MergeRecordAsync(
            connection As SqliteConnection,
            transaction As SqliteTransaction,
            record As NormalizedLibraryTransferRecord
        ) As Task(Of MergeOutcome)
            Const existingUserSql As String =
"SELECT
    status AS Status,
    current_episode AS CurrentEpisode,
    personal_score AS PersonalScore,
    notes AS Notes,
    is_favorite AS IsFavorite,
    started_at AS StartedAt,
    finished_at AS FinishedAt
FROM user_anime
WHERE anime_id = @AnimeId;"

            Await UpsertAnimeMetadataAsync(connection, transaction, record).ConfigureAwait(False)
            Await ReplaceGenresForAnimeAsync(connection, transaction, record.AnimeId, record.Genres).ConfigureAwait(False)

            Dim existing = Await connection.QuerySingleOrDefaultAsync(Of ExistingUserAnimeRow)(
                existingUserSql,
                New With {.AnimeId = record.AnimeId},
                transaction
            ).ConfigureAwait(False)

            If existing Is Nothing Then
                Const insertUserSql As String =
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
);"

                Await connection.ExecuteAsync(
                    insertUserSql,
                    New With {
                        .AnimeId = record.AnimeId,
                        .Status = record.Status.ToString(),
                        .CurrentEpisode = record.CurrentEpisode,
                        .PersonalScore = record.PersonalScore,
                        .Notes = record.Notes,
                        .IsFavorite = If(record.IsFavorite, 1, 0),
                        .StartedAt = record.StartedAt,
                        .FinishedAt = record.FinishedAt
                    },
                    transaction
                ).ConfigureAwait(False)

                Return MergeOutcome.Added
            End If

            If IsEquivalent(existing, record) Then
                Return MergeOutcome.Ignored
            End If

            Const updateUserSql As String =
"UPDATE user_anime
SET
    status = @Status,
    current_episode = @CurrentEpisode,
    personal_score = @PersonalScore,
    notes = @Notes,
    is_favorite = @IsFavorite,
    started_at = @StartedAt,
    finished_at = @FinishedAt,
    updated_at = datetime('now')
WHERE anime_id = @AnimeId;"

            Await connection.ExecuteAsync(
                updateUserSql,
                New With {
                    .AnimeId = record.AnimeId,
                    .Status = record.Status.ToString(),
                    .CurrentEpisode = record.CurrentEpisode,
                    .PersonalScore = record.PersonalScore,
                    .Notes = record.Notes,
                    .IsFavorite = If(record.IsFavorite, 1, 0),
                    .StartedAt = record.StartedAt,
                    .FinishedAt = record.FinishedAt
                },
                transaction
            ).ConfigureAwait(False)

            Return MergeOutcome.Updated
        End Function

        Private Shared Function IsEquivalent(
            existing As ExistingUserAnimeRow,
            imported As NormalizedLibraryTransferRecord
        ) As Boolean
            If existing Is Nothing OrElse imported Is Nothing Then
                Return False
            End If

            If Not String.Equals(existing.Status, imported.Status.ToString(), StringComparison.OrdinalIgnoreCase) Then
                Return False
            End If

            If existing.CurrentEpisode <> imported.CurrentEpisode Then
                Return False
            End If

            If Not Nullable.Equals(existing.PersonalScore, imported.PersonalScore) Then
                Return False
            End If

            If Not String.Equals(
                NormalizeOptionalText(existing.Notes),
                NormalizeOptionalText(imported.Notes),
                StringComparison.Ordinal
            ) Then
                Return False
            End If

            If existing.IsFavorite <> If(imported.IsFavorite, 1, 0) Then
                Return False
            End If

            If Not Nullable.Equals(existing.StartedAt, imported.StartedAt) Then
                Return False
            End If

            If Not Nullable.Equals(existing.FinishedAt, imported.FinishedAt) Then
                Return False
            End If

            Return True
        End Function

        Private Shared Async Function UpsertAnimeMetadataAsync(
            connection As SqliteConnection,
            transaction As SqliteTransaction,
            record As NormalizedLibraryTransferRecord
        ) As Task
            Const existsSql As String =
"SELECT COUNT(1)
FROM animes
WHERE id = @AnimeId;"

            Dim existingCount = Await connection.ExecuteScalarAsync(Of Integer)(
                existsSql,
                New With {.AnimeId = record.AnimeId},
                transaction
            ).ConfigureAwait(False)

            If existingCount > 0 Then
                Const updateAnimeSql As String =
"UPDATE animes
SET
    mal_id = @AnimeMalId,
    title = @Title,
    title_jp = @TitleJp,
    synopsis = @Synopsis,
    image_url = @ImageUrl,
    episodes_total = @EpisodesTotal,
    score = @Score,
    year = @Year,
    season = @Season,
    updated_at = datetime('now')
WHERE id = @AnimeId;"

                Await connection.ExecuteAsync(
                    updateAnimeSql,
                    New With {
                        .AnimeId = record.AnimeId,
                        .AnimeMalId = record.AnimeMalId,
                        .Title = record.Title,
                        .TitleJp = record.TitleJp,
                        .Synopsis = record.Synopsis,
                        .ImageUrl = record.ImageUrl,
                        .EpisodesTotal = record.EpisodesTotal,
                        .Score = record.Score,
                        .Year = record.Year,
                        .Season = record.Season
                    },
                    transaction
                ).ConfigureAwait(False)
                Return
            End If

            Const insertAnimeSql As String =
"INSERT INTO animes (
    id,
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
    @AnimeId,
    @AnimeMalId,
    @Title,
    @TitleJp,
    @Synopsis,
    @ImageUrl,
    @EpisodesTotal,
    @Score,
    @Year,
    @Season,
    datetime('now')
);"

            Await connection.ExecuteAsync(
                insertAnimeSql,
                New With {
                    .AnimeId = record.AnimeId,
                    .AnimeMalId = record.AnimeMalId,
                    .Title = record.Title,
                    .TitleJp = record.TitleJp,
                    .Synopsis = record.Synopsis,
                    .ImageUrl = record.ImageUrl,
                    .EpisodesTotal = record.EpisodesTotal,
                    .Score = record.Score,
                    .Year = record.Year,
                    .Season = record.Season
                },
                transaction
            ).ConfigureAwait(False)
        End Function

        Private Shared Async Function ReplaceGenresForAnimeAsync(
            connection As SqliteConnection,
            transaction As SqliteTransaction,
            animeId As Long,
            genres As IReadOnlyList(Of String)
        ) As Task
            Const deleteSql As String =
"DELETE FROM anime_genres
WHERE anime_id = @AnimeId;"

            Await connection.ExecuteAsync(
                deleteSql,
                New With {.AnimeId = animeId},
                transaction
            ).ConfigureAwait(False)

            Dim normalizedGenres = NormalizeGenres(genres)
            If normalizedGenres.Count = 0 Then
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

            For Each genreName In normalizedGenres
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

        Private Shared Function ToCsvColumn(value As String) As String
            Dim sanitized = If(value, String.Empty)
            Dim requiresQuotes =
                sanitized.Contains(","c) OrElse
                sanitized.Contains(""""c) OrElse
                sanitized.Contains(ControlChars.Cr) OrElse
                sanitized.Contains(ControlChars.Lf)

            If Not requiresQuotes Then
                Return sanitized
            End If

            Dim escaped = sanitized.Replace("""", """""")
            Return $"""{escaped}"""
        End Function

        Private Shared Function FormatNullableInteger(value As Integer?) As String
            If Not value.HasValue Then
                Return String.Empty
            End If

            Return value.Value.ToString(CultureInfo.InvariantCulture)
        End Function

        Private Shared Function FormatNullableDouble(value As Double?) As String
            If Not value.HasValue Then
                Return String.Empty
            End If

            Return value.Value.ToString("0.##########", CultureInfo.InvariantCulture)
        End Function

        Private Shared Function FormatNullableDateTime(value As DateTime?) As String
            If Not value.HasValue Then
                Return String.Empty
            End If

            Return value.Value.ToString("O", CultureInfo.InvariantCulture)
        End Function

        Private Shared Sub ValidateOutputFilePath(filePath As String)
            If String.IsNullOrWhiteSpace(filePath) Then
                Throw New ArgumentException("Caminho de arquivo de exportacao nao informado.", NameOf(filePath))
            End If
        End Sub

        Private Shared Sub ValidateInputFilePath(filePath As String)
            If String.IsNullOrWhiteSpace(filePath) Then
                Throw New ArgumentException("Caminho de arquivo de importacao nao informado.", NameOf(filePath))
            End If

            If Not File.Exists(filePath) Then
                Throw New FileNotFoundException("Arquivo de importacao nao encontrado.", filePath)
            End If
        End Sub

        Private Shared Sub EnsureDirectoryForFilePath(filePath As String)
            Dim directoryPath = Path.GetDirectoryName(filePath)
            If String.IsNullOrWhiteSpace(directoryPath) Then
                Return
            End If

            Directory.CreateDirectory(directoryPath)
        End Sub

        Private Enum MergeOutcome
            Added
            Updated
            Ignored
        End Enum

        Private Class ParsedImportBatch
            Public Property Records As IReadOnlyList(Of LibraryTransferRecord) = Array.Empty(Of LibraryTransferRecord)()
            Public Property InvalidRows As Integer
        End Class

        Private Class LibraryExportPayload
            <JsonPropertyName("version")>
            Public Property Version As Integer

            <JsonPropertyName("exported_at_utc")>
            Public Property ExportedAtUtc As DateTime

            <JsonPropertyName("entries")>
            Public Property Entries As List(Of LibraryTransferRecord) = New List(Of LibraryTransferRecord)()
        End Class

        Private Class LibraryTransferRecord
            <JsonPropertyName("anime_id")>
            Public Property AnimeId As Long

            <JsonPropertyName("anime_mal_id")>
            Public Property AnimeMalId As Integer

            <JsonPropertyName("title")>
            Public Property Title As String = String.Empty

            <JsonPropertyName("title_jp")>
            Public Property TitleJp As String

            <JsonPropertyName("synopsis")>
            Public Property Synopsis As String

            <JsonPropertyName("image_url")>
            Public Property ImageUrl As String

            <JsonPropertyName("episodes_total")>
            Public Property EpisodesTotal As Integer?

            <JsonPropertyName("score")>
            Public Property Score As Double?

            <JsonPropertyName("year")>
            Public Property Year As Integer?

            <JsonPropertyName("season")>
            Public Property Season As String

            <JsonPropertyName("genres")>
            Public Property Genres As IReadOnlyList(Of String) = Array.Empty(Of String)()

            <JsonPropertyName("user_status")>
            Public Property UserStatus As String = String.Empty

            <JsonPropertyName("current_episode")>
            Public Property CurrentEpisode As Integer

            <JsonPropertyName("personal_score")>
            Public Property PersonalScore As Double?

            <JsonPropertyName("notes")>
            Public Property Notes As String

            <JsonPropertyName("is_favorite")>
            Public Property IsFavorite As Boolean

            <JsonPropertyName("started_at")>
            Public Property StartedAt As DateTime?

            <JsonPropertyName("finished_at")>
            Public Property FinishedAt As DateTime?

            <JsonPropertyName("updated_at")>
            Public Property UpdatedAt As DateTime?
        End Class

        Private Class NormalizedLibraryTransferRecord
            Public Property AnimeId As Long
            Public Property AnimeMalId As Integer
            Public Property Title As String = String.Empty
            Public Property TitleJp As String
            Public Property Synopsis As String
            Public Property ImageUrl As String
            Public Property EpisodesTotal As Integer?
            Public Property Score As Double?
            Public Property Year As Integer?
            Public Property Season As String
            Public Property Genres As IReadOnlyList(Of String) = Array.Empty(Of String)()
            Public Property Status As AnimeStatus
            Public Property CurrentEpisode As Integer
            Public Property PersonalScore As Double?
            Public Property Notes As String
            Public Property IsFavorite As Boolean
            Public Property StartedAt As DateTime?
            Public Property FinishedAt As DateTime?
        End Class

        Private Class LibraryTransferRow
            Public Property AnimeId As Long
            Public Property AnimeMalId As Integer
            Public Property Title As String = String.Empty
            Public Property TitleJp As String
            Public Property Synopsis As String
            Public Property ImageUrl As String
            Public Property EpisodesTotal As Integer?
            Public Property Score As Double?
            Public Property Year As Integer?
            Public Property Season As String
            Public Property UserStatus As String = String.Empty
            Public Property CurrentEpisode As Integer
            Public Property PersonalScore As Double?
            Public Property Notes As String
            Public Property IsFavorite As Integer
            Public Property StartedAt As DateTime?
            Public Property FinishedAt As DateTime?
            Public Property UpdatedAt As DateTime?
        End Class

        Private Class AnimeGenreRow
            Public Property AnimeId As Long
            Public Property GenreName As String = String.Empty
        End Class

        Private Class ExistingUserAnimeRow
            Public Property Status As String = String.Empty
            Public Property CurrentEpisode As Integer
            Public Property PersonalScore As Double?
            Public Property Notes As String
            Public Property IsFavorite As Integer
            Public Property StartedAt As DateTime?
            Public Property FinishedAt As DateTime?
        End Class

    End Class
End Namespace
