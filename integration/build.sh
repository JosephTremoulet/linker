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

# create integration packages
dotnet restore
dotnet pack
