Imports System.IO
Imports System.Windows
Imports AnimeScreen.App.Services.Data

Class Application
    Private Async Sub Application_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
        Try
            Dim databasePath = DatabasePaths.GetDatabasePath()
            Dim schemaPath = DatabasePaths.GetSchemaPath()

            Dim dataDirectory = Path.GetDirectoryName(databasePath)
            If Not String.IsNullOrWhiteSpace(dataDirectory) Then
                Directory.CreateDirectory(dataDirectory)
            End If

            AppServices.ConnectionFactory = New DbConnectionFactory(databasePath)
            AppServices.AnimeRepository = New AnimeRepository(AppServices.ConnectionFactory)
            AppServices.UserAnimeRepository = New UserAnimeRepository(AppServices.ConnectionFactory)

            Await DatabaseInitializer.EnsureCreatedAsync(
                AppServices.ConnectionFactory,
                schemaPath
            ).ConfigureAwait(True)
        Catch ex As Exception
            MessageBox.Show(
                $"Falha ao inicializar o banco de dados: {ex.Message}",
                "AnimeScreen",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            )

            Shutdown(-1)
        End Try
    End Sub
End Class
