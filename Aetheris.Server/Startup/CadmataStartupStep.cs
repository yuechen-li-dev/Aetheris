using Aetheris.Server.Api;

namespace Aetheris.Server.Startup;

public sealed class CadmataStartupStep(CadmataStartupStepContent? content)
{
    private CadmataStartupStepContent? _content = content;

    public CadmataStartupStepContent? Claim() => Interlocked.Exchange(ref _content, null);
}

public static class CadmataStartupApi
{
    public static IEndpointRouteBuilder MapCadmataStartupApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/startup/step", (CadmataStartupStep startup) =>
        {
            var content = startup.Claim();
            return content is null ? Results.NoContent() : ApiMappings.Ok(content);
        });

        return endpoints;
    }
}
