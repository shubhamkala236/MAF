using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;

var ollamaProvider = new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.2");
var ollamaAgent = ollamaProvider.AsAIAgent(instructions: "You are a friendly assistant. Keep your answers brief.", name: "ConversationAgent");

AgentSession session = await ollamaAgent.CreateSessionAsync();

// First turn
await foreach (var response in ollamaAgent.RunStreamingAsync("My name is Alice and I love hiking.", session))
{
	Console.Write(response.Text);
}

// Second turn — the agent remembers the user's name and hobby
await foreach (var response in ollamaAgent.RunStreamingAsync("What do you remember about me?", session))
{
	Console.Write(response.Text);
}

List<ChatMessage> chatHistory = new List<ChatMessage>();

session.TryGetInMemoryChatHistory(out chatHistory);

//foreach (var message in chatHistory)
//{
//	var ss = message.Contents;
//	Console.WriteLine($"{message.Role}: {message.MessageId}: {message.Text}");
//	//Console.WriteLine($"---> {ss}");
//}