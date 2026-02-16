Imports MyAnimeScreen.App.Services.Api
Imports MyAnimeScreen.App.Services.Data

Friend Module AppServices
    Friend Property ConnectionFactory As DbConnectionFactory
    Friend Property AnimeApiClient As IAnimeApiClient
    Friend Property AnimeRepository As AnimeRepository
    Friend Property UserAnimeRepository As UserAnimeRepository
End Module
