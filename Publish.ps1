<#
.SYNOPSIS
	Builds DatExplorerIII as two single-file executables in the "publish" folder.

.DESCRIPTION
	DatExplorerIII.exe      needs the .NET 10 Desktop Runtime on the computer (framework-dependent): a small file.
	DatExplorerIII-NET.exe  contains the .NET runtime (self-contained): nothing has to be installed to run it.

.EXAMPLE
	.\Publish.ps1
#>
[CmdletBinding()]
param(
	[ValidateSet('Release', 'Debug')]
	[string]$Configuration = 'Release',

	[ValidateSet('win-x64', 'win-x86', 'win-arm64')]
	[string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path $root 'DatExplorerIII\DatExplorerIII.csproj'
$output = Join-Path $root 'publish'
$work = Join-Path $root 'publish-work'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
	throw 'The .NET SDK was not found. Install the .NET 10 SDK.'
}

# Start from clean folders so that no file of an older publish is left behind.
foreach ($folder in @($output, $work)) {
	if (Test-Path $folder) {
		Remove-Item $folder -Recurse -Force
	}
}

New-Item -ItemType Directory -Path $output | Out-Null

# One publish for each executable: its name in the "publish" folder, and whether it contains the .NET runtime.
$builds = @(
	@{ Name = 'DatExplorerIII.exe'; SelfContained = $false },
	@{ Name = 'DatExplorerIII-NET.exe'; SelfContained = $true }
)

try {
	foreach ($build in $builds) {
		$selfContained = if ($build.SelfContained) { 'true' } else { 'false' }
		$folder = Join-Path $work ($(if ($build.SelfContained) { 'self-contained' } else { 'framework-dependent' }))

		$arguments = @(
			'publish', $project,
			'--configuration', $Configuration,
			'--runtime', $Runtime,
			'--self-contained', $selfContained,
			'--output', $folder,
			'-p:PublishSingleFile=true',
			# The code is compiled ahead for the processor: less to compile when the program starts.
			'-p:PublishReadyToRun=true',
			'-p:DebugType=None',
			'-p:DebugSymbols=false'
		)

		if ($build.SelfContained) {
			# The WPF native libraries are packed into the file and unpacked on first start.
			$arguments += '-p:IncludeNativeLibrariesForSelfExtract=true'
			$arguments += '-p:EnableCompressionInSingleFile=true'
		}

		Write-Host "Publishing $($build.Name) ($Configuration, $Runtime, self-contained: $selfContained)..." -ForegroundColor Cyan
		& dotnet @arguments
		if ($LASTEXITCODE -ne 0) {
			throw "dotnet publish failed with exit code $LASTEXITCODE."
		}

		Copy-Item (Join-Path $folder 'DatExplorerIII.exe') (Join-Path $output $build.Name)
	}
}
finally {
	if (Test-Path $work) {
		Remove-Item $work -Recurse -Force
	}
}

foreach ($build in $builds) {
	$exe = Get-Item (Join-Path $output $build.Name)
	$size = [math]::Round($exe.Length / 1MB, 1)
	Write-Host "Done: $($exe.FullName) ($size MB)" -ForegroundColor Green
}
