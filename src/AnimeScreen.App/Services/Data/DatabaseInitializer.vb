Imports System.IO

Namespace Services.Data
    Public Module DatabaseInitializer
        Public Async Function EnsureCreatedAsync(connectionFactory As DbConnectionFactory, schemaPath As String) As Task
            If Not File.Exists(schemaPath) Then
                Throw New FileNotFoundException("Arquivo de schema não encontrado.", schemaPath)
            End If

            Dim schemaSql = Await File.ReadAllTextAsync(schemaPath).ConfigureAwait(False)
            If String.IsNullOrWhiteSpace(schemaSql) Then
                Throw New InvalidDataException("Arquivo de schema está vazio.")
            End If

            Using connection = Await connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(False)
                Using command = connection.CreateCommand()
                    command.CommandText = schemaSql
                    Await command.ExecuteNonQueryAsync().ConfigureAwait(False)
                End Using
            End Using
        End Function
    End Module
End Namespace
