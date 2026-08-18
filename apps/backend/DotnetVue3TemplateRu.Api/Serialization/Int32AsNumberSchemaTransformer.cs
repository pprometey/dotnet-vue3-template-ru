using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DotnetVue3TemplateRu.Api.Serialization;

// Веб-дефолты System.Text.Json (JsonSerializerDefaults.Web -> JsonNumberHandling
// AllowReadingFromString) заставляют .NET описывать любое целое как union
// integer|string с числовым pattern - контракт отражает толерантный вход (примет и
// 5, и "5"). Для int64 это переопределяет Int64AsStringSchemaTransformer (в string,
// ADR 0010); int32 иначе достался бы фронту как number|string. int32 безопасно
// ложится в JS Number (макс. ~2.1 * 10^9, много ниже 2^53-1), поэтому снимаем string
// из union и очищаем pattern - в контракте остаётся чистое integer, и Orval
// генерирует number. double/decimal (тип number) не трогаем: decimal теряет точность
// в JS так же, как int64, и его нормализация - отдельное решение.
public sealed class Int32AsNumberSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (schema.Type is { } type
            && type.HasFlag(JsonSchemaType.Integer)
            && type.HasFlag(JsonSchemaType.String)
            && schema.Format != "int64")
        {
            schema.Type = type & ~JsonSchemaType.String;
            schema.Pattern = null;
        }

        return Task.CompletedTask;
    }
}
