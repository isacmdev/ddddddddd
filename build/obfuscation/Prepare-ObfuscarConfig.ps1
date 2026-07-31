# T23 Obfuscation Closure — Prepare Obfuscar configuration
# ----------------------------------------------------------------------------
# Substitutes absolute-path placeholders in the checked-in Obfuscar XML
# template and writes a Release-specific staging configuration file consumed
# by `obfuscar.console`.
#
# The template uses Obfuscar 2.2.50's required absolute-path Var keys
# (InPath, OutPath, Module file, LogFile). Obfuscar does NOT perform
# environment-variable expansion and does NOT accept relative paths.
#
# Usage (intended only from the MSBuild target — ObfuscateDomain):
#   powershell -NoProfile -ExecutionPolicy Bypass -File build/obfuscation/Prepare-ObfuscarConfig.ps1
#       -Template "<absolute-template.xml>"
#       -Output   "<absolute-staged.xml>"
#       -InPath   "<absolute-input-dir>"
#       -OutPath  "<absolute-staging-dir>"
#       -ModuleFile "<absolute-input-dll>"
#       -LogFile  "<absolute-mapping-file>"
#
# Returns 0 on success; 1 on any error (with the reason on stderr).
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Template,

    [Parameter(Mandatory = $true)]
    [string]$Output,

    [Parameter(Mandatory = $true)]
    [string]$InPath,

    [Parameter(Mandatory = $true)]
    [string]$OutPath,

    [Parameter(Mandatory = $true)]
    [string]$ModuleFile,

    [Parameter(Mandatory = $true)]
    [string]$LogFile
)

$ErrorActionPreference = 'Stop'

function Fail([string]$message) {
    [Console]::Error.WriteLine("[Prepare-ObfuscarConfig] $message")
    exit 1
}

if (-not (Test-Path -LiteralPath $Template)) {
    Fail "Template not found: $Template"
}

# Ensure the output directory exists; Output is treated as a file path.
$outputDir = Split-Path -Parent -Path $Output
if (-not (Test-Path -LiteralPath $outputDir)) {
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}

# Read the template as one string so Obfuscar's "env" passes receive a
# fully-qualified XML document rather than a per-line rewrite.
$xml = Get-Content -Raw -LiteralPath $Template

$replacements = [ordered]@{
    '{InPath}'     = $InPath
    '{OutPath}'    = $OutPath
    '{ModuleFile}' = $ModuleFile
    '{LogFile}'    = $LogFile
}

foreach ($key in $replacements.Keys) {
    if (-not $replacements[$key]) {
        Fail "Replacement value for $key is empty."
    }

    $xml = $xml.Replace($key, $replacements[$key])
}

Set-Content -LiteralPath $Output -Value $xml -Encoding utf8

exit 0
