Imports System.Collections.ObjectModel
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Runtime.CompilerServices
Imports System.Threading.Tasks
Imports System.Windows.Input
Imports MyAnimeScreen.App.Commands
Imports MyAnimeScreen.App.Models
Imports MyAnimeScreen.App.Services.Data

Namespace ViewModels
    Public Class MainViewModel
        Implements INotifyPropertyChanged

        Private ReadOnly _searchCommand As RelayCommand
        Private ReadOnly _saveToMyListCommand As RelayCommand
        Private _query As String = String.Empty
        Private _selectedAnime As Anime
        Private _isLoading As Boolean
        Private _errorMessage As String = String.Empty
        Private _userStatus As AnimeStatus = AnimeStatus.QueroVer
        Private _currentEpisode As Integer
        Private _personalScore As Double?
        Private _isFavorite As Boolean
        Private _userNotes As String = String.Empty

        Public Sub New()
            Results = New ObservableCollection(Of Anime)()
            _searchCommand = New RelayCommand(AddressOf ExecuteSearch, AddressOf CanExecuteSearch)
            _saveToMyListCommand = New RelayCommand(AddressOf ExecuteSaveToMyList, AddressOf CanExecuteSaveToMyList)
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
                ResetUserEntryDraft()
                _saveToMyListCommand.RaiseCanExecuteChanged()
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
                _saveToMyListCommand.RaiseCanExecuteChanged()
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

        Public Property UserStatus As AnimeStatus
            Get
                Return _userStatus
            End Get
            Set(value As AnimeStatus)
                If _userStatus = value Then
                    Return
                End If

                _userStatus = value
                OnPropertyChanged()
            End Set
        End Property

        Public Property CurrentEpisode As Integer
            Get
                Return _currentEpisode
            End Get
            Set(value As Integer)
                Dim normalizedValue = Math.Max(0, value)
                If _currentEpisode = normalizedValue Then
                    Return
                End If

                _currentEpisode = normalizedValue
                OnPropertyChanged()
            End Set
        End Property

        Public Property PersonalScore As Double?
            Get
                Return _personalScore
            End Get
            Set(value As Double?)
                Dim normalizedValue = NormalizePersonalScore(value)
                If _personalScore.Equals(normalizedValue) Then
                    Return
                End If

                _personalScore = normalizedValue
                OnPropertyChanged()
            End Set
        End Property

        Public Property IsFavorite As Boolean
            Get
                Return _isFavorite
            End Get
            Set(value As Boolean)
                If _isFavorite = value Then
                    Return
                End If

                _isFavorite = value
                OnPropertyChanged()
            End Set
        End Property

        Public Property UserNotes As String
            Get
                Return _userNotes
            End Get
            Set(value As String)
                Dim normalizedValue = If(value, String.Empty)
                If String.Equals(_userNotes, normalizedValue, StringComparison.Ordinal) Then
                    Return
                End If

                _userNotes = normalizedValue
                OnPropertyChanged()
            End Set
        End Property

        Public ReadOnly Property SaveToMyListCommand As ICommand
            Get
                Return _saveToMyListCommand
            End Get
        End Property

        Private Function CanExecuteSearch(parameter As Object) As Boolean
            Return (Not IsLoading) AndAlso (Not String.IsNullOrWhiteSpace(Query))
        End Function

        Private Function CanExecuteSaveToMyList(parameter As Object) As Boolean
            Return (Not IsLoading) AndAlso SelectedAnime IsNot Nothing
        End Function

        Private Async Sub ExecuteSearch(parameter As Object)
            Await SearchAsync().ConfigureAwait(True)
        End Sub

        Private Async Sub ExecuteSaveToMyList(parameter As Object)
            Await SaveToMyListAsync().ConfigureAwait(True)
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

                If Results.Count > 0 Then
                    Dim animeRepository = Global.MyAnimeScreen.App.AppServices.AnimeRepository
                    If animeRepository Is Nothing Then
                        ErrorMessage = "Busca concluída, mas o repositório local não está inicializado."
                    Else
                        Dim failedRows = Await PersistSearchResultsAsync(Results, animeRepository).ConfigureAwait(True)
                        If failedRows > 0 Then
                            ErrorMessage = $"Busca concluída, mas {failedRows} resultado(s) não foram salvos localmente."
                        End If
                    End If
                End If

                SelectedAnime = If(Results.Count > 0, Results(0), Nothing)
            Catch ex As Exception
                Results.Clear()
                SelectedAnime = Nothing
                ErrorMessage = $"Falha na busca: {ex.Message}"
            Finally
                IsLoading = False
            End Try
        End Function

        Private Async Function SaveToMyListAsync() As Task
            Dim selected = SelectedAnime
            If selected Is Nothing Then
                Return
            End If

            IsLoading = True
            ErrorMessage = String.Empty

            Try
                Dim animeRepository = Global.MyAnimeScreen.App.AppServices.AnimeRepository
                Dim userAnimeRepository = Global.MyAnimeScreen.App.AppServices.UserAnimeRepository
                If animeRepository Is Nothing OrElse userAnimeRepository Is Nothing Then
                    Throw New InvalidOperationException("Repositórios locais não inicializados.")
                End If

                If selected.Id <= 0 Then
                    selected.Id = Await animeRepository.UpsertAsync(selected).ConfigureAwait(True)
                End If

                Dim entry = New UserAnime With {
                    .AnimeId = selected.Id,
                    .Status = UserStatus,
                    .CurrentEpisode = CurrentEpisode,
                    .PersonalScore = PersonalScore,
                    .Notes = UserNotes,
                    .IsFavorite = IsFavorite
                }

                Await userAnimeRepository.UpsertAsync(entry).ConfigureAwait(True)
            Catch ex As Exception
                ErrorMessage = $"Falha ao salvar em Minha Lista: {ex.Message}"
            Finally
                IsLoading = False
            End Try
        End Function

        Private Shared Async Function PersistSearchResultsAsync(items As IEnumerable(Of Anime), animeRepository As AnimeRepository) As Task(Of Integer)
            Dim failures = 0

            For Each item In items
                Try
                    item.Id = Await animeRepository.UpsertAsync(item).ConfigureAwait(True)
                Catch
                    failures += 1
                End Try
            Next

            Return failures
        End Function

        Private Sub ResetUserEntryDraft()
            UserStatus = AnimeStatus.QueroVer
            CurrentEpisode = 0
            PersonalScore = Nothing
            IsFavorite = False
            UserNotes = String.Empty
        End Sub

        Private Shared Function NormalizePersonalScore(value As Double?) As Double?
            If Not value.HasValue Then
                Return Nothing
            End If

            If value.Value < 0 OrElse value.Value > 10 Then
                Throw New ArgumentOutOfRangeException(NameOf(value), "A nota pessoal deve ficar entre 0 e 10.")
            End If

            Return value
        End Function

        Private Sub OnPropertyChanged(<CallerMemberName> Optional propertyName As String = Nothing)
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
        End Sub
    End Class
End Namespace
