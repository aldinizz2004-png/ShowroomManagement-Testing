using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Showroom.Api.Services;

public static partial class CustomerValidator
{
    public const int FullNameMaxLength = 100;
    public const int EmailMaxLength = 256;
    public const int AddressMaxLength = 200;

    [GeneratedRegex(@"^\+?[0-9 ]{7,20}$")]
    private static partial Regex PhonePattern();

    public static Dictionary<string, string[]> Validate(string? fullName, string? email, string? phone, string? address)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length < 2)
            errors["fullName"] = ["Full name is required (at least 2 characters)."];
        else if (fullName.Trim().Length > FullNameMaxLength)
            errors["fullName"] = [$"Full name must be at most {FullNameMaxLength} characters."];

        if (string.IsNullOrWhiteSpace(email))
            errors["email"] = ["Email is required."];
        else if (email.Trim().Length > EmailMaxLength || !IsValidEmail(email.Trim()))
            errors["email"] = ["Email is not a valid email address."];

        if (string.IsNullOrWhiteSpace(phone))
            errors["phone"] = ["Phone is required."];
        else if (!PhonePattern().IsMatch(phone.Trim()))
            errors["phone"] = ["Phone must contain 7 to 20 digits, optionally starting with +."];

        if (address is not null && address.Trim().Length > AddressMaxLength)
            errors["address"] = [$"Address must be at most {AddressMaxLength} characters."];

        return errors;
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static bool IsValidEmail(string email) =>
        MailAddress.TryCreate(email, out var parsed) && parsed.Address == email && email.Contains('.');
}
