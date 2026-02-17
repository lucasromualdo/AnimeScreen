# MyAnimeScreen

Aplicativo desktop em VB.NET (WPF) para buscar animes na Jikan API, salvar dados localmente em SQLite e gerenciar a "Minha Lista".

## Principais funcionalidades

- Busca por titulo via Jikan API.
- Persistencia local de resultados em `animes` (upsert).
- Gestao da "Minha Lista" com:
  - status (`QueroVer`, `Assistindo`, `Concluido`, `Pausado`, `Dropado`)
  - episodio atual
  - nota pessoal
  - favorito
  - notas
- Biblioteca local com filtro por status.
- Validacoes de entrada para campos numericos.
- Pipeline CI (build + testes) no GitHub Actions.

## Requisitos

- Windows (WPF)
- .NET SDK 10 (`net10.0-windows`)
- Conexao com internet para consultas na Jikan API

## Como executar

Na raiz do repositorio:

```powershell
dotnet restore MyAnimeScreen.slnx
dotnet build MyAnimeScreen.slnx
dotnet run --project src/MyAnimeScreen.App/MyAnimeScreen.App.vbproj
```

## Como rodar testes

```powershell
dotnet test MyAnimeScreen.slnx
```

## Estrutura do projeto

```text
src/MyAnimeScreen.App
  Commands/
  Converters/
  Data/sql/
  Models/
  Services/
    Api/
    Data/
  Validation/
  ViewModels/
  Views/

tests/MyAnimeScreen.App.Tests
  Data/
  Validation/
```

## Banco de dados local

- Caminho: `%LOCALAPPDATA%\MyAnimeScreen\my_anime_screen.db`
- Schema: `src/MyAnimeScreen.App/Data/sql/schema.sql`

O schema e aplicado no startup da aplicacao.

## CI

Workflow: `.github/workflows/ci.yml`

Executa em `push` na `main` e em `pull_request`:

- `dotnet restore MyAnimeScreen.slnx`
- `dotnet build MyAnimeScreen.slnx --configuration Release --no-restore`
- `dotnet test MyAnimeScreen.slnx --configuration Release --no-build`
