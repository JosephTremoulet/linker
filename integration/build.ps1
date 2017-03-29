$tasksFolder="~\.nuget\packages\illink.tasks"
If (Test-Path $tasksFolder) {
  Remove-Item -r $tasksFolder
}

$linkCmdFolder="~\.nuget\packages\dotnet-link"
If (Test-Path $linkCmdFolder) {
  Remove-Item -r $linkCmdFolder
}

$linkCmdToolsFolder="~\.nuget\packages\.tools\dotnet-link"
If (Test-Path $linkCmdToolsFolder) {
  Remove-Item -r $linkCmdToolsFolder
}

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
