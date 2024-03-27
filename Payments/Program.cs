var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapPost("/pay/{sum:decimal}", (decimal sum) =>
	{
		var clientBalance = Random.Shared.Next(0, (int)(10 * sum));
		
		if (clientBalance < sum)
		{
			return Results.BadRequest("Not enough money on the account");
		}

		return Results.Ok("Payment succeeded");
	});

app.Run();
