using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Server;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer;
using QuickMarkup.LanguageServer.Contracts;
using QuickMarkup.LanguageServer.Diagnostics;
using QuickMarkup.LanguageServer.Handlers;
using QuickMarkup.LanguageServer.Navigation;
using QuickMarkup.LanguageServer.SemanticService;
using QuickMarkup.LanguageServer.Workspace;

const string DebugEnvVar = "QMUI_LSP_DEBUG";
var debugPortEnv = Environment.GetEnvironmentVariable(DebugEnvVar);
Stream input, output;

if (debugPortEnv is not null && int.TryParse(debugPortEnv, out var port))
{
    Console.Error.WriteLine($"QMUI_LSP_DEBUG: waiting for TCP connection on port {port}...");
    var listener = new TcpListener(IPAddress.Loopback, port);
    listener.Start();
    var tcpClient = await listener.AcceptTcpClientAsync();
    listener.Stop();
    Console.Error.WriteLine("QMUI_LSP_DEBUG: connected.");
    input = output = tcpClient.GetStream();
}
else
{
    input = Console.OpenStandardInput();
    output = Console.OpenStandardOutput();
}

var server = await LanguageServer.From(options => options
    .WithInput(input)
    .WithOutput(output)
    .WithHandler<QmuiDidOpenHandler>()
    .WithHandler<QmuiDidChangeHandler>()
    .WithHandler<QmuiDidCloseHandler>()
    .WithHandler<QmuiHoverHandler>()
    .WithHandler<QmuiDefinitionHandler>()
    .WithServices(services =>
    {
        services.AddSingleton<ICompilationService, CompilationService>();
        services.AddSingleton<ICatalogService, CatalogService>();
        services.AddSingleton<IMemberTableService, MemberTableService>();
        services.AddSingleton<IFileWatcherService, FileWatcherService>();
        services.AddSingleton<IQmuiWorkspaceService, QmuiWorkspaceService>();
        services.AddSingleton<IQmuiDiagnosticService, QmuiDiagnosticService>();
        services.AddSingleton<IQmuiDocumentStore, QmuiDocumentStore>();
        services.AddSingleton<IQmuiSemanticService, QmuiSemanticService>();
        services.AddSingleton<IMarkupCursorResolver, MarkupCursorResolver>();
        services.AddSingleton<ISymbolLocationResolver, SymbolLocationResolver>();
        services.AddSingleton<IFileProvider, FileSystemProvider>();
    })
    .OnInitialize(async (server, request, ct) =>
    {
        var initOpts = request.InitializationOptions as JObject;
        var workspaceRoot = (string?)initOpts?["workspaceRoot"];
        if (!string.IsNullOrEmpty(workspaceRoot))
        {
            var workspace = server.Services.GetRequiredService<IQmuiWorkspaceService>();
            await workspace.InitializeAsync(workspaceRoot);
        }
    })
);
await server.WaitForExit;
