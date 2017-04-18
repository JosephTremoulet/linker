#!/usr/bin/env bash

tasksFolder=~/.nuget/packages/illink.tasks
if [ -d $tasksFolder ]
then
  rm -r $tasksFolder
fi

# create integration packages
dotnet restore
dotnet pack
