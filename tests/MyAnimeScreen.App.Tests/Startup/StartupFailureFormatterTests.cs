using MyAnimeScreen.App.Startup;

namespace MyAnimeScreen.App.Tests.Startup;

public sealed class StartupFailureFormatterTests
{
    [Fact]
    public void BuildMessage_QuandoEtapaInformada_DeveIncluirEtapaERaizDoErro()
    {
        var root = new InvalidOperationException("schema ausente.");
        var wrapped = new Exception("falha generica.", root);

        var message = StartupFailureFormatter.BuildMessage("inicializar schema do banco local", wrapped);

        Assert.Equal("Falha ao inicializar schema do banco local: schema ausente.", message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BuildMessage_QuandoEtapaNaoInformada_DeveUsarFallback(string? stepDescription)
    {
        var ex = new Exception("erro qualquer.");

        var message = StartupFailureFormatter.BuildMessage(stepDescription!, ex);

        Assert.Equal("Falha ao inicializar o aplicativo: erro qualquer.", message);
    }

    [Fact]
    public void BuildMessage_QuandoExcecaoNula_DeveLancarArgumentNullException()
    {
        var error = Assert.Throws<ArgumentNullException>(() => StartupFailureFormatter.BuildMessage("inicializar janela principal", null!));

        Assert.Equal("ex", error.ParamName);
    }

    [Fact]
    public void BuildCategory_QuandoErroDeDadosLocais_DeveRetornarDadosLocais()
    {
        var root = new FileNotFoundException("schema ausente.");
        var wrapped = new Exception("falha generica.", root);

        var category = StartupFailureFormatter.BuildCategory(wrapped);

        Assert.Equal("DadosLocais", category);
    }

    [Fact]
    public void BuildCategory_QuandoErroDeRedeAninhado_DeveRetornarRede()
    {
        var root = new HttpRequestException("timeout.");
        var wrapped = new Exception("falha generica.", new InvalidOperationException("wrapper.", root));

        var category = StartupFailureFormatter.BuildCategory(wrapped);

        Assert.Equal("Rede", category);
    }

    [Fact]
    public void BuildCategory_QuandoErroNaoMapeado_DeveRetornarAplicacao()
    {
        var ex = new InvalidOperationException("erro qualquer.");

        var category = StartupFailureFormatter.BuildCategory(ex);

        Assert.Equal("Aplicacao", category);
    }

    [Fact]
    public void BuildCategory_QuandoExcecaoNula_DeveLancarArgumentNullException()
    {
        var error = Assert.Throws<ArgumentNullException>(() => StartupFailureFormatter.BuildCategory(null!));

        Assert.Equal("ex", error.ParamName);
    }
}
