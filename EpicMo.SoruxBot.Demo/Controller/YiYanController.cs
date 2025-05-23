using System.Diagnostics.Tracing;
using EpicMo.SoruxBot.Demo.Models;
using Newtonsoft.Json;
using RestSharp;
using SoruxBot.SDK.Attribute;
using SoruxBot.SDK.Model.Attribute;
using SoruxBot.SDK.Model.Message;
using SoruxBot.SDK.Plugins.Basic;
using SoruxBot.SDK.Plugins.Model;
using SoruxBot.SDK.Plugins.Service;
using SoruxBot.SDK.QQ;

namespace EpicMo.SoruxBot.Demo.Controller;

public class YiYanController: PluginController
{
    private ILoggerService _loggerService;
    private ICommonApi _bot;
    private RestClient _client;
    public YiYanController(ILoggerService loggerService, ICommonApi bot)
    {
        this._loggerService = loggerService;
        this._bot = bot;
        _client = new RestClient("https://v1.hitokoto.cn/");
    }

    [MessageEvent(MessageType.PrivateMessage)]
    [Command(CommandPrefixType.Single,"saying <type>")]
    public PluginFlag YiYanGet(MessageContext context,string? type)
    {
        var request = new RestRequest();
        
        request.Method = Method.Get;
        if (!string.IsNullOrEmpty(type))
        {
            request.AddQueryParameter("c", type);
        }
        var result = _client.Execute(request);
        YiYan model = JsonConvert.DeserializeObject<YiYan>(result.Content!)!;

        var chain = QqMessageBuilder.PrivateMessage(context.TriggerId)
            .Text(model.hitokoto);
        
        if (!string.IsNullOrEmpty(model.from_who))
        {
            chain.Text("   ---" + model.from_who);
        }

        var newctx = MessageContextHelper.WithNewMessageChain(context, chain.Build());
        _bot.SendMessage(newctx);
        
        return PluginFlag.MsgIntercepted;
    }
}