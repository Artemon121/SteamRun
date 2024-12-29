$ErrorActionPreference = "Stop"

$projectDirectory = "$PSScriptRoot\Community.PowerToys.Run.Plugin.SteamRun"
$jsonContent = Get-Content -Path "$projectDirectory\plugin.json" -Raw | ConvertFrom-Json
$version = $jsonContent.version

foreach ($platform in "x64", "ARM64")
{
    if (Test-Path -Path "$PSScriptRoot\bin\SteamRun-$version-$platform.zip")
    {
        Remove-Item -Path "$PSScriptRoot\bin\SteamRun-$version-$platform.zip"
    }

    if (Test-Path -Path "$projectDirectory\bin")
    {
        Remove-Item -Path "$projectDirectory\bin\*" -Recurse
    }

    if (Test-Path -Path "$projectDirectory\obj")
    {
        Remove-Item -Path "$projectDirectory\obj\*" -Recurse
    }
	
	dotnet publish $projectDirectory.sln -c Release /p:Platform=$platform

    Remove-Item -Path "$projectDirectory\bin\*" -Recurse -Include *.xml, *.pdb, PowerToys.*, Wox.*
    Remove-Item -Path "$projectDirectory\bin\$platform\Release\publish\runtimes" -Recurse
    Rename-Item -Path "$projectDirectory\bin\$platform\Release\publish" -NewName "SteamRun"

    Compress-Archive -Path "$projectDirectory\bin\$platform\Release\SteamRun" -DestinationPath "$PSScriptRoot\bin\SteamRun-$version-$platform.zip"
}
