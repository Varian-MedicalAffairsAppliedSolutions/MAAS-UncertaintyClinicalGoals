[CmdletBinding()]
param (
    [String]
    $CsprojFileName,
    [String]
    $CsprojFilePath = ".",
    [String]
    $PackagesPath = "packages"
)

# $xml.Save will only work with an absolute path
# therefore resolving the path of the CSPROJ file
$csprojAbsoluteFilePath = Join-Path $CsprojFilePath $CsprojFileName | Resolve-Path
Write-Host "Updating $csprojAbsoluteFilePath"

# Convert PackagesPath to absolute if it's relative
$packagesAbsolutePath = if ([System.IO.Path]::IsPathRooted($PackagesPath)) {
    $PackagesPath
} else {
    Join-Path (Get-Item $CsprojFilePath).FullName $PackagesPath | Resolve-Path
}
Write-Host "Using packages path: $packagesAbsolutePath"

$xml = [xml](Get-Content $csprojAbsoluteFilePath)

$references = $xml.Project.ItemGroup |
    Foreach-Object { $_.Reference } |
    Where-Object { $_.Include -match "VMS.TPS.Common.Model" }

if ($references.Count -eq 0) {
    Write-Host "No VMS.TPS.Common.Model references found. Attempting to add them..."
    
    # Create ItemGroup if it doesn't exist
    $itemGroup = $xml.Project.ItemGroup | Select-Object -First 1
    if ($null -eq $itemGroup) {
        $itemGroup = $xml.CreateElement("ItemGroup", $xml.DocumentElement.NamespaceURI)
        $xml.Project.AppendChild($itemGroup) | Out-Null
    }
    
    # Add API reference
    $apiRef = $xml.CreateElement("Reference", $xml.DocumentElement.NamespaceURI)
    $apiRef.SetAttribute("Include", "VMS.TPS.Common.Model.API")
    
    $specificVersion = $xml.CreateElement("SpecificVersion", $xml.DocumentElement.NamespaceURI)
    $specificVersion.InnerText = "False"
    $apiRef.AppendChild($specificVersion) | Out-Null
    
    $private = $xml.CreateElement("Private", $xml.DocumentElement.NamespaceURI)
    $private.InnerText = "False"
    $apiRef.AppendChild($private) | Out-Null
    
    $hintPath = $xml.CreateElement("HintPath", $xml.DocumentElement.NamespaceURI)
    $hintPath.InnerText = Join-Path $packagesAbsolutePath "VMS.TPS.Common.Model.API.dll"
    $apiRef.AppendChild($hintPath) | Out-Null
    
    $itemGroup.AppendChild($apiRef) | Out-Null
    Write-Host "Added VMS.TPS.Common.Model.API reference with hint path: $($hintPath.InnerText)"
    
    # Add Types reference
    $typesRef = $xml.CreateElement("Reference", $xml.DocumentElement.NamespaceURI)
    $typesRef.SetAttribute("Include", "VMS.TPS.Common.Model.Types")
    
    $specificVersion2 = $xml.CreateElement("SpecificVersion", $xml.DocumentElement.NamespaceURI)
    $specificVersion2.InnerText = "False"
    $typesRef.AppendChild($specificVersion2) | Out-Null
    
    $private2 = $xml.CreateElement("Private", $xml.DocumentElement.NamespaceURI)
    $private2.InnerText = "False"
    $typesRef.AppendChild($private2) | Out-Null
    
    $hintPath2 = $xml.CreateElement("HintPath", $xml.DocumentElement.NamespaceURI)
    $hintPath2.InnerText = Join-Path $packagesAbsolutePath "VMS.TPS.Common.Model.Types.dll"
    $typesRef.AppendChild($hintPath2) | Out-Null
    
    $itemGroup.AppendChild($typesRef) | Out-Null
    Write-Host "Added VMS.TPS.Common.Model.Types reference with hint path: $($hintPath2.InnerText)"
} else {
    Write-Host "Found $($references.Count) VMS.TPS.Common.Model reference(s)"
    
    $references |
    Foreach-Object {
        # Removing everything after the package name (e.g. Version, Culture, PublicKeyToken, etc.)
        $newInclude = $_.Include -replace ",.*", ""
        Write-Host "Replacing $($_.Include) with $newInclude"

        $_.RemoveAll()
        $_.SetAttribute("Include", $newInclude)

        $specificVersion = $xml.CreateElement("SpecificVersion", $xml.DocumentElement.NamespaceURI)
        $specificVersion.InnerText = "False"
        $_.AppendChild($specificVersion) | Out-Null

        $copyToLocal = $xml.CreateElement("Private", $xml.DocumentElement.NamespaceURI)
        $copyToLocal.InnerText = "False"
        $_.AppendChild($copyToLocal) | Out-Null

        $hintPath = $xml.CreateElement("HintPath", $xml.DocumentElement.NamespaceURI)
        $hintPath.InnerText = Join-Path $packagesAbsolutePath "$newInclude.dll"
        $_.AppendChild($hintPath) | Out-Null
        Write-Host "Added new hint path for ${newInclude}: $($hintPath.InnerText)"
    }
}

Write-Host "Saving updated CSPROJ"
$xml.Save($csprojAbsoluteFilePath)
Write-Host "CSPROJ updated successfully"
