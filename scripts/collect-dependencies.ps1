$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$destination = Join-Path (Get-Location) 'artifacts/dependency-bundle'
if (Test-Path $destination) { throw 'Destination already exists. Preserve or remove it explicitly before retrying.' }
New-Item -ItemType Directory -Path $destination | Out-Null
$cache = Join-Path $destination 'packages'
dotnet restore Ploteus.slnx --packages $cache --use-lock-file
if ($LASTEXITCODE -ne 0) { throw 'Restore failed; bundle is incomplete. No success receipt was written.' }
$report = & dotnet list src/infrastructure/Ploteus.Infrastructure/Ploteus.Infrastructure.csproj package --include-transitive --format json
if ($LASTEXITCODE -ne 0) { throw 'Dependency enumeration failed.' }
[System.IO.File]::WriteAllText((Join-Path $destination 'resolved-dependencies.json'), ($report -join [Environment]::NewLine), (New-Object System.Text.UTF8Encoding($false)))
$entries = Get-ChildItem -Path $cache -Recurse -Filter '*.nupkg' | Sort-Object FullName | ForEach-Object {
    @{ Path = $_.FullName.Substring($destination.Length + 1); Sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash; Bytes = $_.Length }
}
$entries | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $destination 'hashes.json') -Encoding UTF8
Get-ChildItem -Path 'src' -Recurse -Filter 'packages.lock.json' | ForEach-Object {
    $name = $_.Directory.Name + '.packages.lock.json'
    Copy-Item $_.FullName (Join-Path $destination $name)
}
Compress-Archive -Path (Join-Path $destination '*') -DestinationPath ($destination + '.zip')
Write-Output ('Dependency bundle: ' + $destination + '.zip')
