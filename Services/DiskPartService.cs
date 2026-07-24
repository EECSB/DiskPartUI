using System.Text;
using CliWrap;
using CliWrap.Buffered;
using DiskPartUI.Models;

namespace DiskPartUI.Services;

///<summary>
///Runs diskpart by writing the requested commands to a temporary script file and
///invoking <c>diskpart /s &lt;file&gt;</c> via CliWrap, then capturing the output.
///
///diskpart itself requires elevation, so the whole app runs as Administrator
///(see Platforms/Windows/app.manifest). Calls are serialized with a semaphore
///because running two diskpart instances at once is unsafe.
///
///CliWrap usage follows https://eecs.blog/calling-the-command-line-in-c-with-cliwrap/
///— wrap the executable, add arguments, execute buffered, then read
///StandardOutput / StandardError / ExitCode from the result.
///</summary>
public sealed class DiskPartService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    ///<summary>Runs a multi-line diskpart script and returns its combined output.</summary>
    public async Task<DiskPartResult> RunScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeScript(script);

        //diskpart reads a batch of commands from the file passed to /s.
        var scriptPath = Path.Combine(Path.GetTempPath(), $"diskpartui_{Guid.NewGuid():N}.txt");

        //Write UTF-8 WITHOUT a BOM; a leading BOM can make diskpart reject the first command.
        await File.WriteAllTextAsync(scriptPath, normalized, new UTF8Encoding(false), cancellationToken);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var result = await Cli.Wrap("diskpart")
                .WithArguments(new[] { "/s", scriptPath })
                //diskpart signals problems through both exit codes and text, so don't
                //let a non-zero exit throw — surface everything to the user instead.
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);

            var builder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                builder.Append(result.StandardOutput.TrimEnd());

            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                if (builder.Length > 0)
                    builder.AppendLine().AppendLine();

                builder.Append(result.StandardError.TrimEnd());
            }

            return new DiskPartResult(result.ExitCode == 0, builder.ToString());
        }
        finally
        {
            _gate.Release();

            //Best-effort cleanup of the temp script file; ignore failures.
            try
            {
                File.Delete(scriptPath);
            }
            catch { }
        }
    }

    ///<summary>Convenience overload for running one or more commands.</summary>
    public Task<DiskPartResult> RunCommandsAsync(params string[] commands)
    {
        return RunScriptAsync(string.Join(Environment.NewLine, commands));
    }

    private static string NormalizeScript(string script)
    {
        var lines = (script ?? string.Empty)
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0);

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
