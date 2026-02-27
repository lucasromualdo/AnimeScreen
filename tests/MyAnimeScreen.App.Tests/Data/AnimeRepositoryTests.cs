using System.IO;
using Microsoft.Data.Sqlite;
using MyAnimeScreen.App.Models;
using MyAnimeScreen.App.Services.Data;

namespace MyAnimeScreen.App.Tests.Data;

public sealed class AnimeRepositoryTests
{
    [Fact]
    public async Task UpsertAsync_QuandoRecebeGeneros_PersisteERetornaNosMetodosDeConsulta()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new AnimeRepository(database.ConnectionFactory);

        var animeId = await repository.UpsertAsync(new Anime
        {
            MalId = 7001,
            Title = "Vinland Saga",
            Genres = new List<Genre>
            {
                new() { Name = "Action" },
                new() { Name = "Drama" },
                new() { Name = " action " }
            }
        });

        var byId = await repository.GetByIdAsync(animeId);
        Assert.NotNull(byId);
        Assert.Equal(animeId, byId!.Id);
        Assert.Equal(2, byId.Genres.Count);
        Assert.Contains(byId.Genres, x => x.Name == "Action");
        Assert.Contains(byId.Genres, x => x.Name == "Drama");

        var byMalId = await repository.GetByMalIdAsync(7001);
        Assert.NotNull(byMalId);
        Assert.Equal(2, byMalId!.Genres.Count);

        var searchRows = await repository.SearchByTitleAsync("Vinland");
        var searchResult = Assert.Single(searchRows);
        Assert.Equal(2, searchResult.Genres.Count);
    }

    [Fact]
    public async Task UpsertAsync_QuandoGenerosMudam_SubstituiRelacionamentosAntigos()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new AnimeRepository(database.ConnectionFactory);

        var firstId = await repository.UpsertAsync(new Anime
        {
            MalId = 7002,
            Title = "Mob Psycho 100",
            Genres = new List<Genre>
            {
                new() { Name = "Action" },
                new() { Name = "Comedy" }
            }
        });

        var updatedId = await repository.UpsertAsync(new Anime
        {
            MalId = 7002,
            Title = "Mob Psycho 100",
            Genres = new List<Genre>
            {
                new() { Name = "Supernatural" }
            }
        });

        Assert.Equal(firstId, updatedId);

        var reloaded = await repository.GetByMalIdAsync(7002);
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.Genres);
        Assert.Equal("Supernatural", reloaded.Genres[0].Name);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(string directoryPath, DbConnectionFactory connectionFactory)
        {
            DirectoryPath = directoryPath;
            ConnectionFactory = connectionFactory;
        }

        public string DirectoryPath { get; }

        public DbConnectionFactory ConnectionFactory { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var directoryPath = Path.Combine(
                Path.GetTempPath(),
                "MyAnimeScreen.AnimeRepository.Tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directoryPath);
            var databasePath = Path.Combine(directoryPath, "test.db");
            var connectionFactory = new DbConnectionFactory(databasePath);
            var schemaPath = Path.Combine(AppContext.BaseDirectory, "Data", "sql", "schema.sql");

            await DatabaseInitializer.EnsureCreatedAsync(connectionFactory, schemaPath);
            return new TestDatabase(directoryPath, connectionFactory);
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            TryDeleteDirectory(DirectoryPath, retriesRemaining: 3);
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
