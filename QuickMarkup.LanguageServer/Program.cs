using OmniSharp.Extensions.LanguageServer.Server;
using QuickMarkup.LanguageServer.Handlers;

await LanguageServer.From(options => options
    .WithInput(Console.OpenStandardInput())
    .WithOutput(Console.OpenStandardOutput())
    .WithHandler<QmuiDidOpenHandler>()
    .WithHandler<QmuiDidChangeHandler>()
    .WithHandler<QmuiDidCloseHandler>()
);
