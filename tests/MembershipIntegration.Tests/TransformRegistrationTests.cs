using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MembershipIntegration.Tests;

public sealed class TransformRegistrationTests
{
    private readonly TransformRegistration function = new();

    [Fact]
    public async Task Valid_request_returns_the_legacy_payload()
    {
        var result = await function.Run(Request("""
            {"firstName":"Alex","lastName":"Nguyen","dateOfBirth":"1990-04-12",
             "email":"alex.nguyen@example.com","membershipType":"Single"}
            """), CancellationToken.None);

        var success = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<LegacyRegistration>(success.Value);
        Assert.Equal("S", payload.Member.PlanCode);
        Assert.Equal("MILKYWAY", payload.Member.Source);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{broken")]
    [InlineData("[]")]
    [InlineData("{\"firstName\":42}")]
    public async Task Invalid_json_or_wrong_field_types_return_a_clear_400(string json)
    {
        var problem = await InvokeInvalid(json);
        Assert.True(problem.Errors.ContainsKey("body"));
        Assert.False(problem.Errors.ContainsKey("email"));
    }

    [Fact]
    public async Task Null_root_returns_a_clear_400()
    {
        var problem = await InvokeInvalid("null");
        Assert.Equal("A registration object is required.", Assert.Single(problem.Errors["body"]));
    }

    [Fact]
    public async Task Empty_object_returns_all_five_required_fields()
    {
        var problem = await InvokeInvalid("{}");
        Assert.Equal(5, problem.Errors.Count);
    }

    [Theory]
    [InlineData("alex.nguyenexample.com", "Email address must include an '@' symbol.")]
    [InlineData("alex.nguyen@example", "Email domain must include a suffix, such as .com, .org, or .com.au.")]
    public async Task Incomplete_email_returns_400_with_a_specific_message(string email, string expectedMessage)
    {
        var json = JsonSerializer.Serialize(new CrmRegistration
        {
            FirstName = "Alex",
            LastName = "Nguyen",
            DateOfBirth = "1990-04-12",
            Email = email,
            MembershipType = "Single"
        });

        var problem = await InvokeInvalid(json);

        Assert.Single(problem.Errors);
        Assert.Equal(expectedMessage, Assert.Single(problem.Errors["email"]));
    }

    [Fact]
    public async Task Non_json_content_returns_415()
    {
        var request = Request("{}");
        request.ContentType = "text/plain";
        Assert.IsType<UnsupportedMediaTypeResult>(await function.Run(request, CancellationToken.None));
    }

    [Fact]
    public async Task Extra_fields_including_unused_registeredAt_are_ignored()
    {
        var result = await function.Run(Request("""
            {"firstName":"Alex","lastName":"Nguyen","dateOfBirth":"1990-04-12",
             "email":"alex.nguyen@example.com","membershipType":"Single",
             "registeredAt":{"not":"a date"},"extra":[1,2,3]}
            """), CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Cancellation_is_not_converted_to_a_validation_error()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            function.Run(Request("{}"), cancellation.Token));
    }

    private async Task<ValidationProblemDetails> InvokeInvalid(string json)
    {
        var result = Assert.IsType<BadRequestObjectResult>(
            await function.Run(Request(json), CancellationToken.None));
        Assert.Equal(400, result.StatusCode);
        return Assert.IsType<ValidationProblemDetails>(result.Value);
    }

    private static HttpRequest Request(string json)
    {
        var request = new DefaultHttpContext().Request;
        request.ContentType = "application/json";
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return request;
    }
}
