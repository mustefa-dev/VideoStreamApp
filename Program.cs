using VideoStreamApp.Hubs;
using VideoStreamApp.Service;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder =>
        builder.WithOrigins("http://localhost:8082", "https://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

builder.Services.AddControllers();

builder.Services.AddSignalR();
builder.Services.AddSingleton<IRoomService, RoomService>();

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "VideoStreamApp API", Version = "v1" });
});

var app = builder.Build();
app.UseCors("CorsPolicy");
app.MapControllers();
app.MapHub<VideoHub>("/videohub");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "VideoStreamApp API V1");
        c.RoutePrefix = string.Empty; 
    });
}

app.Run();