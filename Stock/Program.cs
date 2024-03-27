var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string[] products = ["milk", "bread", "cheese", "apples", "oranges", "bananas", "eggs", "chicken", "fish", "tomatoes"];

app.MapGet("/products", () =>
{
	return products;
});

app.MapPost("/products/reserve/{product}", (string product) =>
{
	if (!products.Contains(product))
		return Results.BadRequest($"Product {product} not found");

	if (Random.Shared.Next(0, 10) == 0)
		return Results.BadRequest($"Product {product} out of stock");
	
	return Results.Ok($"Product {product} reserved");
});

app.Run();
