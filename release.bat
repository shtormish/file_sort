@echo off
cls
echo.
echo === File Sorter Release Script ===
echo.

echo [1/6] Running tests...
dotnet test
if %errorlevel% neq 0 (
    echo.
    echo ERROR: Tests failed. Aborting release.
    goto end
)
echo.

echo [2/6] Staging all changes...
git add .
echo.

echo [3/6] Committing changes with message "release"...
git commit -m "release" > nul 2>&1
if %errorlevel% equ 0 (
    echo Commit created.
) else (
    echo No changes to commit. Continuing...
)
echo.

echo [4/6] Preparing to tag new version...
git fetch --tags --quiet
rem Find the latest tag by sorting all tags semantically.
rem This is more reliable than 'git describe', which depends on the current commit's history.
set "LATEST_TAG="
for /f "tokens=*" %%a in ('git tag -l --sort=-v:refname "v*" 2^>nul') do (
    set "LATEST_TAG=%%a"
    goto :found_tag
)
:found_tag
if defined LATEST_TAG (
    echo Latest version on server is: %LATEST_TAG%
) else (
    echo No previous versions found on server.
)
set /p USER_VERSION="Enter new version (format: X.Y.Z): "

if "%USER_VERSION%"=="" (
    echo.
    echo ERROR: Version cannot be empty. Aborting.
    goto end
)

set "TAG_VERSION=v%USER_VERSION%"
echo.

echo [5/6] Creating tag %TAG_VERSION%...
git tag %TAG_VERSION%
if %errorlevel% neq 0 (
    echo.
    echo ERROR: Failed to create tag. It might already exist. Aborting.
    goto end
)
echo.

echo [6/6] Synchronizing with GitHub to trigger release...
git pull
git push
git push origin %TAG_VERSION%
echo.

echo === Release process for %TAG_VERSION% initiated! ===
echo Check the 'Actions' tab in your GitHub repository to monitor the build.
echo.

:end
pause