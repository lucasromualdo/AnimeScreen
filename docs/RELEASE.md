# Release Process

## Prerequisites
- Windows machine with .NET SDK 10 installed.
- Clean working tree on `main`.

## 1. Validate build and tests

```powershell
dotnet restore MyAnimeScreen.slnx
dotnet build MyAnimeScreen.slnx --configuration Release --no-restore
dotnet test MyAnimeScreen.slnx --configuration Release --no-build
```

## 2. Publish app artifacts

```powershell
dotnet publish src/MyAnimeScreen.App/MyAnimeScreen.App.vbproj `
  -c Release `
  /p:PublishProfile=Release-win-x64
```

Published output directory:
- `artifacts/publish/MyAnimeScreen/win-x64/`

## 3. Create tag and push

```powershell
git tag v0.1.0
git push origin main
git push origin v0.1.0
```

## 4. Create GitHub Release
- Title: `v0.1.0`
- Attach files from `artifacts/publish/MyAnimeScreen/win-x64/`
- Copy notes from `CHANGELOG.md`
