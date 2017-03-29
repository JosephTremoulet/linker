#!/usr/bin/env bash

tasksFolder=~/.nuget/packages/illink.tasks
if [ -d $tasksFolder ]
then
  rm -r $tasksFolder
fi

linkCmdFolder=~/.nuget/packages/dotnet-link
if [ -d $linkCmdFolder ]
then
    rm -r $linkCmdFolder
fi

linkCmdToolFolder=~/.nuget/packages/.tools/dotnet-link
if [ -d $linkCmdToolFolder ]
then
    rm -r $linkCmdToolFolder
fi

# restore linker projects
dotnet msbuild /t:Restore /p:Configuration=netstandard_Release ../cecil/Mono.Cecil.csproj
dotnet msbuild /t:Restore /p:Configuration=netcore_Release ../linker/Mono.Linker.csproj
dotnet msbuild /t:Restore ./Illink.Tasks/Illink.Tasks.csproj
dotnet msbuild /t:Restore ./dotnet-link/dotnet-link.csproj

# build linker
dotnet msbuild /t:Build /p:Configuration=netcore_Release ../linker/Mono.Linker.csproj

# package msbuild linker tasks
dotnet msbuild /t:Build ./Illink.Tasks/Illink.Tasks.csproj
dotnet msbuild /t:Pack ./Illink.Tasks/Illink.Tasks.csproj

# package linker cli tool
dotnet publish ./dotnet-link/dotnet-link.csproj -o publish
dotnet pack ./dotnet-link/dotnet-link.csproj
