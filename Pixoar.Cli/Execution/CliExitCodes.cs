namespace Pixoar.Cli.Execution;

internal static class CliExitCodes
{
    public const int Success = 0;
    public const int PartialSuccess = 1;
    public const int Failure = 2;
    public const int InvalidArguments = 3;
    public const int MissingDependency = 4;
}
