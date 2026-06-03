using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Server;
using QuickMarkup.LanguageServer.Contracts;
using QuickMarkup.LanguageServer.Diagnostics;
using QuickMarkup.LanguageServer.Handlers;
using QuickMarkup.LanguageServer.Workspace;

var debugPortEnv = Environment.GetEnvironmentVariable("QMUI_LSP_DEBUG");
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
    .WithServices(services =>
    {
        services.AddSingleton<IRoslynWorkspaceManager, RoslynWorkspaceManager>();
        services.AddSingleton<IQmuiDiagnosticService, QmuiDiagnosticService>();
    })
    .OnInitialize(async (server, request, ct) =>
    {
        var initOpts = request.InitializationOptions as JObject;
        var workspaceRoot = (string?)initOpts?["workspaceRoot"];
        if (!string.IsNullOrEmpty(workspaceRoot))
        {
            var workspace = server.Services.GetRequiredService<IRoslynWorkspaceManager>();
            await workspace.InitializeAsync(workspaceRoot);
        }
    })
);
await server.WaitForExit;