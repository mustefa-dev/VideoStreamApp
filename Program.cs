using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using VideoStreamApp.Hubs;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .WithOrigins(
                "http://localhost:8081",
                "http://localhost:5139",
                "https://farah-movie-cyan.vercel.app",
                "http://217.76.57.87:3030",
                "https://video-hub.mooo.com"
            )
            .AllowCredentials();
    });
});
var app = builder.Build();

app.UseCors();

app.MapHub<MovieHub>("/moviehub");

app.Run();
