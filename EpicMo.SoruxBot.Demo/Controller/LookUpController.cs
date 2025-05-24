using System.Net;
using System.Text.Json.Nodes;
using ChatGPTQQBot.Model;
using EpicMo.SoruxBot.Demo.Ability;
using RestSharp;
using SoruxBot.SDK.Attribute;
using SoruxBot.SDK.Model.Attribute;
using SoruxBot.SDK.Model.Message;
using SoruxBot.SDK.Plugins.Basic;
using SoruxBot.SDK.Plugins.Model;
using SoruxBot.SDK.Plugins.Service;
using SoruxBot.SDK.QQ;

namespace EpicMo.SoruxBot.Demo.Controller;

public class LookUpController(ILoggerService loggerService, ICommonApi bot, IPluginsDataStorage dataStorage) : PluginController
{
    private readonly ICommonApi _bot = bot;
    private readonly RestClient _client = new("https://api.soruxgpt.com/v1/chat/completions");
    private readonly WebPageTextExtractor _extractor = new ();
    
    [MessageEvent(MessageType.GroupMessage)]
    [Command(CommandPrefixType.Single, "lookup <link>")]
    public PluginFlag Chat(MessageContext context, string link)
    {
        var token = dataStorage.GetStringSettings("grok", "token");
        
        if (string.IsNullOrEmpty(token))
        {
            _bot.QqSendGroupMessage(
                QqMessageBuilder
                    .GroupMessage(context.TriggerPlatformId)
                    .Text("你好，全局 AI 密钥暂时未设置，无法使用 LookUp 功能，请联系管理员设置！")
                    .Build(), 
                context.BotAccount
            );
            return PluginFlag.MsgIntercepted;
        }


        
        try
        {
	        string content = _extractor.ExtractWebPageTextAsync(link).Result;
            
	        var conversation = new Conversation("grok-3");
	        
	        conversation.Messages.Add(new Message(
		        "system", 
		        "你是一个智能助手，能自动总结用户所提供的网页文本内容，返回关键词和概要信息，必要时还可以自动总结搜索网页文本" +
		        "相关的其他内容并进一步总结，你需要遵守以下的规范：\n" +
		        "1. 你需要只使用文本信息来回答用户，不要使用任何 Markdown 格式，请只使用纯文本内容。\n" +
		        "2. 你需要按照如下的格式回答用户：关键词：xxx | xxx | xxx \n 网页总结：xxx\n" +
		        "3. 如果网页文本涵盖了其他的链接，或者 X 帖子等，你可以进一步基于这些内容进行搜索，并且另外加一行，相关内容总结：xxx\n"
	        ));
	        
	        conversation.Messages.Add(new Message(
		        "user", 
		        "请帮我总结一下这个网页文本的内容：" + content
	        ));
	        
	        var req = GetSoruxGptRequest(token);
	        req.AddJsonBody(
		        conversation
	        );
	        
	        var resp = _client.Execute(req, Method.Post);
	        
	        if(resp.StatusCode == HttpStatusCode.OK)
	        {
		        var rep = JsonNode.Parse(resp.Content!)!;

		        var reply = new Message("assistant", rep["choices"].AsArray()[0]["message"]["content"].ToString());

		        _bot.QqSendGroupMessage(QqMessageBuilder
				        .GroupMessage(context.TriggerPlatformId)
				        .Text(reply.Content)
				        .Build(),
			        context.BotAccount);
	        }
	        else
	        {
		        _bot.QqSendGroupMessage(QqMessageBuilder
				        .GroupMessage(context.TriggerPlatformId)
				        .Text("获取回复失败，错误码：" + resp.StatusCode)
				        .Build(),
			        context.BotAccount);
	        }
        }
        catch (Exception ex)
        {
	        _bot.QqSendGroupMessage(
		        QqMessageBuilder
			        .GroupMessage(context.TriggerPlatformId)
			        .Text("提取网页内容失败，请检查链接是否正确，错误信息：" + ex.Message)
			        .Build(), 
		        context.BotAccount
	        );
        }
        finally
        {
	        _extractor.DisposeAsync().Wait();
        }
        
        return PluginFlag.MsgIntercepted;
    }

    private RestRequest GetSoruxGptRequest(string token)
    {
        var req = new RestRequest()
            .AddHeader("Authorization", $"Bearer {token}")
            .AddHeader("Content-Type", "application/json")
            .AddHeader("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36 Edg/136.0.0.0");
        
        req.Method = Method.Post;
        
        return req;
    }
}