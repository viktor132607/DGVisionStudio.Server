using System.Security.Cryptography;
using DGVisionStudio.Application.Interfaces;

namespace DGVisionStudio.Api.Services;

public sealed record ArchiveEntryDigest(long Length, byte[] Hash);

public sealed class PortfolioArchivePhotoWriter(IFileStorageService storage)
{
    private static readonly HttpClient StaticFiles = new(
        new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    public async Task<ArchiveEntryDigest> CopyAsync(
        string url,
        Stream target,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new IOException("Missing source URL.");

        Stream? source = null;
        try
        {
            source = await storage.OpenReadAsync(url, token);
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException &&
            url.StartsWith("/images/", StringComparison.Ordinal))
        {
        }

        token.ThrowIfCancellationRequested();

        if (source is not null)
        {
            await using (source)
                return await CopyAndHashAsync(source, target, token);
        }

        if (!url.StartsWith("/images/", StringComparison.Ordinal))
            throw new IOException("Source not found.");

        var uri = new Uri(new Uri("https://dgvisionstudio.com"), url);
        if (uri.Host != "dgvisionstudio.com" ||
            !uri.AbsolutePath.StartsWith("/images/", StringComparison.Ordinal))
        {
            throw new IOException("Invalid static image path.");
        }

        using var response = await StaticFiles.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            token);

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentType?.MediaType is
            "text/html" or "application/json")
        {
            throw new IOException("The image URL returned an error page.");
        }

        await using var remote =
            await response.Content.ReadAsStreamAsync(token);

        return await CopyAndHashAsync(remote, target, token);
    }

    private static async Task<ArchiveEntryDigest> CopyAndHashAsync(
        Stream source,
        Stream target,
        CancellationToken token)
    {
        using var hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var buffer = new byte[81920];
        long length = 0;
        int count;

        while ((count = await source.ReadAsync(buffer, token)) > 0)
        {
            await target.WriteAsync(
                buffer.AsMemory(0, count),
                token);

            hash.AppendData(buffer, 0, count);
            length += count;
        }

        if (length == 0)
            throw new IOException("Empty source file.");

        return new ArchiveEntryDigest(
            length,
            hash.GetHashAndReset());
    }
}
