using VideoStreamApp.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add this for detailed errors
builder.Services.AddSignalR().AddNewtonsoftJsonProtocol();

builder.Services.AddCors(options => 
{
    options.AddPolicy("AllowVercel", policy => 
    {
        policy.WithOrigins("https://farah-movie-cyan.vercel.app")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseRouting();
app.UseCors("AllowVercel"); 

app.MapHub<MovieHub>("/moviehub");

app.Run();