param ($assembly, $outputDir, $baseClass)

if (!$assembly)
{
    Write-Host "PowerShell[.exe] .\tsg.ps1 -assembly <file> [-outputDir <directory>] [-baseClass <file>]"
}
else
{
    $scriptPath = split-path -parent $MyInvocation.MyCommand.Definition;

    if (!$outputDir)
    {
        $outputDir = '.';
    }
    $outputDir = [IO.Path]::GetFullPath($outputDir);
    $assembly = [IO.Path]::GetFullPath($assembly);

    # Ensure output directory exists
    if (!(Test-Path -Path $outputDir))
    {
        mkdir $outputDir > $null;
    }

    # Generate CSDL metadata
    $mg = [IO.Path]::Combine($scriptPath, '.bin\MetadataGenerator.exe');
    $csdl = [IO.Path]::Combine($outputDir, 'out.json');
    if (Test-Path -Path $csdl)
    {
        del $csdl > $null;
    }

    $command = '"$mg" -i "$assembly" -o "$csdl"';
    iex "& $command";
    
    if (!(Test-Path -Path $csdl))
    {
        Write-Host "Unexpected error: Metadata generation failed.";
    }
    else
    {
        # Generate typescript
        $tsgen = [IO.Path]::Combine($scriptPath, 'tsgen.js');
        $command = 'node $tsgen -camelCase -i "$csdl" -o "$outputDir"';
        if ($baseClass)
        {
            $command += ' -b "$baseClass"';
        }
        iex "& $command";

        Write-Host "Done";
    }
}