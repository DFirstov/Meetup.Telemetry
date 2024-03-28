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

		var productIndex = Random.Shared.Next(0, products.Length);
		var product = products[productIndex];

		await httpClient.PostAsync($"http://shop:8080/products/buy?product={product}", null);
	}
	catch
	{
		// ignore
	}
}
