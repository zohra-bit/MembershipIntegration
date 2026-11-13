using System.Text.Json.Serialization;

namespace MembershipIntegration;

public sealed record CrmRegistration
{
    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; init; }

    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("membershipType")]
    public string? MembershipType { get; init; }
}

public sealed record LegacyRegistration(
    [property: JsonPropertyName("member")] LegacyMember Member);

public sealed record LegacyMember(
    [property: JsonPropertyName("given_name")] string GivenName,
    [property: JsonPropertyName("family_name")] string FamilyName,
    [property: JsonPropertyName("dob")] string Dob,
    [property: JsonPropertyName("contact")] LegacyContact Contact,
    [property: JsonPropertyName("plan_code")] string PlanCode,
    [property: JsonPropertyName("source")] string Source);

public sealed record LegacyContact(
    [property: JsonPropertyName("email")] string Email);

public sealed record MappingResult(
    LegacyRegistration? Payload,
    Dictionary<string, string[]> Errors);
