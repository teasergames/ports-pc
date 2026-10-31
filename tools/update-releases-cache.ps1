<#
  Rebuilds releases-cache.json from the GitHub Releases of every integrated catalog profile.

  NEXUS uses this cache when the GitHub API is unavailable or rate-limited. The scheduled workflow
  (.github/workflows/releases-cache.yml) runs this script daily and publishes the result on the
  catalog-data branch, so the history of main is not touched.

  Usage: ./tools/update-releases-cache.ps1 -Output out/releases-cache.json [-Previous assets/releases-cache.json] [-Limit 10]
  Set GITHUB_TOKEN to raise the API limit (the workflow does this automatically).
#>
param(
  [Parameter(Mandatory = $true)][string]$Output,
  [string]$Catalog = (Join-Path $PSScriptRoot '..\assets\ports.json'),
  [string]$Previous = (Join-Path $PSScriptRoot '..\assets\releases-cache.json'),
  [int]$Limit = 0
)
$ErrorActionPreference = 'Stop'

$headers = @{ 'Accept' = 'application/vnd.github+json'; 'User-Agent' = 'NexusPortsPC-cache (https://github.com/teasergames/ports-pc)' }
if ($env:GITHUB_TOKEN) { $headers['Authorization'] = 'Bearer ' + $env:GITHUB_TOKEN }

$entries = Get-Content -LiteralPath $Catalog -Raw -Encoding UTF8 | ConvertFrom-Json
$old = @{}
if (Test-Path -LiteralPath $Previous) {
  foreach ($record in (Get-Content -LiteralPath $Previous -Raw -Encoding UTF8 | ConvertFrom-Json)) { $old[$record.Repository] = $record }
}

function Matching($release, $entry) {
  @($release.assets | Where-Object { $_.name -match $entry.AssetPattern })
}

# Mirrors PortCatalog.ParseRelease/ParseReleaseList: stable unless the profile allows betas,
# never drafts, and exactly one asset matching the profile pattern.
function Select-Release($entry) {
  $listing = $entry.AllowPrerelease -or $entry.DiscoverExecutable
  $url = 'https://api.github.com/repos/' + $entry.Repository + $(if ($listing) { '/releases?per_page=30' } else { '/releases/latest' })
  $response = Invoke-RestMethod -Uri $url -Headers $headers -TimeoutSec 30
  $candidates = @($response | ForEach-Object { $_ })
  foreach ($release in $candidates) {
    if ($release.draft) { continue }
    if ($release.prerelease -and -not $entry.AllowPrerelease) { continue }
    $assets = @(Matching $release $entry)
    if ($assets.Count -eq 0 -and $listing) { continue }
    if ($assets.Count -ne 1) { throw "expected one matching asset, found $($assets.Count)" }
    return [ordered]@{
      tag_name = $release.tag_name
      html_url = $release.html_url
      draft = [bool]$release.draft
      prerelease = [bool]$release.prerelease
      assets = @(foreach ($asset in $release.assets) {
        [ordered]@{ name = $asset.name; size = $asset.size; browser_download_url = $asset.browser_download_url; digest = $asset.digest }
      })
    }
  }
  throw 'no compatible release'
}

$targets = @($entries | Where-Object { -not $_.ProjectOnly -and -not $_.ManualPackage -and $_.Repository })
if ($Limit -gt 0) { $targets = $targets | Select-Object -First $Limit }
$records = New-Object System.Collections.Generic.List[object]
$updated = 0; $kept = 0; $missing = 0
foreach ($entry in $targets) {
  try {
    $release = Select-Release $entry
    $records.Add([ordered]@{ Repository = $entry.Repository; CheckedUtc = (Get-Date).ToString('o'); Release = $release })
    $updated++
  } catch {
    # A failed query keeps the last audited record instead of dropping the fallback.
    if ($old.ContainsKey($entry.Repository)) { $records.Add($old[$entry.Repository]); $kept++ }
    else { $missing++ }
    Write-Warning ($entry.Repository + ': ' + $_.Exception.Message)
  }
}

$folder = Split-Path -Parent ([IO.Path]::GetFullPath($Output))
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$json = ConvertTo-Json -InputObject $records.ToArray() -Depth 8
[IO.File]::WriteAllText([IO.Path]::GetFullPath($Output), $json, (New-Object Text.UTF8Encoding $false))
Write-Output "Atualizados: $updated | mantidos do cache anterior: $kept | sem registro: $missing"
