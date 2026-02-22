Imports MyAnimeScreen.App.ViewModels
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media

Namespace Views
    Public Class MainWindow
        Public Sub New(viewModel As MainViewModel)
            If viewModel Is Nothing Then
                Throw New ArgumentNullException(NameOf(viewModel))
            End If

            InitializeComponent()
            DataContext = viewModel
        End Sub

        Private Sub OnCoverImageFailed(sender As Object, e As ExceptionRoutedEventArgs)
            Dim imageControl = TryCast(sender, Image)
            If imageControl Is Nothing Then
                Return
            End If

            imageControl.Visibility = Visibility.Collapsed
        End Sub

        Private Sub OnCoverImageDataContextChanged(sender As Object, e As DependencyPropertyChangedEventArgs)
            Dim imageControl = TryCast(sender, Image)
            If imageControl Is Nothing Then
                Return
            End If

            imageControl.Visibility = Visibility.Visible
        End Sub
    End Class
End Namespace
