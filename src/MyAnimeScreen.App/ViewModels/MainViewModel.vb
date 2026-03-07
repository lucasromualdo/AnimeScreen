Imports System.Collections.ObjectModel
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Runtime.CompilerServices
Imports System.Threading.Tasks
Imports System.Windows.Input
Imports MyAnimeScreen.App.Commands
Imports MyAnimeScreen.App.Models
Imports MyAnimeScreen.App.Services.Api
Imports MyAnimeScreen.App.Services.Data

Namespace ViewModels
    Public Class MainViewModel
        Implements INotifyPropertyChanged

        Private Const SearchPageSize As Integer = 25
        Private ReadOnly _searchCommand As AsyncRelayCommand
        Private ReadOnly _loadMoreCommand As AsyncRelayCommand
        Private ReadOnly _saveToMyListCommand As AsyncRelayCommand
        Private ReadOnly _removeFromMyListCommand As AsyncRelayCommand
        Private ReadOnly _openLibraryItemCommand As RelayCommand
        Private ReadOnly _refreshLibraryCommand As RelayCommand
        Private ReadOnly _animeApiClient As IAnimeApiClient
        Private ReadOnly _animeRepository As AnimeRepository
        Private ReadOnly _userAnimeRepository As UserAnimeRepository
        Private _query As String = String.Empty
        Private _selectedAnime As Anime
        Private _selectedLibraryItem As LibraryAnimeItem
        Private _isSearching As Boolean
        Private _isLoadingMore As Boolean
        Private _isSavingToMyList As Boolean
        Private _isRemovingFromMyList As Boolean
        Private _isLibraryLoading As Boolean
        Private _errorMessage As String = String.Empty
        Private _currentPage As Integer
        Private _hasMore As Boolean
        Private _userStatus As AnimeStatus = AnimeStatus.QueroVer
        Private _currentEpisode As Integer
        Private _personalScore As Double?
        Private _isFavorite As Boolean
        Private _userNotes As String = String.Empty
        Private _libraryFilterStatus As AnimeStatus? = AnimeStatus.QueroVer
        Private _libraryFilterGenre As String
        Private _librarySortBy As LibrarySortBy = LibrarySortBy.UpdatedAtDesc
        Private _selectionLoadVersion As Integer
        Private _libraryLoadVersion As Integer
        Private _suppressLibrarySelectionLoad As Boolean
        Private _activeSearchTerm As String = String.Empty

        Public Sub New(animeApiClient As IAnimeApiClient, animeRepository As AnimeRepository, userAnimeRepository As UserAnimeRepository)
            If animeApiClient Is Nothing Then
                Throw New ArgumentNullException(NameOf(animeApiClient))
            End If

            If animeRepository Is Nothing Then
                Throw New ArgumentNullException(NameOf(animeRepository))
            End If

            If userAnimeRepository Is Nothing Then
                Throw New ArgumentNullException(NameOf(userAnimeRepository))
            End If

            _animeApiClient = animeApiClient
            _animeRepository = animeRepository
            _userAnimeRepository = userAnimeRepository
            Results = New ObservableCollection(Of Anime)()
            LibraryItems = New ObservableCollection(Of LibraryAnimeItem)()
            LibraryFilterOptions = CreateLibraryFilterOptions()
            LibraryGenreOptions = New ObservableCollection(Of LibraryGenreOption)(CreateDefaultLibraryGenreOptions())
            LibrarySortOptions = CreateLibrarySortOptions()
            _searchCommand = New AsyncRelayCommand(AddressOf SearchAsync, AddressOf CanExecuteSearch)
            _loadMoreCommand = New AsyncRelayCommand(AddressOf LoadMoreAsync, AddressOf CanExecuteLoadMore)
            _saveToMyListCommand = New AsyncRelayCommand(AddressOf SaveToMyListAsync, AddressOf CanExecuteSaveToMyList)
            _removeFromMyListCommand = New AsyncRelayCommand(AddressOf RemoveFromMyListAsync, AddressOf CanExecuteRemoveFromMyList)
            _openLibraryItemCommand = New RelayCommand(AddressOf ExecuteOpenLibraryItem, AddressOf CanExecuteOpenLibraryItem)
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
                _loadMoreCommand.RaiseCanExecuteChanged()
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
                _removeFromMyListCommand.RaiseCanExecuteChanged()
                SyncLibrarySelectionWithSelectedAnime()

                If value IsNot Nothing Then
                    LoadUserEntryForSelectedAnime(value, currentLoadVersion)
                End If
            End Set
        End Property

        Public Property IsSearching As Boolean
            Get
                Return _isSearching
            End Get
            Private Set(value As Boolean)
                If _isSearching = value Then
                    Return
                End If

                _isSearching = value
                OnPropertyChanged()
                NotifyBusyStateChanged()
            End Set
        End Property

        Public Property IsLoadingMore As Boolean
            Get
                Return _isLoadingMore
            End Get
            Private Set(value As Boolean)
                If _isLoadingMore = value Then
                    Return
                End If

                _isLoadingMore = value
                OnPropertyChanged()
                NotifyBusyStateChanged()
            End Set
        End Property

        Public Property IsSavingToMyList As Boolean
            Get
                Return _isSavingToMyList
            End Get
            Private Set(value As Boolean)
                If _isSavingToMyList = value Then
                    Return
                End If

                _isSavingToMyList = value
                OnPropertyChanged()
                NotifyBusyStateChanged()
            End Set
        End Property

        Public Property IsRemovingFromMyList As Boolean
            Get
                Return _isRemovingFromMyList
            End Get
            Private Set(value As Boolean)
                If _isRemovingFromMyList = value Then
                    Return
                End If

                _isRemovingFromMyList = value
                OnPropertyChanged()
                NotifyBusyStateChanged()
            End Set
        End Property

        Public ReadOnly Property IsBusy As Boolean
            Get
                Return IsSearching OrElse IsLoadingMore OrElse IsSavingToMyList OrElse IsRemovingFromMyList
            End Get
        End Property

        Public ReadOnly Property IsLoading As Boolean
            Get
                Return IsBusy
            End Get
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

        Public ReadOnly Property LoadMoreCommand As ICommand
            Get
                Return _loadMoreCommand
            End Get
        End Property

        Public Property CurrentPage As Integer
            Get
                Return _currentPage
            End Get
            Private Set(value As Integer)
                Dim normalizedValue = Math.Max(0, value)
                If _currentPage = normalizedValue Then
                    Return
                End If

                _currentPage = normalizedValue
                OnPropertyChanged()
                _loadMoreCommand.RaiseCanExecuteChanged()
            End Set
        End Property

        Public Property HasMore As Boolean
            Get
                Return _hasMore
            End Get
            Private Set(value As Boolean)
                If _hasMore = value Then
                    Return
                End If

                _hasMore = value
                OnPropertyChanged()
                _loadMoreCommand.RaiseCanExecuteChanged()
            End Set
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

        Public ReadOnly Property RemoveFromMyListCommand As ICommand
            Get
                Return _removeFromMyListCommand
            End Get
        End Property

        Public ReadOnly Property OpenLibraryItemCommand As ICommand
            Get
                Return _openLibraryItemCommand
            End Get
        End Property

        Public ReadOnly Property LibraryItems As ObservableCollection(Of LibraryAnimeItem)
        Public ReadOnly Property LibraryFilterOptions As IReadOnlyList(Of LibraryFilterOption)
        Public ReadOnly Property LibraryGenreOptions As ObservableCollection(Of LibraryGenreOption)
        Public ReadOnly Property LibrarySortOptions As IReadOnlyList(Of LibrarySortOption)

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

        Public Property LibraryFilterStatus As AnimeStatus?
            Get
                Return _libraryFilterStatus
            End Get
            Set(value As AnimeStatus?)
                If Nullable.Equals(_libraryFilterStatus, value) Then
                    Return
                End If

                _libraryFilterStatus = value
                OnPropertyChanged()
                ScheduleLibraryReload()
            End Set
        End Property

        Public Property LibraryFilterGenre As String
            Get
                Return _libraryFilterGenre
            End Get
            Set(value As String)
                Dim normalizedValue = NormalizeLibraryGenreValue(value)
                If String.Equals(_libraryFilterGenre, normalizedValue, StringComparison.OrdinalIgnoreCase) Then
                    Return
                End If

                _libraryFilterGenre = normalizedValue
                OnPropertyChanged()
                ScheduleLibraryReload()
            End Set
        End Property

        Public Property LibrarySortBy As LibrarySortBy
            Get
                Return _librarySortBy
            End Get
            Set(value As LibrarySortBy)
                If _librarySortBy = value Then
                    Return
                End If

                _librarySortBy = value
                OnPropertyChanged()
                ScheduleLibraryReload()
            End Set
        End Property

        Public ReadOnly Property RefreshLibraryCommand As ICommand
            Get
                Return _refreshLibraryCommand
            End Get
        End Property

        Private Sub NotifyBusyStateChanged()
            OnPropertyChanged(NameOf(IsBusy))
            OnPropertyChanged(NameOf(IsLoading))
            _searchCommand.RaiseCanExecuteChanged()
            _loadMoreCommand.RaiseCanExecuteChanged()
            _saveToMyListCommand.RaiseCanExecuteChanged()
            _removeFromMyListCommand.RaiseCanExecuteChanged()
            _openLibraryItemCommand.RaiseCanExecuteChanged()
        End Sub

        Private Function CanExecuteSearch(parameter As Object) As Boolean
            Return (Not IsBusy) AndAlso (Not String.IsNullOrWhiteSpace(Query))
        End Function

        Private Function CanExecuteSaveToMyList(parameter As Object) As Boolean
            Return (Not IsBusy) AndAlso SelectedAnime IsNot Nothing
        End Function

        Private Function CanExecuteLoadMore(parameter As Object) As Boolean
            If IsBusy Then
                Return False
            End If

            If CurrentPage <= 0 OrElse Not HasMore OrElse Results.Count = 0 Then
                Return False
            End If

            If String.IsNullOrWhiteSpace(_activeSearchTerm) Then
                Return False
            End If

            Return String.Equals(Query.Trim(), _activeSearchTerm, StringComparison.Ordinal)
        End Function

        Private Function CanExecuteRemoveFromMyList(parameter As Object) As Boolean
            Return (Not IsBusy) AndAlso SelectedAnime IsNot Nothing AndAlso SelectedAnime.Id > 0
        End Function

        Private Function CanExecuteOpenLibraryItem(parameter As Object) As Boolean
            If IsBusy Then
                Return False
            End If

            Return TryCast(parameter, LibraryAnimeItem) IsNot Nothing
        End Function

        Private Function CanExecuteRefreshLibrary(parameter As Object) As Boolean
            Return Not IsLibraryLoading
        End Function

        Private Sub ExecuteOpenLibraryItem(parameter As Object)
            Dim item = TryCast(parameter, LibraryAnimeItem)
            If item Is Nothing Then
                Return
            End If

            If Object.ReferenceEquals(SelectedLibraryItem, item) Then
                LoadAnimeFromLibrarySelection(item)
                Return
            End If

            SelectedLibraryItem = item
        End Sub

        Private Sub ExecuteRefreshLibrary(parameter As Object)
            ScheduleLibraryReload()
        End Sub

        Private Async Function SearchAsync() As Task
            IsSearching = True
            ErrorMessage = String.Empty

            Try
                Dim apiClient = _animeApiClient
                If apiClient Is Nothing Then
                    Throw New InvalidOperationException("Servico de API nao inicializado.")
                End If

                Dim searchTerm = Query.Trim()
                Dim animeRepository = _animeRepository
                Dim searchResult As AnimeSearchResult = Nothing
                Dim usedLocalFallback = False
                _activeSearchTerm = String.Empty
                CurrentPage = 0
                HasMore = False

                Dim apiEx As Exception = Nothing
                Try
                    searchResult = Await apiClient.SearchAsync(searchTerm, page:=1, maxRows:=SearchPageSize).ConfigureAwait(True)
                Catch ex As Exception
                    apiEx = ex
                End Try

                If apiEx IsNot Nothing Then
                    If animeRepository Is Nothing Then
                        Throw apiEx
                    End If

                    Try
                        Dim localItems = Await animeRepository.SearchByTitleAsync(searchTerm, SearchPageSize).ConfigureAwait(True)
                        searchResult = New AnimeSearchResult With {
                            .Page = 1,
                            .HasMore = False,
                            .Items = localItems
                        }
                        usedLocalFallback = True
                    Catch localEx As Exception
                        Throw New InvalidOperationException(
                            $"Falha na busca online e no cache local. API: {apiEx.Message}. Cache: {localEx.Message}",
                            localEx)
                    End Try

                    If searchResult.Items.Count > 0 Then
                        ErrorMessage = $"Falha na busca online: {apiEx.Message}. Exibindo resultados do cache local."
                    Else
                        ErrorMessage = $"Falha na busca online: {apiEx.Message}. Nenhum resultado encontrado no cache local."
                    End If
                End If

                If searchResult Is Nothing Then
                    searchResult = New AnimeSearchResult With {
                        .Page = 1,
                        .HasMore = False,
                        .Items = Array.Empty(Of Anime)()
                    }
                End If

                Results.Clear()
                Dim appendedItems = AppendUniqueSearchResults(searchResult.Items)
                _activeSearchTerm = searchTerm
                CurrentPage = 1
                HasMore = searchResult.HasMore AndAlso (Not usedLocalFallback)

                If Results.Count > 0 AndAlso Not usedLocalFallback Then
                    If animeRepository Is Nothing Then
                        ErrorMessage = "Busca concluida, mas o repositorio local nao esta inicializado."
                    Else
                        Dim failedRows = Await PersistSearchResultsAsync(appendedItems, animeRepository).ConfigureAwait(True)
                        If failedRows > 0 Then
                            ErrorMessage = $"Busca concluida, mas {failedRows} resultado(s) nao foram salvos localmente."
                        End If
                    End If
                End If

                SelectedAnime = If(Results.Count > 0, Results(0), Nothing)
            Catch ex As Exception
                Results.Clear()
                SelectedAnime = Nothing
                _activeSearchTerm = String.Empty
                CurrentPage = 0
                HasMore = False
                ErrorMessage = $"Falha na busca: {ex.Message}"
            Finally
                IsSearching = False
            End Try
        End Function

        Private Async Function LoadMoreAsync() As Task
            Dim searchTerm = _activeSearchTerm
            If String.IsNullOrWhiteSpace(searchTerm) Then
                Return
            End If

            Dim nextPage = CurrentPage + 1
            IsLoadingMore = True
            ErrorMessage = String.Empty

            Try
                Dim apiClient = _animeApiClient
                If apiClient Is Nothing Then
                    Throw New InvalidOperationException("Servico de API nao inicializado.")
                End If

                Dim pageResult = Await apiClient.SearchAsync(searchTerm, page:=nextPage, maxRows:=SearchPageSize).ConfigureAwait(True)
                If pageResult Is Nothing Then
                    pageResult = New AnimeSearchResult With {
                        .Page = nextPage,
                        .HasMore = False,
                        .Items = Array.Empty(Of Anime)()
                    }
                End If

                Dim appendedItems = AppendUniqueSearchResults(pageResult.Items)
                CurrentPage = nextPage
                HasMore = pageResult.HasMore

                Dim animeRepository = _animeRepository
                If appendedItems.Count > 0 AndAlso animeRepository IsNot Nothing Then
                    Dim failedRows = Await PersistSearchResultsAsync(appendedItems, animeRepository).ConfigureAwait(True)
                    If failedRows > 0 Then
                        ErrorMessage = $"Mais resultados carregados, mas {failedRows} resultado(s) nao foram salvos localmente."
                    End If
                End If
            Catch ex As Exception
                ErrorMessage = $"Falha ao carregar mais resultados (pagina {nextPage.ToString()}): {ex.Message}"
            Finally
                IsLoadingMore = False
            End Try
        End Function

        Private Async Function RemoveFromMyListAsync() As Task
            Dim selected = SelectedAnime
            If selected Is Nothing OrElse selected.Id <= 0 Then
                Return
            End If

            IsRemovingFromMyList = True
            ErrorMessage = String.Empty

            Try
                Dim userAnimeRepository = _userAnimeRepository
                If userAnimeRepository Is Nothing Then
                    Throw New InvalidOperationException("Repositório local não inicializado.")
                End If

                Dim affectedRows = Await userAnimeRepository.DeleteByAnimeIdAsync(selected.Id).ConfigureAwait(True)
                If affectedRows > 0 Then
                    ResetUserEntryDraft()
                End If
                ScheduleLibraryReload()
            Catch ex As Exception
                ErrorMessage = $"Falha ao remover da Minha Lista: {ex.Message}"
            Finally
                IsRemovingFromMyList = False
            End Try
        End Function

        Private Async Function SaveToMyListAsync() As Task
            Dim selected = SelectedAnime
            If selected Is Nothing Then
                Return
            End If

            IsSavingToMyList = True
            ErrorMessage = String.Empty

            Try
                Dim animeRepository = _animeRepository
                Dim userAnimeRepository = _userAnimeRepository
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
                IsSavingToMyList = False
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

                Dim userAnimeRepository = _userAnimeRepository
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
                Dim userAnimeRepository = _userAnimeRepository
                If userAnimeRepository Is Nothing Then
                    If requestedVersion = _libraryLoadVersion Then
                        LibraryItems.Clear()
                    End If

                    Return
                End If

                Dim userItems = Await userAnimeRepository.ListLibraryByStatusAsync(
                    LibraryFilterStatus,
                    LibrarySortBy,
                    LibraryFilterGenre
                ).ConfigureAwait(True)
                If requestedVersion <> _libraryLoadVersion Then
                    Return
                End If

                Dim genres = Await userAnimeRepository.ListLibraryGenresAsync().ConfigureAwait(True)
                If requestedVersion <> _libraryLoadVersion Then
                    Return
                End If

                UpdateLibraryGenreOptions(genres)

                Dim libraryRows = New List(Of LibraryAnimeItem)
                For Each userItem In userItems
                    libraryRows.Add(New LibraryAnimeItem With {
                        .AnimeId = userItem.AnimeId,
                        .Title = userItem.Title,
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

        Private Sub UpdateLibraryGenreOptions(genres As IReadOnlyList(Of Genre))
            Dim options = CreateLibraryGenreOptions(genres, _libraryFilterGenre)

            LibraryGenreOptions.Clear()
            For Each optionItem In options
                LibraryGenreOptions.Add(optionItem)
            Next
        End Sub

        Private Function AppendUniqueSearchResults(items As IEnumerable(Of Anime)) As IReadOnlyList(Of Anime)
            Dim appendedItems = New List(Of Anime)
            If items Is Nothing Then
                Return appendedItems
            End If

            Dim knownKeys = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each existingItem In Results
                Dim existingKey = GetSearchIdentityKey(existingItem)
                If Not String.IsNullOrWhiteSpace(existingKey) Then
                    knownKeys.Add(existingKey)
                End If
            Next

            For Each item In items
                If item Is Nothing Then
                    Continue For
                End If

                Dim itemKey = GetSearchIdentityKey(item)
                If String.IsNullOrWhiteSpace(itemKey) OrElse knownKeys.Add(itemKey) Then
                    Results.Add(item)
                    appendedItems.Add(item)
                End If
            Next

            Return appendedItems
        End Function

        Private Shared Function GetSearchIdentityKey(item As Anime) As String
            If item Is Nothing Then
                Return String.Empty
            End If

            If item.MalId > 0 Then
                Return $"mal:{item.MalId.ToString()}"
            End If

            If item.Id > 0 Then
                Return $"id:{item.Id.ToString()}"
            End If

            If String.IsNullOrWhiteSpace(item.Title) Then
                Return String.Empty
            End If

            Return $"title:{item.Title.Trim()}"
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
                Dim animeRepository = _animeRepository
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

        Private Shared Function CreateLibraryFilterOptions() As IReadOnlyList(Of LibraryFilterOption)
            Return New List(Of LibraryFilterOption) From {
                New LibraryFilterOption With {
                    .Label = "Todos",
                    .Status = Nothing
                },
                New LibraryFilterOption With {
                    .Label = "Quero ver",
                    .Status = AnimeStatus.QueroVer
                },
                New LibraryFilterOption With {
                    .Label = "Assistindo",
                    .Status = AnimeStatus.Assistindo
                },
                New LibraryFilterOption With {
                    .Label = "Concluido",
                    .Status = AnimeStatus.Concluido
                },
                New LibraryFilterOption With {
                    .Label = "Pausado",
                    .Status = AnimeStatus.Pausado
                },
                New LibraryFilterOption With {
                    .Label = "Dropado",
                    .Status = AnimeStatus.Dropado
                }
            }
        End Function

        Private Shared Function CreateLibrarySortOptions() As IReadOnlyList(Of LibrarySortOption)
            Return New List(Of LibrarySortOption) From {
                New LibrarySortOption With {
                    .Label = "Atualizado recentemente",
                    .SortBy = LibrarySortBy.UpdatedAtDesc
                },
                New LibrarySortOption With {
                    .Label = "Maior nota pessoal",
                    .SortBy = LibrarySortBy.PersonalScoreDesc
                },
                New LibrarySortOption With {
                    .Label = "Maior episodio atual",
                    .SortBy = LibrarySortBy.CurrentEpisodeDesc
                },
                New LibrarySortOption With {
                    .Label = "Titulo (A-Z)",
                    .SortBy = LibrarySortBy.TitleAsc
                }
            }
        End Function

        Private Shared Function CreateDefaultLibraryGenreOptions() As IReadOnlyList(Of LibraryGenreOption)
            Return New List(Of LibraryGenreOption) From {
                New LibraryGenreOption With {
                    .Label = "Todos",
                    .GenreName = Nothing
                }
            }
        End Function

        Private Shared Function CreateLibraryGenreOptions(
            genres As IReadOnlyList(Of Genre),
            selectedGenre As String
        ) As IReadOnlyList(Of LibraryGenreOption)
            Dim options = New List(Of LibraryGenreOption)(CreateDefaultLibraryGenreOptions())
            Dim normalizedSelected = NormalizeLibraryGenreValue(selectedGenre)
            Dim seenNames = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            If genres IsNot Nothing Then
                For Each genre In genres
                    If genre Is Nothing Then
                        Continue For
                    End If

                    Dim name = NormalizeLibraryGenreValue(genre.Name)
                    If String.IsNullOrWhiteSpace(name) Then
                        Continue For
                    End If

                    If seenNames.Add(name) Then
                        options.Add(New LibraryGenreOption With {
                            .Label = name,
                            .GenreName = name
                        })
                    End If
                Next
            End If

            If Not String.IsNullOrWhiteSpace(normalizedSelected) AndAlso seenNames.Add(normalizedSelected) Then
                options.Add(New LibraryGenreOption With {
                    .Label = normalizedSelected,
                    .GenreName = normalizedSelected
                })
            End If

            Return options
        End Function

        Private Shared Function NormalizeLibraryGenreValue(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then
                Return Nothing
            End If

            Return value.Trim()
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

    Public Class LibraryFilterOption
        Public Property Label As String = String.Empty
        Public Property Status As AnimeStatus?
    End Class

    Public Class LibrarySortOption
        Public Property Label As String = String.Empty
        Public Property SortBy As LibrarySortBy
    End Class

    Public Class LibraryGenreOption
        Public Property Label As String = String.Empty
        Public Property GenreName As String
    End Class
End Namespace
