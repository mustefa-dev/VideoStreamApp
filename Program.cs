using VideoStreamApp.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder =>
        builder.WithOrigins("http://localhost:3001", "https://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

builder.Services.AddControllers();

builder.Services.AddSignalR();

var app = builder.Build();
app.UseCors("CorsPolicy");
app.MapControllers();
app.MapHub<VideoHub>("/videosync");

app.Run();

