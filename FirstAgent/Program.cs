using System.ComponentModel;
using System.Text.Json;
using Google.GenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OllamaSharp;

const string geminiApiKey = "XXXXX";
const string geminiModel = "gemini-3.6-flash";

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        })
        .SetMinimumLevel(LogLevel.Trace);
});

// Gemini, ollama Provider + Client
//var geminiProvider = new Client(apiKey: geminiApiKey);
//var ollamaProvider = new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.2");
//var ollamaChatClient = ollamaProvider.AsAIAgent(geminiModel).ChatClient; //Abstractions by Microsoft.Extensions.AI
//var geminiChatClient = geminiProvider.AsIChatClient(geminiModel); //Abstractions by Microsoft.Extensions.AI

Console.WriteLine("Choose a ai model (Qwen3, llama3.2, gemini-3.6-flash):");

var model = Console.ReadLine()!.Trim().ToLower();

var chatClientProvider = model switch
{
    "qwen3" =>
        new OllamaApiClient(
            new Uri("http://localhost:11434"),
            "qwen3:8b"),

    "llama3.2" =>
        new OllamaApiClient(
            new Uri("http://localhost:11434"),
            "llama3.2"),

    "gemini-3.6-flash" =>
        new Client(apiKey: geminiApiKey)
            .AsIChatClient(model),

    _ => throw new ArgumentException($"Unknown model: {model}")
};

var client = new FunctionInvokingChatClient(chatClientProvider, loggerFactory);

//Tools
var locationTool = AIFunctionFactory.Create(
    GetLocationAsync,
    name: "get_location",
    description: "Gets latitude and longitude for a city."
);

var weatherTool = AIFunctionFactory.Create(
    GetWeatherAsync,
    name: "get_weather",
    description: "Gets the current weather using latitude and longitude."
);

async Task<string> GetLocationAsync([Description("The location for which to get coordinates.")] string location)
{
    using var httpClient = new HttpClient();

    var url =
        $"https://geocoding-api.open-meteo.com/v1/search" +
        $"?name={Uri.EscapeDataString(location)}&count=1";

    var result = await httpClient.GetStringAsync(url);

    using var json = JsonDocument.Parse(result);

    var firstResult = json.RootElement
        .GetProperty("results")[0];

    var name = firstResult.GetProperty("name").GetString();
    var latitude = firstResult.GetProperty("latitude").GetDouble();
    var longitude = firstResult.GetProperty("longitude").GetDouble();

    return $"""
        City: {name}
        Latitude: {latitude}
        Longitude: {longitude}
        """;
}

async Task<string> GetWeatherAsync([Description("The latitude for which to get weather information.")] double latitude, [Description("The longitude for which to get weather information.")] double longitude)
{
    using var httpClient = new HttpClient();

    var url =
        $"https://api.open-meteo.com/v1/forecast" +
        $"?latitude={latitude}" +
        $"&longitude={longitude}" +
        $"&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m";

    return await httpClient.GetStringAsync(url);
}


// Weather Agent
ChatClientAgent weatherAgent = new(
    client,
    name: "WeatherAgent",
    instructions: """
    You are a weather agent.

    To answer a weather question for a city:

    First call get_location with the city name.

    After receiving latitude and longitude from get_location,
    immediately call get_weather with those coordinates.

    Do not answer the user after get_location.
    The weather question is not complete until get_weather has been called.
    """,
    tools: [locationTool, weatherTool]
);

Console.WriteLine("Enter a city to get the weather information:");
var query = Console.ReadLine();

await foreach (var update in weatherAgent.RunStreamingAsync(query))
{
    Console.Write(update);
}
