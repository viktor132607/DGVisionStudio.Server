using System.IO.Compression;
using System.Security.Cryptography;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioArchiveVerifier
{
    public async Task VerifyAsync(
        string path,
        IReadOnlyDictionary<string, ArchiveEntryDigest> manifest,
        int directoryCount,
        CancellationToken token)
    {
        using var zip = ZipFile.OpenRead(path);

        if (zip.Entries.Count != manifest.Count + directoryCount)
            throw new IOException("Archive entry count mismatch.");

        foreach (var (name, expected) in manifest)
        {
            token.ThrowIfCancellationRequested();

            var entry = zip.GetEntry(name) ??
                throw new IOException("Missing ZIP entry.");

            await using var content = entry.Open();
            var hash = await SHA256.HashDataAsync(content, token);

            if (entry.Length != expected.Length ||
                !CryptographicOperations.FixedTimeEquals(
                    hash,
                    expected.Hash))
            {
                throw new IOException("Archive verification failed.");
            }
        }
    }
}
