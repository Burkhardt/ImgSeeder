using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using OsLib;
using RaiImage;

namespace ImgSeeder;

public static class Icons
{
	public const char Error = '\uea87';
	public const char Info = '\uea74';
	public const char Help = '\uf059';
	public const char NotAvailable = '\ueabd';
	public const char File = '\uea7b';
	public const char Folder = '\uea83';
	public const char Banner = '\ueb1e';
	public const char NoBanner = '\ueb24';
	public const char Bug = '\uf188';
	public const char Force = '\uf0e7';
	public const char Runner = '\uf04b';
	public const char ArrowLeft = '\uf060';
	public const string DropboxBoxOutline = "\U000F0BF4";
	public const string GoogleDriveBoxOutline = "\U000F0BFD";
	public const string ICloudDriveBoxOutline = "\U000F0C03";
	public const string OneDriveBoxOutline = "\U000F0C15";
	public const string HelpLineWidthCompensation = "  ";
	public static readonly string[] NumberBoxes =
	[
		"\U000F03A4", "\U000F03A7", "\U000F03AA", "\U000F03AD", "\U000F03B1",
		"\U000F03B3", "\U000F03B6", "\U000F03B9", "\U000F03BC"
	];
	public static readonly string[] NumberBoxOutlines =
	[
		"\U000F03A6", "\U000F03A9", "\U000F03AC", "\U000F03AE", "\U000F03B0",
		"\U000F03B5", "\U000F03B8", "\U000F03BB", "\U000F03BE"
	];
}

public static class Messages
{
	public static bool Debug { get; set; }
	public static bool Banner { get; set; } = true;
	public static string? CloudProvider { get; set; }
	public static RaiPath? DestinationRoot { get; set; }
	public static RaiPath? ImageRoot { get; set; }
	public static string? RootParam { get; set; }
	public static int? SourceImageCount { get; set; }
	public static RaiPath? SourceRoot { get; set; }
	public static string? Subscriber { get; set; }
	public const PathConventionType DefaultPathConvention = PathConventionType.ItemIdTree8x2;
	public const ImageNamingConvention DefaultNamingConvention = ImageNamingConvention.Structured;
	public static PathConventionType PathConvention { get; set; } = DefaultPathConvention;
	public static ImageNamingConvention NamingConvention { get; set; } = DefaultNamingConvention;

	public static string[] Help
	{
		get
		{
			var lines = new List<string>
			{
				HelpLine("Commands", Icons.Info, "organize, list, move, clean"),
				"  iorg organize --source <dir> (-r|--root) <dir> (-p|--pathconv) <1|2|3|4> (-n|--nameconv) <1|2|3>",
				"  iorg list <FileNamePattern> (-r|--root) <dir> [--subscriber <name>] [--json|--quiet]",
				"  iorg move <SourceItemId> [<TargetItemId>] (-r|--root) <dir> [--subscriber <name>] [--pathconv <1|2|3|4>]",
				"  iorg clean <ItemId> (-r|--root) <dir> [--force]",
				"  iorg clean --cache (-r|--root) <dir>",
				HelpLine("-h, --help", Icons.Help, "print out all options"),
				HelpLine("-v, --version", Icons.Info, "print version info"),
				HelpLine("-l, --nologo", BannerIcon(), "do not display the banner"),
				HelpLine("-d, --debug", DebugIcon(), Debug ? "TRUE" : "FALSE"),
				HelpLine("-c, --cloud", CloudIcon(), CloudDescription()),
				HelpLine("-s, --source", Icons.Folder, SourceDescription()),
				HelpLine("-rm, --rm", Icons.File, "list images that would be deleted for ShortName"),
				HelpLine("-rmc, --rm-cache", Icons.File, "list cached images that would be deleted for ShortName"),
				HelpLine("--force", Icons.Force, "perform exact-ItemId deletion (or legacy -rm/-rmc)"),
				HelpLine("-p, --pathconv", SelectedOptionIcon(PathConvention), PathConventionDescription()),
				HelpLine("-n, --nameconv", SelectedOptionIcon(NamingConvention), NamingConventionDescription()),
			};

			if (SourceRoot != null)
				lines.Add($"{Icons.Info} SourceImages\t{Icons.Folder}\t{SourceImageDescription()}");

			if (!string.IsNullOrWhiteSpace(Subscriber))
				lines.Add($"{Icons.Info} Subscriber\t{Icons.Folder}\t{Subscriber}");

			lines.Add(HelpLine("-r, --root", Icons.Folder, RootDescription()));
			lines.Add($"ImageRoot: {ImageRootDescription()}");
			return lines.ToArray();
		}
	}

	internal static string CloudDescription()
	{
		var options = CloudProviderOptions();
		return options.Length > 0
			? string.Join(", ", options.Select((name, index) =>
				$"{CloudProviderIcon(name, index + 1)} {name}{(index == 0 ? " (default)" : string.Empty)}"))
			: "no DefaultCloudOrder providers are configured";
	}

	internal static string PathConventionDescription()
		=> NumberedOptions(Enum.GetNames<PathConventionType>(), DefaultPathConvention.ToString());

	internal static string NamingConventionDescription()
		=> NumberedOptions(Enum.GetNames<ImageNamingConvention>(), DefaultNamingConvention.ToString());

	private static string HelpLine(string option, object icon, string description)
	{
		return $"{option.PadRight(24)}\t{icon}\t{description}";
	}

	private static char BannerIcon() => Banner ? Icons.Banner : Icons.NoBanner;
	private static char DebugIcon() => Debug ? Icons.Bug : Icons.Runner;
	private static string CloudIcon()
	{
		var options = CloudProviderOptions();
		var provider = options.FirstOrDefault(option =>
			string.Equals(option, CloudProvider, StringComparison.OrdinalIgnoreCase));
		return provider != null ? CloudProviderIcon(provider, Array.IndexOf(options, provider) + 1) : Icons.Folder.ToString();
	}

	private static string CloudProviderIcon(string provider, int fallbackNumber)
		=> provider.ToLowerInvariant() switch
		{
			"dropbox" => Icons.DropboxBoxOutline,
			"googledrive" => Icons.GoogleDriveBoxOutline,
			"iclouddrive" => Icons.ICloudDriveBoxOutline,
			"onedrive" => Icons.OneDriveBoxOutline,
			_ => NumberIcon(fallbackNumber)
		};

	private static string NumberIcon(int number)
		=> number is > 0 and <= 9 ? Icons.NumberBoxOutlines[number - 1] : $"({number})";

	private static string SelectedNumberIcon(int number)
		=> number is > 0 and <= 9 ? Icons.NumberBoxes[number - 1] : $"({number})";

	private static string SelectedOptionIcon<TEnum>(TEnum value)
		where TEnum : struct, Enum
	{
		var values = Enum.GetValues<TEnum>();
		var index = Array.IndexOf(values, value);
		return SelectedNumberIcon(index + 1);
	}

	private static string NumberedOptions(IReadOnlyList<string> names, string? defaultName)
	{
		return string.Join(", ", names.Select((name, index) =>
			$"{NumberIcon(index + 1)} {name}{(string.Equals(name, defaultName, StringComparison.OrdinalIgnoreCase) ? " (default)" : string.Empty)}"));
	}

	internal static string[] CloudProviderOptions()
	{
		var ordered = CloudProviderOrderOptions();
		var configured = new List<string>();
		try
		{
			dynamic? cloud = Os.Config?.Cloud;
			if (cloud == null)
				return [];

			IEnumerable<dynamic> properties = cloud.Properties();
			configured.AddRange(properties
				.Where(property => !string.IsNullOrWhiteSpace(property.Value?.ToString()))
				.Select(property => (string)property.Name));
		}
		catch
		{
			return [];
		}

		return FilterConfiguredDefaultCloudProviders(ordered, configured);
	}

	internal static string[] FilterConfiguredDefaultCloudProviders(
		IEnumerable<string> defaultCloudOrder,
		IEnumerable<string> configuredCloudProviders)
	{
		var configured = configuredCloudProviders.ToHashSet(StringComparer.OrdinalIgnoreCase);
		return defaultCloudOrder
			.Where(name => !string.IsNullOrWhiteSpace(name) && configured.Contains(name))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	private static string[] CloudProviderOrderOptions()
	{
		try
		{
			dynamic? defaultCloudOrder = Os.Config?.DefaultCloudOrder;
			if (defaultCloudOrder == null)
				return [];

			var options = new List<string>();
			foreach (var item in defaultCloudOrder)
			{
				string? name = item?.ToString();
				if (!string.IsNullOrWhiteSpace(name))
					options.Add(name);
			}

			return options.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
		}
		catch
		{
			return [];
		}
	}

	private static string RootDescription()
	{
		return !string.IsNullOrWhiteSpace(RootParam)
			? RootParam
			: "destination image root, resolved under --cloud when provided";
	}

	private static string ImageRootDescription()
	{
		return DestinationRoot != null
			? DestinationRoot.FullPath
			: ImageRoot != null
				? ImageRoot.FullPath
			: "complete destination image root";
	}

	private static string SourceDescription()
	{
		return SourceRoot != null
			? SourceRoot.FullPath
			: "no source image directory given";
	}

	private static string SourceImageDescription()
	{
		return SourceImageCount.HasValue
			? $"{SourceImageCount.Value} images detected; supported: {ImageTypes.Default.String}"
			: $"image count unavailable; supported: {ImageTypes.Default.String}";
	}

	public static void WriteError(string text) => WriteHighlighted(text, ConsoleColor.DarkRed, ConsoleColor.White);
	public static void WriteInfo(string text) => WriteHighlighted(text, ConsoleColor.Blue);
	public static void WriteSuccess(string text) => WriteHighlighted(text, ConsoleColor.DarkGreen);
	public static void WriteDebug(string text) { if (Debug) WriteHighlighted(text, ConsoleColor.DarkYellow); }

	private static void WriteHighlighted(string text, ConsoleColor foreground = ConsoleColor.Black, ConsoleColor? background = null)
	{
		var oldForeground = Console.ForegroundColor;
		var oldBackground = Console.BackgroundColor;
		Console.ForegroundColor = foreground;
		Console.BackgroundColor = background ?? oldBackground;
		Console.WriteLine(text);
		Console.ForegroundColor = oldForeground;
		Console.BackgroundColor = oldBackground;
	}

	public static void WriteBanner(string text)
	{
		Console.Write($"{Icons.Banner} ");
		WriteLine(text);
		Console.WriteLine(text);
		Console.Write($"{Icons.Banner} ");
		WriteLine(text);
	}

	private static void WriteLine(string text, char underlineChar = '─')
	{
		for (int i = 0; i < text.Length; i++) Console.Write(underlineChar);
		Console.WriteLine();
	}

	public static void WriteHelp()
	{
		foreach (var line in Help) WriteSuccess(line + Icons.HelpLineWidthCompensation);
	}
}

public static class ImageOrganizer
{
	public sealed record ImageCopySuccess(string SourceName, string SourceFullName, string DestinationFullName);
	public sealed record ImageCopyFailure(string SourceName, string SourceFullName, string Problem, string ErrorType);
	public sealed record ImageDeleteSuccess(string Name, string FullName);
	public sealed record ImageDeleteFailure(string Name, string FullName, string Problem, string ErrorType);
	public sealed record ItemMove(string SourceFullName, string DestinationFullName);

	public sealed class ImageOrganizeReport
	{
		public ImageOrganizeReport(int sourceImageCount)
		{
			SourceImageCount = sourceImageCount;
		}

		public int SourceImageCount { get; }
		public List<ImageCopySuccess> Copied { get; } = [];
		public List<ImageCopyFailure> Failed { get; } = [];
		public int CopiedCount => Copied.Count;
		public int FailedCount => Failed.Count;
	}

	public sealed class ImageDeleteReport
	{
		public ImageDeleteReport(string shortName, bool cacheOnly, bool force, int matchedCount)
		{
			ShortName = shortName;
			CacheOnly = cacheOnly;
			Force = force;
			MatchedCount = matchedCount;
		}

		public string ShortName { get; }
		public bool CacheOnly { get; }
		public bool Force { get; }
		public int MatchedCount { get; }
		public List<ImageDeleteSuccess> Deleted { get; } = [];
		public List<ImageDeleteFailure> Failed { get; } = [];
		public int DeletedCount => Deleted.Count;
		public int FailedCount => Failed.Count;
	}

	public static IReadOnlyList<RaiFile> ListTreeFiles(RaiPath subscriberRoot, string fileNamePattern)
	{
		ArgumentNullException.ThrowIfNull(subscriberRoot);
		if (string.IsNullOrWhiteSpace(fileNamePattern)
			|| fileNamePattern.Contains('/')
			|| fileNamePattern.Contains('\\'))
			throw new ArgumentException("FileNamePattern must be one filename pattern, not a path.", nameof(fileNamePattern));
		if (!subscriberRoot.Exists())
			return Array.Empty<RaiFile>();

		return subscriberRoot
			.EnumerateFiles(fileNamePattern, recursive: true)
			.Where(IsManagedTreeArtifact)
			.OrderBy(file => file.FullName, StringComparer.Ordinal)
			.ToList();
	}

	public static IReadOnlyList<ItemMove> MoveItem(
		RaiPath subscriberRoot,
		string sourceItemId,
		string? targetItemId = null,
		PathConventionType targetConvention = PathConventionType.ItemIdTree8x2)
	{
		ArgumentNullException.ThrowIfNull(subscriberRoot);
		ValidateItemId(sourceItemId, nameof(sourceItemId));
		if (targetItemId != null)
			ValidateItemId(targetItemId, nameof(targetItemId));
		var destinationId = string.IsNullOrWhiteSpace(targetItemId) ? sourceItemId : targetItemId;

		var sourceHomes = Enum.GetValues<PathConventionType>()
			.Select(convention => new ItemTreePath(subscriberRoot, sourceItemId, convention))
			.Select(home => (Home: home, Files: home.SelectFiles()))
			.Where(candidate => candidate.Files.Count > 0)
			.GroupBy(candidate => candidate.Home.SubdirRoot.FullPath, StringComparer.OrdinalIgnoreCase)
			.Select(group => group.First())
			.ToList();
		if (sourceHomes.Count == 0)
			throw new RaiImageNotFoundException(
				$"No files were found for ItemId '{sourceItemId}' under '{subscriberRoot.FullPath}'.");
		if (sourceHomes.Count > 1)
			throw new RaiImageIOException(
				$"ItemId '{sourceItemId}' exists under multiple path conventions. Resolve the duplicate placement before moving it.");

		var source = sourceHomes[0];
		var destination = new ItemTreePath(subscriberRoot, destinationId!, targetConvention);
		var oldNames = source.Files.Select(file => file.FullName).ToArray();
		var moved = destination.mv(source.Home);

		return oldNames.Zip(
			moved,
			(sourceFullName, destinationFile) => new ItemMove(sourceFullName, destinationFile.FullName))
			.Where(move => !string.Equals(move.SourceFullName, move.DestinationFullName, StringComparison.Ordinal))
			.ToList();
	}

	public static int Organize(
		RaiPath sourceRoot,
		RaiPath subscriberRoot,
		string subscriber,
		PathConventionType pathConvention = PathConventionType.ItemIdTree8x2,
		ImageNamingConvention namingConvention = ImageNamingConvention.Structured,
		RaiPath? tempRoot = null,
		TextWriter? output = null,
		bool debug = false)
	{
		return OrganizeWithReport(
			sourceRoot,
			subscriberRoot,
			subscriber,
			pathConvention,
			namingConvention,
			tempRoot,
			output,
			debug).CopiedCount;
	}

	public static ImageOrganizeReport OrganizeWithReport(
		RaiPath sourceRoot,
		RaiPath subscriberRoot,
		string subscriber,
		PathConventionType pathConvention = PathConventionType.ItemIdTree8x2,
		ImageNamingConvention namingConvention = ImageNamingConvention.Structured,
		RaiPath? tempRoot = null,
		TextWriter? output = null,
		bool debug = false)
	{
		if (string.IsNullOrWhiteSpace(subscriber))
			throw new ArgumentException("Subscriber is required.", nameof(subscriber));

		output ??= Console.Out;
		var sources = EnumerateImageFiles(sourceRoot).ToList();
		var report = new ImageOrganizeReport(sources.Count);
		var stagingRoot = (tempRoot ?? Os.TempDir) / new RaiRelPath(subscriber);

		foreach (var source in sources)
		{
			try
			{
				var normalizedFullName = ImageFile.EasyFileName(source.FullName);
				var normalized = new ImageFile(normalizedFullName, namingConvention);
				var staged = new RaiFile(stagingRoot, normalized.NameWithExtension);
				staged.mkdir();
				staged.cp(source);

				var destination = new ImageTreeFile(
					subscriberRoot,
					normalized.ItemId,
					string.Empty,
					normalized.Ext,
					pathConvention,
					namingConvention)
				{
					ImageNumber = normalized.ImageNumber
				};

				destination.mv(staged);
				report.Copied.Add(new ImageCopySuccess(source.NameWithExtension, source.FullName, destination.FullName));
				output.WriteLine(debug
					? $"{destination.FullName} {Icons.ArrowLeft} {source.FullName}"
					: source.NameWithExtension);
			}
			catch (Exception ex) when (IsPerFileFailure(ex))
			{
				report.Failed.Add(new ImageCopyFailure(source.NameWithExtension, source.FullName, ProblemDescription(ex), ex.GetType().Name));
				output.WriteLine(debug
					? $"not copied: {source.FullName}; {ex.GetType().Name}: {ex.Message}"
					: $"not copied: {source.NameWithExtension}");
			}
		}

		return report;
	}

	public static int CountSourceImages(RaiPath sourceRoot)
	{
		return EnumerateImageFiles(sourceRoot).Count();
	}

	public static ImageDeleteReport DeleteByShortName(
		RaiPath subscriberRoot,
		string shortName,
		bool cacheOnly,
		bool force,
		PathConventionType pathConvention = PathConventionType.ItemIdTree8x2,
		ImageNamingConvention namingConvention = ImageNamingConvention.Structured,
		TextWriter? output = null,
		bool debug = false)
	{
		if (subscriberRoot == null)
			throw new ArgumentNullException(nameof(subscriberRoot));
		if (string.IsNullOrWhiteSpace(shortName))
			throw new ArgumentException("ShortName is required.", nameof(shortName));

		var probe = ParseShortName(shortName);
		var bucket = new ImageTreeFile(subscriberRoot, probe.ItemId, string.Empty, string.Empty, pathConvention, namingConvention).SubdirRoot;
		var candidates = EnumerateDeleteCandidates(bucket, probe, cacheOnly, pathConvention, namingConvention).ToList();
		var report = new ImageDeleteReport(shortName, cacheOnly, force, candidates.Count);
		output ??= Console.Out;

		foreach (var candidate in candidates)
		{
			try
			{
				if (force)
					candidate.rm();

				report.Deleted.Add(new ImageDeleteSuccess(candidate.NameWithExtension, candidate.FullName));
				output.WriteLine(DeleteLine(candidate, cacheOnly, force, debug));
			}
			catch (Exception ex) when (IsPerFileFailure(ex))
			{
				report.Failed.Add(new ImageDeleteFailure(candidate.NameWithExtension, candidate.FullName, ProblemDescription(ex), ex.GetType().Name));
				output.WriteLine(debug
					? $"not deleted: {candidate.FullName}; {ex.GetType().Name}: {ex.Message}"
					: $"not deleted: {candidate.NameWithExtension}");
			}
		}

		return report;
	}

	public static ImageDeleteReport CleanItem(
		RaiPath subscriberRoot,
		string itemId,
		bool force,
		TextWriter? output = null,
		bool debug = false)
	{
		ArgumentNullException.ThrowIfNull(subscriberRoot);
		ValidateItemId(itemId, nameof(itemId));
		var homes = Enum.GetValues<PathConventionType>()
			.Select(convention => new ItemTreePath(subscriberRoot, itemId, convention))
			.Select(home => (Home: home, Files: home.SelectFiles()))
			.Where(candidate => candidate.Files.Count > 0)
			.GroupBy(candidate => candidate.Home.SubdirRoot.FullPath, StringComparer.OrdinalIgnoreCase)
			.Select(group => group.First())
			.ToList();
		var files = homes
			.SelectMany(candidate => candidate.Files)
			.GroupBy(file => file.FullName, StringComparer.Ordinal)
			.Select(group => group.First())
			.OrderBy(file => file.FullName, StringComparer.Ordinal)
			.ToList();
		var report = DeleteFiles(itemId, files, cacheOnly: false, force, output, debug);
		if (force && report.FailedCount == 0)
			foreach (var home in homes.Select(candidate => candidate.Home))
				home.PruneEmptyDirectories();
		return report;
	}

	public static ImageDeleteReport CleanCache(
		RaiPath subscriberRoot,
		TextWriter? output = null,
		bool debug = false)
	{
		ArgumentNullException.ThrowIfNull(subscriberRoot);
		var files = subscriberRoot.Exists()
			? subscriberRoot.EnumerateFiles("*", recursive: true)
				.Where(IsRenderedDerivative)
				.OrderBy(file => file.FullName, StringComparer.Ordinal)
				.ToList()
			: [];
		return DeleteFiles("cache", files, cacheOnly: true, force: true, output, debug);
	}

	private static ImageDeleteReport DeleteFiles(
		string target,
		IReadOnlyList<RaiFile> files,
		bool cacheOnly,
		bool force,
		TextWriter? output,
		bool debug)
	{
		var report = new ImageDeleteReport(target, cacheOnly, force, files.Count);
		output ??= Console.Out;
		foreach (var file in files)
		{
			try
			{
				if (force)
					file.rm();
				report.Deleted.Add(new ImageDeleteSuccess(file.NameWithExtension, file.FullName));
				output.WriteLine(DeleteLine(file, cacheOnly, force, debug));
			}
			catch (Exception ex) when (IsPerFileFailure(ex))
			{
				report.Failed.Add(new ImageDeleteFailure(file.NameWithExtension, file.FullName, ProblemDescription(ex), ex.GetType().Name));
				output.WriteLine(debug
					? $"not deleted: {file.FullName}; {ex.GetType().Name}: {ex.Message}"
					: $"not deleted: {file.NameWithExtension}");
			}
		}
		return report;
	}

	private static IEnumerable<RaiFile> EnumerateImageFiles(RaiPath sourceRoot)
	{
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var extension in ImageTypes.Default.Array)
		{
			var ext = (extension ?? string.Empty).Trim().TrimStart('.');
			if (string.IsNullOrWhiteSpace(ext) || !seen.Add(ext))
				continue;

			foreach (var source in sourceRoot.EnumerateFiles($"*.{ext}"))
				yield return source;
		}
	}

	private static bool IsManagedTreeArtifact(RaiFile file)
	{
		var ext = file.Ext.TrimStart('.');
		if (ext.Equals("puml", StringComparison.OrdinalIgnoreCase)
			|| ext.Equals("raid", StringComparison.OrdinalIgnoreCase))
			return true;
		return DeleteExtensions().Contains(ext);
	}

	private static bool IsRenderedDerivative(RaiFile file)
	{
		if (!DeleteExtensions().Contains(file.Ext.TrimStart('.')))
			return false;
		if (file.Ext.Equals("webp", StringComparison.OrdinalIgnoreCase)
			|| file.Ext.Equals("avif", StringComparison.OrdinalIgnoreCase))
			return true;

		var structured = new ImageFile(file.FullName, ImageNamingConvention.Structured);
		return IsCacheImage(structured);
	}

	private static void ValidateItemId(string itemId, string parameterName)
	{
		if (string.IsNullOrWhiteSpace(itemId)
			|| itemId.Contains('/')
			|| itemId.Contains('\\'))
			throw new ArgumentException("ItemId must be one plain item identifier.", parameterName);
	}

	private static ImageFile ParseShortName(string shortName)
	{
		if (shortName.Contains('/') || shortName.Contains('\\'))
			throw new ArgumentException("ShortName must be ItemId or ItemId_Nr, not a path.", nameof(shortName));

		var stem = new RaiFile(shortName.Trim()).Name;
		if (string.IsNullOrWhiteSpace(stem))
			throw new ArgumentException("ShortName is required.", nameof(shortName));

		return new ImageFile(stem, ImageNamingConvention.Structured);
	}

	private static IEnumerable<ImageTreeFile> EnumerateDeleteCandidates(
		RaiPath bucket,
		ImageFile probe,
		bool cacheOnly,
		PathConventionType pathConvention,
		ImageNamingConvention namingConvention)
	{
		if (!bucket.Exists())
			yield break;

		var extensions = DeleteExtensions();
		foreach (var file in bucket.EnumerateFiles("*"))
		{
			if (!extensions.Contains(file.Ext.TrimStart('.')))
				continue;

			var image = new ImageTreeFile(file.FullName, pathConvention, namingConvention);
			if (!string.Equals(image.ItemId, probe.ItemId, StringComparison.OrdinalIgnoreCase))
				continue;
			if (probe.ImageNumber != ImageFile.NoImageNumber && image.ImageNumber != probe.ImageNumber)
				continue;
			if (cacheOnly && !IsCacheImage(image))
				continue;

			yield return image;
		}
	}

	private static HashSet<string> DeleteExtensions()
	{
		var extensions = ImageTreeFile.DefaultSourceExtensions
			.Split([',', ';', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
			.Concat(ImageTypes.Default.Array)
			.Select(ext => ext.Trim().TrimStart('.').ToLowerInvariant());

		return new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
	}

	private static bool IsCacheImage(ImageFile image)
	{
		return !string.IsNullOrWhiteSpace(image.NameExt)
			|| !string.IsNullOrWhiteSpace(image.TemplateName)
			|| !string.IsNullOrWhiteSpace(image.TileTemplate)
			|| !string.IsNullOrWhiteSpace(image.TileNumber);
	}

	private static string DeleteLine(RaiFile candidate, bool cacheOnly, bool force, bool debug)
	{
		var action = force ? "deleted" : "would delete";
		var kind = cacheOnly ? " cached" : string.Empty;
		return debug
			? $"{action}{kind}: {candidate.FullName}"
			: $"{action}{kind}: {candidate.NameWithExtension}";
	}

	private static bool IsPerFileFailure(Exception ex)
	{
		return ex is IOException
			or UnauthorizedAccessException
			or NotSupportedException
			or ArgumentException;
	}

	private static string ProblemDescription(Exception ex)
	{
		return ex.Message;
	}
}

internal static class Program
{
	private const int CommandHelpOptionWidth = 15;

	private static int Main(string[] args)
	{
		if (args.Length > 0 && args[0] is "organize" or "list" or "move" or "clean")
			return RunCommand(args[0], args[1..]);

		return RunMappedArguments(args);
	}

	private static int RunMappedArguments(string[] args)
	{
		try
		{
			if (HasOption(args, "-v", "--version"))
			{
				Messages.WriteSuccess(GetVersion());
				return 0;
			}

			Messages.Debug = HasOption(args, "-d", "--debug");
			Messages.Banner = !HasOption(args, "-l", "--nologo");
			var showHelp = HasOption(args, "-h", "--help");
			var requestedCloudProvider = ParamValue(args, "-c", "--cloudprovider", "--cloud");
			Messages.CloudProvider = ResolveCloudProvider(requestedCloudProvider);
			var rootParam = ParamValue(args, "-r", "--root", "--imageroot");
			var sourceParam = ParamValue(args, "-s", "--source");
			var deleteShortName = ParamValue(args, "-rm", "--rm");
			var deleteCacheShortName = ParamValue(args, "-rmc", "--rm-cache", "-rm-cache");
			var force = HasOption(args, "--force");
			Messages.RootParam = rootParam;
			Messages.Subscriber = PositionalArg(args);

			if (!TryParseEnum(ParamValue(args, "-p", "--pathconv", "--path-conv"), out PathConventionType pathConvention))
				return 1;
			if (!TryParseEnum(ParamValue(args, "-n", "--nameconv", "--name-conv"), out ImageNamingConvention namingConvention))
				return 1;
			Messages.PathConvention = pathConvention;
			Messages.NamingConvention = namingConvention;

			Messages.SourceRoot = !string.IsNullOrWhiteSpace(sourceParam) ? new RaiPath(sourceParam) : null;
			Messages.SourceImageCount = Messages.SourceRoot != null && Messages.SourceRoot.Exists()
				? ImageOrganizer.CountSourceImages(Messages.SourceRoot)
				: null;
			var effectiveCloudProvider = EffectiveCloudProvider(
				Messages.CloudProvider,
				rootParam,
				!string.IsNullOrWhiteSpace(requestedCloudProvider));
			var imageRoot = ResolveImageRoot(effectiveCloudProvider, rootParam);
			Messages.ImageRoot = imageRoot;
			var destinationRoot = ResolveDestinationRoot(imageRoot, Messages.Subscriber);
			Messages.DestinationRoot = destinationRoot;
			var deleteModeCount = (deleteShortName != null ? 1 : 0) + (deleteCacheShortName != null ? 1 : 0);
			var deleteMode = deleteModeCount > 0;
			var deleteCacheOnly = deleteCacheShortName != null;
			var deleteTarget = deleteCacheShortName ?? deleteShortName;

			var runBlockerReason = deleteMode
				? DeleteBlockerReason(deleteModeCount, deleteTarget, destinationRoot, Messages.Subscriber, Messages.Debug)
				: RunBlockerReason(
				Messages.SourceRoot,
				Messages.SourceImageCount,
				destinationRoot,
				Messages.Subscriber,
				Messages.Debug);
			var canRun = runBlockerReason == null;

			if (Messages.Banner)
				Messages.WriteBanner($"{Icons.Info} Image Organizer CLI");

			if (Messages.Debug)
			{
				Messages.WriteDebug($"ImageRoot: {destinationRoot?.FullPath}");
				Messages.WriteDebug($"ImageRootExists: {destinationRoot?.Exists()}");
				Messages.WriteDebug($"Target: {destinationRoot?.FullPath}");
				Messages.WriteDebug($"TargetExists: {destinationRoot?.Exists()}");
				Messages.WriteDebug($"Subscriber: {Messages.Subscriber}");
				Messages.WriteDebug($"Source: {Messages.SourceRoot?.FullPath}");
				Messages.WriteDebug($"SourceExists: {Messages.SourceRoot?.Exists()}");
				Messages.WriteDebug($"SourceImages: {Messages.SourceImageCount}");
				Messages.WriteDebug($"SupportedExtensions: {ImageTypes.Default.String}");
				Messages.WriteDebug($"PathConv: {pathConvention}");
				Messages.WriteDebug($"NameConv: {namingConvention}");
				Messages.WriteDebug($"DeleteMode: {deleteMode}");
				Messages.WriteDebug($"DeleteCacheOnly: {deleteCacheOnly}");
				Messages.WriteDebug($"DeleteTarget: {deleteTarget}");
				Messages.WriteDebug($"Force: {force}");
				Messages.WriteDebug($"CanRun: {canRun}");
				Messages.WriteDebug($"RunBlocker: {runBlockerReason ?? "(none)"}");
			}

			if (showHelp)
			{
				Messages.WriteHelp();
				return 0;
			}

			if (!canRun)
			{
				Messages.WriteHelp();
				Messages.WriteInfo(deleteMode
					? $"No files deleted: {runBlockerReason}"
					: $"No files copied: {runBlockerReason}");
				return 1;
			}

			if (deleteMode)
			{
				var deleteReport = ImageOrganizer.DeleteByShortName(
					destinationRoot!,
					deleteTarget!,
					deleteCacheOnly,
					force,
					pathConvention,
					namingConvention,
					debug: Messages.Debug);

				Messages.WriteSuccess(DeleteSummary(deleteReport, destinationRoot!, Messages.Debug));
				return deleteReport.FailedCount == 0 ? 0 : 1;
			}

			var report = ImageOrganizer.OrganizeWithReport(
				Messages.SourceRoot!,
				destinationRoot!,
				Messages.Subscriber!,
				pathConvention,
				namingConvention,
				debug: Messages.Debug);

			Messages.WriteSuccess(CopySummary(report, Messages.SourceRoot!, destinationRoot!, Messages.Debug));
			return 0;
		}
		catch (Exception ex)
		{
			Messages.WriteError(ex.Message);
			if (Messages.Debug)
				Console.Error.WriteLine(ex);
			return 1;
		}
	}

	private static int RunCommand(string command, string[] args)
	{
		try
		{
			var requestedCloudProvider = ParamValue(args, "-c", "--cloudprovider", "--cloud");
			Messages.CloudProvider = ResolveCloudProvider(requestedCloudProvider);
			if (HasOption(args, "-h", "--help"))
			{
				WriteCommandHelp(command);
				return 0;
			}
			if (HasOption(args, "-v", "--version"))
				return RunMappedArguments(args);

			return command switch
			{
				"organize" => RunOrganizeCommand(args),
				"list" => RunListCommand(args),
				"move" => RunMoveCommand(args),
				"clean" => RunCleanCommand(args),
				_ => throw new ArgumentException($"Unknown command '{command}'.")
			};
		}
		catch (ArgumentException ex)
		{
			Messages.WriteError($"CLI Error: {ex.Message}");
			Messages.WriteInfo($"Run 'iorg {command} --help' for command usage.");
			return 1;
		}
	}

	private static int RunOrganizeCommand(string[] args)
	{
		var valueOptions = CommandGlobalValueOptions
			.Concat(["--source", "-r", "--root", "-p", "--pathconv", "-n", "--nameconv", "--subscriber"])
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var allowed = CommandGlobalSwitchOptions.Concat(valueOptions).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var positionals = ValidateCommandTokens(args, allowed, valueOptions);
		if (positionals.Count > 1)
			throw new ArgumentException("organize accepts at most one positional <Subscriber>.");

		var source = RequiredCommandValue(args, "--source");
		var root = RequiredCommandValue(args, "--root", "-r");
		var pathConvention = NormalizeNumberedCommandEnum<PathConventionType>(RequiredCommandValue(args, "--pathconv", "-p"), "--pathconv");
		var nameConvention = NormalizeNumberedCommandEnum<ImageNamingConvention>(RequiredCommandValue(args, "--nameconv", "-n"), "--nameconv");
		var subscriberOption = ParamValue(args, "--subscriber");
		if (positionals.Count == 1 && !string.IsNullOrWhiteSpace(subscriberOption))
			throw new ArgumentException("Specify the subscriber either positionally or with --subscriber, not both.");

		var rootBinding = BindCommandRoot(
			root,
			Messages.CloudProvider,
			positionals.SingleOrDefault() ?? subscriberOption,
			HasOption(args, "-c", "--cloudprovider", "--cloud"));
		var mapped = CommandGlobalArguments(args, rootBinding);
		mapped.AddRange(["--source", source, "--pathconv", pathConvention, "--nameconv", nameConvention, rootBinding.Subscriber]);
		return RunMappedArguments(mapped.ToArray());
	}

	private static int RunCleanCommand(string[] args)
	{
		var valueOptions = CommandGlobalValueOptions
			.Concat(["-r", "--root", "--subscriber"])
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var allowed = CommandGlobalSwitchOptions.Concat(valueOptions).Concat(["--cache", "--force"])
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var positionals = ValidateCommandTokens(args, allowed, valueOptions);
		var cacheOnly = HasOption(args, "--cache");
		if (cacheOnly && positionals.Count != 0)
			throw new ArgumentException("clean --cache does not accept an ItemId.");
		if (cacheOnly && HasOption(args, "--force"))
			throw new ArgumentException("clean --cache is already explicit and does not accept --force.");
		if (!cacheOnly && positionals.Count != 1)
			throw new ArgumentException("clean requires exactly one <ItemId>, unless --cache is selected.");

		var root = RequiredCommandValue(args, "--root", "-r");
		var rootBinding = BindCommandRoot(
			root,
			Messages.CloudProvider,
			ParamValue(args, "--subscriber"),
			HasOption(args, "-c", "--cloudprovider", "--cloud"));
		var subscriberRoot = ResolveCommandSubscriberRoot(rootBinding);
		var report = cacheOnly
			? ImageOrganizer.CleanCache(subscriberRoot, debug: HasOption(args, "-d", "--debug"))
			: ImageOrganizer.CleanItem(
				subscriberRoot,
				positionals[0],
				HasOption(args, "--force"),
				debug: HasOption(args, "-d", "--debug"));
		Messages.WriteSuccess($"{report.DeletedCount} file(s) {(report.Force ? "deleted" : "selected for deletion")}.");
		return report.FailedCount == 0 ? 0 : 1;
	}

	private static int RunListCommand(string[] args)
	{
		var valueOptions = CommandGlobalValueOptions
			.Concat(["-r", "--root", "--subscriber"])
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var allowed = CommandGlobalSwitchOptions.Concat(valueOptions).Concat(["--json", "--quiet"])
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var positionals = ValidateCommandTokens(args, allowed, valueOptions);
		if (positionals.Count != 1)
			throw new ArgumentException("list requires exactly one <FileNamePattern>.");
		if (HasOption(args, "--json") && HasOption(args, "--quiet"))
			throw new ArgumentException("Use only one of --json or --quiet.");

		var rootBinding = BindCommandRoot(
			RequiredCommandValue(args, "--root", "-r"),
			Messages.CloudProvider,
			ParamValue(args, "--subscriber"),
			HasOption(args, "-c", "--cloudprovider", "--cloud"));
		var subscriberRoot = ResolveCommandSubscriberRoot(rootBinding);
		var files = ImageOrganizer.ListTreeFiles(subscriberRoot, positionals[0]);

		if (HasOption(args, "--json"))
			Console.WriteLine(JsonSerializer.Serialize(files.Select(file => file.NameWithExtension)));
		else
		{
			foreach (var file in files)
				Console.WriteLine(HasOption(args, "--quiet") ? file.FullName : file.NameWithExtension);
			if (!HasOption(args, "--quiet"))
				Messages.WriteSuccess($"{files.Count} matching file(s).");
		}
		return 0;
	}

	private static int RunMoveCommand(string[] args)
	{
		var valueOptions = CommandGlobalValueOptions
			.Concat(["-r", "--root", "--subscriber", "-p", "--pathconv"])
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var allowed = CommandGlobalSwitchOptions.Concat(valueOptions).Concat(["--json", "--quiet"])
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var positionals = ValidateCommandTokens(args, allowed, valueOptions);
		if (positionals.Count is < 1 or > 2)
			throw new ArgumentException("move requires <SourceItemId> and accepts one optional <TargetItemId>.");
		if (HasOption(args, "--json") && HasOption(args, "--quiet"))
			throw new ArgumentException("Use only one of --json or --quiet.");

		var pathValue = ParamValue(args, "--pathconv", "-p");
		if (!TryParseCommandPathConvention(pathValue, out var pathConvention))
			throw new ArgumentException($"Invalid PathConventionType: {pathValue}.");
		var rootBinding = BindCommandRoot(
			RequiredCommandValue(args, "--root", "-r"),
			Messages.CloudProvider,
			ParamValue(args, "--subscriber"),
			HasOption(args, "-c", "--cloudprovider", "--cloud"));
		var subscriberRoot = ResolveCommandSubscriberRoot(rootBinding);
		var moves = ImageOrganizer.MoveItem(
			subscriberRoot,
			positionals[0],
			positionals.Count == 2 ? positionals[1] : null,
			pathConvention);

		if (HasOption(args, "--json"))
			Console.WriteLine(JsonSerializer.Serialize(moves));
		else
		{
			foreach (var move in moves)
				Console.WriteLine(HasOption(args, "--quiet")
					? move.DestinationFullName
					: $"{move.SourceFullName} -> {move.DestinationFullName}");
			if (!HasOption(args, "--quiet"))
				Messages.WriteSuccess($"{moves.Count} file(s) moved.");
		}
		return 0;
	}

	private sealed record CommandRootBinding(string Root, string Subscriber, string? CloudProvider);

	private static CommandRootBinding BindCommandRoot(
		string root,
		string? cloudProvider,
		string? subscriber,
		bool cloudProviderExplicit)
	{
		cloudProvider = EffectiveCloudProvider(cloudProvider, root, cloudProviderExplicit);
		if (!string.IsNullOrWhiteSpace(subscriber))
			return new CommandRootBinding(root, subscriber, cloudProvider);

		var destination = ResolveImageRoot(cloudProvider, root)
			?? throw new ArgumentException("The destination root could not be resolved.");
		var inferredSubscriber = destination.Segments.LastOrDefault();
		if (string.IsNullOrWhiteSpace(inferredSubscriber))
			throw new ArgumentException("Cannot infer a subscriber from --root; provide --subscriber <name>.");

		return new CommandRootBinding(destination.Parent.FullPath, inferredSubscriber, null);
	}

	private static List<string> CommandGlobalArguments(string[] args, CommandRootBinding rootBinding)
	{
		var mapped = new List<string>();
		if (HasOption(args, "-d", "--debug")) mapped.Add("--debug");
		if (HasOption(args, "-l", "--nologo")) mapped.Add("--nologo");
		if (!string.IsNullOrWhiteSpace(rootBinding.CloudProvider))
			mapped.AddRange(["--cloud", rootBinding.CloudProvider]);
		mapped.AddRange(["--root", rootBinding.Root]);
		return mapped;
	}

	private static RaiPath ResolveCommandSubscriberRoot(CommandRootBinding rootBinding)
	{
		var root = ResolveImageRoot(rootBinding.CloudProvider, rootBinding.Root)
			?? throw new ArgumentException("The ImageTree root could not be resolved.");
		return ResolveDestinationRoot(root, rootBinding.Subscriber)
			?? throw new ArgumentException("The subscriber root could not be resolved.");
	}

	private static readonly string[] CommandGlobalValueOptions =
	[
		"-c", "--cloudprovider", "--cloud"
	];

	private static readonly string[] CommandGlobalSwitchOptions =
	[
		"-h", "--help", "-v", "--version", "-d", "--debug", "-l", "--nologo"
	];

	private static List<string> ValidateCommandTokens(
		string[] args,
		IReadOnlySet<string> allowedOptions,
		IReadOnlySet<string> valueOptions)
	{
		var positionals = new List<string>();
		for (var i = 0; i < args.Length; i++)
		{
			var token = args[i];
			if (!token.StartsWith("-", StringComparison.Ordinal))
			{
				positionals.Add(token);
				continue;
			}
			if (!allowedOptions.Contains(token))
				throw new ArgumentException($"Unknown option '{token}'.");
			if (!valueOptions.Contains(token))
				continue;
			if (i + 1 >= args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal))
				throw new ArgumentException($"The option '{token}' requires a value.");
			i++;
		}
		return positionals;
	}

	private static string RequiredCommandValue(string[] args, string option, params string[] aliases)
	{
		var value = ParamValue(args, [option, .. aliases]);
		return !string.IsNullOrWhiteSpace(value)
			? value
			: throw new ArgumentException($"The option '{option}' is required.");
	}

	private static string NormalizeNumberedCommandEnum<TEnum>(string value, string option)
		where TEnum : struct, Enum
	{
		if (!int.TryParse(value, out var number))
			return value;
		var names = Enum.GetNames<TEnum>();
		if (number < 1 || number > names.Length)
			throw new ArgumentException($"The option '{option}' must be between 1 and {names.Length}.");
		return names[number - 1];
	}

	private static bool TryParseCommandPathConvention(string? value, out PathConventionType convention)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			convention = Messages.DefaultPathConvention;
			return true;
		}
		var normalized = NormalizeNumberedCommandEnum<PathConventionType>(value, "--pathconv");
		return Enum.TryParse(normalized, ignoreCase: true, out convention)
			&& Enum.IsDefined(convention);
	}

	private static void WriteCommandHelp(string command)
	{
		var lines = command switch
		{
			"organize" => new[]
			{
				"Usage: iorg organize [<Subscriber> | --subscriber <name>] --source <dir> (-r|--root) <dir> (-p|--pathconv) <1|2|3|4> (-n|--nameconv) <1|2|3> [global options]",
				"Without an explicit subscriber, -r/--root is the complete subscriber destination and its final segment supplies the subscriber identity."
			},
			"list" => new[]
			{
				"Usage: iorg list <FileNamePattern> [--subscriber <name>] (-r|--root) <dir> [--json|--quiet] [global options]",
				"List is strictly read-only and includes image, .puml, and .raid artifacts."
			},
			"move" => new[]
			{
				"Usage: iorg move <SourceItemId> [<TargetItemId>] [--subscriber <name>] (-r|--root) <dir> [--pathconv <1|2|3|4>] [--json|--quiet] [global options]",
				"Path conventions: 1 CanonicalByName, 2 ItemIdTree3x3, 3 ItemIdTree8x2 (default), 4 Flat."
			},
			"clean" => new[]
			{
				"Usage: iorg clean <ItemId> [--subscriber <name>] (-r|--root) <dir> [--force] [global options]",
				"       iorg clean --cache [--subscriber <name>] (-r|--root) <dir> [global options]",
				"Item cleanup is a dry run unless --force is present; --cache explicitly deletes only rendered derivatives."
			},
			_ => Array.Empty<string>()
		};
		foreach (var line in lines)
			Messages.WriteSuccess(line);
		Messages.WriteInfo("Global options: -c|--cloud, -d|--debug, -l|--nologo");
		WriteCommandHelpOption("-c|--cloud:", Messages.CloudDescription());
		if (command is "organize" or "move")
			WriteCommandHelpOption("-p|--pathconv:", Messages.PathConventionDescription());
		if (command == "organize")
			WriteCommandHelpOption("-n|--nameconv:", Messages.NamingConventionDescription());
	}

	private static void WriteCommandHelpOption(string option, string description)
		=> Messages.WriteInfo(
			$"{option.PadRight(CommandHelpOptionWidth)}{description}{Icons.HelpLineWidthCompensation}");

	private static string? RunBlockerReason(
		RaiPath? sourceRoot,
		int? sourceImageCount,
		RaiPath? destinationRoot,
		string? subscriber,
		bool debug)
	{
		if (sourceRoot == null)
			return "no source image directory was given.";
		if (!sourceRoot.Exists())
			return debug
				? $"source image directory does not exist: {sourceRoot.FullPath}"
				: "source image directory does not exist.";
		if (sourceImageCount.GetValueOrDefault() == 0)
			return debug
				? $"no supported image files were found in {sourceRoot.FullPath}; supported: {ImageTypes.Default.String}"
				: "no supported image files were found.";
		if (string.IsNullOrWhiteSpace(subscriber))
			return "no subscriber was given.";
		if (destinationRoot == null)
			return debug
				? "destination image root could not be resolved from --cloud/--root/subscriber."
				: "destination image root could not be resolved.";

		return null;
	}

	private static string? DeleteBlockerReason(
		int deleteModeCount,
		string? shortName,
		RaiPath? destinationRoot,
		string? subscriber,
		bool debug)
	{
		if (deleteModeCount > 1)
			return "use only one of -rm or -rmc/--rm-cache.";
		if (string.IsNullOrWhiteSpace(shortName))
			return "no ShortName was given for delete mode.";
		if (shortName.Contains('/') || shortName.Contains('\\'))
			return debug
				? $"ShortName must be ItemId or ItemId_Nr, not a path: {shortName}"
				: "ShortName must be ItemId or ItemId_Nr, not a path.";
		if (string.IsNullOrWhiteSpace(subscriber))
			return "no subscriber was given.";
		if (destinationRoot == null)
			return debug
				? "destination image root could not be resolved from --cloud/--root/subscriber."
				: "destination image root could not be resolved.";

		return null;
	}

	private static string CopySummary(ImageOrganizer.ImageOrganizeReport report, RaiPath sourceRoot, RaiPath destinationRoot, bool debug)
	{
		var summary = debug
			? $"{report.CopiedCount}/{report.SourceImageCount} source images copied.\nSourceRoot: {sourceRoot.FullPath}\nImageRoot: {destinationRoot.FullPath}\nSupportedExtensions: {ImageTypes.Default.String}"
			: $"{report.CopiedCount}/{report.SourceImageCount} source images copied from {sourceRoot.FullPath} to {destinationRoot.FullPath}";

		if (report.FailedCount == 0)
			return summary;

		var failureSummary = string.Join(", ", report.Failed
			.GroupBy(failure => debug ? $"{failure.ErrorType}: {failure.Problem}" : failure.ErrorType)
			.Select(group => $"{group.Count()} not copied because {group.Key}"));

		if (debug)
		{
			var failedFiles = string.Join("\n", report.Failed.Select(failure =>
				$"NotCopied: {failure.SourceFullName}; {failure.ErrorType}: {failure.Problem}"));
			return $"{summary}\n{failureSummary}\n{failedFiles}";
		}

		return $"{summary}. {failureSummary}";
	}

	private static string DeleteSummary(ImageOrganizer.ImageDeleteReport report, RaiPath destinationRoot, bool debug)
	{
		var mode = report.CacheOnly ? "cached images" : "images";
		var action = report.Force ? "deleted" : "would be deleted";
		var summary = debug
			? $"{report.DeletedCount}/{report.MatchedCount} {mode} {action} for {report.ShortName}.\nImageRoot: {destinationRoot.FullPath}\nForce: {report.Force}"
			: $"{report.DeletedCount}/{report.MatchedCount} {mode} {action} for {report.ShortName}";

		if (!report.Force && report.MatchedCount > 0)
			summary += debug
				? "\nDryRun: add --force to delete these files."
				: " (dry-run; add --force to delete)";

		if (report.FailedCount == 0)
			return summary;

		var failureSummary = string.Join(", ", report.Failed
			.GroupBy(failure => debug ? $"{failure.ErrorType}: {failure.Problem}" : failure.ErrorType)
			.Select(group => $"{group.Count()} not deleted because {group.Key}"));

		if (!debug)
			return $"{summary}. {failureSummary}";

		var failedFiles = string.Join("\n", report.Failed.Select(failure =>
			$"NotDeleted: {failure.FullName}; {failure.ErrorType}: {failure.Problem}"));
		return $"{summary}\n{failureSummary}\n{failedFiles}";
	}

	private static RaiPath? ResolveImageRoot(string? cloudProvider, string? rootParam)
	{
		RaiPath? root = null;
		if (!string.IsNullOrWhiteSpace(cloudProvider))
		{
			string? cloudDir = Os.Config?.Cloud?[cloudProvider];
			if (string.IsNullOrWhiteSpace(cloudDir))
				throw new InvalidOperationException($"The requested cloud provider '{cloudProvider}' is missing or empty in {Os.DefaultConfigFileLocation}.");

			var cloudRoot = new RaiPath(cloudDir);
			root = !string.IsNullOrWhiteSpace(rootParam)
				? cloudRoot / new RaiRelPath(rootParam.TrimStart('/', '\\'))
				: cloudRoot;
		}
		else if (!string.IsNullOrWhiteSpace(rootParam))
		{
			root = new RaiPath(rootParam);
		}

		return root;
	}

	private static string? ResolveCloudProvider(string? requestedCloudProvider)
	{
		var configured = Messages.CloudProviderOptions();
		if (string.IsNullOrWhiteSpace(requestedCloudProvider))
			return configured.FirstOrDefault();

		var resolved = configured.FirstOrDefault(provider =>
			string.Equals(provider, requestedCloudProvider, StringComparison.OrdinalIgnoreCase));
		if (resolved != null)
			return resolved;

		var available = configured.Length > 0 ? string.Join(", ", configured) : "none";
		throw new ArgumentException(
			$"The cloud provider '{requestedCloudProvider}' is not configured as a DefaultDrive on this machine. " +
			$"Configured DefaultCloudOrder options: {available}.");
	}

	private static string? EffectiveCloudProvider(
		string? cloudProvider,
		string? rootParam,
		bool cloudProviderExplicit)
	{
		if (cloudProviderExplicit || string.IsNullOrWhiteSpace(rootParam))
			return cloudProvider;
		if (rootParam == ".")
			return null;

		try
		{
			_ = new RaiRelPath(rootParam);
			return cloudProvider;
		}
		catch (ArgumentException)
		{
			return null;
		}
	}

	private static RaiPath? ResolveDestinationRoot(RaiPath? imageRoot, string? subscriber)
	{
		if (imageRoot == null || string.IsNullOrWhiteSpace(subscriber))
			return null;

		var sPath = new RaiRelPath(subscriber);
		return imageRoot / sPath;
	}

	private static bool TryParseEnum<TEnum>(string? value, out TEnum result)
		where TEnum : struct, Enum
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			result = default;
			if (typeof(TEnum) == typeof(PathConventionType))
				result = (TEnum)(object)Messages.DefaultPathConvention;
			else if (typeof(TEnum) == typeof(ImageNamingConvention))
				result = (TEnum)(object)Messages.DefaultNamingConvention;
			return true;
		}

		if (Enum.TryParse(value, ignoreCase: true, out result))
			return true;

		Messages.WriteError($"Invalid {typeof(TEnum).Name}: {value}. Options: {string.Join(", ", Enum.GetNames<TEnum>())}");
		return false;
	}

	private static bool HasOption(string[] args, params string[] names)
	{
		return args.Any(arg => names.Contains(arg, StringComparer.OrdinalIgnoreCase));
	}

	private static string? ParamValue(string[] args, params string[] names)
	{
		for (int i = 0; i < args.Length; i++)
			{
			if (!names.Contains(args[i], StringComparer.OrdinalIgnoreCase))
				continue;

			return i + 1 < args.Length ? args[i + 1] : null;
		}
		return null;
	}

	private static string? PositionalArg(string[] args)
	{
		var optionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"-h", "--help", "-v", "--version", "-l", "--nologo", "-d", "--debug",
			"-c", "--cloudprovider", "--cloud", "-r", "--root", "--imageroot",
			"-s", "--source", "-p", "--pathconv", "--path-conv",
			"-n", "--nameconv", "--name-conv", "-rm", "--rm", "-rmc", "--rm-cache", "-rm-cache",
			"--force"
		};

		for (int i = 0; i < args.Length; i++)
		{
			if (optionNames.Contains(args[i]))
			{
				if (OptionTakesValue(args[i]))
					i++;
				continue;
			}

			if (!args[i].StartsWith("-", StringComparison.Ordinal))
				return args[i];
		}

		return null;
	}

	private static bool OptionTakesValue(string arg)
	{
		return !new[] { "-h", "--help", "-v", "--version", "-l", "--nologo", "-d", "--debug", "--force" }
			.Contains(arg, StringComparer.OrdinalIgnoreCase);
	}

	private static string GetVersion()
	{
		var assembly = Assembly.GetEntryAssembly();
		var version = assembly?
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
			.InformationalVersion
			.Split('+')[0]
			?? assembly?.GetName().Version?.ToString()
			?? "unknown";
		return $"iorg v{version}";
	}

}
