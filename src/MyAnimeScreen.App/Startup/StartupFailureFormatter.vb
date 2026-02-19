Namespace Startup
    Public Module StartupFailureFormatter
        Public Function BuildMessage(stepDescription As String, ex As Exception) As String
            If ex Is Nothing Then
                Throw New ArgumentNullException(NameOf(ex))
            End If

            Dim normalizedStep = NormalizeStepDescription(stepDescription)
            Dim rootCause = GetRootCause(ex)
            Return $"Falha ao {normalizedStep}: {rootCause.Message}"
        End Function

        Private Function NormalizeStepDescription(stepDescription As String) As String
            If String.IsNullOrWhiteSpace(stepDescription) Then
                Return "inicializar o aplicativo"
            End If

            Return stepDescription.Trim()
        End Function

        Private Function GetRootCause(ex As Exception) As Exception
            Dim current = ex
            While current.InnerException IsNot Nothing
                current = current.InnerException
            End While

            Return current
        End Function
    End Module
End Namespace
