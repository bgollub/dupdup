# Dupdup
Dupdup is a file de-duplicator written in C#.

## Usage

### Find and preserve duplicates
```
dupdup -target "C:\DIRECTORY" -searchpattern "*.jpg"
```

### Find and delete duplicates
```
dupdup -target "C:\DIRECTORY" -searchpattern "*" -delete
```

## Compiling
Run `dotnet publish Dupdup.csproj` or use the `buildrelease.ps1` script in the repository.

`buildrelease.ps1` builds self-contained release executables for the following runtimes:
- win-x64
- linux-x64
- osx-arm64
- osx-x64
