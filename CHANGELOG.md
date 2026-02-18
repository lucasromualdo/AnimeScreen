# Changelog

All notable changes to this project will be documented in this file.

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
