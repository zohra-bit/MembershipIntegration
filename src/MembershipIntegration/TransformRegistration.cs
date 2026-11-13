using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace MembershipIntegration;

public sealed class TransformRegistration
{
    [Function(nameof(TransformRegistration))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "registrations/transform")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return new UnsupportedMediaTypeResult();
        }

        CrmRegistration? registration;
        try
        {
            registration = await JsonSerializer.DeserializeAsync<CrmRegistration>(
                request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return Invalid(new() { ["body"] = ["Provide valid JSON with the expected field types."] });
        }

        if (registration is null)
        {
            return Invalid(new() { ["body"] = ["A registration object is required."] });
        }

        var result = RegistrationMapper.Map(registration);
        return result.Errors.Count > 0
            ? Invalid(result.Errors)
            : new OkObjectResult(result.Payload);
    }

    private static BadRequestObjectResult Invalid(Dictionary<string, string[]> errors) => new(
        new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Registration validation failed."
        });
}
