using DataCollector.Server.Application.Extensions;
using DataCollector.Server.DataAccess.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDataAccess();
builder.Services.AddApplication();

var app = builder.Build();

app.MapControllers();

app.Run();