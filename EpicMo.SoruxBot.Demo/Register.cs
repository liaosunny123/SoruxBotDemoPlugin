using SoruxBot.SDK.Plugins.Ability;
using SoruxBot.SDK.Plugins.Basic;

namespace EpicMo.SoruxBot.Demo;

public class Register: SoruxBotPlugin , ICommandPrefix
{
    public override string GetPluginName() => "SoruxBotWhiteCat";

    public override string GetPluginVersion() => "1.0.0";

    public override string GetPluginAuthorName() => "EpicMo";

    public override string GetPluginDescription() => "一款综合性质的插件";

    public string GetPluginPrefix() => "#";
}