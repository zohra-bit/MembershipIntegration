using System.Globalization;
using System.Net.Mail;

namespace MembershipIntegration;

public static class RegistrationMapper
{
    private static readonly Dictionary<string, string> PlanCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Single"] = "S",
        ["Family"] = "F",
        ["Couple"] = "C"
    };

    public static MappingResult Map(CrmRegistration registration)
    {
        var errors = new Dictionary<string, string[]>();
        var firstName = Required(registration.FirstName, "firstName", errors);
        var lastName = Required(registration.LastName, "lastName", errors);
        var dateText = Required(registration.DateOfBirth, "dateOfBirth", errors);
        var email = Required(registration.Email, "email", errors);
        var membershipType = Required(registration.MembershipType, "membershipType", errors);

        var validDate = DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var dateOfBirth);
        if (dateText.Length > 0 && !validDate)
        {
            errors["dateOfBirth"] = ["Use a valid date in yyyy-MM-dd format."];
        }

        if (email.Length > 0 && EmailError(email) is { } emailError)
        {
            errors["email"] = [emailError];
        }

        var validPlan = PlanCodes.TryGetValue(membershipType, out var planCode);
        if (membershipType.Length > 0 && !validPlan)
        {
            errors["membershipType"] = ["Allowed values are Single, Family, and Couple."];
        }

        if (errors.Count > 0)
        {
            return new MappingResult(null, errors);
        }

        var member = new LegacyMember(
            firstName,
            lastName,
            dateOfBirth.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            new LegacyContact(email),
            planCode!,
            "MILKYWAY");

        return new MappingResult(new LegacyRegistration(member), errors);
    }

    private static string? EmailError(string email)
    {
        var separator = email.LastIndexOf('@');
        if (separator < 0)
        {
            return "Email address must include an '@' symbol.";
        }
        if (separator == 0)
        {
            return "Enter the part of the email address before '@'.";
        }
        if (separator == email.Length - 1)
        {
            return "Enter a domain after '@', such as example.org.";
        }
        if (!MailAddress.TryCreate(email, out var address) ||
            !string.Equals(address.Address, email, StringComparison.Ordinal))
        {
            return "Enter a valid email address in the format name@domain.extension.";
        }
        if (!address.Host.Contains('.') || address.Host.EndsWith('.'))
        {
            return "Email domain must include a suffix, such as .com, .org, or .com.au.";
        }
        if (address.Host.Contains('_') ||
            address.Host.Split('.').Any(label => label.StartsWith('-') || label.EndsWith('-')) ||
            Uri.CheckHostName(address.Host) != UriHostNameType.Dns)
        {
            return "Enter a valid email domain, such as example.org.";
        }
        return null;
    }

    private static string Required(string? value, string field, Dictionary<string, string[]> errors)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            errors[field] = ["This field is required."];
        }
        return text;
    }
}
