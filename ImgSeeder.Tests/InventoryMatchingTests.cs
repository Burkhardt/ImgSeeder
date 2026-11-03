using System.Text.Json;
using OsLib;
using RaiImage;

namespace Iorg.Tests;

public sealed class InventoryMatchingTests : IDisposable
{
	private readonly RaiPath root = Os.TempDir / ("cr075-" + Guid.NewGuid().ToString("N"));
	public InventoryMatchingTests() => root.mkdir();
	private RaiFile Write(string name, string content)
	{
		var file = root.ContainedFile(name); file.mkdir();
		new TextFile(file.FullName, content);
		return file;
	}
	private RaiSystemResult Run(params string[] arguments) => IorgCommand.ForManagedAssembly(new RaiFile(new RaiPath(AppContext.BaseDirectory), "ImgSeeder", "dll")).Run(arguments);
	private RaiSystemResult List(params string[] flags) => Run(["list", "*", "--root", root.FullPath, .. flags]);
	[Fact]
	public async Task AC1_AC2_MetadataDoesNotChangeLegacyJsonAndContainsTypedFields()
	{
		var video = Write("PLVanguard/IMG_5443.MOV", "video-data");
		using var legacy = JsonDocument.Parse(List("--json").StandardOutput);
		Assert.Equal("IMG_5443.MOV", legacy.RootElement[0].GetString());
		var run = Run("list", "*.mov", "--root", root.FullPath, "--ignore-case", "--long", "--json", "--checksum", "sha256");
		Assert.Equal(0, run.ExitCode); Assert.Empty(run.StandardError);
		using var json = JsonDocument.Parse(run.StandardOutput); var item = json.RootElement[0];
		Assert.Equal("PLVanguard/IMG_5443.MOV", item.GetProperty("RelativePath").GetString());
		Assert.Equal(video.Length, item.GetProperty("SizeBytes").GetInt64());
		Assert.Equal("Video", item.GetProperty("MediaKind").GetString());
		Assert.Equal(video.CanonicalFullName, item.GetProperty("FullPath").GetString());
		Assert.EndsWith("Z", item.GetProperty("LastModifiedUtc").GetString());
		Assert.Equal(Environment.MachineName, item.GetProperty("ExecutionHost").GetString());
		Assert.Equal(JsonValueKind.Null, item.GetProperty("Cloud").ValueKind);
		Assert.Equal(await MediaContent.Sha256Async(video), item.GetProperty("Sha256").GetString());
	}
	[Fact]
	public void AC3_AllFilesIsMetadataOnlyAndRejectsChecksum()
	{
		Write("clip.mov", "video"); Write("unknown.bin", "not-media");
		using var normal = JsonDocument.Parse(List("--long", "--json").StandardOutput);
		Assert.Equal(1, normal.RootElement.GetArrayLength());
		var run = List("--all-files", "--long", "--json"); Assert.Equal(0, run.ExitCode);
		using var json = JsonDocument.Parse(run.StandardOutput); Assert.Equal(2, json.RootElement.GetArrayLength());
		Assert.Equal(JsonValueKind.Null, json.RootElement[1].GetProperty("MediaKind").ValueKind);
		var invalid = List("--all-files", "--long", "--json", "--checksum", "sha256");
		Assert.NotEqual(0, invalid.ExitCode); Assert.Empty(invalid.StandardOutput); Assert.Contains("metadata-only", invalid.StandardError);
	}
	[Fact]
	public async Task AC4_AC5_AC6_AC7_LiteralAndWildcardUseVerifiedContentNotNamesOrSizes()
	{
		Write("original.mov", "123456"); Write("Archive/renamed.mp4", "123456"); Write("unique.mov", "654321");
		var before = await Snapshot();
		var run = Run("duplicates", "original.mov", "--root", root.FullPath, "--json"); Assert.Equal(0, run.ExitCode);
		using var json = JsonDocument.Parse(run.StandardOutput); Assert.Equal(1, json.RootElement.GetArrayLength());
		var match = json.RootElement[0]; Assert.Equal("Bytes", match.GetProperty("MatchMode").GetString());
		Assert.Equal("SHA-256", match.GetProperty("Evidence").GetProperty("Algorithm").GetString());
		Assert.True(match.GetProperty("Evidence").GetProperty("ByteComparisonVerified").GetBoolean());
		Assert.Equal("Archive/renamed.mp4", match.GetProperty("File").GetProperty("RelativePath").GetString());
		var all = Run("duplicates", "*", "--root", root.FullPath, "--json"); Assert.Equal(0, all.ExitCode);
		using var dictionary = JsonDocument.Parse(all.StandardOutput);
		Assert.Equal(JsonValueKind.Object, dictionary.RootElement.ValueKind);
		Assert.Equal(0, dictionary.RootElement.GetProperty("unique.mov").GetArrayLength());
		Assert.Equal(1, dictionary.RootElement.GetProperty("original.mov").GetArrayLength());
		Assert.Equal(before, await Snapshot());
	}
	[Fact]
	public void WildcardSelectingOneFileStillReturnsDictionaryAndNoSourcesReturnsObject()
	{
		Write("single.mov", "data");
		using var one = JsonDocument.Parse(Run("duplicates", "single*", "--root", root.FullPath, "--json").StandardOutput);
		Assert.Equal(JsonValueKind.Object, one.RootElement.ValueKind);
		Assert.Equal(0, one.RootElement.GetProperty("single.mov").GetArrayLength());
		Assert.Equal("{}", Run("duplicates", "missing*", "--root", root.FullPath, "--json").StandardOutput.Trim());
	}
	[Theory]
	[InlineData("missing.mov")]
	[InlineData("../../escape.mov")]
	public void AC9_MissingOrEscapingSourceFailsWithoutFalseEmptyJson(string selector)
	{
		var run = Run("duplicates", selector, "--root", root.FullPath, "--json");
		Assert.NotEqual(0, run.ExitCode); Assert.Empty(run.StandardOutput); Assert.Contains("error:", run.StandardError);
	}
	[Fact]
	public void AmbiguousNameRequiresRelativePathAndVisualRequiresExplicitProfile()
	{
		Write("a/clip.mov", "one"); Write("b/clip.mov", "two");
		var ambiguous = Run("duplicates", "clip.mov", "--root", root.FullPath, "--json");
		Assert.NotEqual(0, ambiguous.ExitCode); Assert.Contains("ambiguous", ambiguous.StandardError);
		var visual = Run("duplicates", "a/clip.mov", "--root", root.FullPath, "--match", "visual", "--json");
		Assert.NotEqual(0, visual.ExitCode); Assert.Contains("strict-v1", visual.StandardError); Assert.Empty(visual.StandardOutput);
	}
	[Fact]
	public void AC8_VisualImagesMatchChangedMetadataAndReportAudioIsolation()
	{
		var first = root.ContainedFile("original.png"); var second = root.ContainedFile("other.png");
		var magick = new ImageMagickCommand(); Assert.True(magick.IsAvailable());
		Assert.Equal(0, magick.Run(["-size", "16x16", "xc:red", first.FullName]).ExitCode);
		Assert.Equal(0, magick.Run([first.FullName, "-set", "comment", "different metadata", second.FullName]).ExitCode);
		var run = Run("duplicates", "original.png", "--root", root.FullPath, "--match", "visual", "--visual-profile", "strict-v1", "--json");
		Assert.True(run.ExitCode == 0, run.StandardError);
		using var json = JsonDocument.Parse(run.StandardOutput); var evidence = json.RootElement[0].GetProperty("Evidence");
		Assert.False(evidence.GetProperty("AudioCompared").GetBoolean()); Assert.False(evidence.GetProperty("ByteComparisonVerified").GetBoolean());
		Assert.Equal("DecodedExact", evidence.GetProperty("Method").GetString());
	}
	[Fact]
	public async Task AppAddressProvidesCloudIndependentImageTreeContext()
	{
		Write("App/Image/Customer/clip.mov", "x");
		var run = Run("list", "*", "--app", (root / "App").FullPath, "--tenant", "Customer", "--long", "--json");
		Assert.Equal(0, run.ExitCode);
		using var json = JsonDocument.Parse(run.StandardOutput);
		Assert.NotEqual(JsonValueKind.Null, json.RootElement[0].GetProperty("ItemTreePath").ValueKind);
		Assert.NotEmpty(await Snapshot());
	}
	private async Task<string> Snapshot()
	{
		var rows = new List<string>();
		foreach (var file in root.EnumerateFiles("*", recursive: true).OrderBy(f => f.FullName, StringComparer.Ordinal))
			rows.Add(file.FullName + "|" + file.LastWriteTimeUtc + "|" + await MediaContent.Sha256Async(file));
		return string.Join('\n', rows);
	}
	public void Dispose() => root.rmdir(depth: 20, deleteFiles: true);
}
