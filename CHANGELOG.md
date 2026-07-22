# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased [2.1.0] - 2026-07-22
### Added
- Adds an early exit when doing a rotational log backups, see feature [here](https://github.com/psuong/binget/issues/7)
### Removed
- Scriban dependency
### Fixed
- Fixed an [issue](https://github.com/psuong/binget/issues/6) where binget errors out due to the directory not being empty when attempting to delete the package
- Fixed an [issue](https://github.com/psuong/binget/issues/5) where reading a configuration toml file without a valid `repositories` section, causes a NullReferenceException

## [2.0.0] - 2026-07-21
### Added
- Adds the following dev dependencies
    - Spectre.Console
    - ConsoleAppFramework
    - ZLogger
    - Scriban
- Adds commands
    - clean, c
    - install, update, l, u
- Adds checksum comparison
- Adds version checking
- Writes a manifest.toml for each package installed
- Adds a binget.log file with rotational backups
- Adds trimmed release binary build configuration

### Removed
- Console UI framework

## [1.1.0] - 2025-03-05
* Adds progress bars for the download progress
* Renames LspManager to binget, due to fetching more than LSPs

## [1.0.0] - 2025-02-23
* Initial release
    * Supports reading a configuration toml file to fetch executables from Github
