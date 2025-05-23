using System.Text.Json.Serialization;

namespace ChatGPTQQBot.Model;

public class Message(string role, string content)
{
    [JsonPropertyName("role")]
    public string Role = role;
    [JsonPropertyName("content")]
    public string Content = content;
}