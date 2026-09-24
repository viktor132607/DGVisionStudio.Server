using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class PrivacyAnonymizationService(
    AppDbContext context,
    PrivacyAnonymizationMapper mapper)
{
    public async Task<bool> AnonymizeUserDataAsync(string userId)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user is null)
            return false;

        var oldEmail = user.Email;
        var anonymizedEmail = mapper.AnonymizeAccount(user);

        var ownedGalleries = await context.PortfolioAlbums
            .IgnoreQueryFilters()
            .Where(x => x.OwnerUserId == userId)
            .ToListAsync();

        foreach (var gallery in ownedGalleries)
            gallery.OwnerUserId = null;

        var accesses = await context.UserAlbumAccesses
            .Where(x => x.UserId == userId)
            .ToListAsync();

        context.UserAlbumAccesses.RemoveRange(accesses);

        var printRequests = await context.PrintRequests
            .Where(x => x.UserId == userId)
            .ToListAsync();

        foreach (var request in printRequests)
            mapper.AnonymizePrintRequest(request, anonymizedEmail);

        if (!string.IsNullOrWhiteSpace(oldEmail))
        {
            var contactRequests = await context.ContactRequests
                .Where(x => x.Email == oldEmail)
                .ToListAsync();

            foreach (var request in contactRequests)
                mapper.AnonymizeContactRequest(request, anonymizedEmail);
        }

        await context.SaveChangesAsync();
        return true;
    }
}
