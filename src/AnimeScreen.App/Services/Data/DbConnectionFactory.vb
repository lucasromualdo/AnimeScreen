Imports Microsoft.Data.Sqlite

Namespace Services.Data
    Public Class DbConnectionFactory
        Private ReadOnly _connectionString As String

        Public Sub New(databasePath As String)
            _connectionString = $"Data Source={databasePath};Mode=ReadWriteCreate;Cache=Shared"
        End Sub

        Public Async Function CreateOpenConnectionAsync() As Task(Of SqliteConnection)
            Dim connection = New SqliteConnection(_connectionString)
            Await connection.OpenAsync().ConfigureAwait(False)
            Return connection
        End Function
    End Class
End Namespace
