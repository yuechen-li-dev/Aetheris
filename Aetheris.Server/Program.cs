using Aetheris.Server.Api;
using Aetheris.Server.Configuration;
using Aetheris.Server.Documents;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapKernelApi();

app.Run();

public partial class Program;
