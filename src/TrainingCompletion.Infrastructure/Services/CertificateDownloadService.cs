using Microsoft.EntityFrameworkCore;
using TrainingCompletion.Application;
using TrainingCompletion.Domain;
using TrainingCompletion.Infrastructure.Persistence;

namespace TrainingCompletion.Infrastructure.Services;

public sealed class CertificateDownloadService(
    TrainingDbContext dbContext,
    ICertificateStore certificateStore)
{
    public async Task<CertificateDownload> DownloadAsync(
        Guid completionId,
        CancellationToken cancellationToken)
    {
        var completion = await dbContext.CourseCompletions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == completionId, cancellationToken)
            ?? throw new NotFoundException("completion_not_found", "The completion was not found.");

        if (completion.CertificateStatus != CertificateStatus.Ready ||
            string.IsNullOrWhiteSpace(completion.CertificateBlobName))
        {
            throw new ConflictException(
                "certificate_not_ready",
                "The certificate is not ready yet.");
        }

        return await certificateStore.DownloadAsync(
            completion.CertificateBlobName,
            cancellationToken)
            ?? throw new ConflictException(
                "certificate_not_ready",
                "The certificate blob is not available yet.");
    }
}

