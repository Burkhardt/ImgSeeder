using System.Globalization;
using OsLib;
using RaiImage;

namespace ImgSeeder;

/// <summary>A single read-only scan with explicit execution-host provenance.</summary>
public sealed class ReadOnlyMedia
{
	private readonly RaiPath root;
	private readonly RaiPath? cloudRoot;
	private readonly string? cloud;
	private readonly bool managed;
	public ReadOnlyMedia(RaiPath root, RaiPath? cloudRoot = null, string? cloud = null, bool managed = false)
	{
		if (!root.Exists()) throw new DirectoryNotFoundException($"Search root does not exist: {root.FullPath}");
		this.root = root.Canonical(); this.cloudRoot = cloudRoot?.Canonical(); this.cloud = cloud; this.managed = managed;
		if (this.cloudRoot is not null) _ = this.cloudRoot.RelativeFileName(new RaiFile(this.root, "containment-probe"));
	}
	public static bool IsMedia(RaiFile file) => MediaTypes.Default.Array.Contains(file.Ext.TrimStart('.'), StringComparer.OrdinalIgnoreCase);
	private static bool IsArtifact(RaiFile file) => IsMedia(file) || file.Ext.TrimStart('.').ToLowerInvariant() is "puml" or "raid";
	public static bool IsWildcard(string selector) => selector.Contains('*') || selector.Contains('?');
	public static bool Matches(string name, string pattern, bool ignoreCase) => RaiFile.MatchesPattern(name, pattern, ignoreCase || OperatingSystem.IsWindows());
	private List<RaiFile> Scan(bool allFiles)
	{
		var result = new List<RaiFile>();
		var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
		void Walk(RaiPath directory)
		{
			foreach (var file in directory.EnumerateFiles("*"))
			{
				_ = root.RelativeFileName(file);
				if (seen.Add(file.CanonicalFullName) && (allFiles || IsArtifact(file))) result.Add(file);
			}
			foreach (var child in directory.EnumerateDirectories("*"))
			{
				if (child.IsSymbolicLink) throw new IOException($"Directory link is not traversed by a complete inventory: {child.FullPath}");
				Walk(child);
			}
		}
		Walk(root);
		return result.OrderBy(file => root.RelativeFileName(file), StringComparer.Ordinal).ToList();
	}
	private IorgInventoryItem Item(RaiFile file, string? hash = null)
	{
		var relative = root.RelativeFileName(file);
		var cloudRelative = cloudRoot?.RelativeFileName(file);
		return new()
		{
			Name = file.NameWithExtension, FullPath = file.CanonicalFullName, RelativePath = relative,
			CloudRelativePath = cloudRelative, ItemTreePath = managed ? cloudRelative ?? relative : null,
			Extension = string.IsNullOrEmpty(file.Ext) ? "" : "." + file.Ext.TrimStart('.').ToLowerInvariant(),
			MediaKind = IsMedia(file) ? MediaTypes.IsVideo(file.Ext) ? "Video" : "Image" : null,
			SizeBytes = file.Length, LastModifiedUtc = file.LastWriteTimeUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
			Cloud = cloud, ResolvedRoot = root.FullPath, ExecutionHost = Environment.MachineName, Sha256 = hash
		};
	}
	public async Task<IReadOnlyList<IorgInventoryItem>> InventoryAsync(string pattern, bool ignoreCase = false, bool allFiles = false, bool checksum = false, CancellationToken ct = default)
	{
		if (pattern.Contains('/') || pattern.Contains('\\')) throw new ArgumentException("Inventory pattern must be a filename pattern, not a path.");
		if (allFiles && checksum) throw new ArgumentException("--all-files is a metadata-only census and cannot use --checksum.");
		var items = new List<IorgInventoryItem>();
		foreach (var file in Scan(allFiles).Where(f => Matches(f.NameWithExtension, pattern, ignoreCase)))
		{
			ct.ThrowIfCancellationRequested();
			var stamp = MediaContent.Inspect(file);
			var hash = checksum ? await MediaContent.Sha256Async(file, ct) : null;
			items.Add(Item(file, hash)); MediaContent.RequireUnchanged(file, stamp);
		}
		return items;
	}
	public async Task<object> DuplicatesAsync(string selector, string mode = "bytes", string? profile = null, bool ignoreCase = false, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(selector) || selector.StartsWith('-')) throw new ArgumentException("A source file or filename pattern is required.");
		if (mode is not ("bytes" or "visual")) throw new ArgumentException("--match must be bytes or visual.");
		if (mode == "visual" ? profile != "strict-v1" : profile is not null) throw new ArgumentException("Visual mode requires --visual-profile strict-v1; byte mode has no visual profile.");
		if (selector.Replace('\\', '/').Split('/').Contains("..")) throw new ArgumentException("Source path cannot escape the selected root.");
		var wildcard = IsWildcard(selector);
		if (wildcard && (selector.Contains('/') || selector.Contains('\\'))) throw new ArgumentException("Use a filename wildcard and select its subtree through --root.");
		var files = Scan(false).Where(IsMedia).ToList();
		var source = wildcard ? files.Where(f => Matches(f.NameWithExtension, selector, ignoreCase)).ToList()
			: selector.Contains('/') || selector.Contains('\\') ? files.Where(f => f.CanonicalFullName == root.ContainedFile(selector).CanonicalFullName).ToList()
			: files.Where(f => Matches(f.NameWithExtension, selector, ignoreCase)).ToList();
		if (!wildcard && source.Count != 1) throw new ArgumentException(source.Count == 0 ? "Source media file was not found." : "Source filename is ambiguous; provide its root-relative path.");
		var stamps = files.ToDictionary(f => f.FullName, MediaContent.Inspect, StringComparer.Ordinal);
		var metadata = files.ToDictionary(f => f.FullName, f => Item(f), StringComparer.Ordinal);
		var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
		var comparisons = new Dictionary<(string, string), IorgMatchEvidence?>();
		var visual = new MediaVisualComparison();
		if (mode == "visual") foreach (var file in files) await visual.ValidateAsync(file, ct);
		async Task<string> Hash(RaiFile file)
		{
			if (!hashes.TryGetValue(file.FullName, out var hash)) hashes[file.FullName] = hash = await MediaContent.Sha256Async(file, ct);
			return hash;
		}
		var byteGroups = new Dictionary<string, List<RaiFile>>(StringComparer.Ordinal);
		if (mode == "bytes")
		{
			var neededSizes = source.Select(f => stamps[f.FullName].Size).ToHashSet();
			foreach (var group in files.GroupBy(f => stamps[f.FullName].Size).Where(g => neededSizes.Contains(g.Key) && g.Count() > 1))
				foreach (var file in group)
				{
					var digest = await Hash(file);
					if (!byteGroups.TryGetValue(digest, out var members)) byteGroups.Add(digest, members = []);
					members.Add(file);
				}
		}
		var dictionary = new SortedDictionary<string, List<IorgMatchResult>>(StringComparer.Ordinal);
		foreach (var first in source)
		{
			ct.ThrowIfCancellationRequested();
			var matches = new List<IorgMatchResult>(); dictionary.Add(root.RelativeFileName(first), matches);
			var candidates = mode == "visual" ? files
				: hashes.TryGetValue(first.FullName, out var fingerprint) ? byteGroups[fingerprint] : [];
			foreach (var second in candidates)
			{
				if (first.CanonicalFullName == second.CanonicalFullName) continue;
				var key = string.CompareOrdinal(first.FullName, second.FullName) < 0 ? (first.FullName, second.FullName) : (second.FullName, first.FullName);
				if (!comparisons.TryGetValue(key, out var evidence))
				{
					evidence = null;
					if (mode == "bytes")
					{
						if (stamps[first.FullName].Size == stamps[second.FullName].Size && await Hash(first) == await Hash(second)
							&& await MediaContent.ByteEqualsAsync(first, second, ct))
							evidence = new() { Algorithm = "SHA-256", Digest = hashes[first.FullName], ByteComparisonVerified = true, Method = "StreamingBytes" };
					}
					else evidence = await visual.CompareAsync(first, second, ct);
					if (evidence is not null) comparisons.Add(key, evidence);
				}
				if (evidence is not null) matches.Add(new(metadata[second.FullName] with { Sha256 = hashes.GetValueOrDefault(second.FullName) }, mode == "bytes" ? "Bytes" : "Visual", evidence));
			}
		}
		foreach (var file in files) MediaContent.RequireUnchanged(file, stamps[file.FullName]);
		return wildcard ? dictionary : dictionary.Values.Single();
	}
}
