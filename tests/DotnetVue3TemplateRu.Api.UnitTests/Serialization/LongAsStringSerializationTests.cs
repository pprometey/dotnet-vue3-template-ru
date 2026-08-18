using System.Text.Json;
using DotnetVue3TemplateRu.Api.Serialization;

namespace DotnetVue3TemplateRu.Api.UnitTests.Serialization;

/// <summary>
/// Соглашение проекта: long (int64) всегда сериализуется строкой - JS Number
/// теряет точность выше 2^53-1. Эти тесты фиксируют поведение конвертеров.
/// </summary>
public class LongAsStringSerializationTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new LongAsStringJsonConverter());
        options.Converters.Add(new NullableLongAsStringJsonConverter());
        return options;
    }

    private sealed record Sample(long Value, long? Optional);

    [Test]
    [Arguments(long.MaxValue, "9223372036854775807")]
    [Arguments(long.MinValue, "-9223372036854775808")]
    public async Task Long_IsWritten_AsString(long value, string expected)
    {
        string json = JsonSerializer.Serialize(new Sample(value, null), Options);

        await Assert.That(json).IsEqualTo($"{{\"Value\":\"{expected}\",\"Optional\":null}}");
    }

    [Test]
    public async Task Long_RoundTrips_FromString()
    {
        string json = JsonSerializer.Serialize(new Sample(long.MaxValue, long.MinValue), Options);

        Sample? back = JsonSerializer.Deserialize<Sample>(json, Options);

        await Assert.That(back!.Value).IsEqualTo(long.MaxValue);
        await Assert.That(back.Optional).IsEqualTo(long.MinValue);
    }

    [Test]
    public async Task Long_AlsoReads_FromNumber()
    {
        // На вход терпимы и число, и строка - старые клиенты не ломаются.
        Sample? back = JsonSerializer.Deserialize<Sample>("{\"Value\":42,\"Optional\":7}", Options);

        await Assert.That(back!.Value).IsEqualTo(42L);
        await Assert.That(back.Optional).IsEqualTo(7L);
    }
}
