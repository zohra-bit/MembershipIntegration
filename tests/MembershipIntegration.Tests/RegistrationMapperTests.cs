using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using MembershipIntegration;
using Xunit;

namespace MembershipIntegration.Tests;

public sealed class RegistrationMapperTests
{
    [Fact]
    public void Assessment_example_produces_the_exact_legacy_json_contract()
    {
        var payload = AssertSuccess(RegistrationMapper.Map(ValidRegistration()));

        var json = JsonSerializer.SerializeToElement(payload);
        Assert.Equal(new[] { "member" }, json.EnumerateObject().Select(property => property.Name));
        var member = json.GetProperty("member");
        Assert.Equal(
            new[] { "contact", "dob", "family_name", "given_name", "plan_code", "source" },
            member.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal("Alex", member.GetProperty("given_name").GetString());
        Assert.Equal("Nguyen", member.GetProperty("family_name").GetString());
        Assert.Equal("12/04/1990", member.GetProperty("dob").GetString());
        Assert.Equal("S", member.GetProperty("plan_code").GetString());
        Assert.Equal("MILKYWAY", member.GetProperty("source").GetString());
        var contact = member.GetProperty("contact");
        Assert.Equal(new[] { "email" }, contact.EnumerateObject().Select(property => property.Name));
        Assert.Equal("alex.nguyen@example.com", contact.GetProperty("email").GetString());
    }

    [Theory]
    [InlineData("firstName", null)]
    [InlineData("firstName", "")]
    [InlineData("firstName", " \t ")]
    [InlineData("lastName", null)]
    [InlineData("lastName", "")]
    [InlineData("lastName", " \t ")]
    [InlineData("dateOfBirth", null)]
    [InlineData("dateOfBirth", "")]
    [InlineData("dateOfBirth", " \t ")]
    [InlineData("email", null)]
    [InlineData("email", "")]
    [InlineData("email", " \t ")]
    [InlineData("membershipType", null)]
    [InlineData("membershipType", "")]
    [InlineData("membershipType", " \t ")]
    public void Missing_empty_or_whitespace_required_values_return_field_errors(string field, string? value)
    {
        var result = RegistrationMapper.Map(WithField(ValidRegistration(), field, value));

        Assert.Null(result.Payload);
        Assert.Single(result.Errors);
        Assert.True(result.Errors.TryGetValue(field, out var messages));
        Assert.NotNull(messages);
        Assert.NotEmpty(messages);
        Assert.All(messages, message => Assert.False(string.IsNullOrWhiteSpace(message)));
    }

    [Theory]
    [InlineData("Single", "S")]
    [InlineData("Family", "F")]
    [InlineData("Couple", "C")]
    public void Known_membership_types_map_to_legacy_plan_codes(string membershipType, string expectedCode)
    {
        var request = ValidRegistration() with { MembershipType = membershipType };

        var payload = AssertSuccess(RegistrationMapper.Map(request));

        Assert.Equal(expectedCode, payload.Member.PlanCode);
    }

    [Theory]
    [InlineData("12/04/1990")]
    [InlineData("1990-4-12")]
    [InlineData("1990-04-31")]
    [InlineData("2023-02-29")]
    [InlineData("1990-04-12T00:00:00Z")]
    public void Alternate_or_impossible_dates_return_a_date_error(string dateOfBirth)
    {
        var result = RegistrationMapper.Map(ValidRegistration() with { DateOfBirth = dateOfBirth });

        Assert.Null(result.Payload);
        Assert.Single(result.Errors);
        Assert.True(result.Errors.ContainsKey("dateOfBirth"));
        Assert.NotEmpty(result.Errors["dateOfBirth"]);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("alex@")]
    [InlineData("Alex Nguyen <alex.nguyen@example.com>")]
    public void Invalid_or_display_name_email_values_return_an_email_error(string email)
    {
        var result = RegistrationMapper.Map(ValidRegistration() with { Email = email });

        Assert.Null(result.Payload);
        Assert.Single(result.Errors);
        Assert.True(result.Errors.ContainsKey("email"));
        Assert.NotEmpty(result.Errors["email"]);
    }

    [Theory]
    [InlineData("alex.nguyenexample.com", "Email address must include an '@' symbol.")]
    [InlineData("@example.org", "Enter the part of the email address before '@'.")]
    [InlineData("alex.nguyen@", "Enter a domain after '@', such as example.org.")]
    [InlineData("alex.nguyen@example", "Email domain must include a suffix, such as .com, .org, or .com.au.")]
    [InlineData("alex.nguyen@example.", "Email domain must include a suffix, such as .com, .org, or .com.au.")]
    public void Incomplete_email_addresses_explain_the_missing_component(string email, string expectedMessage)
    {
        var result = RegistrationMapper.Map(ValidRegistration() with { Email = email });

        Assert.Null(result.Payload);
        Assert.Equal(expectedMessage, Assert.Single(result.Errors["email"]));
    }

    [Theory]
    [InlineData("alex@example.org")]
    [InlineData("alex@example.net")]
    [InlineData("alex@example.com.au")]
    [InlineData("alex@example.technology")]
    [InlineData("\"alex@team\"@example.org")]
    public void Valid_email_domains_do_not_require_a_com_suffix(string email)
    {
        var payload = AssertSuccess(RegistrationMapper.Map(ValidRegistration() with { Email = email }));

        Assert.Equal(email, payload.Member.Contact.Email);
    }

    [Theory]
    [InlineData("alex@example..org")]
    [InlineData("alex@-example.org")]
    [InlineData("alex@example-.org")]
    [InlineData("alex@exam_ple.org")]
    [InlineData("alex@127.0.0.1")]
    [InlineData("alex@[127.0.0.1]")]
    public void Malformed_or_ip_address_domains_are_rejected(string email)
    {
        var result = RegistrationMapper.Map(ValidRegistration() with { Email = email });

        Assert.Null(result.Payload);
        Assert.True(result.Errors.ContainsKey("email"));
    }

    [Fact]
    public void Unknown_membership_type_returns_a_membership_error()
    {
        var result = RegistrationMapper.Map(ValidRegistration() with { MembershipType = "Corporate" });

        Assert.Null(result.Payload);
        Assert.Single(result.Errors);
        Assert.True(result.Errors.ContainsKey("membershipType"));
        Assert.NotEmpty(result.Errors["membershipType"]);
    }

    [Fact]
    public void Extra_fields_and_unused_registeredAt_do_not_affect_mapping()
    {
        const string json = """
            {
              "firstName": "Alex",
              "lastName": "Nguyen",
              "dateOfBirth": "1990-04-12",
              "email": "alex.nguyen@example.com",
              "membershipType": "Single",
              "registeredAt": "not-a-timestamp",
              "unexpected": { "nested": [1, 2, 3] }
            }
            """;
        var request = JsonSerializer.Deserialize<CrmRegistration>(json);

        var payload = AssertSuccess(RegistrationMapper.Map(Assert.IsType<CrmRegistration>(request)));

        Assert.Equal("Alex", payload.Member.GivenName);
        Assert.Equal("Nguyen", payload.Member.FamilyName);
        Assert.Equal("12/04/1990", payload.Member.Dob);
        Assert.Equal("alex.nguyen@example.com", payload.Member.Contact.Email);
        Assert.Equal("S", payload.Member.PlanCode);
        Assert.Equal("MILKYWAY", payload.Member.Source);
    }

    [Fact]
    public void Valid_leap_day_is_preserved_when_reformatting_the_date()
    {
        var request = ValidRegistration() with { DateOfBirth = "2000-02-29" };

        var payload = AssertSuccess(RegistrationMapper.Map(request));

        Assert.Equal("29/02/2000", payload.Member.Dob);
    }

    [Fact]
    public void Date_conversion_is_independent_of_current_culture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar-SA");

            var payload = AssertSuccess(RegistrationMapper.Map(ValidRegistration()));

            Assert.Equal("12/04/1990", payload.Member.Dob);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void Text_is_trimmed_and_membership_names_are_case_insensitive()
    {
        var request = ValidRegistration() with
        {
            FirstName = "  Alex ",
            LastName = "\tNguyen\n",
            DateOfBirth = " 1990-04-12 ",
            Email = " Alex.Nguyen@example.com ",
            MembershipType = " fAmIlY "
        };

        var payload = AssertSuccess(RegistrationMapper.Map(request));

        Assert.Equal("Alex", payload.Member.GivenName);
        Assert.Equal("Nguyen", payload.Member.FamilyName);
        Assert.Equal("12/04/1990", payload.Member.Dob);
        Assert.Equal("Alex.Nguyen@example.com", payload.Member.Contact.Email);
        Assert.Equal("F", payload.Member.PlanCode);
    }

    [Fact]
    public void Independent_validation_failures_are_reported_together_without_a_payload()
    {
        var request = ValidRegistration() with
        {
            FirstName = null,
            LastName = " ",
            DateOfBirth = "yesterday",
            Email = "invalid",
            MembershipType = "Corporate"
        };

        var result = RegistrationMapper.Map(request);

        Assert.Null(result.Payload);
        Assert.Equal(
            new[] { "dateOfBirth", "email", "firstName", "lastName", "membershipType" },
            result.Errors.Keys.OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(result.Errors.Values, messages =>
        {
            Assert.NotEmpty(messages);
            Assert.All(messages, message => Assert.False(string.IsNullOrWhiteSpace(message)));
        });
    }

    private static LegacyRegistration AssertSuccess(MappingResult result)
    {
        Assert.Empty(result.Errors);
        return Assert.IsType<LegacyRegistration>(result.Payload);
    }

    private static CrmRegistration ValidRegistration() => new()
    {
        FirstName = "Alex",
        LastName = "Nguyen",
        DateOfBirth = "1990-04-12",
        Email = "alex.nguyen@example.com",
        MembershipType = "Single"
    };

    private static CrmRegistration WithField(CrmRegistration request, string field, string? value) => field switch
    {
        "firstName" => request with { FirstName = value },
        "lastName" => request with { LastName = value },
        "dateOfBirth" => request with { DateOfBirth = value },
        "email" => request with { Email = value },
        "membershipType" => request with { MembershipType = value },
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown test field.")
    };
}
