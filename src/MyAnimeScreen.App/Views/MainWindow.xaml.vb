Imports MyAnimeScreen.App.ViewModels

Namespace Views
    Public Class MainWindow
        Public Sub New(viewModel As MainViewModel)
            If viewModel Is Nothing Then
                Throw New ArgumentNullException(NameOf(viewModel))
            End If

            InitializeComponent()
            DataContext = viewModel
        End Sub
    End Class
End Namespace
