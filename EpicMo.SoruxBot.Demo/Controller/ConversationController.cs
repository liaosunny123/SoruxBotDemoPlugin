using System.Net;
using System.Text.Json.Nodes;
using ChatGPTQQBot.Model;
using RestSharp;
using SoruxBot.SDK.Attribute;
using SoruxBot.SDK.Model.Attribute;
using SoruxBot.SDK.Model.Message;
using SoruxBot.SDK.Plugins.Basic;
using SoruxBot.SDK.Plugins.Model;
using SoruxBot.SDK.Plugins.Service;
using SoruxBot.SDK.QQ;

namespace EpicMo.SoruxBot.Demo.Controller;

public class ConversationController(ILoggerService loggerService, ICommonApi bot, IPluginsDataStorage dataStorage) : PluginController
{
    private readonly ICommonApi _bot = bot;
    private readonly RestClient _client = new("https://api.soruxgpt.com/v1/chat/completions");
	private const int MaxConversationCount = 10;

	[MessageEvent(MessageType.PrivateMessage)]
    [Command(CommandPrefixType.Single, "grok <action> <param>")]
    public PluginFlag SetUserToken(MessageContext context, string action, string param)
    {
        switch (action)
        {
            case "set":
            {
                dataStorage.AddStringSettings("grok", "token", param);
                var chain = QqMessageBuilder
                    .PrivateMessage(context.TriggerId)
                    .Text("设置成功！")
                    .Build();
                
                _bot.QqSendGroupMessage(chain, context.BotAccount);
                break;
            }
            default:
            {
                var chain = QqMessageBuilder
                    .PrivateMessage(context.TriggerId)
                    .Text("你好，请输入正确的指令：grok set <token>")
                    .Build();
                
                _bot.QqSendGroupMessage(chain, context.BotAccount);
                break;
            }
        }
        return PluginFlag.MsgIntercepted;
    }

    [MessageEvent(MessageType.GroupMessage)]
    [Command(CommandPrefixType.Single, "grok <action> [model]")]
    public PluginFlag Chat(MessageContext context, string action, string? model)
    {
        if (action != "start")
        {
            _bot.QqSendGroupMessage(
                QqMessageBuilder
                    .GroupMessage(context.TriggerPlatformId)
                    .Text("你好，ChatGPT AI 在未对话时只能运行输入启动对话指令：#grok start")
                    .Build(), 
                    context.BotAccount
                );
            return PluginFlag.MsgIntercepted;
        }
        
        var token = dataStorage.GetStringSettings("grok", "token");
        
        if (string.IsNullOrEmpty(token))
        {
            _bot.QqSendGroupMessage(
                QqMessageBuilder
                    .GroupMessage(context.TriggerPlatformId)
                    .Text("你好，全局 Grok 密钥暂时未设置！")
                    .Build(), 
                context.BotAccount
            );
            return PluginFlag.MsgIntercepted;
        }

        if (string.IsNullOrEmpty(model))
        {
            model = "grok-3";
        }

        // 开始对话
        var loop = true;
        var conversation = new Conversation(model);
        
        _bot.QqSendGroupMessage(
            QqMessageBuilder
                .GroupMessage(context.TriggerPlatformId)
                .Text("您好，有什么是 SoruxBot 可以帮助到您的吗？")
                .Build(), 
            context.BotAccount
        );

        do
        {
	        var msg = bot.QqReadNextGroupMessageAsync(context.TriggerId, context.TriggerPlatformId).Result;
			
	        if(msg != null)
			{
				var userMessage = new Message("user",
					msg.MessageChain!.Messages.Select(p => p.ToPreviewText()).Aggregate((t1, t2) => t1 + t2));

				if (userMessage.Content == "#grok stop")
				{
					bot.QqSendGroupMessage(
						QqMessageBuilder
							.GroupMessage(context.TriggerPlatformId)
							.Text("对话结束！")
							.Build(), 
						context.BotAccount);
					break;
				}
				
				conversation.Messages.Add(userMessage);

				var req = GetSoruxGptRequest(token);
				req.AddJsonBody(
					conversation
				);
				
				var resp = _client.Execute(req);
				if(resp.StatusCode == HttpStatusCode.OK)
				{
					loggerService.Info("ChatGPTQQBot-Chat", "Successfully get response");
					var rep = JsonNode.Parse(resp.Content!)!;

					var reply = new Message("assistant",
						rep["choices"].AsArray()[0]["message"]["content"].ToString());
					conversation.Messages.Add(reply);
					
					if (conversation.Messages.Count > 2 * MaxConversationCount)
					{
						_bot.QqSendGroupMessage(QqMessageBuilder
						.GroupMessage(context.TriggerPlatformId)
						.Text($"对话数量已超上限({MaxConversationCount}条)，对话自动终止")
						.Build(),
						context.BotAccount);
						break;
					}

					_bot.QqSendGroupMessage(QqMessageBuilder
							.GroupMessage(context.TriggerPlatformId)
							.Text(reply.Content)
							.Build(),
						context.BotAccount);
					
					conversation.Messages.Add(reply);
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
        } while (loop);
        return PluginFlag.MsgIntercepted;
    }

    private RestRequest GetSoruxGptRequest(string token)
    {
        var req = new RestRequest()
            .AddHeader("Authorization", $"Bearer {token}")
            .AddHeader("Content-Type", "application/json");
        return req;
    }
}