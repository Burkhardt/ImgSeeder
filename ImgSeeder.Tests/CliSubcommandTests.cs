using ImgSeeder;
using OsLib;
using RaiImage;

namespace Iorg.Tests;

public sealed class CliSubcommandTests : IDisposable
{
	private readonly RaiPath root = Os.TempDir / "RAIkeep" / "iorg-tests" / "cr006-subcommands";

	public CliSubcommandTests()
	{
		Cleanup();
		root.mkdir();
	}

	public void Dispose() => Cleanup();

	[Fact]
	public void OrganizeCommand_InfersSubscriberFromCompleteRoot_AndCopiesImage()
	{
		var sourceRoot = (root / "command-source").mkdir();
		var source = WriteImage(sourceRoot, "nomsa-concert-11", "jpg");
		var destination = root / "command-dest" / "nomsa";

		var run = RunIorg(
			"organize", "--source", sourceRoot.FullPath, "-r", destination.FullPath,
			"--pathconv", "3", "--nameconv", "3", "--nologo");

		Assert.Equal(0, run.exitCode);
		var expected = new ImageTreeFile(
			destination, "NomsaConcert", string.Empty, "jpg",
			PathConventionType.ItemIdTree8x2, ImageNamingConvention.Structured)
		{
			ImageNumber = 11
		};
		Assert.True(source.Exists());
		Assert.True(expected.Exists(), $"Expected organized image: {expected.FullName}\n{run.output}");
	}

	[Fact]
	public void CleanCommand_IsDryRunByDefault_AndForceDeletesExactItemFamily()
	{
		var destination = root / "clean-dest" / "nomsa";
		var item = new ItemTreePath(destination, "AfricanBrisket");
		var target = WriteArtifact(item, "AfricanBrisket_01.png");
		var diagram = WriteArtifact(item, "AfricanBrisket.raid");
		var sibling = WriteArtifact(item, "AfricanBrigadine.png");

		var dryRun = RunIorg("clean", "AfricanBrisket", "-r", destination.FullPath, "--nologo");
		Assert.Equal(0, dryRun.exitCode);
		Assert.Contains("would delete", dryRun.output, StringComparison.OrdinalIgnoreCase);
		Assert.True(target.Exists());
		Assert.True(diagram.Exists());

		var force = RunIorg("clean", "AfricanBrisket", "--root", destination.FullPath, "--force", "--nologo");
		Assert.Equal(0, force.exitCode);
		Assert.False(target.Exists());
		Assert.False(diagram.Exists());
		Assert.True(sibling.Exists());
	}

	[Fact]
	public void CleanCache_DeletesOnlyRenderedDerivatives_AndPreservesSourcesAndDiagramFiles()
	{
		var destination = root / "cache-clean-dest" / "nomsa";
		var item = new ItemTreePath(destination, "AfricanBrisket");
		var pngSource = WriteArtifact(item, "AfricanBrisket_01.png");
		var pngDerivative = WriteArtifact(item, "AfricanBrisket_01_Small.png");
		var webpDerivative = WriteArtifact(item, "AfricanBrisket_02.webp");
		var avifDerivative = WriteArtifact(item, "AfricanBrisket_03.avif");
		var svg = WriteArtifact(item, "AfricanBrisket.svg");
		var puml = WriteArtifact(item, "AfricanBrisket.puml");
		var config = WriteArtifact(item, "AfricanBrisket_config.puml");
		var raid = WriteArtifact(item, "AfricanBrisket.raid");

		var run = RunIorg("clean", "--cache", "--root", destination.FullPath, "--nologo");

		Assert.Equal(0, run.exitCode);
		Assert.True(pngSource.Exists());
		Assert.False(pngDerivative.Exists());
		Assert.False(webpDerivative.Exists());
		Assert.False(avifDerivative.Exists());
		Assert.True(svg.Exists());
		Assert.True(puml.Exists());
		Assert.True(config.Exists());
		Assert.True(raid.Exists());
	}

	[Fact]
	public void CleanCache_RejectsRedundantForceOption()
	{
		var destination = root / "cache-force-dest" / "nomsa";

		var run = RunIorg(
			"clean", "--cache", "--force", "--root", destination.FullPath, "--nologo");

		Assert.Equal(1, run.exitCode);
		Assert.Contains("does not accept --force", run.output);
	}

	[Fact]
	public void LegacyOrganize_RemainsAvailable()
	{
		var sourceRoot = (root / "legacy-source").mkdir();
		_ = WriteImage(sourceRoot, "legacy-picture-01", "jpg");
		var destinationBase = root / "legacy-dest";

		var run = RunIorg(
			"--nologo", "--source", sourceRoot.FullPath, "--root", destinationBase.FullPath,
			"legacy", "--pathconv", "ItemIdTree8x2", "--nameconv", "Structured");

		Assert.Equal(0, run.exitCode);
		var expected = new ImageTreeFile(
			destinationBase / "legacy", "LegacyPicture", string.Empty, "jpg",
			PathConventionType.ItemIdTree8x2, ImageNamingConvention.Structured)
		{
			ImageNumber = 1
		};
		Assert.True(expected.Exists(), $"Expected legacy organized image: {expected.FullName}\n{run.output}");
	}

	[Fact]
	public void CommandOptions_AreIsolated_AndHelpIsContextual()
	{
		var invalid = RunIorg("clean", "Picture", "--root", (root / "dest").FullPath, "--pathconv", "1", "--nologo");
		Assert.Equal(1, invalid.exitCode);
		Assert.Contains("Unknown option '--pathconv'", invalid.output, StringComparison.OrdinalIgnoreCase);

		var cleanHelp = RunIorg("clean", "--help");
		Assert.Equal(0, cleanHelp.exitCode);
		Assert.Contains("--cache", cleanHelp.output);
		Assert.Contains("-r|--root", cleanHelp.output);
		Assert.DoesNotContain("--pathconv", cleanHelp.output);

		var organizeHelp = RunIorg("organize", "--help");
		Assert.Equal(0, organizeHelp.exitCode);
		Assert.Contains("-r|--root", organizeHelp.output);

		var moveHelp = RunIorg("move", "--help");
		Assert.Equal(0, moveHelp.exitCode);
		Assert.Contains("--pathconv", moveHelp.output);
		Assert.DoesNotContain("--nameconv", moveHelp.output);

		var rootHelp = RunIorg("--help");
		Assert.Equal(0, rootHelp.exitCode);
		Assert.DoesNotContain("===", rootHelp.output, StringComparison.Ordinal);
		Assert.Contains("organize, list, move, clean", rootHelp.output, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("flat operation syntax remains supported", rootHelp.output, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void ListCommand_ReturnsImageAndDiagramArtifactsRecursively()
	{
		var imageTreeRoot = root / "list-tree";
		var item = new ItemTreePath(imageTreeRoot / "Nomsa", "AfricanBrisket");
		WriteArtifact(item, "AfricanBrisket_01.png");
		WriteArtifact(item, "AfricanBrisket_02.webp");
		WriteArtifact(item, "AfricanBrisket.svg");
		WriteArtifact(item, "AfricanBrisket.puml");
		WriteArtifact(item, "AfricanBrisket_config.puml");
		WriteArtifact(item, "AfricanBrisket.raid");
		WriteArtifact(item, "AfricanBrisket.tmp");

		var run = RunIorg(
			"list", "AfricanBrisket*", "--root", imageTreeRoot.FullPath,
			"--subscriber", "Nomsa", "--nologo");

		Assert.Equal(0, run.exitCode);
		Assert.Contains("AfricanBrisket_01.png", run.output);
		Assert.Contains("AfricanBrisket_02.webp", run.output);
		Assert.Contains("AfricanBrisket.svg", run.output);
		Assert.Contains("AfricanBrisket.puml", run.output);
		Assert.Contains("AfricanBrisket_config.puml", run.output);
		Assert.Contains("AfricanBrisket.raid", run.output);
		Assert.DoesNotContain("AfricanBrisket.tmp", run.output);
		Assert.Contains("6 matching file(s).", run.output);
	}

	[Fact]
	public void ListCommand_JsonProducesOnlyMachineReadableArray()
	{
		var imageTreeRoot = root / "list-json-tree";
		var item = new ItemTreePath(imageTreeRoot / "Nomsa", "ScheduleRehearsal");
		WriteArtifact(item, "ScheduleRehearsal.raid");
		WriteArtifact(item, "ScheduleRehearsal.svg");

		var run = RunIorg(
			"list", "ScheduleRehearsal*", "--root", imageTreeRoot.FullPath,
			"--subscriber", "Nomsa", "--json", "--nologo");

		Assert.Equal(0, run.exitCode);
		Assert.Equal(
			"[\"ScheduleRehearsal.raid\",\"ScheduleRehearsal.svg\"]",
			 run.output.Trim());
	}

	[Fact]
	public void ListCommand_SupportsDocumentedPumlRaidAndCompleteInventoryPatterns()
	{
		var imageTreeRoot = root / "list-pattern-tree";
		var item = new ItemTreePath(imageTreeRoot / "Nomsa", "ScheduleRehearsal");
		WriteArtifact(item, "ScheduleRehearsal.puml");
		WriteArtifact(item, "ScheduleRehearsal_config.puml");
		WriteArtifact(item, "ScheduleRehearsal.raid");
		WriteArtifact(item, "ScheduleRehearsal.svg");

		var puml = RunIorg(
			"list", "*.puml", "--root", imageTreeRoot.FullPath,
			"--subscriber", "Nomsa", "--json", "--nologo");
		var raid = RunIorg(
			"list", "*.raid", "--root", imageTreeRoot.FullPath,
			"--subscriber", "Nomsa", "--json", "--nologo");
		var all = RunIorg(
			"list", "*", "--root", imageTreeRoot.FullPath,
			"--subscriber", "Nomsa", "--json", "--nologo");

		Assert.Equal(0, puml.exitCode);
		Assert.Equal(
			"[\"ScheduleRehearsal.puml\",\"ScheduleRehearsal_config.puml\"]",
			puml.output.Trim());
		Assert.Equal(0, raid.exitCode);
		Assert.Equal("[\"ScheduleRehearsal.raid\"]", raid.output.Trim());
		Assert.Equal(0, all.exitCode);
		Assert.Equal(
			"[\"ScheduleRehearsal.puml\",\"ScheduleRehearsal.raid\",\"ScheduleRehearsal.svg\",\"ScheduleRehearsal_config.puml\"]",
			all.output.Trim());
	}

	[Fact]
	public void ListCommand_ZeroMatches_IsSuccessfulAndReadOnly()
	{
		var imageTreeRoot = root / "list-empty-tree";
		var item = new ItemTreePath(imageTreeRoot / "Nomsa", "ScheduleRehearsal");
		var existing = WriteArtifact(item, "ScheduleRehearsal.raid");

		var run = RunIorg(
			"list", "Missing*", "--root", imageTreeRoot.FullPath,
			"--subscriber", "Nomsa", "--nologo");

		Assert.Equal(0, run.exitCode);
		Assert.Contains("0 matching file(s).", run.output);
		Assert.True(existing.Exists());
	}

	[Fact]
	public void MoveCommand_RenamesWholeItemFamily_AndLeavesSharedBucketSibling()
	{
		var imageTreeRoot = root / "move-tree";
		var source = new ItemTreePath(imageTreeRoot / "Nomsa", "AfricanBrisket");
		WriteArtifact(source, "AfricanBrisket_01.png");
		WriteArtifact(source, "AfricanBrisket_02.webp");
		WriteArtifact(source, "AfricanBrisket.svg");
		WriteArtifact(source, "AfricanBrisket.puml");
		WriteArtifact(source, "AfricanBrisket_config.puml");
		WriteArtifact(source, "AfricanBrisket.raid");
		var sibling = WriteArtifact(source, "AfricanBrigadine.png");

		var run = RunIorg(
			"move", "AfricanBrisket", "AfricanDinner",
			"--root", imageTreeRoot.FullPath, "--subscriber", "Nomsa",
			"--pathconv", "3", "--nologo");

		Assert.Equal(0, run.exitCode);
		Assert.Contains("6 file(s) moved.", run.output);
		Assert.Empty(source.SelectFiles());
		Assert.True(sibling.Exists());
		var destination = new ItemTreePath(imageTreeRoot / "Nomsa", "AfricanDinner");
		Assert.Equal(6, destination.SelectFiles().Count);
	}

	[Fact]
	public void MoveCommand_MigratesItemFrom8x2ToFlat_WithCompatibilitySafeOptionFour()
	{
		var imageTreeRoot = root / "move-flat-tree";
		var source = new ItemTreePath(imageTreeRoot / "Nomsa", "AfricanBrisket");
		WriteArtifact(source, "AfricanBrisket.png");
		WriteArtifact(source, "AfricanBrisket.raid");

		var run = RunIorg(
			"move", "AfricanBrisket", "--root", imageTreeRoot.FullPath,
			"--subscriber", "Nomsa", "--pathconv", "4", "--nologo");

		Assert.Equal(0, run.exitCode);
		var flat = new ItemTreePath(imageTreeRoot / "Nomsa", "AfricanBrisket", PathConventionType.Flat);
		Assert.Equal(2, flat.SelectFiles().Count);
		Assert.Empty(source.SelectFiles());
	}

	[Fact]
	public void MoveCommand_MigratesItemFrom3x3To8x2_AndPrunesVacatedBuckets()
	{
		var imageTreeRoot = root / "move-convention-tree";
		var source = new ItemTreePath(
			imageTreeRoot / "Nomsa",
			"AfricanBrisket",
			PathConventionType.ItemIdTree3x3);
		WriteArtifact(source, "AfricanBrisket_01.png");
		WriteArtifact(source, "AfricanBrisket.puml");

		var run = RunIorg(
			"move", "AfricanBrisket", "--root", imageTreeRoot.FullPath,
			"--subscriber", "Nomsa", "--pathconv", "3", "--nologo");

		Assert.Equal(0, run.exitCode);
		Assert.False(source.SubdirRoot.Exists());
		Assert.False(source.TopdirRoot.Exists());
		var destination = new ItemTreePath(imageTreeRoot / "Nomsa", "AfricanBrisket");
		Assert.Equal(2, destination.SelectFiles().Count);
	}

	[Fact]
	public void MoveCommand_RenamesAfricanPicnicToAfricanBreakfast_WithAllSuffixesIntact()
	{
		var imageTreeRoot = root / "move-rename-tree";
		var source = new ItemTreePath(imageTreeRoot / "Nomsa", "AfricanPicnic");
		WriteArtifact(source, "AfricanPicnic_01.png");
		WriteArtifact(source, "AfricanPicnic_01_Small.webp");
		WriteArtifact(source, "AfricanPicnic.puml");
		WriteArtifact(source, "AfricanPicnic.raid");

		var run = RunIorg(
			"move", "AfricanPicnic", "AfricanBreakfast",
			"--root", imageTreeRoot.FullPath, "--subscriber", "Nomsa",
			"--pathconv", "3", "--nologo");

		Assert.Equal(0, run.exitCode);
		var destination = new ItemTreePath(imageTreeRoot / "Nomsa", "AfricanBreakfast");
		Assert.Equal(
			new[]
			{
				"AfricanBreakfast_01.png",
				"AfricanBreakfast_01_Small.webp",
				"AfricanBreakfast.puml",
				"AfricanBreakfast.raid"
			}.Order(StringComparer.Ordinal),
			destination.SelectFiles()
				.Select(file => file.NameWithExtension)
				.Order(StringComparer.Ordinal));
	}

	[Fact]
	public void MoveCommand_RenamesIndexedItemIdWithoutConfusingItsIndexForAFileSuffix()
	{
		var imageTreeRoot = root / "move-indexed-tree";
		var source = new ItemTreePath(imageTreeRoot / "Nomsa", "AfricanBreakfast_04");
		WriteArtifact(source, "AfricanBreakfast_04.png");
		WriteArtifact(source, "AfricanBreakfast_04_config.puml");

		var run = RunIorg(
			"move", "AfricanBreakfast_04", "AfricanLunch_01",
			"--root", imageTreeRoot.FullPath, "--subscriber", "Nomsa",
			"--pathconv", "3", "--nologo");

		Assert.Equal(0, run.exitCode);
		var destination = new ItemTreePath(imageTreeRoot / "Nomsa", "AfricanLunch_01");
		Assert.Equal(
			new[] { "AfricanLunch_01.png", "AfricanLunch_01_config.puml" }.Order(StringComparer.Ordinal),
			destination.SelectFiles()
				.Select(file => file.NameWithExtension)
				.Order(StringComparer.Ordinal));
	}

	[Fact]
	public void MoveCommand_ShortItemId_SharedPhysicalConventionHomeIsNotReportedAsDuplicate()
	{
		var imageTreeRoot = root / "move-short-id-tree";
		var source = new ItemTreePath(
			imageTreeRoot / "Nomsa",
			"AIA",
			PathConventionType.ItemIdTree3x3);
		WriteArtifact(source, "AIA.png");
		WriteArtifact(source, "AIA.raid");

		var run = RunIorg(
			"move", "AIA", "AIA2", "--root", imageTreeRoot.FullPath,
			"--subscriber", "Nomsa", "--pathconv", "3", "--nologo");

		Assert.Equal(0, run.exitCode);
		var destination = new ItemTreePath(imageTreeRoot / "Nomsa", "AIA2");
		Assert.Equal(2, destination.SelectFiles().Count);
	}

	[Fact]
	public void OrganizeHelp_AlignsNumberedOptionDescriptions()
	{
		var help = RunIorg("organize", "--help", "--nologo");
		Assert.Equal(0, help.exitCode);

		var lines = help.output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
			.Select(line => line.TrimEnd('\r'))
			.ToArray();
		var cloud = Assert.Single(lines, line => line.StartsWith("-c|--cloud:", StringComparison.Ordinal));
		var pathConvention = Assert.Single(lines, line => line.StartsWith("-p|--pathconv:", StringComparison.Ordinal));
		var namingConvention = Assert.Single(lines, line => line.StartsWith("-n|--nameconv:", StringComparison.Ordinal));

		var firstProvider = Assert.Single(Messages.CloudProviderOptions().Take(1));
		var providerIcon = firstProvider.ToLowerInvariant() switch
		{
			"dropbox" => Icons.DropboxBoxOutline,
			"googledrive" => Icons.GoogleDriveBoxOutline,
			"iclouddrive" => Icons.ICloudDriveBoxOutline,
			"onedrive" => Icons.OneDriveBoxOutline,
			_ => throw new Xunit.Sdk.XunitException($"Unexpected configured cloud provider: {firstProvider}")
		};
		Assert.False(string.IsNullOrEmpty(providerIcon));
		Assert.Equal(15, cloud.IndexOf(providerIcon, StringComparison.Ordinal));
		Assert.Equal(15, pathConvention.IndexOf(Icons.NumberBoxOutlines[0], StringComparison.Ordinal));
		Assert.Equal(15, namingConvention.IndexOf(Icons.NumberBoxOutlines[0], StringComparison.Ordinal));
		Assert.DoesNotContain("①", help.output, StringComparison.Ordinal);
		Assert.EndsWith(Icons.HelpLineWidthCompensation, cloud, StringComparison.Ordinal);
		Assert.EndsWith(Icons.HelpLineWidthCompensation, pathConvention, StringComparison.Ordinal);
		Assert.EndsWith(Icons.HelpLineWidthCompensation, namingConvention, StringComparison.Ordinal);
	}

	[Fact]
	public void Help_IdentifiesDefaults_AndRejectsCloudOutsideConfiguredDefaultOrder()
	{
		var help = RunIorg("--help", "--nologo");
		Assert.Equal(0, help.exitCode);
		Assert.Contains("ItemIdTree8x2 (default)", help.output);
		Assert.Contains("Structured (default)", help.output);

		var configured = Messages.CloudProviderOptions();
		Assert.NotEmpty(configured);
		Assert.Contains($"{configured[0]} (default)", help.output);

		var invalid = RunIorg("--help", "--nologo", "-c", "NotConfiguredCloud");
		Assert.Equal(1, invalid.exitCode);
		Assert.Contains("not configured as a DefaultDrive on this machine", invalid.output);
	}

	[Fact]
	public void CloudChoices_PreserveDefaultOrder_AndExcludeUnconfiguredProviders()
	{
		var filtered = Messages.FilterConfiguredDefaultCloudProviders(
			["Dropbox", "OneDrive"],
			["OneDrive", "Dropbox", "GoogleDrive"]);

		Assert.Equal(["Dropbox", "OneDrive"], filtered);
	}

	[Fact]
	public void IorgCommand_LiveSmoke_InvokesRealCliVersionAndInstalledCommandName()
	{
		var run = RunIorg("--version");
		Assert.Equal(0, run.exitCode);
		Assert.Equal("iorg v4.2.9", run.output.Trim());
	}

	private static TextFile WriteImage(RaiPath directory, string name, string extension)
	{
		var file = new TextFile(directory, name, extension)
		{
			Lines = ["test-image-content"],
			Changed = true
		};
		file.Save();
		return file;
	}

	private static ImageTreeFile SeedTreeImage(RaiPath destination, string itemId, int imageNumber)
	{
		var file = new ImageTreeFile(
			destination, itemId, string.Empty, "jpg",
			PathConventionType.ItemIdTree8x2, ImageNamingConvention.Structured)
		{
			ImageNumber = imageNumber
		};
		file.mkdir();
		var payload = new TextFile(file.FullName)
		{
			Lines = ["test-image-content"],
			Changed = true
		};
		payload.Save();
		return file;
	}

	private static RaiFile WriteArtifact(ItemTreePath item, string name)
	{
		var file = new TextFile(item.SubdirRoot, name);
		file.DeleteAll().Append(name).Save();
		return new RaiFile(file.FullName);
	}

	private void Cleanup()
	{
		try
		{
			if (root.Exists())
				root.rmdir(depth: 10, deleteFiles: true);
		}
		catch { }
	}

	private static (int exitCode, string output) RunIorg(params string[] args)
	{
		var dll = new RaiFile(new RaiPath(AppContext.BaseDirectory), "ImgSeeder", "dll");
		Assert.True(dll.Exists(), $"Expected ImgSeeder.dll at {dll.FullName}");

		var result = IorgCommand.ForManagedAssembly(dll).Run(args);
		return (result.ExitCode, result.Output);
	}
}
