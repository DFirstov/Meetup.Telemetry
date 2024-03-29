var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders(); // disable default logging
builder.Services.AddHttpClient();

var app = builder.Build();

app.MapGet("/products", (HttpClient httpClient) =>
{
	return httpClient.GetFromJsonAsync<string[]>("http://stock:8080/products");
});

app.MapPost("/products/buy", async (string product, HttpClient httpClient) =>
{
	var sum = Random.Shared.Next(100, 10000);
	
	var reservationResult = await httpClient.PostAsync($"http://stock:8080/products/reserve/{product}", null);
	if (!reservationResult.IsSuccessStatusCode)
	{
		var reservationFailureMessage = reservationResult.Content.ReadAsStringAsync();
		return Results.BadRequest(reservationFailureMessage);
	}

	var paymentResult = await httpClient.PostAsync($"http://payments:8080/pay/{sum}", null);
	if (!paymentResult.IsSuccessStatusCode)
	{
		var paymentFailureMessage = paymentResult.Content.ReadAsStringAsync();
		return Results.BadRequest(paymentFailureMessage);
	}

	return Results.Ok($"The product {product} was bought for {sum} roubles");
});

app.Run();
