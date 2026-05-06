<#
.SYNOPSIS
Converts a file to a Base64 string.

.DESCRIPTION
Reads the specified file as binary content and converts it to a Base64 string.
If OutputPath is provided, the Base64 string is written to that file.
Otherwise, the Base64 string is written to standard output.

.PARAMETER FilePath
Path to the input file that will be converted to Base64.

.PARAMETER OutputPath
Optional path to a file where the Base64 string will be written.
If omitted, the Base64 string is written to standard output.

.EXAMPLE
.\Convert-FileToBase64.ps1 -FilePath .\Documents\example.pdf

Converts example.pdf to Base64 and writes the result to standard output.

.EXAMPLE
.\Convert-FileToBase64.ps1 -FilePath .\Documents\example.pdf -OutputPath .\example.base64.txt

Converts example.pdf to Base64 and writes the result to example.base64.txt.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$FilePath,

    [string]$OutputPath
)

$resolvedFilePath = Resolve-Path -LiteralPath $FilePath -ErrorAction Stop
$fileBytes = [System.IO.File]::ReadAllBytes($resolvedFilePath)
$base64 = [System.Convert]::ToBase64String($fileBytes)

if ([string]::IsNullOrWhiteSpace($OutputPath))
{
    Write-Output $base64
    return
}

[System.IO.File]::WriteAllText($OutputPath, $base64)
Write-Output "Base64 written to $OutputPath"
