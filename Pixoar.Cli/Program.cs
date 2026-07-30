using Microsoft.Extensions.DependencyInjection;
using Pixoar.Cli.Arguments;
using Pixoar.Cli.Commands;
using Pixoar.Cli.Execution;
using Pixoar.Core.Configuration;
using Pixoar.Core.Interfaces;

var services = new ServiceCollection();
services.AddPixoarCore();
services.AddPixoarCli();

await using var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<IApplicationLogger>();

try
{
    await serviceProvider.GetRequiredService<ISettingsService>().LoadAsync();

    var arguments = new CommandLineArguments(args);
    await logger.LogInformationAsync($"CLI command started: {arguments.CommandName}");

    var context = new CommandContext(
        arguments,
        serviceProvider.GetRequiredService<IApplicationPathProvider>(),
        serviceProvider.GetRequiredService<ISettingsService>());

    var dispatcher = serviceProvider.GetRequiredService<CommandDispatcher>();
    var result = await dispatcher.DispatchAsync(arguments, context, CancellationToken.None);
    await logger.LogInformationAsync($"CLI exit code: {result.ExitCode}");

    if (!string.IsNullOrWhiteSpace(result.Message))
    {
        var writer = result.ExitCode == 0 ? Console.Out : Console.Error;
        await writer.WriteLineAsync(result.Message);
    }

    return result.ExitCode;
}
catch (Exception ex)
{
    await logger.LogErrorAsync("Pixoar CLI failed.", ex);
    await Console.Error.WriteLineAsync("Pixoar CLI failed. Check the log file for details.");
    return CliExitCodes.Failure;
}
