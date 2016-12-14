# prerequisites:
# - path contains a version of dotnet that supports msbuild.
#   see https://github.com/dotnet/cli for instructions.
# - monolinker.exe must exist on the path.
#   after building monolinker.exe, it can be found at
#   <repo_root>/linker/bin/Debug/monolinker.exe.
#   to add this to the path in powershell:
#       $env:Path += ";D:\linker\linker\bin\Debug"
# - script expects to be run from its containing directory.


# This script will build and package msbuild link tasks and targtes
# that invoke the linker, as well as the dotnet link command.
# it then builds a test project and invokes the likner using "dotnet link".


# package dotnet link command
cd dotnet-link
dotnet restore
dotnet build
dotnet pack -o ..\nupkgs
cd ..

# package Mono.Linker.Tasks
cd Mono.Linker.Tasks
dotnet restore
dotnet build
dotnet pack -o ..\nupkgs
cd ..

# clear old versions of link command and link task from nuget cache
# uncomment these lines to test changes to the above packages
#rm -force -recurse ~\.nuget\packages\mono.linker.tasks
#rm -force -recurse ~\.nuget\packages\dotnet-link
#rm -force -recurse ~\.nuget\packages\.tools\dotnet-link

# build library
cd TestApp\Library
dotnet restore
dotnet build
dotnet pack -o ..\..\nupkgs
cd ..\..\

# build app and test link command
cd TestApp\App
dotnet restore
dotnet build -r win10-x64
dotnet publish -r win10-x64
dotnet link -r win10-x64 -v d
cd ..\..\

