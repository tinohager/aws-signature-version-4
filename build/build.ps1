# -------------------------------------------------------------------------------------------------
# LOGO
# -------------------------------------------------------------------------------------------------
$LOGO = (Invoke-WebRequest "https://raw.githubusercontent.com/FantasticFiasco/logo/master/logo.raw").toString();
Write-Host "$LOGO" -ForegroundColor Green

# -------------------------------------------------------------------------------------------------
# VARIABLES
# -------------------------------------------------------------------------------------------------
$GIT_SHA = "$env:APPVEYOR_REPO_COMMIT".substring(0, 7)
$IS_TAGGED_BUILD = If ("$env:APPVEYOR_REPO_TAG" -eq "true") { $true } Else { $false }
$IS_PULL_REQUEST = If ("$env:APPVEYOR_PULL_REQUEST_NUMBER" -eq "") { $false } Else { $true }
Write-Host "[info] git sha: $GIT_SHA"
Write-Host "[info] is git tag: $IS_TAGGED_BUILD"
Write-Host "[info] is pull request: $IS_PULL_REQUEST"

# -------------------------------------------------------------------------------------------------
# BUILD
# -------------------------------------------------------------------------------------------------
Write-Host "[build] build started"
Write-Host "[build] dotnet cli v$(dotnet --version)"
$VERSION_SUFFIX_ARG = If ($IS_TAGGED_BUILD -eq $true) { "" } Else { "--version-suffix=sha-$GIT_SHA" }
&dotnet build -c Release "$VERSION_SUFFIX_ARG"
&dotnet pack -c Release --include-symbols -o ./../artifacts --no-build "$VERSION_SUFFIX_ARG"

# -------------------------------------------------------------------------------------------------
# TEST
# -------------------------------------------------------------------------------------------------
Write-Host "[test] test started"

# Exclude integration tests if we run as part of a pull requests. Integration tests rely on
# secrets, which are omitted by AppVeyor on pull requests.
$TEST_FILTER = If ($IS_PULL_REQUEST -eq $true) { "--filter Category!=Integration" } Else { "" }
Write-Host "[test] test filter: $TEST_FILTER"

&dotnet tool install --global coverlet.console
&coverlet ./test/bin/Release/netcoreapp2.1/AwsSignatureVersion4.Test.dll `
    --target "dotnet" `
    --targetargs "test --configuration Release --no-build ${TEST_FILTER}" `
    --exclude "[xunit.*]*" `
    --format opencover
