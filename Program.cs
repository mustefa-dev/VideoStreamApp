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
                "http://217.76.57.87:3030"
            )
            .AllowCredentials();
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();

app.UseCors();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "VideoStreamApp API V1");
    c.RoutePrefix = "swagger";
});
app.MapHub<MovieHub>("/moviehub");

app.MapGet("/weather", () => new { TemperatureC = 25, Summary = "Sunny" });

app.Run();

