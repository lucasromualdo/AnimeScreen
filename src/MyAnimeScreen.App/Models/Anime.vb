Imports System.Collections.Generic

Namespace Models
    Public Class Anime
        Public Property Id As Long
        Public Property MalId As Integer
        Public Property Title As String = String.Empty
        Public Property TitleJp As String
        Public Property Synopsis As String
        Public Property ImageUrl As String
        Public Property EpisodesTotal As Integer?
        Public Property Score As Double?
        Public Property Year As Integer?
        Public Property Season As String
        Public Property Genres As IReadOnlyList(Of Genre) = Array.Empty(Of Genre)()
        Public Property CreatedAt As DateTime?
        Public Property UpdatedAt As DateTime?
    End Class
End Namespace
