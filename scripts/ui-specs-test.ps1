#!/usr/bin/env pwsh
# Checks the screen specifications (E02-03, docs/ui/README.md), run as the verify step "ui specs":
# - relative Markdown links in docs/ lead to existing headings (anchors); files are checked by "markdown links";
# - every key screen has a file with sections for 1440 and 390 px and the states Empty, Loading, Error
#   (in Russian; the script stays ASCII so Windows PowerShell 5.1 reads it without a BOM);
# - every FR-... a spec names is a section of docs/spec.md, and every file names at least one;
# - every token a spec names (--color-*, --size-*, ..., breakpoint.*) exists in brand/tokens.json or brand/brand.json;
# - the specs are linked from docs/ARCHITECTURE.md.
# -Root points at another tree (scripts/verify-selftest.ps1 feeds it a broken one).
param([string]$Root = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$problems = [System.Collections.Generic.List[string]]::new()
$screens = 'feed', 'post', 'editor', 'blog', 'topic', 'comments', 'notifications', 'settings'

function Read-Text([string]$Path) { Get-Content -Raw -Encoding utf8 -LiteralPath $Path }

# GitHub heading anchors: lower case, punctuation dropped, spaces to hyphens, repeats get -1, -2, ...
function Get-Anchors([string]$Path) {
    $seen = @{}
    $anchors = [System.Collections.Generic.HashSet[string]]::new()
    $inFence = $false
    foreach ($line in (Read-Text $Path) -split "`r?`n") {
        if ($line -match '^\s*(```|~~~)') { $inFence = -not $inFence; continue }
        if ($inFence -or $line -notmatch '^#{1,6}\s+(.+?)\s*#*\s*$') { continue }
        $text = $Matches[1] -replace '`', '' -replace '\[([^\]]*)\]\([^)]*\)', '$1'
        $slug = ($text.ToLowerInvariant() -replace '[^\p{L}\p{Nd}\s_-]', '') -replace ' ', '-'
        $n = $seen[$slug]
        $anchors.Add($(if ($n) { "$slug-$n" } else { $slug })) | Out-Null
        $seen[$slug] = [int]$n + 1
    }
    $anchors
}

# 1. Anchors of relative links in docs/.
$docs = Join-Path $Root 'docs'
$anchorCache = @{}
foreach ($file in Get-ChildItem -LiteralPath $docs -Recurse -File -Filter *.md) {
    foreach ($m in [regex]::Matches((Read-Text $file.FullName), '\]\(([^)\s]+)\)')) {
        $target = $m.Groups[1].Value
        if ($target -match '^(https?:|mailto:)' -or -not $target.Contains('#')) { continue }
        $path, $anchor = $target -split '#', 2
        $targetFile = if ($path) { Join-Path $file.DirectoryName $path } else { $file.FullName }
        if (-not $anchor -or $targetFile -notmatch '\.md$' -or -not (Test-Path -LiteralPath $targetFile)) { continue }
        $key = (Resolve-Path -LiteralPath $targetFile).Path
        if (-not $anchorCache.ContainsKey($key)) { $anchorCache[$key] = Get-Anchors $key }
        if (-not $anchorCache[$key].Contains([uri]::UnescapeDataString($anchor))) {
            $problems.Add("$($file.FullName.Substring($Root.Length + 1)): no heading for $target")
        }
    }
}

# 2-4. The screens, their FR references and tokens.
$spec = Read-Text (Join-Path $Root 'docs/spec.md')
$sections = [regex]::Matches($spec, '(?m)^###\s+(FR-[A-Z]+)\.') | ForEach-Object { $_.Groups[1].Value }
$tokens = Read-Text (Join-Path $Root 'brand/tokens.json') | ConvertFrom-Json
$brand = Read-Text (Join-Path $Root 'brand/brand.json') | ConvertFrom-Json
$known = [System.Collections.Generic.HashSet[string]]::new()
foreach ($palette in $brand.palette.light, $tokens.color.light) {
    foreach ($p in $palette.PSObject.Properties) { $known.Add('--color-' + ($p.Name -creplace '([A-Z])', '-$1').ToLowerInvariant()) | Out-Null }
}
foreach ($group in $tokens.font.PSObject.Properties) {
    $prefix = if ($group.Name -in 'family', 'size', 'weight') { "--font-$($group.Name)" } else { "--$($group.Name)" }
    foreach ($p in $group.Value.PSObject.Properties) { $known.Add("$prefix-$($p.Name)") | Out-Null }
}
foreach ($group in 'space', 'radius', 'size') {
    foreach ($p in $tokens.$group.PSObject.Properties) { $known.Add("--$group-$($p.Name)") | Out-Null }
}
$breakpoints = @($tokens.breakpoint.PSObject.Properties.Name)

$ui = Join-Path $Root 'docs/ui'
$readme = Join-Path $ui 'README.md'
$specFiles = @(Get-ChildItem -LiteralPath $ui -File -Filter *.md -ErrorAction SilentlyContinue)
if (-not (Test-Path -LiteralPath $readme)) { $problems.Add('docs/ui/README.md is missing') }
foreach ($screen in $screens) {
    $path = Join-Path $ui "$screen.md"
    $name = "docs/ui/$screen.md"
    if (-not (Test-Path -LiteralPath $path)) { $problems.Add("$name is missing"); continue }
    $text = Read-Text $path
    $headings = [regex]::Matches($text, '(?m)^#{2,4}\s+(.+)$') | ForEach-Object { $_.Groups[1].Value }
    foreach ($required in '1440 px', '390 px', ([regex]::Unescape('\u041f\u0443\u0441\u0442\u043e')), ([regex]::Unescape('\u0417\u0430\u0433\u0440\u0443\u0437\u043a\u0430')), ([regex]::Unescape('\u041e\u0448\u0438\u0431\u043a\u0430'))) {
        if (-not ($headings | Where-Object { $_.Contains($required) })) { $problems.Add("${name}: no section for $required") }
    }
    $fr = @([regex]::Matches($text, '\bFR-[A-Z]+\b') | ForEach-Object Value | Sort-Object -Unique)
    if (-not $fr) { $problems.Add("${name}: no FR requirement referenced") }
    if ((Test-Path -LiteralPath $readme) -and -not (Read-Text $readme).Contains("]($screen.md)")) {
        $problems.Add("docs/ui/README.md does not link $screen.md")
    }
}
foreach ($file in $specFiles) {
    $name = "docs/ui/$($file.Name)"
    $text = Read-Text $file.FullName
    foreach ($id in [regex]::Matches($text, '\bFR-[A-Z]+\b') | ForEach-Object Value | Sort-Object -Unique) {
        if ($id -notin $sections) { $problems.Add("${name}: $id is not a section of docs/spec.md") }
    }
    foreach ($token in [regex]::Matches($text, '(?<![\w-])--[a-z][a-z0-9-]*') | ForEach-Object Value | Sort-Object -Unique) {
        if (-not $known.Contains($token)) { $problems.Add("${name}: unknown token $token") }
    }
    foreach ($bp in [regex]::Matches($text, '\bbreakpoint\.([a-z0-9]+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique) {
        if ($bp -notin $breakpoints) { $problems.Add("${name}: unknown token breakpoint.$bp") }
    }
}

# 5. Linked from the related docs.
$architecture = Join-Path $Root 'docs/ARCHITECTURE.md'
if (-not (Test-Path -LiteralPath $architecture) -or -not (Read-Text $architecture).Contains('](ui/README.md)')) {
    $problems.Add('docs/ARCHITECTURE.md does not link ui/README.md')
}

if ($problems) {
    $problems | ForEach-Object { Write-Host "  $_" }
    exit 1
}
Write-Host "ui specs: OK ($($screens.Count) screens)"
