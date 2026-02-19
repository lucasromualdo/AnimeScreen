Imports System.IO
Imports System.Windows
Imports MyAnimeScreen.App.Services.Api
Imports MyAnimeScreen.App.Services.Data
Imports MyAnimeScreen.App.Startup
Imports MyAnimeScreen.App.ViewModels
Imports MyAnimeScreen.App.Views

Class Application
    Private Async Sub Application_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
        Dim startupStep = "inicializar o aplicativo"

        Try
            startupStep = "resolver caminhos de dados locais"
            Dim databasePath = DatabasePaths.GetDatabasePath()
            Dim schemaPath = DatabasePaths.GetSchemaPath()

            startupStep = "preparar diretorio de dados local"
            Dim dataDirectory = Path.GetDirectoryName(databasePath)
            If Not String.IsNullOrWhiteSpace(dataDirectory) Then
                Directory.CreateDirectory(dataDirectory)
            End If

            startupStep = "inicializar repositorios locais"
            Dim connectionFactory = New DbConnectionFactory(databasePath)
            Dim animeApiClient = New JikanApiClient()
            Dim animeRepository = New AnimeRepository(connectionFactory)
            Dim userAnimeRepository = New UserAnimeRepository(connectionFactory)

            startupStep = "inicializar schema do banco local"
            Await DatabaseInitializer.EnsureCreatedAsync(
                connectionFactory,
                schemaPath
            ).ConfigureAwait(True)

            startupStep = "inicializar janela principal"
            Dim mainViewModel = New MainViewModel(animeApiClient, animeRepository, userAnimeRepository)
            Dim mainWindow = New MainWindow(mainViewModel)
            MainWindow = mainWindow
            mainWindow.Show()
        Catch ex As Exception
            Dim failureMessage = StartupFailureFormatter.BuildMessage(startupStep, ex)
            Dim failureCategory = StartupFailureFormatter.BuildCategory(ex)
            MessageBox.Show(
                $"{failureMessage}{Environment.NewLine}Categoria: {failureCategory}",
                "MyAnimeScreen",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            )

            Shutdown(-1)
        End Try
    End Sub
End Class
