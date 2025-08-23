using EmailTrackerBackend.Services;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Add services
var dbService = new DatabaseService(configuration.GetConnectionString("DefaultConnection"));
var emailSettings = configuration.GetSection("EmailSettings");
var emailService = new EmailService(
    emailSettings["Host"],
    int.Parse(emailSettings["Port"]),
    emailSettings["Username"],
    emailSettings["Password"],
    dbService
);

builder.Services.AddSingleton(dbService);
builder.Services.AddSingleton(emailService);
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
