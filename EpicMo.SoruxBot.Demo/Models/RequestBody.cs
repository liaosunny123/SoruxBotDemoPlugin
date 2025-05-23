
namespace ChatGPTQQBot.Model
{
	public class RequestBody(string model, List<Message> messages)
	{
		public string Model = model;
		public List<Message> Messages = messages;
	}
}
