using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aetherctl.Orleans;

namespace Aetherctl.Commands
{
    public static class PromptsCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            var promptsCmd = new Command("prompts", "Prompt template management");

            var addCmd = new Command("add", "Add a new prompt template");
            var nameArg = new Argument<string>("name", "Prompt template name");
            var fileArg = new Argument<string>("file", "Path to markdown file");
            addCmd.AddArgument(nameArg);
            addCmd.AddArgument(fileArg);
            addCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var name = parseResult.GetValueForArgument(nameArg);
                    var file = parseResult.GetValueForArgument(fileArg);
                    if (!File.Exists(file))
                    {
                        Common.WriteError(parseResult, $"File not found at {file}");
                        return;
                    }
                    var content = await File.ReadAllTextAsync(file);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var registry = factory.GetPromptRegistry();
                    await registry.AddOrUpdateAsync(name, content);
                    Common.WriteSuccess(parseResult, $"Added/updated prompt '{name}'");
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error adding prompt: {ex.Message}");
                }
            });

            var listCmd = new Command("list", "List all available prompt templates");
            listCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var registry = factory.GetPromptRegistry();
                    var names = await registry.ListAsync();
                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new { success = true, count = names.Count, prompts = names });
                    }
                    else
                    {
                        if (names.Count == 0)
                        {
                            Console.WriteLine("No prompts found.");
                            return;
                        }
                        foreach (var n in names)
                            Console.WriteLine(n);
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error listing prompts: {ex.Message}");
                }
            });

            var editCmd = new Command("edit", "Edit a prompt template (update from file or print)");
            var editNameArg = new Argument<string>("name", "Prompt template name");
            var editFileOpt = new Option<string?>("--file", "Path to markdown file to replace content");
            var editPrintOpt = new Option<bool>("--print", "Print current content to stdout (no changes)");
            editCmd.AddArgument(editNameArg);
            editCmd.AddOption(editFileOpt);
            editCmd.AddOption(editPrintOpt);
            editCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var name = parseResult.GetValueForArgument(editNameArg);
                    var file = parseResult.GetValueForOption(editFileOpt);
                    var doPrint = parseResult.GetValueForOption(editPrintOpt);

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var registry = factory.GetPromptRegistry();
                    var names = await registry.ListAsync();
                    if (!names.Contains(name))
                    {
                        Common.WriteError(parseResult, $"Prompt '{name}' not found. Use 'prompts list' to see available prompts.");
                        return;
                    }

                    if (doPrint)
                    {
                        var content = await registry.GetAsync(name);
                        if (Common.IsJsonOutput(parseResult))
                        {
                            Common.WriteOutput(parseResult, new { success = true, name, content });
                        }
                        else
                        {
                            Console.WriteLine(content);
                        }
                        return;
                    }

                    if (!string.IsNullOrEmpty(file))
                    {
                        if (!File.Exists(file))
                        {
                            Common.WriteError(parseResult, $"File not found at {file}");
                            return;
                        }
                        var content = await File.ReadAllTextAsync(file);
                        await registry.AddOrUpdateAsync(name, content);
                        Common.WriteSuccess(parseResult, $"Updated prompt '{name}' from file");
                        return;
                    }

                    // No-op if neither --print nor --file provided
                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new { success = false, error = "Specify --file to update or --print to view content." });
                    }
                    else
                    {
                        Console.WriteLine("Specify --file to update or --print to view content.");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error editing prompt: {ex.Message}");
                }
            });

            var deleteCmd = new Command("delete", "Delete a prompt template");
            var deleteNameArg = new Argument<string>("name", "Prompt template name");
            deleteCmd.AddArgument(deleteNameArg);
            deleteCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var name = parseResult.GetValueForArgument(deleteNameArg);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var registry = factory.GetPromptRegistry();
                    // Note: Delete not yet implemented in IPromptRegistryGrain - would need server-side support
                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new { success = false, error = "Delete functionality not yet implemented. Requires server-side API support." });
                    }
                    else
                    {
                        Console.WriteLine("✗ Delete functionality not yet implemented. Requires server-side API support.");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error deleting prompt: {ex.Message}");
                }
            });

            promptsCmd.AddCommand(addCmd);
            promptsCmd.AddCommand(listCmd);
            promptsCmd.AddCommand(editCmd);
            promptsCmd.AddCommand(deleteCmd);
            root.AddCommand(promptsCmd);
        }
    }
}

