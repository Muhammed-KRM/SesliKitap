using System.Text.Json;
using FluentAssertions;
using SesliKitap.Business.Constants;
using SesliKitap.Business.DTOs.Synthesis;
using SesliKitap.Business.Events;
using SesliKitap.Data.Entities;

namespace SesliKitap.UnitTests.Contracts;

public sealed class SynthesisContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static string GetContractsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SesliKitap.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir!.FullName, "tests", "contracts");
    }

    [Fact]
    public void SynthesisInputPayload_SerializesToExpectedContractJson()
    {
        var payload = new SynthesisInputPayload(
            Guid.Parse("01930000-0000-7000-8000-000000000001"),
            new SynthesisInputBook(
                Guid.Parse("01930000-0000-7000-8000-000000000002"),
                "Kürk Mantolu Madonna",
                "Sabahattin Ali",
                "books/01930000-0000-7000-8000-000000000002/cover.jpg"),
            new SynthesisInputVoiceProfile(
                Guid.Parse("01930000-0000-7000-8000-000000000003"),
                "Anlatıcı Sesi",
                "voices/01930000-0000-7000-8000-000000000003/reference.wav",
                new string('1', 64),
                "Merhaba dünya, bu bir ses profili referans metnidir.",
                new VoiceParameters
                {
                    CfgValue = 2.0,
                    InferenceTimesteps = 10,
                    KonusmaHizi = 1.0,
                    Tohum = 42
                }),
            ["voxcpm2_modal", "xtts2_modal"],
            new SynthesisInputSettings(
                CumleArasiMs: 420,
                ParagrafArasiMs: 950,
                BaslikSonrasiMs: 1400,
                DiyalogMs: 320,
                BolumBasligiOku: true,
                OpusBitrateKbps: 48,
                Mp3BitrateKbps: 64,
                HedefLufs: -18.0),
            [
                new SynthesisInputPronunciation("Madonna", "Madonna", BuyukKucukDuyarli: false)
            ],
            [
                new SynthesisInputChapter(
                    Guid.Parse("01930000-0000-7000-8000-000000000004"),
                    SiraNo: 1,
                    Baslik: "Birinci Bölüm",
                    MetinSurumu: 1,
                    TemizMetin: "Raif Efendi sessiz bir adamdı.\n\nBerlin yıllarını kimseye anlatmazdı.",
                    SesSurumu: null)
            ]);

        var contractPath = Path.Combine(GetContractsDirectory(), "synthesis_input.sample.json");
        var contractJson = File.ReadAllText(contractPath);

        using var expectedDoc = JsonDocument.Parse(contractJson);
        using var actualDoc = JsonDocument.Parse(JsonSerializer.Serialize(payload, JsonOptions));

        actualDoc.RootElement.GetProperty("jobId").GetString().Should().Be(expectedDoc.RootElement.GetProperty("jobId").GetString());
        actualDoc.RootElement.GetProperty("book").GetProperty("baslik").GetString().Should().Be("Kürk Mantolu Madonna");
        actualDoc.RootElement.GetProperty("voiceProfile").GetProperty("referansHash").GetString().Should().Be(new string('1', 64));
        actualDoc.RootElement.GetProperty("ayarlar").GetProperty("cumleArasiMs").GetInt32().Should().Be(420);
        actualDoc.RootElement.GetProperty("chapters")[0].GetProperty("siraNo").GetInt32().Should().Be(1);
    }

    [Fact]
    public void ChapterSynthesizedEvent_DeserializesFromPythonContractSample()
    {
        var contractPath = Path.Combine(GetContractsDirectory(), "chapter_synthesized.sample.json");
        var contractJson = File.ReadAllText(contractPath);

        var evt = JsonSerializer.Deserialize<ChapterSynthesizedEvent>(contractJson, JsonOptions);

        evt.Should().NotBeNull();
        evt!.JobId.Should().Be(Guid.Parse("01930000-0000-7000-8000-000000000001"));
        evt.BookId.Should().Be(Guid.Parse("01930000-0000-7000-8000-000000000002"));
        evt.ChapterId.Should().Be(Guid.Parse("01930000-0000-7000-8000-000000000004"));
        evt.MetinSurumu.Should().Be(1);
        evt.OpusBoyutu.Should().Be(124_500);
        evt.Mp3Boyutu.Should().Be(168_900);
        evt.SureSn.Should().BeApproximately(62.4, 0.01);
        evt.CumleSayisi.Should().Be(14);
        evt.Motor.Should().Be("voxcpm2_modal");
        evt.GpuMs.Should().Be(14_200);
        evt.OnbellekParca.Should().Be(3);
        evt.UretilenParca.Should().Be(11);
    }

    [Fact]
    public void CacheKeys_JobCancel_MatchesCrossLanguagePattern()
    {
        var sampleJobId = Guid.Parse("01930000-0000-7000-8000-000000000001");

        CacheKeys.JobCancel(sampleJobId).Should().Be("sk:job:01930000-0000-7000-8000-000000000001:cancel");
    }
}
