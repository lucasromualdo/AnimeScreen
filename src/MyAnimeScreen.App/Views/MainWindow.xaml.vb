Imports MyAnimeScreen.App.ViewModels

Namespace Views
    Public Class MainWindow
        Public Sub New()
            InitializeComponent()
            DataContext = New MainViewModel()
        End Sub
    End Class
End Namespace
