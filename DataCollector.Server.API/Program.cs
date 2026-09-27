using System.Text.Json.Serialization;
using DataCollector.Server.API.Infrastructure;
using DataCollector.Server.Application.Extensions;
using DataCollector.Server.DataAccess.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
  .AddControllers()
  .AddJsonOptions(options =>
  {
    // Типи полів у JSON як рядки ("Number"), числа на вході теж приймаються.
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
  });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddDataAccess();
builder.Services.AddApplication();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
  // Специфікація доступна за адресою /openapi/v1.json
  app.MapOpenApi();
}

app.MapControllers();

app.Run();
