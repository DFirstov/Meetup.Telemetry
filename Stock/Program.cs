using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Events;
using ILogger = Serilog.ILogger;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((_, loggerConfiguration) => loggerConfiguration
	.MinimumLevel.Debug()
	.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
	.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
	.WriteTo.Console()
	.WriteTo.Seq("http://seq"));

var app = builder.Build();

string[] products = ["milk", "bread", "cheese", "apples", "oranges", "bananas", "eggs", "chicken", "fish", "tomatoes"];

app.MapGet("/products", ([FromServices] ILogger logger) =>
{
	logger.Debug("Getting products");
	return products;
});

app.MapPost("/products/reserve/{product}", (string product, [FromServices] ILogger logger) =>
{
	logger.Debug("Reserving product {Product}", product);

	if (!products.Contains(product))
	{
		logger.Warning("Product {Product} not found", product);
		return Results.BadRequest($"Product {product} not found");
	}

	if (Random.Shared.Next(0, 10) == 0)
	{
		logger.Warning("Product {Product} out of stock", product);
		return Results.BadRequest($"Product {product} out of stock");
	}
	
	logger.Debug("Product {Product} reserved", product);
	return Results.Ok($"Product {product} reserved");
});

app.Run();
