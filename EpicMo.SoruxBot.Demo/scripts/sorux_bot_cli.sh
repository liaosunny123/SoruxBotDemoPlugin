#!/bin/bash

SORUX_SCRIPT_PATH=$(cd "$(dirname "$0")"; pwd)

dotnet ${SORUX_SCRIPT_PATH}/publish/SoruxBotPublishCli.dll

