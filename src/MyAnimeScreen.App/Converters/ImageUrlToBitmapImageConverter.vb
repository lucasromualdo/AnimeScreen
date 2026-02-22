Imports System.Globalization
Imports System.Windows.Data
Imports System.Windows.Media.Imaging

Namespace Converters
    Public Class ImageUrlToBitmapImageConverter
        Implements IValueConverter

        Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
            Dim imageUrl = TryCast(value, String)
            If String.IsNullOrWhiteSpace(imageUrl) Then
                Return Nothing
            End If

            Dim uri As Uri = Nothing
            If Not Uri.TryCreate(imageUrl, UriKind.Absolute, uri) Then
                Return Nothing
            End If

            If uri.Scheme <> Uri.UriSchemeHttp AndAlso uri.Scheme <> Uri.UriSchemeHttps Then
                Return Nothing
            End If

            Try
                Dim bitmap = New BitmapImage()
                bitmap.BeginInit()
                bitmap.UriSource = uri
                bitmap.CacheOption = BitmapCacheOption.OnDemand
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile

                Dim decodeWidth As Integer
                If parameter IsNot Nothing AndAlso Integer.TryParse(parameter.ToString(), decodeWidth) AndAlso decodeWidth > 0 Then
                    bitmap.DecodePixelWidth = decodeWidth
                End If

                bitmap.EndInit()

                If bitmap.CanFreeze Then
                    bitmap.Freeze()
                End If

                Return bitmap
            Catch
                Return Nothing
            End Try
        End Function

        Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
            Throw New NotSupportedException()
        End Function
    End Class
End Namespace
