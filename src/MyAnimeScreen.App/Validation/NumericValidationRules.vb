Imports System.Globalization
Imports System.Windows.Controls

Namespace Validation
    Public Class NonNegativeIntegerValidationRule
        Inherits ValidationRule

        Public Overrides Function Validate(value As Object, cultureInfo As CultureInfo) As ValidationResult
            Dim text = If(value, String.Empty).ToString().Trim()
            If String.IsNullOrWhiteSpace(text) Then
                Return New ValidationResult(False, "Informe um número inteiro.")
            End If

            Dim parsedValue As Integer
            If Not Integer.TryParse(text, NumberStyles.Integer, cultureInfo, parsedValue) Then
                Return New ValidationResult(False, "Use apenas números inteiros.")
            End If

            If parsedValue < 0 Then
                Return New ValidationResult(False, "O episódio atual deve ser maior ou igual a 0.")
            End If

            Return ValidationResult.ValidResult
        End Function
    End Class

    Public Class PersonalScoreValidationRule
        Inherits ValidationRule

        Public Overrides Function Validate(value As Object, cultureInfo As CultureInfo) As ValidationResult
            Dim text = If(value, String.Empty).ToString().Trim()
            If String.IsNullOrWhiteSpace(text) Then
                Return ValidationResult.ValidResult
            End If

            Const parseStyle As NumberStyles = NumberStyles.AllowLeadingSign Or NumberStyles.AllowDecimalPoint Or NumberStyles.AllowLeadingWhite Or NumberStyles.AllowTrailingWhite
            Dim parsedValue As Double
            If Not Double.TryParse(text, parseStyle, cultureInfo, parsedValue) AndAlso
               Not Double.TryParse(text, parseStyle, CultureInfo.InvariantCulture, parsedValue) Then
                Return New ValidationResult(False, "Use um número válido para nota.")
            End If

            If parsedValue < 0 OrElse parsedValue > 10 Then
                Return New ValidationResult(False, "A nota pessoal deve ficar entre 0 e 10.")
            End If

            Return ValidationResult.ValidResult
        End Function
    End Class
End Namespace
