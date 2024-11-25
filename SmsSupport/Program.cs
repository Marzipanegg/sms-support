using Azure;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.TwiML.Messaging;
using Twilio.Types;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container  
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
});

var azureOpenAiEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
var azureOpenAiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");

var twilioAccountSid = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID");
var twilioAuthToken = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN");
var twilioPhoneNumber = Environment.GetEnvironmentVariable("TWILIO_PHONE_NUMBER");

// Add Azure OpenAI client to DI  
builder.Services.AddSingleton(new OpenAIClient(
    new Uri(azureOpenAiEndpoint),
    new AzureKeyCredential(azureOpenAiKey)));

// Initialize the Twilio client  
TwilioClient.Init(twilioAccountSid, twilioAuthToken);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
});

app.UseHttpsRedirection();

// Endpoint to receive and process incoming SMS  
app.MapPost("/sms", async (
    [FromForm] string From,
    [FromForm] string Body,
    OpenAIClient openAiClient) =>
{
    try
    {
        // Call Azure OpenAI for a response  
        var openAiResponse = await GetOpenAiResponseAsync(openAiClient, Body, deploymentName);

        // Send reply SMS using Twilio  
        var message = MessageResource.Create(
            to: new PhoneNumber(From),
            from: new PhoneNumber(twilioPhoneNumber),
            body: openAiResponse
        );
        return Results.Ok(new { message.Sid });
    }
    catch (Exception ex)
    {
        // Log the exception and return an error response  
        Console.Error.WriteLine(ex.ToString());
        return Results.StatusCode(500);
    }
}).DisableAntiforgery();

app.Run();

// Azure OpenAI API Call using SDK  
static async Task<string> GetOpenAiResponseAsync(OpenAIClient openAiClient, string userInput, string deploymentName)
{
    string guidelines = @"Generate jokes based solely on the provided questions without directly answering them. Focus on humorous interpretations, puns, or light-hearted responses that play off the context or wording of the question.  
# Steps  
1. **Identify Key Elements**: Break down the question to identify elements that can be humorously interpreted or rephrased.  
2. **Find a Comedic Angle**: Develop a humorous angle using puns, wordplay, or absurdity.  
3. **Craft the Joke**: Construct a joke that relates to the question's theme while maintaining a light-hearted tone.  
# Output Format   
Provide a humorous response in one or two sentences, styled as a joke.  
# Examples  
**Input**: ""What is the square root of 49?""  **Output**: ""Why did the number 7 go to therapy? Because it realized it had been living as a square root its whole life!""  
**Input**: ""Can plants grow without sunlight?""  **Output**: ""Why don't plants surprise party in the dark? Because they hate being left in the shade!""  
# Notes   
- Focus on relevance to the original question theme.  
- Maintain a playful and positive tone.";

    var messages = new List<ChatRequestMessage>
        {
            new ChatRequestSystemMessage (guidelines),
            new ChatRequestUserMessage(userInput)
        };
    var chatCompletionsOptions = new ChatCompletionsOptions(deploymentName, messages)
    {
        MaxTokens = 75, 
        Temperature = 0.7f,
        FrequencyPenalty = 0,
        PresencePenalty = 0,
    };

    var completionsResponse = await openAiClient.GetChatCompletionsAsync(chatCompletionsOptions);
    // Return the first choice's text  
    return completionsResponse.Value.Choices.FirstOrDefault().Message.Content.ToString() ?? "Sorry, I couldn't process your request.";
}