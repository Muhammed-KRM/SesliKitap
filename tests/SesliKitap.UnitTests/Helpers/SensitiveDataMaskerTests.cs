using FluentAssertions;
using SesliKitap.Business.Helpers;

namespace SesliKitap.UnitTests.Helpers;

public sealed class SensitiveDataMaskerTests
{
    [Fact]
    public void Mask_ShouldRedactSensitiveJsonKeysAndEmailsAndHeaders()
    {
        var input = """
            {"apiKey": "SECRET_123456", "mediaToken": "tok_abc", "t": "sig_999", "email": "test@ornek.com", "baslik": "Çalıkuşu"}
            X-API-Key: SUPER_GIZLI_ANAHTAR
            """;

        var masked = SensitiveDataMasker.Mask(input);

        masked.Should().NotContain("SECRET_123456");
        masked.Should().NotContain("tok_abc");
        masked.Should().NotContain("sig_999");
        masked.Should().NotContain("test@ornek.com");
        masked.Should().NotContain("SUPER_GIZLI_ANAHTAR");
        masked.Should().Contain("Çalıkuşu");
    }

    [Fact]
    public void MaskQueryString_ShouldRedactMediaTokenAndAccessToken()
    {
        var query = "?format=opus&t=SECRET_MEDIA_TOKEN_123&access_token=HUB_KEY_456&sayfa=2";

        var masked = SensitiveDataMasker.MaskQueryString(query);

        masked.Should().Be("?format=opus&t=***&access_token=***&sayfa=2");
    }

    [Fact]
    public void EnsureValidJsonb_WhenPlainTextGiven_ShouldWrapInValidJsonObject()
    {
        var jsonb = SensitiveDataMasker.EnsureValidJsonb("Düz metin açıklama, X-API-Key: gizli123");

        jsonb.Should().NotBeNull();
        jsonb.Should().NotContain("gizli123");
        jsonb.Should().StartWith("{\"message\":");
    }

    [Fact]
    public void EnsureValidJsonb_WhenValidJsonGiven_ShouldPreserveStructureAndMaskSecrets()
    {
        var jsonb = SensitiveDataMasker.EnsureValidJsonb("{\"sayfa\": 210, \"ttsApiToken\": \"secret_tok\"}");

        jsonb.Should().Be("{\"sayfa\": 210, \"ttsApiToken\": \"***\"}");
    }
}
