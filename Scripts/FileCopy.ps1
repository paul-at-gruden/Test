$source = "C:\Clients\Globus\GlobusWebsite\GlobusWebsite";
$destination = "C:\Sitecore\Globus\Website";
$env = "local";

# copy website dll files
foreach ($dll in Get-ChildItem -Path $source\bin -Recurse)
{
	if ($dll -match "(.*)\.dll$")
    {
        write-output("--------------------------");
        write-output($dll.FullName);
        Copy-Item -Path $source\bin\$dll -Destination $destination\bin\$dll;
    }
}

Copy-Item -Path $source\bin\GlobusWebsite.dll -Destination $destination\bin\GlobusWebsite.dll

# copy layout files (aspx)
foreach ($aspx in Get-ChildItem -Path $source\layouts -Recurse)
{
    if ($aspx -match "(.*)\.aspx$")
    {
        write-output("--------------------------");
        write-output($aspx.FullName);
        Copy-Item -Path $source\layouts\$aspx -Destination $destination\layouts\$aspx;
    }
}

# copy sublayout files (ascx)
if (!(Test-Path $destination\sublayouts))
{
    write-output "Create Sublayout directory";
    New-Item -ItemType directory -Path $destination\sublayouts;
}
foreach ($ascx in Get-ChildItem -Path $source\sublayouts -Recurse)
{
    if ($ascx -match "(.*)\.aspx$")
    {
        write-output($ascx.FullName);
        Copy-Item -Path $source\sublayouts\$ascx -Destination $destination\sublayouts\$ascx;
    }
}
# copy sublayout files (xsl)
if (!(Test-Path $destination\xsl))
{
    write-output "Create Sublayout directory";
    New-Item -ItemType directory -Path $destination\xsl;
}
foreach ($xsl in Get-ChildItem -Path $source\xsl -Recurse)
{
    if ($xsl -match "(.*)\.xslt$")
    {
        write-output($xsl.FullName);
        Copy-Item -Path $source\xsl\$xsl -Destination $destination\xsl\$xsl;
    }
}