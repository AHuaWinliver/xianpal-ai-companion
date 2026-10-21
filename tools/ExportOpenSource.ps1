param([string]$Destination)
# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'TestRepository.ps1')
. (Join-Path $PSScriptRoot 'PublicFiles.ps1')
$program=Join-Path $projectRoot 'src/DesktopTasks/XianPal/Program.cs'
$version=[regex]::Match([IO.File]::ReadAllText($program),'AssemblyFileVersion\("([0-9.]+)"\)').Groups[1].Value
$version=([Version]$version).ToString(3)
if(-not $Destination){$Destination=Join-Path $projectRoot ('dist/XianPal-'+$version+'-Source.zip')}
$Destination=[IO.Path]::GetFullPath($Destination)
if(Test-Path -LiteralPath $Destination){throw 'Destination exists; refusing to overwrite a prior source archive.'}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Destination) | Out-Null
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipArchive]::new([IO.File]::Open($Destination,[IO.FileMode]::CreateNew),[IO.Compression.ZipArchiveMode]::Create)
$manifest=New-Object Text.StringBuilder
try {
    foreach($file in (Get-PublicProjectFiles $projectRoot | Sort-Object FullName -Unique)) {
        $relative=$file.FullName.Substring($projectRoot.Length+1).Replace('\','/')
        [void]$manifest.AppendLine((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash+'  '+$relative)
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,('XianPal-'+$version+'/'+$relative),[IO.Compression.CompressionLevel]::Optimal)
    }
    $writer=[IO.StreamWriter]::new($archive.CreateEntry('XianPal-'+$version+'/SOURCE-SHA256SUMS.txt').Open(),[Text.UTF8Encoding]::new($false))
    try{$writer.Write($manifest.ToString())}finally{$writer.Dispose()}
}finally{$archive.Dispose()}
$hash=(Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
[IO.File]::WriteAllText(($Destination+'.sha256'),($hash+'  '+[IO.Path]::GetFileName($Destination)+[Environment]::NewLine),[Text.UTF8Encoding]::new($false))
Write-Output ('Exported clean public source: '+$Destination)
