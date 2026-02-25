# MyAnimeScreen

Aplicativo desktop em VB.NET (WPF) para buscar animes na Jikan API, salvar dados localmente em SQLite e gerenciar a "Minha Lista".

## Status do projeto (2026-02-25)

- Milestone `v0.2.1` concluido e fechado no GitHub (bugs `#11` e `#12`).
- Milestones anteriores concluidas: `v0.2.0`, `v0.1.1`.
- Backlog atual (`v0.3.0`): expansoes abertas `#6`, `#8`, `#9`, `#10`.
- Proxima prioridade sugerida: escolher e implementar a primeira entrega da `v0.3.0`.

## Download (Windows x64)

- Ultima release: `v0.2.1`
- Pagina de releases: <https://github.com/lucasromualdo/MyAnimeScreen/releases>
- Download direto (zip): <https://github.com/lucasromualdo/MyAnimeScreen/releases/download/v0.2.1/MyAnimeScreen-v0.2.1-win-x64.zip>

### Executar a release

1. Baixe e extraia o arquivo `.zip`.
2. Execute `MyAnimeScreen.App.exe`.
3. Se necessario, permita a execucao no Windows/SmartScreen.

Observacao:
- O pacote `v0.2.1` publicado atualmente e **framework-dependent** (requer `.NET Desktop Runtime 10 x64`).
- O repositorio ja possui perfil e automacao para gerar releases futuras `self-contained`.

## Principais funcionalidades

- Busca por titulo via Jikan API.
- Busca offline local com fallback para cache quando a API estiver indisponivel.
- Persistencia local de resultados em `animes` (upsert).
- Gestao da "Minha Lista" com:
  - status (`QueroVer`, `Assistindo`, `Concluido`, `Pausado`, `Dropado`)
  - episodio atual
  - nota pessoal
  - favorito
  - notas
- Biblioteca local com filtro por status e opcao `Todos`.
- Validacoes de entrada para campos numericos.
- Pipeline CI (build + testes) no GitHub Actions.

## Requisitos

- Windows (WPF)
- .NET SDK 10 (`net10.0-windows`)
- Conexao com internet para consultas na Jikan API (uso basico pode operar com cache local)

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

## Como gerar build de release (framework-dependent)

```powershell
dotnet publish src/MyAnimeScreen.App/MyAnimeScreen.App.vbproj `
  -c Release `
  /p:PublishProfile=Release-win-x64
```

Saida de publish:
- `artifacts/publish/MyAnimeScreen/win-x64/`

## Como gerar build de release (self-contained)

```powershell
dotnet publish src/MyAnimeScreen.App/MyAnimeScreen.App.vbproj `
  -c Release `
  /p:PublishProfile=Release-win-x64-selfcontained
```

Saida de publish:
- `artifacts/publish/MyAnimeScreen/win-x64/`
- Inclui runtime do .NET (nao exige instalacao previa do .NET Desktop Runtime)

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

## Releases automatizadas

Workflow: `.github/workflows/release.yml`

Ao criar uma tag no formato `v*` (ex.: `v0.2.2`), o GitHub Actions:

- executa build e testes em `Release`
- publica `win-x64` com perfil `Release-win-x64-selfcontained`
- gera `.zip` de distribuicao
- cria/atualiza a GitHub Release e anexa o asset
- monta notas da release a partir do `CHANGELOG.md`

## Release notes

- Processo: `docs/RELEASE.md`
- Historico de mudancas: `CHANGELOG.md`
