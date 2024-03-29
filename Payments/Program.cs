using Destructurama;
using Destructurama.Attributed;
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
	.WriteTo.Seq("http://seq")
	.Destructure.UsingAttributes());

var app = builder.Build();

string[] clients = ["John", "Paul", "George", "Ringo", "Yoko", "Pete", "Mick", "Keith", "Charlie", "Ronnie", "David"];

app.MapPost("/pay/{sum:decimal}", (decimal sum, [FromServices] ILogger logger) =>
{
	var clientAccount = GetClientAccount(sum);

	logger.Debug("Paying {Sum} roubles from the account {@ClientAccount}", sum, clientAccount);

	if (clientAccount.Balance < sum)
	{
		logger.Warning("Not enough money on the account {@ClientAccount}", clientAccount);
		return Results.BadRequest($"Not enough money on the account {clientAccount.ClientName}");
	}

	logger.Debug("Payment from the account {@ClientAccount} succeeded", clientAccount);
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

record ClientAccount
(
	[property: LogMasked(PreserveLength = true, ShowFirst = 1, ShowLast = 1)]
	string ClientName,

	[property: NotLogged]
	decimal Balance
);
