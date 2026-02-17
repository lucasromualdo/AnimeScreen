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
        Private ReadOnly _refreshLibraryCommand As RelayCommand
        Private _query As String = String.Empty
        Private _selectedAnime As Anime
        Private _selectedLibraryItem As LibraryAnimeItem
        Private _isLoading As Boolean
        Private _isLibraryLoading As Boolean
        Private _errorMessage As String = String.Empty
        Private _userStatus As AnimeStatus = AnimeStatus.QueroVer
        Private _currentEpisode As Integer
        Private _personalScore As Double?
        Private _isFavorite As Boolean
        Private _userNotes As String = String.Empty
        Private _libraryFilterStatus As AnimeStatus = AnimeStatus.QueroVer
        Private _selectionLoadVersion As Integer
        Private _libraryLoadVersion As Integer
        Private _suppressLibrarySelectionLoad As Boolean

        Public Sub New()
            Results = New ObservableCollection(Of Anime)()
            LibraryItems = New ObservableCollection(Of LibraryAnimeItem)()
            _searchCommand = New RelayCommand(AddressOf ExecuteSearch, AddressOf CanExecuteSearch)
            _saveToMyListCommand = New RelayCommand(AddressOf ExecuteSaveToMyList, AddressOf CanExecuteSaveToMyList)
            _refreshLibraryCommand = New RelayCommand(AddressOf ExecuteRefreshLibrary, AddressOf CanExecuteRefreshLibrary)
            ScheduleLibraryReload()
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
                _selectionLoadVersion += 1
                Dim currentLoadVersion = _selectionLoadVersion
                ResetUserEntryDraft()
                _saveToMyListCommand.RaiseCanExecuteChanged()
                SyncLibrarySelectionWithSelectedAnime()

                If value IsNot Nothing Then
                    LoadUserEntryForSelectedAnime(value, currentLoadVersion)
                End If
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

        Public Property IsLibraryLoading As Boolean
            Get
                Return _isLibraryLoading
            End Get
            Set(value As Boolean)
                If _isLibraryLoading = value Then
                    Return
                End If

                _isLibraryLoading = value
                OnPropertyChanged()
                _refreshLibraryCommand.RaiseCanExecuteChanged()
            End Set
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

        Public ReadOnly Property LibraryItems As ObservableCollection(Of LibraryAnimeItem)

        Public Property SelectedLibraryItem As LibraryAnimeItem
            Get
                Return _selectedLibraryItem
            End Get
            Set(value As LibraryAnimeItem)
                If Object.ReferenceEquals(_selectedLibraryItem, value) Then
                    Return
                End If

                _selectedLibraryItem = value
                OnPropertyChanged()

                If _suppressLibrarySelectionLoad OrElse value Is Nothing Then
                    Return
                End If

                LoadAnimeFromLibrarySelection(value)
            End Set
        End Property

        Public Property LibraryFilterStatus As AnimeStatus
            Get
                Return _libraryFilterStatus
            End Get
            Set(value As AnimeStatus)
                If _libraryFilterStatus = value Then
                    Return
                End If

                _libraryFilterStatus = value
                OnPropertyChanged()
                ScheduleLibraryReload()
            End Set
        End Property

        Public ReadOnly Property RefreshLibraryCommand As ICommand
            Get
                Return _refreshLibraryCommand
            End Get
        End Property

        Private Function CanExecuteSearch(parameter As Object) As Boolean
            Return (Not IsLoading) AndAlso (Not String.IsNullOrWhiteSpace(Query))
        End Function

        Private Function CanExecuteSaveToMyList(parameter As Object) As Boolean
            Return (Not IsLoading) AndAlso SelectedAnime IsNot Nothing
        End Function

        Private Function CanExecuteRefreshLibrary(parameter As Object) As Boolean
            Return Not IsLibraryLoading
        End Function

        Private Async Sub ExecuteSearch(parameter As Object)
            Await SearchAsync().ConfigureAwait(True)
        End Sub

        Private Async Sub ExecuteSaveToMyList(parameter As Object)
            Await SaveToMyListAsync().ConfigureAwait(True)
        End Sub

        Private Sub ExecuteRefreshLibrary(parameter As Object)
            ScheduleLibraryReload()
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
                ScheduleLibraryReload()
            Catch ex As Exception
                ErrorMessage = $"Falha ao salvar em Minha Lista: {ex.Message}"
            Finally
                IsLoading = False
            End Try
        End Function

        Private Async Sub LoadUserEntryForSelectedAnime(selected As Anime, loadVersion As Integer)
            Await LoadUserEntryForSelectedAnimeAsync(selected, loadVersion).ConfigureAwait(True)
        End Sub

        Private Async Function LoadUserEntryForSelectedAnimeAsync(selected As Anime, loadVersion As Integer) As Task
            Try
                If selected.Id <= 0 Then
                    Return
                End If

                Dim userAnimeRepository = Global.MyAnimeScreen.App.AppServices.UserAnimeRepository
                If userAnimeRepository Is Nothing Then
                    Return
                End If

                Dim savedEntry = Await userAnimeRepository.GetByAnimeIdAsync(selected.Id).ConfigureAwait(True)

                If loadVersion <> _selectionLoadVersion Then
                    Return
                End If

                If Not Object.ReferenceEquals(SelectedAnime, selected) Then
                    Return
                End If

                If savedEntry Is Nothing Then
                    Return
                End If

                ApplyUserEntry(savedEntry)
            Catch ex As Exception
                If loadVersion = _selectionLoadVersion Then
                    ErrorMessage = $"Falha ao carregar dados da Minha Lista: {ex.Message}"
                End If
            End Try
        End Function

        Private Sub ScheduleLibraryReload()
            _libraryLoadVersion += 1
            Dim requestedVersion = _libraryLoadVersion
            LoadLibraryForCurrentFilter(requestedVersion)
        End Sub

        Private Async Sub LoadLibraryForCurrentFilter(requestedVersion As Integer)
            Await LoadLibraryForCurrentFilterAsync(requestedVersion).ConfigureAwait(True)
        End Sub

        Private Async Function LoadLibraryForCurrentFilterAsync(requestedVersion As Integer) As Task
            IsLibraryLoading = True

            Try
                Dim animeRepository = Global.MyAnimeScreen.App.AppServices.AnimeRepository
                Dim userAnimeRepository = Global.MyAnimeScreen.App.AppServices.UserAnimeRepository
                If animeRepository Is Nothing OrElse userAnimeRepository Is Nothing Then
                    If requestedVersion = _libraryLoadVersion Then
                        LibraryItems.Clear()
                    End If

                    Return
                End If

                Dim userItems = Await userAnimeRepository.ListByStatusAsync(LibraryFilterStatus).ConfigureAwait(True)
                If requestedVersion <> _libraryLoadVersion Then
                    Return
                End If

                Dim libraryRows = New List(Of LibraryAnimeItem)
                For Each userItem In userItems
                    Dim anime = Await animeRepository.GetByIdAsync(userItem.AnimeId).ConfigureAwait(True)
                    If requestedVersion <> _libraryLoadVersion Then
                        Return
                    End If

                    If anime Is Nothing Then
                        Continue For
                    End If

                    libraryRows.Add(New LibraryAnimeItem With {
                        .AnimeId = userItem.AnimeId,
                        .Title = anime.Title,
                        .Status = userItem.Status,
                        .CurrentEpisode = userItem.CurrentEpisode,
                        .PersonalScore = userItem.PersonalScore,
                        .IsFavorite = userItem.IsFavorite
                    })
                Next

                If requestedVersion <> _libraryLoadVersion Then
                    Return
                End If

                LibraryItems.Clear()
                For Each row In libraryRows
                    LibraryItems.Add(row)
                Next

                SyncLibrarySelectionWithSelectedAnime()
            Catch ex As Exception
                If requestedVersion = _libraryLoadVersion Then
                    ErrorMessage = $"Falha ao carregar biblioteca local: {ex.Message}"
                End If
            Finally
                If requestedVersion = _libraryLoadVersion Then
                    IsLibraryLoading = False
                End If
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

        Private Async Sub LoadAnimeFromLibrarySelection(selectedItem As LibraryAnimeItem)
            Await LoadAnimeFromLibrarySelectionAsync(selectedItem).ConfigureAwait(True)
        End Sub

        Private Async Function LoadAnimeFromLibrarySelectionAsync(selectedItem As LibraryAnimeItem) As Task
            Try
                Dim animeRepository = Global.MyAnimeScreen.App.AppServices.AnimeRepository
                If animeRepository Is Nothing Then
                    Return
                End If

                Dim anime = Await animeRepository.GetByIdAsync(selectedItem.AnimeId).ConfigureAwait(True)
                If anime Is Nothing Then
                    ErrorMessage = "Anime selecionado não foi encontrado na base local."
                    Return
                End If

                If Not Object.ReferenceEquals(SelectedLibraryItem, selectedItem) Then
                    Return
                End If

                SelectedAnime = anime
            Catch ex As Exception
                ErrorMessage = $"Falha ao abrir item da biblioteca local: {ex.Message}"
            End Try
        End Function

        Private Sub SyncLibrarySelectionWithSelectedAnime()
            Dim matchingItem As LibraryAnimeItem = Nothing

            If _selectedAnime IsNot Nothing AndAlso _selectedAnime.Id > 0 Then
                For Each libraryItem In LibraryItems
                    If libraryItem.AnimeId = _selectedAnime.Id Then
                        matchingItem = libraryItem
                        Exit For
                    End If
                Next
            End If

            SetSelectedLibraryItemSilently(matchingItem)
        End Sub

        Private Sub SetSelectedLibraryItemSilently(item As LibraryAnimeItem)
            If Object.ReferenceEquals(_selectedLibraryItem, item) Then
                Return
            End If

            _suppressLibrarySelectionLoad = True
            Try
                _selectedLibraryItem = item
                OnPropertyChanged(NameOf(SelectedLibraryItem))
            Finally
                _suppressLibrarySelectionLoad = False
            End Try
        End Sub

        Private Sub ApplyUserEntry(entry As UserAnime)
            UserStatus = entry.Status
            CurrentEpisode = entry.CurrentEpisode
            PersonalScore = entry.PersonalScore
            IsFavorite = entry.IsFavorite
            UserNotes = If(entry.Notes, String.Empty)
        End Sub

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

    Public Class LibraryAnimeItem
        Public Property AnimeId As Long
        Public Property Title As String = String.Empty
        Public Property Status As AnimeStatus
        Public Property CurrentEpisode As Integer
        Public Property PersonalScore As Double?
        Public Property IsFavorite As Boolean
    End Class
End Namespace
