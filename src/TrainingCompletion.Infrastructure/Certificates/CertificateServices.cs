using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TrainingCompletion.Application;

namespace TrainingCompletion.Infrastructure.Certificates;

public sealed class QuestPdfCertificateGenerator : ICertificateDocumentGenerator
{
    public byte[] Generate(string learnerName, string courseTitle, DateTimeOffset completedAt)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(20));
                page.Content()
                    .Border(3)
                    .BorderColor(Colors.Blue.Medium)
                    .Padding(40)
                    .Column(column =>
                {
                    column.Spacing(24);
                    column.Item().AlignCenter().Text("Certificate of Completion")
                        .FontSize(34).Bold().FontColor(Colors.Blue.Darken2);
                    column.Item().AlignCenter().Text("This certifies that");
                    column.Item().AlignCenter().Text(learnerName).FontSize(30).Bold();
                    column.Item().AlignCenter().Text("successfully completed");
                    column.Item().AlignCenter().Text(courseTitle).FontSize(26).Bold();
                    column.Item().AlignCenter().Text($"Completed {completedAt:MMMM d, yyyy}");
                    column.Item().AlignCenter().Text("Training Completion Platform")
                        .FontSize(14).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();
    }
}

public sealed class AzureBlobCertificateStore : ICertificateStore
{
    private readonly BlobContainerClient containerClient;

    public AzureBlobCertificateStore(IConfiguration configuration)
    {
        var containerName = configuration["Storage:CertificateContainer"] ?? "certificates";
        var connectionString = configuration["Storage:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            containerClient = new BlobContainerClient(connectionString, containerName);
            return;
        }

        var serviceUri = configuration["Storage:ServiceUri"]
            ?? throw new InvalidOperationException(
                "Configure Storage:ConnectionString or Storage:ServiceUri.");
        containerClient = new BlobContainerClient(
            new Uri($"{serviceUri.TrimEnd('/')}/{containerName}"),
            new DefaultAzureCredential());
    }

    public async Task UploadAsync(
        string blobName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var blob = containerClient.GetBlobClient(blobName);
        await blob.UploadAsync(
            BinaryData.FromBytes(content),
            overwrite: true,
            cancellationToken);
        await blob.SetHttpHeadersAsync(
            new BlobHttpHeaders { ContentType = "application/pdf" },
            cancellationToken: cancellationToken);
    }

    public async Task<CertificateDownload?> DownloadAsync(
        string blobName,
        CancellationToken cancellationToken)
    {
        var blob = containerClient.GetBlobClient(blobName);
        if (!await blob.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return new CertificateDownload(
            response.Value.Content,
            response.Value.Details.ContentType ?? "application/pdf",
            response.Value.Details.ContentLength);
    }
}
