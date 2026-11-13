# Membership registration transformation

An HTTP-triggered Azure Function that validates the CRM registration and returns the legacy JSON required by the exercise. It does not send a request to a legacy API.

## Run locally

Install the .NET 10 SDK, Azure Functions Core Tools v4, and Azurite. A current Visual Studio release with .NET 10/Azure tooling, or a terminal and editor, can run the project. .NET 10 is used as a supported LTS version; the exercise does not prescribe a version.

Check `func --version` prints a 4.x version in your terminal. After installing Core Tools, reopen Visual Studio/your terminal so the updated PATH is available.

From the solution root:

```powershell
dotnet restore
dotnet build
dotnet test
Copy-Item src/MembershipIntegration/local.settings.example.json src/MembershipIntegration/local.settings.json
```

Start Azurite in a separate terminal:

```powershell
azurite --silent --location .azurite
```

Then start the Function:

```powershell
cd src/MembershipIntegration
dotnet run
```

The Functions Worker SDK launches Core Tools from the build output. Core Tools must be on PATH. On Linux/macOS use `cp` in place of PowerShell's `Copy-Item`.

POST `samples/registration.json` to `http://localhost:7071/api/registrations/transform` with `Content-Type: application/json`. The response should match `samples/expected.json`.

If port 7071 is already in use, start with `dotnet run -- --port 7072` and use port 7072 in the request URL.

## Design decisions

- The Function handles HTTP/JSON errors; `RegistrationMapper` validates and transforms without Azure dependencies in its code. Tests exercise both.
- Five input fields are required. Invalid requests return HTTP 400 with field errors; malformed JSON gets a safe body error. Non-JSON content returns 415.
- Dates must be real dates in `yyyy-MM-dd` format. Alternate formats receive a clear validation error instead of guessing the user's locale.
- Surrounding whitespace is trimmed; membership names are matched case insensitively. Names and email case are preserved.
- Email errors identify missing components, such as `@` or a domain suffix. This registration form requires a dotted domain: `name@example.org` and `name@example.com.au` are accepted, while `name@example` is rejected. No particular suffix is required. Checks cover syntax, not whether the domain or mailbox exists; display names and IP-address domains are rejected.
- Unknown fields are ignored. `registeredAt` is unused and is neither required nor validated.
- Output uses explicit JSON names, nested `contact.email`, and the literal `MILKYWAY` shown in the target contract.

## Connecting a real legacy API

After validation succeeds, a typed client registered through `IHttpClientFactory` would POST the mapped JSON to a configured legacy API endpoint. The following describes how to extend this exercise; no outbound call is currently implemented.

- **Authentication:** use the method supported by the legacy API. If it supports Microsoft Entra ID, use managed identity where available. Otherwise obtain its API key or client credentials from secure configuration/Key Vault, keeping secrets outside source control. The inbound Function key does not authenticate an outbound legacy request.
- **Timeouts:** configure a bounded overall timeout, including retries, and pass the invocation's cancellation token to the HTTP client. Choose the timeout to fit the legacy API's service expectations and the Function's execution budget.
- **Retries:** use a small bounded number of attempts with exponential backoff and jitter for agreed transient failures, such as 429 or 502/503/504; respect `Retry-After`. Do not retry validation or authentication failures automatically. Before retrying a membership-creation POST, agree a stable idempotency key and server-side duplicate handling: a timeout or network failure can happen after the member has already been created.

Log status, duration, and correlation identifiers rather than registration bodies or personal details. Local Core Tools normally does not enforce the Function's inbound key authorization.

Reference: [Microsoft's HTTP resilience guidance](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience).
