var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders(); // disable default logging

var app = builder.Build();

string[] clients = ["John", "Paul", "George", "Ringo", "Yoko", "Pete", "Mick", "Keith", "Charlie", "Ronnie", "David"];

app.MapPost("/pay/{sum:decimal}", (decimal sum) =>
{
	var clientAccount = GetClientAccount(sum);
	if (clientAccount.Balance < sum)
	{
		return Results.BadRequest($"Not enough money on the account {clientAccount.ClientName}");
	}

	return Results.Ok($"Payment of the client {clientAccount.ClientName} succeeded");
});

app.Run();

ClientAccount GetClientAccount(decimal sum)
{
	var clientIndex = Random.Shared.Next(0, clients.Length);
	var clientName = clients[clientIndex];
	var clientBalance = Random.Shared.Next(0, (int)(10 * sum));

	return new ClientAccount(clientName, clientBalance);
}

record ClientAccount(
	string ClientName,
	decimal Balance);
