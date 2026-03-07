using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MyAnimeScreen.App.Models;
using MyAnimeScreen.App.Services.Data;

namespace MyAnimeScreen.App.Tests.Data;

public sealed class LibraryTransferServiceTests
{
    [Fact]
    public async Task ExportAsJsonAsync_ExportsUserLibrary()
    {
        await using var database = await TestDatabase.CreateAsync();
        var animeId = await database.SeedAnimeAsync(70101, "Cowboy Bebop");
        await database.UserAnimeRepository.UpsertAsync(new UserAnime
        {
            AnimeId = animeId,
            Status = AnimeStatus.Assistindo,
            CurrentEpisode = 8,
            PersonalScore = 9.2,
            IsFavorite = true
        });

        var outputPath = Path.Combine(database.RootPath, "library.json");
        var exportedCount = await database.LibraryTransferService.ExportAsJsonAsync(outputPath);

        Assert.Equal(1, exportedCount);
        Assert.True(File.Exists(outputPath));

        var json = await File.ReadAllTextAsync(outputPath, Encoding.UTF8);
        Assert.Contains("\"entries\"", json, StringComparison.Ordinal);
        Assert.Contains("Cowboy Bebop", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsCsvAsync_WritesHeaderAndRows()
    {
        await using var database = await TestDatabase.CreateAsync();
        var animeId = await database.SeedAnimeAsync(70102, "Samurai Champloo");
        await database.UserAnimeRepository.UpsertAsync(new UserAnime
        {
            AnimeId = animeId,
            Status = AnimeStatus.QueroVer
        });

        var outputPath = Path.Combine(database.RootPath, "library.csv");
        var exportedCount = await database.LibraryTransferService.ExportAsCsvAsync(outputPath);

        Assert.Equal(1, exportedCount);
        Assert.True(File.Exists(outputPath));

        var lines = await File.ReadAllLinesAsync(outputPath, Encoding.UTF8);
        Assert.NotEmpty(lines);
        Assert.StartsWith("anime_id,anime_mal_id,title", lines[0], StringComparison.Ordinal);
        Assert.True(lines.Length >= 2);
    }

    [Fact]
    public async Task ImportAsync_WhenJsonHasConflictMergeAndInvalid_ReturnsAccurateSummary()
    {
        await using var database = await TestDatabase.CreateAsync();
        var updatedAnimeId = await database.SeedAnimeAsync(70201, "Steins;Gate");
        var ignoredAnimeId = await database.SeedAnimeAsync(70202, "Violet Evergarden");

        await database.UserAnimeRepository.UpsertAsync(new UserAnime
        {
            AnimeId = updatedAnimeId,
            Status = AnimeStatus.Assistindo,
            CurrentEpisode = 3,
            PersonalScore = 8.0,
            Notes = "old",
            IsFavorite = false
        });

        await database.UserAnimeRepository.UpsertAsync(new UserAnime
        {
            AnimeId = ignoredAnimeId,
            Status = AnimeStatus.QueroVer,
            CurrentEpisode = 0,
            PersonalScore = null,
            Notes = null,
            IsFavorite = false
        });

        var importPath = Path.Combine(database.RootPath, "import.json");
        var payload = BuildJsonPayload(new[]
        {
            BuildEntry(
                animeId: updatedAnimeId,
                animeMalId: 70201,
                title: "Steins;Gate",
                status: "Concluido",
                currentEpisode: 24,
                personalScore: 9.4,
                notes: "updated",
                isFavorite: true),
            BuildEntry(
                animeId: ignoredAnimeId,
                animeMalId: 70202,
                title: "Violet Evergarden",
                status: "QueroVer",
                currentEpisode: 0,
                personalScore: null,
                notes: null,
                isFavorite: false),
            BuildEntry(
                animeId: 99001,
                animeMalId: 70901,
                title: "Mob Psycho 100",
                status: "Assistindo",
                currentEpisode: 5,
                personalScore: 8.7,
                notes: "new",
                isFavorite: false),
            BuildEntry(
                animeId: 99002,
                animeMalId: 70201,
                title: "Conflict Row",
                status: "Assistindo",
                currentEpisode: 1,
                personalScore: null,
                notes: null,
                isFavorite: false)
        });

        await File.WriteAllTextAsync(importPath, payload, Encoding.UTF8);

        var summary = await database.LibraryTransferService.ImportAsync(importPath);

        Assert.Equal(1, summary.NewEntries);
        Assert.Equal(1, summary.UpdatedEntries);
        Assert.Equal(1, summary.IgnoredEntries);
        Assert.Equal(1, summary.InvalidEntries);

        var updated = await database.UserAnimeRepository.GetByAnimeIdAsync(updatedAnimeId);
        Assert.NotNull(updated);
        Assert.Equal(AnimeStatus.Concluido, updated!.Status);
        Assert.Equal(24, updated.CurrentEpisode);
        Assert.Equal(9.4, updated.PersonalScore);
        Assert.True(updated.IsFavorite);

        var added = await database.UserAnimeRepository.GetByAnimeIdAsync(99001);
        Assert.NotNull(added);
        Assert.Equal(AnimeStatus.Assistindo, added!.Status);

        var conflicted = await database.UserAnimeRepository.GetByAnimeIdAsync(99002);
        Assert.Null(conflicted);
    }

    [Fact]
    public async Task ImportAsync_WhenCsvHeaderIsInvalid_ThrowsInvalidDataException()
    {
        await using var database = await TestDatabase.CreateAsync();
        var csvPath = Path.Combine(database.RootPath, "invalid.csv");
        await File.WriteAllTextAsync(csvPath, "foo,bar\r\n1,2\r\n", Encoding.UTF8);

        await Assert.ThrowsAsync<InvalidDataException>(() => database.LibraryTransferService.ImportAsync(csvPath));
    }

    [Fact]
    public async Task ImportAsync_WhenImportedTwice_IsIdempotent()
    {
        await using var source = await TestDatabase.CreateAsync();
        var animeId = await source.SeedAnimeAsync(70301, "Fullmetal Alchemist: Brotherhood");
        await source.UserAnimeRepository.UpsertAsync(new UserAnime
        {
            AnimeId = animeId,
            Status = AnimeStatus.Assistindo,
            CurrentEpisode = 12,
            PersonalScore = 9.8,
            Notes = "source",
            IsFavorite = true
        });

        var exportPath = Path.Combine(source.RootPath, "portable.json");
        await source.LibraryTransferService.ExportAsJsonAsync(exportPath);

        await using var target = await TestDatabase.CreateAsync();
        var firstImport = await target.LibraryTransferService.ImportAsync(exportPath);
        var secondImport = await target.LibraryTransferService.ImportAsync(exportPath);

        Assert.Equal(1, firstImport.NewEntries);
        Assert.Equal(0, firstImport.UpdatedEntries);
        Assert.Equal(0, firstImport.IgnoredEntries);
        Assert.Equal(0, firstImport.InvalidEntries);

        Assert.Equal(0, secondImport.NewEntries);
        Assert.Equal(0, secondImport.UpdatedEntries);
        Assert.Equal(1, secondImport.IgnoredEntries);
        Assert.Equal(0, secondImport.InvalidEntries);
    }

    private static Dictionary<string, object?> BuildEntry(
        long animeId,
        int animeMalId,
        string title,
        string status,
        int currentEpisode,
        double? personalScore,
        string? notes,
        bool isFavorite)
    {
        return new Dictionary<string, object?>
        {
            ["anime_id"] = animeId,
            ["anime_mal_id"] = animeMalId,
            ["title"] = title,
            ["title_jp"] = null,
            ["synopsis"] = null,
            ["image_url"] = null,
            ["episodes_total"] = null,
            ["score"] = null,
            ["year"] = null,
            ["season"] = null,
            ["genres"] = Array.Empty<string>(),
            ["user_status"] = status,
            ["current_episode"] = currentEpisode,
            ["personal_score"] = personalScore,
            ["notes"] = notes,
            ["is_favorite"] = isFavorite,
            ["started_at"] = null,
            ["finished_at"] = null,
            ["updated_at"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };
    }

    private static string BuildJsonPayload(IEnumerable<Dictionary<string, object?>> entries)
    {
        var payload = new Dictionary<string, object?>
        {
            ["version"] = 1,
            ["exported_at_utc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            ["entries"] = entries
        };

        return JsonSerializer.Serialize(payload);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(
            string rootPath,
            DbConnectionFactory connectionFactory,
            AnimeRepository animeRepository,
            UserAnimeRepository userAnimeRepository,
            LibraryTransferService libraryTransferService)
        {
            RootPath = rootPath;
            ConnectionFactory = connectionFactory;
            AnimeRepository = animeRepository;
            UserAnimeRepository = userAnimeRepository;
            LibraryTransferService = libraryTransferService;
        }

        public string RootPath { get; }

        public DbConnectionFactory ConnectionFactory { get; }

        public AnimeRepository AnimeRepository { get; }

        public UserAnimeRepository UserAnimeRepository { get; }

        public LibraryTransferService LibraryTransferService { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var rootPath = Path.Combine(
                Path.GetTempPath(),
                "MyAnimeScreen.LibraryTransfer.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);

            var dbPath = Path.Combine(rootPath, "library_transfer.db");
            var connectionFactory = new DbConnectionFactory(dbPath);
            var schemaPath = Path.Combine(AppContext.BaseDirectory, "Data", "sql", "schema.sql");
            await DatabaseInitializer.EnsureCreatedAsync(connectionFactory, schemaPath);

            var animeRepository = new AnimeRepository(connectionFactory);
            var userAnimeRepository = new UserAnimeRepository(connectionFactory);
            var libraryTransferService = new LibraryTransferService(connectionFactory);

            return new TestDatabase(
                rootPath,
                connectionFactory,
                animeRepository,
                userAnimeRepository,
                libraryTransferService);
        }

        public async Task<long> SeedAnimeAsync(int malId, string title)
        {
            return await AnimeRepository.UpsertAsync(new Anime
            {
                MalId = malId,
                Title = title
            });
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            TryDeleteDirectory(RootPath, retriesRemaining: 3);
            return ValueTask.CompletedTask;
        }

        private static void TryDeleteDirectory(string path, int retriesRemaining)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch when (retriesRemaining > 0)
            {
                Thread.Sleep(50);
                TryDeleteDirectory(path, retriesRemaining - 1);
            }
            catch
            {
                // Cleanup best-effort.
            }
        }
    }
}
