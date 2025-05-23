using System.Text.Json.Serialization;

namespace ChatGPTQQBot.Model;

public class Conversation
{
    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("messages")]
    public List<Message> Messages { get; set; }

    public Conversation(string model)
    {
        Model = model;
        Messages = new List<Message>();
    }
}