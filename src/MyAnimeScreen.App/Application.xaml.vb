Imports System.IO
Imports System.Windows
Imports MyAnimeScreen.App.Services.Api
Imports MyAnimeScreen.App.Services.Data
Imports MyAnimeScreen.App.ViewModels
Imports MyAnimeScreen.App.Views

Class Application
    Private Async Sub Application_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
        Try
            Dim databasePath = DatabasePaths.GetDatabasePath()
            Dim schemaPath = DatabasePaths.GetSchemaPath()

            Dim dataDirectory = Path.GetDirectoryName(databasePath)
            If Not String.IsNullOrWhiteSpace(dataDirectory) Then
                Directory.CreateDirectory(dataDirectory)
            End If

            Dim connectionFactory = New DbConnectionFactory(databasePath)
            Dim animeApiClient = New JikanApiClient()
            Dim animeRepository = New AnimeRepository(connectionFactory)
            Dim userAnimeRepository = New UserAnimeRepository(connectionFactory)

            Await DatabaseInitializer.EnsureCreatedAsync(
                connectionFactory,
                schemaPath
            ).ConfigureAwait(True)

            Dim mainViewModel = New MainViewModel(animeApiClient, animeRepository, userAnimeRepository)
            Dim mainWindow = New MainWindow(mainViewModel)
            MainWindow = mainWindow
            mainWindow.Show()
        Catch ex As Exception
            MessageBox.Show(
                $"Falha ao inicializar o banco de dados: {ex.Message}",
                "MyAnimeScreen",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            )

            Shutdown(-1)
        End Try
    End Sub
End Class
