$tasksFolder="~\.nuget\packages\illink.tasks"
If (Test-Path $tasksFolder) {
  Remove-Item -r $tasksFolder
}

# create integration packages
dotnet restore
dotnet pack
