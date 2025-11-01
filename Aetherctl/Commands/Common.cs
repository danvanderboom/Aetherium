using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;

namespace Aetherctl.Commands
{
    /// <summary>
    /// Common utilities for command handlers.
    /// </summary>
    public static class Common
    {
        // Global options - set by Program.cs
        public static Option<bool>? JsonOption { get; set; }
        public static Option<bool>? VerboseOption { get; set; }
        public static Option<bool>? QuietOption { get; set; }

        public static bool IsJsonOutput(ParseResult parseResult)
        {
            if (JsonOption == null) return false;
            return parseResult.GetValueForOption(JsonOption);
        }

        public static bool IsVerbose(ParseResult parseResult)
        {
            if (VerboseOption == null) return false;
            return parseResult.GetValueForOption(VerboseOption);
        }

        public static bool IsQuiet(ParseResult parseResult)
        {
            if (QuietOption == null) return false;
            return parseResult.GetValueForOption(QuietOption);
        }

        public static void WriteOutput(ParseResult parseResult, object? output, string? error = null)
        {
            var isJson = IsJsonOutput(parseResult);
            var isQuiet = IsQuiet(parseResult);

            if (!string.IsNullOrEmpty(error))
            {
                if (isJson)
                {
                    var errorJson = JsonSerializer.Serialize(new { success = false, error });
                    Console.Error.WriteLine(errorJson);
                }
                else
                {
                    Console.Error.WriteLine($"✗ {error}");
                }
                Environment.Exit(1);
                return;
            }

            if (output != null)
            {
                if (isJson)
                {
                    var json = JsonSerializer.Serialize(output, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    Console.WriteLine(json);
                }
                else if (!isQuiet)
                {
                    // For non-JSON output, commands will handle their own formatting
                    // This is a fallback for simple cases
                    Console.WriteLine(output);
                }
            }
        }

        public static void WriteSuccess(ParseResult parseResult, string message)
        {
            if (IsQuiet(parseResult))
                return;

            if (IsJsonOutput(parseResult))
            {
                WriteOutput(parseResult, new { success = true, message });
            }
            else
            {
                Console.WriteLine($"✓ {message}");
            }
        }

        public static void WriteError(ParseResult parseResult, string error)
        {
            WriteOutput(parseResult, null, error);
        }
    }
}

