using VideoStreamApp.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR().AddNewtonsoftJsonProtocol();

builder.Services.AddCors(options => 
{
    options.AddPolicy("AllowVercel", policy => 
    {
        policy.WithOrigins("https://farah-movei.vercel.app")
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