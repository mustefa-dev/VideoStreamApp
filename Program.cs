using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using VideoStreamApp.Hubs;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 1024 * 1024; 
});builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .WithOrigins("http://localhost:8081", "http://localhost:5139")
            .AllowCredentials();
    });
});
var app = builder.Build();

app.UseCors();

app.MapHub<MovieHub>("/moviehub");

app.Run();