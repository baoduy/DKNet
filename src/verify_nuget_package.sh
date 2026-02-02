#!/bin/bash
# Build and pack the DKNet.FW solution into a NuGet package
# Verify the package using this website https://nuget.info/packages

VERSION="10.0.$(date +%y%m%d)"

echo "Generating NuGet packages with version: $VERSION"

# Clean old packages
rm -f ./nupkgs/*

# Pack with generated version
dotnet pack DKNet.FW.sln --configuration Release --output ./nupkgs -p:PackageVersion=$VERSION

echo "Package generation complete. Version: $VERSION"
echo "Verify packages at: https://nuget.info/packages"
