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

# create integration packages
dotnet restore
dotnet pack
