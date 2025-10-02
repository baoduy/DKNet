# Build and pack the DKNet.FW solution into a NuGet package
# Verify the package using this website https://nuget.info/packages
rm -f ./nupkgs/*
dotnet pack DKNet.FW.sln --configuration Release --output ./nupkgs -p:PackageVersion=9.0.0