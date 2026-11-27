using System.Net.Mail;
using Aahar.Domain;

namespace Aahar.Application;

public sealed record RestaurantProfile
{
    public string Name { get; init; } = "";
    public string Tagline { get; init; } = "";
    public string Address { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Email { get; init; } = "";
    public string Website { get; init; } = "";
    public string ReceiptFooter { get; init; } = "";
    public Guid? Version { get; init; }
}

public interface IRestaurantProfileService
{
    Task<RestaurantProfile> ReadAsync(CancellationToken ct);
    Task<RestaurantProfile> SaveAsync(RestaurantProfile profile, CancellationToken ct);
}

public static class RestaurantProfileRules
{
    public static RestaurantProfile Normalize(RestaurantProfile profile)
    {
        static string Text(string? value, int limit, string label)
        {
            var text = value?.Trim() ?? "";
            if (text.Length > limit || text.Any(c => char.IsControl(c) &&
                !(label is "Address" or "Receipt footer" && c is '\r' or '\n')))
                throw new DomainException($"{label} is too long or contains unsupported characters.");
            return text;
        }

        var result = profile with
        {
            Name = Text(profile.Name, 100, "Restaurant name"),
            Tagline = Text(profile.Tagline, 150, "Tagline"),
            Address = Text(profile.Address, 300, "Address"),
            Phone = Text(profile.Phone, 50, "Phone"),
            Email = Text(profile.Email, 150, "Email"),
            Website = Text(profile.Website, 250, "Website"),
            ReceiptFooter = Text(profile.ReceiptFooter, 300, "Receipt footer")
        };
        if (result.Name.Length == 0) throw new DomainException("Restaurant name is required.");
        if (result.Email.Length > 0 && (!MailAddress.TryCreate(result.Email, out var email) || email.Address != result.Email))
            throw new DomainException("Enter a valid email address.");
        if (result.Website.Length > 0 && (!Uri.TryCreate(result.Website, UriKind.Absolute, out var website) ||
            website.Scheme is not ("https" or "http") || website.UserInfo.Length > 0 || website.Host.Length == 0))
            throw new DomainException("Website must be an http or https address without embedded credentials.");
        return result;
    }
}
