using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;
using Serilog.Events;
using ILogger = Serilog.ILogger;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((_, loggerConfiguration) => loggerConfiguration
	.MinimumLevel.Debug()
	.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
	.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
	.MinimumLevel.Override("System.Net.Http.HttpClient.Default.LogicalHandler", LogEventLevel.Warning)
	.MinimumLevel.Override("System.Net.Http.HttpClient.Default.ClientHandler", LogEventLevel.Warning)
	.WriteTo.Console()
	.WriteTo.Seq("http://seq"));

builder.Services
	.AddOpenTelemetry()
	.ConfigureResource(resource => resource.AddService("shop"))
	.WithTracing(tracing => tracing
		.AddAspNetCoreInstrumentation()
		.AddSource("my-source")
		.AddOtlpExporter(options => options.Endpoint = new Uri("http://jaeger:4317")));

builder.Services.AddHttpClient();

var app = builder.Build();

app.UseMetricServer(url: "/metrics");
app.UseHttpMetrics();

var moneyCounter = Metrics.CreateCounter("money_spent", "Money spent on products", "currency");

app.MapGet("/products", (HttpClient httpClient, [FromServices] ILogger logger) =>
{
	logger.Debug("Getting products");
	return httpClient.GetFromJsonAsync<string[]>("http://stock:8080/products");
});

app.MapPost("/products/buy", async (string product, HttpClient httpClient, [FromServices] ILogger logger) =>
{
	var sum = Random.Shared.Next(100, 10000);
	
	logger.Debug("Buying {Product} for {Sum} roubles", product, sum);

	ActivitySource mySource = new("my-source");

	using (mySource.StartActivity("buying", ActivityKind.Client))
	{
		var reservationResult = await httpClient.PostAsync($"http://stock:8080/products/reserve/{product}", null);
		if (!reservationResult.IsSuccessStatusCode)
		{
			var reservationFailureMessage = reservationResult.Content.ReadAsStringAsync();
			logger.Warning("Reservation of {Product} failed: {ReservationFailureMessage}", product,
				reservationFailureMessage);
			return Results.BadRequest(reservationFailureMessage);
		}
	}
	
	using (mySource.StartActivity("paying", ActivityKind.Client))
	{
		var paymentResult = await httpClient.PostAsync($"http://payments:8080/pay/{sum}", null);
		if (!paymentResult.IsSuccessStatusCode)
		{
			var paymentFailureMessage = paymentResult.Content.ReadAsStringAsync();
			logger.Warning("Payment of {Sum} roubles failed: {PaymentFailureMessage}", sum, paymentFailureMessage);
			return Results.BadRequest(paymentFailureMessage);
		}
	}

	moneyCounter.WithLabels("roubles").Inc(sum);
	logger.Debug("The product {Product} was bought for {Sum} roubles", product, sum);
	return Results.Ok($"The product {product} was bought for {sum} roubles");
});

app.Run();
