using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Datavello.Api.Infrastructure.Security;

/// <summary>Documents the required client header on state-changing operations and pre-fills it in Swagger UI.</summary>
public sealed class ClientHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var method = context.ApiDescription.HttpMethod;
        if (method is null || HttpMethods.IsGet(method) || HttpMethods.IsHead(method))
        {
            return;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = RequireClientHeaderMiddleware.HeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Description = "Proteção CSRF. Use sempre o valor \"datavello\".",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Default = JsonValue.Create(RequireClientHeaderMiddleware.HeaderValue) }
        });
    }
}
