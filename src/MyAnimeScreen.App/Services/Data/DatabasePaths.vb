Imports System.IO

Namespace Services.Data
    Public Module DatabasePaths
        Public Function GetDatabasePath() As String
            Dim appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            Return Path.Combine(appDataPath, "MyAnimeScreen", "my_anime_screen.db")
        End Function

        Public Function GetSchemaPath() As String
            Return Path.Combine(AppContext.BaseDirectory, "Data", "sql", "schema.sql")
        End Function
    End Module
End Namespace
