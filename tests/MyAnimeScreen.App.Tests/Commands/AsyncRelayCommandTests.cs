using MyAnimeScreen.App.Commands;

namespace MyAnimeScreen.App.Tests.Commands;

public sealed class AsyncRelayCommandTests
{
    [Fact]
    public async Task Execute_QuandoAcaoFalha_DeveNotificarObservadorSemPropagarExcecao()
    {
        var observed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new AsyncRelayCommand(
            execute: () => Task.FromException(new InvalidOperationException("falha controlada.")),
            onException: ex => observed.TrySetResult(ex));

        var thrown = Record.Exception(() => command.Execute(null));

        Assert.Null(thrown);

        var captured = await observed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsType<InvalidOperationException>(captured);
        Assert.Equal("falha controlada.", captured.Message);
    }

    [Fact]
    public async Task Execute_QuandoObservadorFalha_NaoDevePropagarExcecao()
    {
        var command = new AsyncRelayCommand(
            execute: () => Task.FromException(new InvalidOperationException("falha controlada.")),
            onException: _ => throw new InvalidOperationException("observer falhou."));

        var thrown = Record.Exception(() => command.Execute(null));

        Assert.Null(thrown);
        await Task.Delay(200);
    }
}
