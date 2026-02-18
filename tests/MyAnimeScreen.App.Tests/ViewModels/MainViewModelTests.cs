using System.Diagnostics;
using System.IO;
using Microsoft.Data.Sqlite;
using MyAnimeScreen.App.Models;
using MyAnimeScreen.App.Services.Api;
using MyAnimeScreen.App.Services.Data;
using MyAnimeScreen.App.ViewModels;

namespace MyAnimeScreen.App.Tests.ViewModels;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task SearchCommand_CanExecuteDependeDoEstadoDaConsulta()
    {
        await using var db = await TestDatabase.CreateAsync();
        var vm = new MainViewModel(new FakeAnimeApiClient(), db.AnimeRepository, db.UserAnimeRepository);

        Assert.False(vm.SearchCommand.CanExecute(null));

        vm.Query = "One Piece";
        Assert.True(vm.SearchCommand.CanExecute(null));
    }

    [Fact]
    public async Task SearchCommand_BloqueiaReentradaEnquantoBuscaEmAndamento()
    {
        await using var db = await TestDatabase.CreateAsync();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var apiClient = new FakeAnimeApiClient
        {
            SearchGate = gate,
            SearchResult = new List<Anime>
            {
                new()
                {
                    MalId = 21,
                    Title = "One Piece"
                }
            }
        };

        var vm = new MainViewModel(apiClient, db.AnimeRepository, db.UserAnimeRepository)
        {
            Query = "One Piece"
        };

        vm.SearchCommand.Execute(null);
        await WaitUntilAsync(() => vm.IsLoading);

        Assert.False(vm.SearchCommand.CanExecute(null));
        vm.SearchCommand.Execute(null);

        gate.SetResult(true);
        await WaitForBackgroundWorkAsync(vm);

        Assert.Equal(1, apiClient.SearchCallCount);
        Assert.Single(vm.Results);
        Assert.True(vm.SearchCommand.CanExecute(null));
    }

    [Fact]
    public async Task SearchCommand_QuandoApiRetornaResultados_AtualizaResultadosEPersisteLocalmente()
    {
        await using var db = await TestDatabase.CreateAsync();
        var apiClient = new FakeAnimeApiClient
        {
            SearchResult = new List<Anime>
            {
                new()
                {
                    MalId = 5114,
                    Title = "Fullmetal Alchemist: Brotherhood",
                    EpisodesTotal = 64,
                    Score = 9.1
                }
            }
        };

        var vm = new MainViewModel(apiClient, db.AnimeRepository, db.UserAnimeRepository)
        {
            Query = "Fullmetal"
        };

        vm.SearchCommand.Execute(null);
        await WaitForBackgroundWorkAsync(vm);

        Assert.Single(vm.Results);
        Assert.Equal("Fullmetal Alchemist: Brotherhood", vm.Results[0].Title);
        Assert.NotNull(vm.SelectedAnime);
        Assert.True(vm.SelectedAnime!.Id > 0);

        var persisted = await db.AnimeRepository.GetByMalIdAsync(5114);
        Assert.NotNull(persisted);
        Assert.Equal("Fullmetal Alchemist: Brotherhood", persisted!.Title);
    }

    [Fact]
    public async Task SearchCommand_QuandoApiFalha_PreencheMensagemDeErroELimpaResultados()
    {
        await using var db = await TestDatabase.CreateAsync();
        var apiClient = new FakeAnimeApiClient
        {
            SearchException = new HttpRequestException("Servico indisponivel.")
        };

        var vm = new MainViewModel(apiClient, db.AnimeRepository, db.UserAnimeRepository)
        {
            Query = "Naruto"
        };

        vm.SearchCommand.Execute(null);
        await WaitForBackgroundWorkAsync(vm);

        Assert.Empty(vm.Results);
        Assert.Null(vm.SelectedAnime);
        Assert.Contains("Falha na busca", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveToMyListCommand_SalvaEntradaEAtualizaBibliotecaLocal()
    {
        await using var db = await TestDatabase.CreateAsync();
        var apiClient = new FakeAnimeApiClient();
        var animeId = await db.SeedAnimeAsync(9253, "Steins;Gate");
        var selectedAnime = await db.AnimeRepository.GetByIdAsync(animeId);
        Assert.NotNull(selectedAnime);

        var vm = new MainViewModel(apiClient, db.AnimeRepository, db.UserAnimeRepository)
        {
            SelectedAnime = selectedAnime!,
            LibraryFilterStatus = AnimeStatus.Assistindo,
            UserStatus = AnimeStatus.Assistindo,
            CurrentEpisode = 12,
            PersonalScore = 9.5,
            IsFavorite = true,
            UserNotes = "Muito bom."
        };

        vm.SaveToMyListCommand.Execute(null);
        await WaitForBackgroundWorkAsync(vm);

        var saved = await db.UserAnimeRepository.GetByAnimeIdAsync(animeId);
        Assert.NotNull(saved);
        Assert.Equal(AnimeStatus.Assistindo, saved!.Status);
        Assert.Equal(12, saved.CurrentEpisode);
        Assert.Equal(9.5, saved.PersonalScore);
        Assert.True(saved.IsFavorite);
        Assert.Contains(vm.LibraryItems, x => x.AnimeId == animeId);
    }

    [Fact]
    public async Task RemoveFromMyListCommand_RemoveEntradaELimpaBibliotecaDoFiltroAtual()
    {
        await using var db = await TestDatabase.CreateAsync();
        var apiClient = new FakeAnimeApiClient();
        var animeId = await db.SeedAnimeAsync(16498, "Shingeki no Kyojin");
        await db.UserAnimeRepository.UpsertAsync(new UserAnime
        {
            AnimeId = animeId,
            Status = AnimeStatus.QueroVer,
            CurrentEpisode = 0,
            PersonalScore = null,
            IsFavorite = false
        });

        var selectedAnime = await db.AnimeRepository.GetByIdAsync(animeId);
        Assert.NotNull(selectedAnime);

        var vm = new MainViewModel(apiClient, db.AnimeRepository, db.UserAnimeRepository)
        {
            LibraryFilterStatus = AnimeStatus.QueroVer,
            SelectedAnime = selectedAnime!
        };

        await WaitForBackgroundWorkAsync(vm);
        Assert.Contains(vm.LibraryItems, x => x.AnimeId == animeId);

        vm.RemoveFromMyListCommand.Execute(null);
        await WaitForBackgroundWorkAsync(vm);

        var removed = await db.UserAnimeRepository.GetByAnimeIdAsync(animeId);
        Assert.Null(removed);
        Assert.DoesNotContain(vm.LibraryItems, x => x.AnimeId == animeId);
    }

    [Fact]
    public async Task SaveToMyListCommand_CanExecuteDependeDoAnimeSelecionado()
    {
        await using var db = await TestDatabase.CreateAsync();
        var vm = new MainViewModel(new FakeAnimeApiClient(), db.AnimeRepository, db.UserAnimeRepository);

        Assert.False(vm.SaveToMyListCommand.CanExecute(null));

        vm.SelectedAnime = NewAnime(30276, "One Punch Man");
        Assert.True(vm.SaveToMyListCommand.CanExecute(null));
    }

    [Fact]
    public async Task SelectedLibraryItem_QuandoDefinido_CarregaAnimeSelecionado()
    {
        await using var db = await TestDatabase.CreateAsync();
        var animeId = await db.SeedAnimeAsync(44511, "Chainsaw Man");
        await db.UserAnimeRepository.UpsertAsync(new UserAnime
        {
            AnimeId = animeId,
            Status = AnimeStatus.QueroVer
        });

        var vm = new MainViewModel(new FakeAnimeApiClient(), db.AnimeRepository, db.UserAnimeRepository)
        {
            LibraryFilterStatus = AnimeStatus.QueroVer
        };

        await WaitForBackgroundWorkAsync(vm);
        var item = Assert.Single(vm.LibraryItems);

        Assert.True(vm.OpenLibraryItemCommand.CanExecute(item));
        vm.SelectedLibraryItem = item;
        await WaitForBackgroundWorkAsync(vm);

        Assert.NotNull(vm.SelectedAnime);
        Assert.Equal(item.AnimeId, vm.SelectedAnime!.Id);
    }

    [Fact]
    public async Task PersonalScore_QuandoForaDoIntervalo_LancaExcecao()
    {
        await using var db = await TestDatabase.CreateAsync();
        var vm = new MainViewModel(new FakeAnimeApiClient(), db.AnimeRepository, db.UserAnimeRepository);

        Assert.Throws<ArgumentOutOfRangeException>(() => vm.PersonalScore = -0.1);
        Assert.Throws<ArgumentOutOfRangeException>(() => vm.PersonalScore = 10.1);
    }

    [Fact]
    public async Task SelectedLibraryItem_QuandoAnimeNaoExiste_ExibeMensagemCorretaSemMojibake()
    {
        await using var db = await TestDatabase.CreateAsync();
        var vm = new MainViewModel(new FakeAnimeApiClient(), db.AnimeRepository, db.UserAnimeRepository);

        vm.SelectedLibraryItem = new LibraryAnimeItem
        {
            AnimeId = 999_999,
            Title = "Inexistente",
            Status = AnimeStatus.QueroVer
        };

        await WaitUntilAsync(() => vm.HasError);

        Assert.Equal("Anime selecionado não foi encontrado na base local.", vm.ErrorMessage);
        Assert.DoesNotContain("Ã", vm.ErrorMessage, StringComparison.Ordinal);
    }

    private static async Task WaitForBackgroundWorkAsync(MainViewModel vm, int timeoutMs = 5000)
    {
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);
        var sw = Stopwatch.StartNew();
        var stableCycles = 0;

        while (sw.Elapsed < timeout)
        {
            if (!vm.IsLoading && !vm.IsLibraryLoading)
            {
                stableCycles++;
                if (stableCycles >= 3)
                {
                    return;
                }
            }
            else
            {
                stableCycles = 0;
            }

            await Task.Delay(40);
        }

        throw new TimeoutException("Timeout aguardando finalizacao de operacoes async do MainViewModel.");
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("Condicao esperada nao foi atingida no tempo limite.");
    }

    private static Anime NewAnime(int malId, string title)
    {
        return new Anime
        {
            MalId = malId,
            Title = title
        };
    }

    private sealed class FakeAnimeApiClient : IAnimeApiClient
    {
        public IReadOnlyList<Anime> SearchResult { get; init; } = Array.Empty<Anime>();

        public Exception? SearchException { get; init; }

        public TaskCompletionSource<bool>? SearchGate { get; init; }

        public int SearchCallCount { get; private set; }

        public Task<Anime> GetByMalIdAsync(int malId)
        {
            return Task.FromResult(new Anime { MalId = malId, Title = $"Anime {malId}" });
        }

        public async Task<IReadOnlyList<Anime>> SearchAsync(string title, int maxRows = 25)
        {
            SearchCallCount++;

            if (SearchGate is not null)
            {
                await SearchGate.Task;
            }

            if (SearchException is not null)
            {
                throw SearchException;
            }

            return SearchResult;
        }
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(string rootPath, DbConnectionFactory connectionFactory, AnimeRepository animeRepository, UserAnimeRepository userAnimeRepository)
        {
            RootPath = rootPath;
            ConnectionFactory = connectionFactory;
            AnimeRepository = animeRepository;
            UserAnimeRepository = userAnimeRepository;
        }

        public string RootPath { get; }

        public DbConnectionFactory ConnectionFactory { get; }

        public AnimeRepository AnimeRepository { get; }

        public UserAnimeRepository UserAnimeRepository { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "MyAnimeScreen.MainViewModel.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);

            var dbPath = Path.Combine(rootPath, "main_vm.db");
            var connectionFactory = new DbConnectionFactory(dbPath);
            var schemaPath = Path.Combine(AppContext.BaseDirectory, "Data", "sql", "schema.sql");
            await DatabaseInitializer.EnsureCreatedAsync(connectionFactory, schemaPath);

            var animeRepository = new AnimeRepository(connectionFactory);
            var userAnimeRepository = new UserAnimeRepository(connectionFactory);
            return new TestDatabase(rootPath, connectionFactory, animeRepository, userAnimeRepository);
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
