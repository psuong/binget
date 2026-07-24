# BinGet

Pulls the latest binaries and zips from public github repositories. This utility reads from 
a configuration `toml` file that you supply.

## Example config
```toml
url = "https://api.github.com/repos/"
destination = "your-destination-path"

# Security Levels determine how to deal with the checksum when downloading the pkg from Github
# 0 : None, ignore any checksum validation (not recommended)
# 1 : Laxed, ignore checksum validation if it is not published in Github (this can be due to a manual upload by the repo owner 
# or they did not provide one.) Generally, not recommended since there is no way to validate.
# 2 : Strict, checksum validation is required. If it does not exist the package install will fail.
security_level = 2

[repositories.omnisharp-lsp]
id = "OmniSharp/omnisharp-roslyn"
targetPattern = "omnisharp-win-x64-net\\d+\\.\\d+\\.zip"
```

Your `config.toml` file is effectively your primary package list.

| Key | Description |
|-----|-------------|
| url | The repository you want to pull from. Currently only tested with Github. |
| destination | The location of where the packages will be downloaded and extracted to. **Use an absolute path.** |
| security_level | See the comments in the (Example config)[#example-config] for more details. Must be an integer 0, 1, or 2. |
| repositories.ALIAS | Replace ALIAS with a name you want to identify the package with with. E.g. omnisharp-roslyn would be displayed as omnisharp-lsp. |
| id | The unique identifier for the repository. Github repositories typically follow {owner}/{repository-name}.
| targetPattern | Repository owners will typically release multiple assets. Use a regex pattern to find the asset you want or exact name. |

You can add multiple entries to `repositories`.

## Usage
```sh
binget -h

Usage: [command] [-h|--help] [--version]

Commands:
  clean, c                 Removes any local packages that are not listed in the config file. Your config file is effectively your primary list.
  install, update, i, u    Installs/updates packages from a configuration file.
  list, l                  Displays all packages in the destination directory and the config file.
```

## Development
1. Clone the repository
2. Run `dotnet restore` to restore the dependencies.
3. Make changes to the project.
4. Build with `dotnet build -c (Release|Debug)`