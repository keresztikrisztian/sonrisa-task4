using Microsoft.EntityFrameworkCore;
using Sonrisa.EventAlertSystem.Api.Application;
using Sonrisa.EventAlertSystem.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<EventAlertDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("EventAlertDatabase")));
builder.Services.AddSingleton<AlertMatchingService>();
builder.Services.AddSingleton<DemoIdentityRegistry>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
