using Aetheris.Server.Api;
using Aetheris.Server.Configuration;
using Aetheris.Server.Documents;
using Microsoft.AspNetCore.StaticFiles;

namespace Aetheris.Server.Startup;

public static class CadmataApplication
{
    public static WebApplication Create(string[] args, CadmataLaunchOptions launchOptions, bool portable = false, ILoggerProvider? logging = null, string? sessionToken = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            EnvironmentName = portable ? Environments.Production : null,
            ContentRootPath = AppContext.BaseDirectory,
        });

        if (!portable && launchOptions.Step is not null && !launchOptions.HasExplicitUrls)
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0");
        }

        if (portable || launchOptions.Step is not null)
        {
            // The CLI intentionally returns after process creation. Keep the detached
            // host from retaining noisy per-request console output in redirected pipes.
            builder.Logging.ClearProviders();
            if (logging is not null) builder.Logging.AddProvider(logging);
        }

        if (portable)
        {
            builder.Configuration.Sources.Clear();
            builder.WebHost.ConfigureKestrel(options => options.Listen(System.Net.IPAddress.Loopback, 0));
        }

        var stepUploadOptions = builder.Configuration
            .GetSection(StepUploadOptions.SectionName)
            .Get<StepUploadOptions>()
            ?? new StepUploadOptions();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = stepUploadOptions.MaxUploadSizeBytes;
        });

        builder.Services.AddSingleton(stepUploadOptions);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSingleton<KernelDocumentStore>();
        builder.Services.AddSingleton(new CadmataStartupStep(launchOptions.Step));

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        if (sessionToken is not null) ConfigureSession(app, sessionToken);
        app.MapKernelApi();
        app.MapCadmataStartupApi();
        app.MapPaperclipDemoApi();
        app.MapStandardProductGalleryApi();

        app.UseDefaultFiles();
        var staticContentTypes = new FileExtensionContentTypeProvider();
        staticContentTypes.Mappings[".wgsl"] = "text/plain";
        app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = staticContentTypes });
        app.MapFallbackToFile("index.html");

        return app;
    }

    private static void ConfigureSession(WebApplication app, string token)
    {
        // The embedded window receives an unguessable session cookie. Other
        // browser origins cannot invoke the engineering API without the cookie.
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/__cadmata/session/" + token)
            {
                context.Response.Cookies.Append("cadmata-session", token, new CookieOptions
                {
                    HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/",
                });
                context.Response.Redirect("/");
                return;
            }
            if (context.Request.Cookies["cadmata-session"] != token)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            await next(context);
        });
    }
}
