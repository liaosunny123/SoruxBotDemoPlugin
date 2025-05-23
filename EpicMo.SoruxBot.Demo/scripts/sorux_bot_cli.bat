
:: 获取脚本所在的目录路径
set "SORUX_SCRIPT_PATH=%~dp0"

:: 运行项目
dotnet %SORUX_SCRIPT_PATH%publish\SoruxBotPublishCli.dll
