using Azure;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SMS Bot API",
        Version = "v1"
    });
});

var azureOpenAiEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
var azureOpenAiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");

var twilioAccountSid = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID");
var twilioAuthToken = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN");
var twilioPhoneNumber = Environment.GetEnvironmentVariable("TWILIO_PHONE_NUMBER");

// Add Azure OpenAI client to DI
// Add Azure OpenAI client to DI
builder.Services.AddSingleton(new OpenAIClient(
    new Uri(azureOpenAiEndpoint),
    new AzureKeyCredential(azureOpenAiKey)
));

// Initialize the Twilio client
TwilioClient.Init(twilioAccountSid, twilioAuthToken);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Endpoint to receive and process incoming SMS
app.MapPost("/sms", async (
    [FromForm] string From,
    [FromForm] string Body,
    OpenAIClient openAiClient) =>
{
    // Call Azure OpenAI for a response
    var openAiResponse = await GetOpenAiResponseAsync(openAiClient, Body);

    // Send reply SMS using Twilio
    var message = MessageResource.Create(
        to: new PhoneNumber(From),
        from: new PhoneNumber(twilioPhoneNumber),
        body: openAiResponse
    );

    return Results.Ok(new { message.Sid });
}).WithOpenApi();

app.Run();

// Azure OpenAI API Call using SDK
static async Task<string> GetOpenAiResponseAsync(OpenAIClient openAiClient, string userInput)
{
    var completionsOptions = new CompletionsOptions
    {
        Prompts = { $"You are a helpful support bot. User says: {userInput}. Respond appropriately." },
        MaxTokens = 150,
        Temperature = (float?)0.7,
        FrequencyPenalty = 0,
        PresencePenalty = 0
    };

    var completionsResponse = await openAiClient.GetCompletionsAsync(completionsOptions);

    // Return the first choice's text
    return completionsResponse.Value.Choices.FirstOrDefault()?.Text?.Trim() ?? "Sorry, I couldn't process your request.";
}