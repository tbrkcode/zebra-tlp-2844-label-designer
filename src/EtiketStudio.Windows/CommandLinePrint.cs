// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json;
using EtiketStudio.Windows.Printing;

namespace EtiketStudio.Windows;

internal static class CommandLinePrint
{
    // Explicit CLI operation used by the opt-in hardware test script. Successful submission is not paper confirmation.
    public static int Run(string[] arguments)
    {
        string Argument(string name)
        {
            int i = Array.IndexOf(arguments, name);
            return i >= 0 && i + 1 < arguments.Length ? arguments[i + 1] : throw new ArgumentException("Missing " + name);
        }
        string? resultPath = null;
        try
        {
            resultPath = Path.GetFullPath(Argument("--result"));
            string file = Path.GetFullPath(Argument("--send-epl")), printer = Argument("--printer");
            if (!arguments.Contains("--confirm-print")) throw new ArgumentException("--confirm-print is required; this operation feeds real labels.");
            if (new FileInfo(file).Length > 5_000_000) throw new ArgumentException("EPL file exceeds 5 MB.");
            byte[] bytes = File.ReadAllBytes(file);
            int job = new WindowsSpooler().Send(printer, bytes);
            File.WriteAllText(resultPath, JsonSerializer.Serialize(new { status = "SpoolAccepted", printer, jobId = job, file, time = DateTimeOffset.Now, physicalPrintVerified = false }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex)
        {
            if (resultPath is not null) File.WriteAllText(resultPath, JsonSerializer.Serialize(new { status = "FailedOrUncertain", error = ex.Message, physicalPrintVerified = false }));
            return 1;
        }
    }
}
