using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

namespace MauiVersion.Commands;

public abstract class BaseCommand : Command
{
    protected BaseCommand(string name, string? description = null) : base(name, description)
    {
        this.SetHandler(async (context) =>
        {
            var exitCode = await ExecuteAsync(context.ParseResult, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });
    }

    protected abstract Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken);
}
