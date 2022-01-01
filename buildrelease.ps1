param(
  [Parameter()][String]$Version = "0.0.0.0"
)

# This script builds self-contained release executables for the following runtimes:
$RuntimeTargets = @("win-x64", "linux-x64", "osx-arm64", "osx-x64");

$ProjectDirectory = Split-Path $MyInvocation.MyCommand.Path -Parent;
$OutputDirectory = New-Item -Path "$($ProjectDirectory)/build" -ItemType Directory -Force;

$RuntimeTargets | ForEach-Object {
    $CurrentRuntime = $_;
    $RuntimeOutputDirectory = New-Item -Path $OutputDirectory -Name "dupdup-$Version-$CurrentRuntime" -ItemType Directory -Force;
    dotnet publish "$ProjectDirectory/Dupdup/Dupdup.csproj" /property:Version=$Version --configuration release --runtime $CurrentRuntime --self-contained --output $RuntimeOutputDirectory;
    # If the OS can preserve executable file permissions for Linux/MacOS, this is preferable
    if (($CurrentRuntime -notlike "win*") -and ($PSVersionTable.Platform -like "Unix")) {
        Get-ChildItem -Path $RuntimeOutputDirectory | ForEach-Object {
            $CurrentFile = $_;
            chmod +x "$CurrentFile" --verbose;
            Rename-Item -Path "$CurrentFile" -NewName $CurrentFile.Name.ToLower();
        }
        tar -cJvf "$RuntimeOutputDirectory.tar.xz" -C $OutputDirectory $($RuntimeOutputDirectory | Split-Path -Leaf);
    # Otherwise, chmod +x will be required when the archive is extracted for Linux/MacOS builds
    } else {
        Compress-Archive -Path "$RuntimeOutputDirectory/*" -DestinationPath "$RuntimeOutputDirectory.zip" -CompressionLevel Optimal -Force;
    }
    Remove-Item -Path $RuntimeOutputDirectory -Recurse -Force;
}