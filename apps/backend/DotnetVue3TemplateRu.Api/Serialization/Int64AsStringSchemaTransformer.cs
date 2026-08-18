using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DotnetVue3TemplateRu.Api.Serialization;

// Кастомный JsonConverter (LongAsStringJsonConverter) сам по себе не меняет
// генерируемую схему - .NET OpenAPI всё равно отдаёт integer/int64. Этот
// трансформер приводит спек в соответствие с рантаймом: int64 -> string,
// чтобы Orval сгенерировал TS-тип string. Признак nullable сохраняется.
public sealed class Int64AsStringSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (schema.Type is { } type
            && type.HasFlag(JsonSchemaType.Integer)
            && schema.Format == "int64")
        {
            bool nullable = type.HasFlag(JsonSchemaType.Null);
            schema.Type = nullable ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String;
            // Format "int64" оставляем как подсказку о происхождении значения;
            // Orval всё равно маппит любой string-тип в TS string.
        }

        return Task.CompletedTask;
    }
}
