using System.IO;
using MyAnimeScreen.App.Models;
using MyAnimeScreen.App.Services.Data;
using Microsoft.Data.Sqlite;

namespace MyAnimeScreen.App.Tests.Data;

public sealed class UserAnimeRepositoryTests
{
    [Fact]
    public async Task UpsertAndGetByAnimeId_PersistsAndUpdatesValues()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new UserAnimeRepository(database.ConnectionFactory);
        var animeId = await database.SeedAnimeAsync(1001, "Cowboy Bebop");

        var first = NewEntry(animeId);
        var firstId = await repository.UpsertAsync(first);
        var saved = await repository.GetByAnimeIdAsync(animeId);

        Assert.NotNull(saved);
        Assert.Equal(firstId, saved!.Id);
        Assert.Equal(AnimeStatus.Assistindo, saved.Status);
        Assert.Equal(4, saved.CurrentEpisode);
        Assert.Equal(8.5, saved.PersonalScore);
        Assert.True(saved.IsFavorite);

        var updatedEntry = NewEntry(animeId);
        updatedEntry.Status = AnimeStatus.Concluido;
        updatedEntry.CurrentEpisode = 26;
        updatedEntry.PersonalScore = 9.2;
        updatedEntry.IsFavorite = false;

        var updatedId = await repository.UpsertAsync(updatedEntry);
        var updated = await repository.GetByAnimeIdAsync(animeId);

        Assert.NotNull(updated);
        Assert.Equal(firstId, updatedId);
        Assert.Equal(firstId, updated!.Id);
        Assert.Equal(AnimeStatus.Concluido, updated.Status);
        Assert.Equal(26, updated.CurrentEpisode);
        Assert.Equal(9.2, updated.PersonalScore);
        Assert.False(updated.IsFavorite);
    }

    [Fact]
    public async Task ListLibraryByStatusAsync_UsesJoinAndFavoriteOrdering()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new UserAnimeRepository(database.ConnectionFactory);
        var firstAnimeId = await database.SeedAnimeAsync(2001, "Steins;Gate");
        var secondAnimeId = await database.SeedAnimeAsync(2002, "Fullmetal Alchemist: Brotherhood");
        var otherStatusAnimeId = await database.SeedAnimeAsync(2003, "Haikyuu!!");
        Assert.NotEqual(firstAnimeId, secondAnimeId);

        var firstEntry = NewEntry(firstAnimeId);
        firstEntry.IsFavorite = false;
        await repository.UpsertAsync(firstEntry);

        var favoriteEntry = NewEntry(secondAnimeId);
        favoriteEntry.IsFavorite = true;
        await repository.UpsertAsync(favoriteEntry);

        var otherStatus = NewEntry(otherStatusAnimeId);
        otherStatus.Status = AnimeStatus.Pausado;
        await repository.UpsertAsync(otherStatus);

        var rows = await repository.ListLibraryByStatusAsync(AnimeStatus.Assistindo);

        Assert.Equal(2, rows.Count);
        Assert.Equal(secondAnimeId, rows[0].AnimeId);
        Assert.Equal("Fullmetal Alchemist: Brotherhood", rows[0].Title);
        Assert.True(rows[0].IsFavorite);
        Assert.Equal(firstAnimeId, rows[1].AnimeId);
    }

    [Fact]
    public async Task DeleteByAnimeIdAsync_RemovesEntry()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new UserAnimeRepository(database.ConnectionFactory);
        var animeId = await database.SeedAnimeAsync(3001, "Mushishi");
        await repository.UpsertAsync(NewEntry(animeId));

        var affected = await repository.DeleteByAnimeIdAsync(animeId);
        var deleted = await repository.GetByAnimeIdAsync(animeId);

        Assert.Equal(1, affected);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task GetByAnimeIdAsync_WhenStatusIsInvalid_ThrowsInvalidOperationException()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new UserAnimeRepository(database.ConnectionFactory);
        var animeId = await database.SeedAnimeAsync(4001, "Trigun");

        await database.InsertUserAnimeWithInvalidStatusAsync(animeId, "StatusInvalido");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GetByAnimeIdAsync(animeId));
        Assert.Contains("Status", error.Message);
    }

    private static UserAnime NewEntry(long animeId)
    {
        return new UserAnime
        {
            AnimeId = animeId,
            Status = AnimeStatus.Assistindo,
            CurrentEpisode = 4,
            PersonalScore = 8.5,
            Notes = "Teste",
            IsFavorite = true
        };
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
                "MyAnimeScreen.Tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directoryPath);
            var databasePath = Path.Combine(directoryPath, "test.db");
            var connectionFactory = new DbConnectionFactory(databasePath);
            var schemaPath = Path.Combine(AppContext.BaseDirectory, "Data", "sql", "schema.sql");

            await DatabaseInitializer.EnsureCreatedAsync(connectionFactory, schemaPath);
            return new TestDatabase(directoryPath, connectionFactory);
        }

        public async Task<long> SeedAnimeAsync(int malId, string title)
        {
            using var connection = await ConnectionFactory.CreateOpenConnectionAsync();
            using var command = connection.CreateCommand();
            command.CommandText =
                @"INSERT INTO animes (mal_id, title, updated_at)
                  VALUES (@MalId, @Title, datetime('now'));";

            var malIdParameter = command.CreateParameter();
            malIdParameter.ParameterName = "@MalId";
            malIdParameter.Value = malId;
            command.Parameters.Add(malIdParameter);

            var titleParameter = command.CreateParameter();
            titleParameter.ParameterName = "@Title";
            titleParameter.Value = title;
            command.Parameters.Add(titleParameter);

            await command.ExecuteNonQueryAsync();

            using var idCommand = connection.CreateCommand();
            idCommand.CommandText = "SELECT last_insert_rowid();";
            var scalar = await idCommand.ExecuteScalarAsync();
            return Convert.ToInt64(scalar, System.Globalization.CultureInfo.InvariantCulture);
        }

        public async Task InsertUserAnimeWithInvalidStatusAsync(long animeId, string rawStatus)
        {
            using var connection = await ConnectionFactory.CreateOpenConnectionAsync();

            using (var pragmaCommand = connection.CreateCommand())
            {
                pragmaCommand.CommandText = "PRAGMA ignore_check_constraints = ON;";
                await pragmaCommand.ExecuteNonQueryAsync();
            }

            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText =
                @"INSERT INTO user_anime (anime_id, status, current_episode, is_favorite, updated_at)
                  VALUES (@AnimeId, @Status, 0, 0, datetime('now'));";

            var animeIdParameter = insertCommand.CreateParameter();
            animeIdParameter.ParameterName = "@AnimeId";
            animeIdParameter.Value = animeId;
            insertCommand.Parameters.Add(animeIdParameter);

            var statusParameter = insertCommand.CreateParameter();
            statusParameter.ParameterName = "@Status";
            statusParameter.Value = rawStatus;
            insertCommand.Parameters.Add(statusParameter);

            await insertCommand.ExecuteNonQueryAsync();
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            TryDeleteDirectory(DirectoryPath);
            return ValueTask.CompletedTask;
        }

        private static void TryDeleteDirectory(string path)
        {
            TryDeleteDirectory(path, retriesRemaining: 3);
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
                // Cleanup best-effort para não quebrar execução de testes.
            }
        }
    }
}
