using System.Text.Json;
using OsLib;

namespace ImgSeeder;

internal static partial class Program
{
	private static ReadOnlyMedia ReadOnlyScope(string[] args, RaiPath? resolved = null)
	{
		var selection = RequiredCommandRoot(args);
		var provider = EffectiveCloudProvider(Messages.CloudProvider, selection.Value, HasOption(args, "-c", "--cloud", "--cloudprovider"));
		var cloudRoot = provider is null ? null : new RaiPath((string)Os.Config.Cloud[provider]);
		if (cloudRoot is not null)
		{
			var relative = selection.Value.Replace('\\', '/');
			if (relative.Split('/').Contains("..")) throw new ArgumentException("Search root cannot escape the configured cloud.");
		}
		resolved ??= ResolveCommandSubscriberRoot(BindCommandRoot(selection, Messages.CloudProvider, CommandSubscriber(args), HasOption(args, "-c", "--cloud", "--cloudprovider")));
		return new ReadOnlyMedia(resolved, cloudRoot, provider, selection.IsApplicationRoot);
	}
	private static int RunDuplicatesCommand(string[] args)
	{
		var values = CommandGlobalValueOptions.Concat(["-r", "--root", "-a", "--app", "-t", "--tenant", "--subscriber", "--match", "--visual-profile"]).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var allowed = CommandGlobalSwitchOptions.Concat(values).Concat(["--json", "--ignore-case"]).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var positionals = ValidateCommandTokens(args, allowed, values);
		if (positionals.Count != 1) throw new ArgumentException("duplicates requires exactly one source file or filename pattern.");
		var result = ReadOnlyScope(args).DuplicatesAsync(positionals[0], ParamValue(args, "--match") ?? "bytes", ParamValue(args, "--visual-profile"), HasOption(args, "--ignore-case")).GetAwaiter().GetResult();
		if (HasOption(args, "--json")) Console.WriteLine(JsonSerializer.Serialize(result));
		else Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
		return 0;
	}
}
