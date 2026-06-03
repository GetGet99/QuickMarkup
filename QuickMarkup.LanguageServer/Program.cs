using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Server;
using QuickMarkup.LanguageServer.Contracts;
using QuickMarkup.LanguageServer.Diagnostics;
using QuickMarkup.LanguageServer.Handlers;
using QuickMarkup.LanguageServer.Workspace;

await LanguageServer.From(options => options
    .WithInput(Console.OpenStandardInput())
    .WithOutput(Console.OpenStandardOutput())
    .WithHandler<QmuiDidOpenHandler>()
    .WithHandler<QmuiDidChangeHandler>()
    .WithHandler<QmuiDidCloseHandler>()
    .WithServices(services =>
    {
        services.AddSingleton<IRoslynWorkspaceManager, RoslynWorkspaceManager>();
        services.AddSingleton<IQmuiDiagnosticService, QmuiDiagnosticService>();
    })
    .OnInitialize(async (server, request, ct) =>
    {
        var initOpts = request.InitializationOptions as JObject;
        var workspaceRoot = (string?)initOpts?["workspaceRoot"];
        var csprojPath = ProjectFinder.FindCsproj(workspaceRoot);
        if (csprojPath is not null)
        {
            var workspace = server.Services.GetRequiredService<IRoslynWorkspaceManager>();
            await workspace.TryLoadAsync(csprojPath);
            workspace.WatchProjectChanges(csprojPath);
        }
    })
);
