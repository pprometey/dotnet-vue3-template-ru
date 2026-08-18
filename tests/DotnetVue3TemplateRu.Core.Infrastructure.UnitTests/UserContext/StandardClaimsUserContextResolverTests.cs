using System.Security.Claims;
using DotnetVue3TemplateRu.Core.Application.UserContext;
using DotnetVue3TemplateRu.Core.Infrastructure.UserContext;

namespace DotnetVue3TemplateRu.Core.Infrastructure.UnitTests.UserContext;

/// <summary>
/// Разбор claims - шов между форматом токена конкретного провайдера и приложением
/// (ADR 0023). Порт принимает System.Security.Claims.Claim, поэтому проверяется
/// без сети и без поднятия хоста: ни провайдер, ни ASP.NET здесь не участвуют.
/// </summary>
public class StandardClaimsUserContextResolverTests
{
    private static readonly StandardClaimsUserContextResolver Resolver = new();

    [Test]
    public async Task Resolve_ReadsSubject_FromSubClaim()
    {
        UserContextSnapshot snapshot = Resolver.Resolve([new Claim("sub", "user-42")]);

        await Assert.That(snapshot).IsEqualTo(new UserContextSnapshot("user-42"));
    }

    [Test]
    public async Task Resolve_FallsBackToNameIdentifier_WhenSubIsAbsent()
    {
        // Запасной путь на случай включённого MapInboundClaims: ASP.NET переименовал бы
        // "sub" в длинный URI, и без запасного чтения идентичность стала бы пустой.
        UserContextSnapshot snapshot = Resolver.Resolve(
            [new Claim(ClaimTypes.NameIdentifier, "user-42")]);

        await Assert.That(snapshot).IsEqualTo(new UserContextSnapshot("user-42"));
    }

    [Test]
    public async Task Resolve_PrefersSubject_OverNameIdentifier()
    {
        UserContextSnapshot snapshot = Resolver.Resolve(
        [
            new Claim(ClaimTypes.NameIdentifier, "mapped"),
            new Claim("sub", "raw"),
        ]);

        await Assert.That(snapshot).IsEqualTo(new UserContextSnapshot("raw"));
    }

    [Test]
    public async Task Resolve_IgnoresProfileClaims()
    {
        // Профиль стандарт адресует клиентскому приложению, а не API: в токене
        // доступа его может не быть вовсе, и приложение на него не опирается.
        UserContextSnapshot snapshot = Resolver.Resolve(
        [
            new Claim("sub", "user-42"),
            new Claim("email", "user@example.com"),
            new Claim("preferred_username", "user"),
            new Claim("scope", "openid profile email"),
        ]);

        await Assert.That(snapshot).IsEqualTo(new UserContextSnapshot("user-42"));
    }

    [Test]
    public async Task Resolve_YieldsEmptySubject_WhenNoIdentifyingClaimPresent()
    {
        UserContextSnapshot snapshot = Resolver.Resolve([new Claim("scope", "openid")]);

        await Assert.That(snapshot).IsEqualTo(new UserContextSnapshot(string.Empty));
    }

    [Test]
    public async Task Resolve_TreatsBlankSubject_AsAbsent()
    {
        UserContextSnapshot snapshot = Resolver.Resolve(
        [
            new Claim("sub", "   "),
            new Claim(ClaimTypes.NameIdentifier, "user-42"),
        ]);

        await Assert.That(snapshot).IsEqualTo(new UserContextSnapshot("user-42"));
    }
}
