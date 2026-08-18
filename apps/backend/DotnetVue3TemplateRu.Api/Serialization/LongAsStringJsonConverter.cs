using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotnetVue3TemplateRu.Api.Serialization;

// long доходит до ~9.2e18, а JS Number теряет точность выше 2^53-1 (~9e15).
// Поэтому long всегда сериализуется строкой - так точность не теряется на
// JSON.parse во фронтенде. На входе терпимо принимаем и строку, и число.
// Парная схема для OpenAPI - Int64AsStringSchemaTransformer.
public sealed class LongAsStringJsonConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.String => long.Parse(reader.GetString()!, CultureInfo.InvariantCulture),
            JsonTokenType.Number => reader.GetInt64(),
            _ => throw new JsonException($"Ожидались строка или число для long, получено {reader.TokenType}."),
        };

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}

// Nullable-вариант: null остаётся null, иначе делегирует логику выше.
public sealed class NullableLongAsStringJsonConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => long.Parse(reader.GetString()!, CultureInfo.InvariantCulture),
            JsonTokenType.Number => reader.GetInt64(),
            _ => throw new JsonException($"Ожидались строка, число или null для long?, получено {reader.TokenType}."),
        };

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToString(CultureInfo.InvariantCulture));
    }
}
