using QuestPDF.Infrastructure;
using TrainingCompletion.Infrastructure.Certificates;

namespace TrainingCompletion.UnitTests;

public sealed class CertificateGeneratorTests
{
    [Fact]
    public void Generate_ReturnsPdfDocument()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var generator = new QuestPdfCertificateGenerator();

        var document = generator.Generate(
            "Demo Learner",
            "Event-Driven Architecture Fundamentals",
            new DateTimeOffset(2026, 7, 27, 0, 0, 0, TimeSpan.Zero));

        Assert.True(document.Length > 1_000);
        Assert.Equal("%PDF"u8.ToArray(), document[..4]);
    }
}

