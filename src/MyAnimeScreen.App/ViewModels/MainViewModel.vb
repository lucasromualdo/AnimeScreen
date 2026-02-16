Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Runtime.CompilerServices
Imports System.Threading.Tasks
Imports System.Windows.Input
Imports MyAnimeScreen.App.Commands
Imports MyAnimeScreen.App.Models

Namespace ViewModels
    Public Class MainViewModel
        Implements INotifyPropertyChanged

        Private ReadOnly _searchCommand As RelayCommand
        Private _query As String = String.Empty
        Private _selectedAnime As Anime
        Private _isLoading As Boolean
        Private _errorMessage As String = String.Empty

        Public Sub New()
            Results = New ObservableCollection(Of Anime)()
            _searchCommand = New RelayCommand(AddressOf ExecuteSearch, AddressOf CanExecuteSearch)
        End Sub

        Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

        Public Property Query As String
            Get
                Return _query
            End Get
            Set(value As String)
                Dim normalizedValue = If(value, String.Empty)
                If String.Equals(_query, normalizedValue, StringComparison.Ordinal) Then
                    Return
                End If

                _query = normalizedValue
                OnPropertyChanged()
                _searchCommand.RaiseCanExecuteChanged()
            End Set
        End Property

        Public ReadOnly Property Results As ObservableCollection(Of Anime)

        Public Property SelectedAnime As Anime
            Get
                Return _selectedAnime
            End Get
            Set(value As Anime)
                If Object.ReferenceEquals(_selectedAnime, value) Then
                    Return
                End If

                _selectedAnime = value
                OnPropertyChanged()
            End Set
        End Property

        Public Property IsLoading As Boolean
            Get
                Return _isLoading
            End Get
            Set(value As Boolean)
                If _isLoading = value Then
                    Return
                End If

                _isLoading = value
                OnPropertyChanged()
                _searchCommand.RaiseCanExecuteChanged()
            End Set
        End Property

        Public Property ErrorMessage As String
            Get
                Return _errorMessage
            End Get
            Set(value As String)
                Dim normalizedValue = If(value, String.Empty)
                If String.Equals(_errorMessage, normalizedValue, StringComparison.Ordinal) Then
                    Return
                End If

                _errorMessage = normalizedValue
                OnPropertyChanged()
                OnPropertyChanged(NameOf(HasError))
            End Set
        End Property

        Public ReadOnly Property HasError As Boolean
            Get
                Return Not String.IsNullOrWhiteSpace(ErrorMessage)
            End Get
        End Property

        Public ReadOnly Property SearchCommand As ICommand
            Get
                Return _searchCommand
            End Get
        End Property

        Private Function CanExecuteSearch(parameter As Object) As Boolean
            Return (Not IsLoading) AndAlso (Not String.IsNullOrWhiteSpace(Query))
        End Function

        Private Async Sub ExecuteSearch(parameter As Object)
            Await SearchAsync().ConfigureAwait(True)
        End Sub

        Private Async Function SearchAsync() As Task
            IsLoading = True
            ErrorMessage = String.Empty

            Try
                Dim apiClient = Global.MyAnimeScreen.App.AppServices.AnimeApiClient
                If apiClient Is Nothing Then
                    Throw New InvalidOperationException("Serviço de API não inicializado.")
                End If

                Dim items = Await apiClient.SearchAsync(Query.Trim()).ConfigureAwait(True)

                Results.Clear()
                For Each item In items
                    Results.Add(item)
                Next

                SelectedAnime = If(Results.Count > 0, Results(0), Nothing)
            Catch ex As Exception
                Results.Clear()
                SelectedAnime = Nothing
                ErrorMessage = $"Falha na busca: {ex.Message}"
            Finally
                IsLoading = False
            End Try
        End Function

        Private Sub OnPropertyChanged(<CallerMemberName> Optional propertyName As String = Nothing)
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
        End Sub
    End Class
End Namespace
