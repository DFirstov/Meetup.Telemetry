using System.Net.Http.Json;

HttpClient httpClient = new();

while (true)
{
	try
	{
		await Task.Delay(Random.Shared.Next(0, 1000));

		var products = await httpClient.GetFromJsonAsync<string[]>("http://shop:8080/products");
		if (products == null)
			continue;

		var productIndex = Random.Shared.Next(0, products.Length + 1);
		var product = productIndex < products.Length
			? products[productIndex]
			: "unknown";

		await httpClient.PostAsync($"http://shop:8080/products/buy?product={product}", null);
	}
	catch
	{
		// ignore
	}
}
