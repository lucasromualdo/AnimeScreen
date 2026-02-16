Imports System.Globalization
Imports System.Windows.Data
Imports MyAnimeScreen.App.Models

Namespace Converters
    Public Class AnimeStatusToTextConverter
        Implements IValueConverter

        Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
            If TypeOf value Is AnimeStatus Then
                Select Case DirectCast(value, AnimeStatus)
                    Case AnimeStatus.QueroVer
                        Return "Quero ver"
                    Case AnimeStatus.Assistindo
                        Return "Assistindo"
                    Case AnimeStatus.Concluido
                        Return "Concluído"
                    Case AnimeStatus.Pausado
                        Return "Pausado"
                    Case AnimeStatus.Dropado
                        Return "Dropado"
                End Select
            End If

            Return "Desconhecido"
        End Function

        Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
            Throw New NotSupportedException()
        End Function
    End Class
End Namespace
