# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Changed
- Added repository-level validation in `UserAnimeRepository` to block invalid episode progress and out-of-range personal scores before persistence.
- Added database `CHECK` constraints for `current_episode` and `personal_score` in `user_anime`.
- Expanded `UserAnimeRepository` tests to cover invalid persistence attempts.
- Added `.sfdx/` to `.gitignore` to avoid local tooling noise in the repository.

### Fixed
- Startup failure handling now classifies and displays root-cause category (`DadosLocais`, `BancoLocal`, `Rede`, `Aplicacao`) with test coverage for formatting and classification paths.
- `AsyncRelayCommand` now observes and logs unhandled async exceptions (`Trace.TraceError`) without crashing the UI, with dedicated exception-path tests.

## [0.1.0] - 2026-02-18

### Added
- WPF desktop app in VB.NET with anime search via Jikan API.
- Local SQLite persistence for anime data and user library entries.
- "Minha Lista" management (status, episode progress, score, favorite, notes).
- Automated tests and CI pipeline on GitHub Actions.

### Changed
- MainViewModel async actions migrated to `AsyncRelayCommand` for safer execution.
- Test coverage expanded for command behavior, selection flow, and score validation.
- Guard test added to prevent mojibake regressions in error messages.
