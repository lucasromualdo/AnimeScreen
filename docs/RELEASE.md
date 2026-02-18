# Release Process

## Prerequisites
- Windows machine with .NET SDK 10 installed.
- Clean working tree on `main`.
- The new version defined in `src/MyAnimeScreen.App/MyAnimeScreen.App.vbproj`.

## 1. Choose next version

Use semantic versioning:
- Patch (`0.1.0` -> `0.1.1`): bug fixes and low-risk adjustments.
- Minor (`0.1.0` -> `0.2.0`): new features compatible with existing behavior.
- Major (`0.1.0` -> `1.0.0`): breaking changes.

Update these fields in `src/MyAnimeScreen.App/MyAnimeScreen.App.vbproj`:
- `<Version>`
- `<AssemblyVersion>`
- `<FileVersion>`
- `<InformationalVersion>`

## 2. Update changelog

- Move release notes from `## [Unreleased]` to `## [<VERSION>] - <YYYY-MM-DD>` in `CHANGELOG.md`.
- Keep `## [Unreleased]` at the top for future changes.

## 3. Validate build and tests

```powershell
dotnet restore MyAnimeScreen.slnx
dotnet build MyAnimeScreen.slnx --configuration Release --no-restore
dotnet test MyAnimeScreen.slnx --configuration Release --no-build
```

## 4. Publish app artifacts

```powershell
dotnet publish src/MyAnimeScreen.App/MyAnimeScreen.App.vbproj `
  -c Release `
  /p:PublishProfile=Release-win-x64
```

Published output directory:
- `artifacts/publish/MyAnimeScreen/win-x64/`

## 5. Create tag and push

Replace `<VERSION>` with the chosen version:

```powershell
git push origin main
git tag v<VERSION>
git push origin v<VERSION>
```

Important:
- Never retag an already published version.
- Do not force-push `main`.

## 6. Create GitHub Release
- Title: `v<VERSION>`
- Attach files from `artifacts/publish/MyAnimeScreen/win-x64/`
- Copy notes from `CHANGELOG.md`
