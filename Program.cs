
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using VideoStreamApp.Hubs;
using VideoStreamApp.Models;
using VideoStreamApp.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:8082")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddSingleton<IRoomService, RoomService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseCors("AllowReactApp");
app.UseRouting();
app.MapControllers();
app.MapHub<VideoHub>("/videohub");

app.Run("http://localhost:5000");




