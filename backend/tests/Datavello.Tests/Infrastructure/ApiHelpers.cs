using System.Net;
using System.Net.Http.Json;
using Datavello.Api.Domain;
using Datavello.Api.Features.Categories;
using Datavello.Api.Features.Transactions;

namespace Datavello.Tests.Infrastructure;

public static class ApiHelpers
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"{(int)response.StatusCode} {response.StatusCode}: {body}");
        }

        return (await response.Content.ReadFromJsonAsync<T>(Json.Options))!;
    }

    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url) =>
        await (await client.GetAsync(url)).ReadAsync<T>();

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body, Json.Options);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body, Json.Options);

    public static async Task<CategoryDto> CategoryAsync(this HttpClient client, string name) =>
        (await client.GetJsonAsync<List<CategoryDto>>("/api/categories")).Single(c => c.Name == name);

    public static async Task<TransactionDto> AddTransactionAsync(
        this HttpClient client, string date, TransactionType type, decimal amount, Guid categoryId, string description = "Lançamento")
    {
        var response = await client.PostJsonAsync("/api/transactions", new
        {
            date,
            description,
            type,
            amount,
            categoryId
        });
        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw new HttpRequestException($"Expected 201, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        return await response.ReadAsync<TransactionDto>();
    }
}
