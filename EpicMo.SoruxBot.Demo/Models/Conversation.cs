namespace ChatGPTQQBot.Model;

public class Conversation(string model)
{
    public string Model { get; set; } = model;

    public List<Message> Messages { get; } = new();
}