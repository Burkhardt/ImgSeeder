using System.Diagnostics;
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
	public void CleanCommand_IsDryRunByDefault_AndForceDeletesOnlyNamedTarget()
	{
		var destination = root / "clean-dest" / "nomsa";
		var target = SeedTreeImage(destination, "AfricanPicnic", imageNumber: 1);
		var other = SeedTreeImage(destination, "AfricanPicnic", imageNumber: 2);

		var dryRun = RunIorg("clean", "AfricanPicnic_01", "-r", destination.FullPath, "--nologo");
		Assert.Equal(0, dryRun.exitCode);
		Assert.Contains("would delete", dryRun.output, StringComparison.OrdinalIgnoreCase);
		Assert.True(target.Exists());

		var force = RunIorg("clean", "AfricanPicnic_01", "--root", destination.FullPath, "--force", "--nologo");
		Assert.Equal(0, force.exitCode);
		Assert.False(target.Exists());
		Assert.True(other.Exists());
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

		var rootHelp = RunIorg("--help");
		Assert.Equal(0, rootHelp.exitCode);
		Assert.DoesNotContain("===", rootHelp.output, StringComparison.Ordinal);
		Assert.Contains("organize, clean", rootHelp.output, StringComparison.OrdinalIgnoreCase);
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
	public void Version_PrintsPreparedPackageVersionAndInstalledCommandName()
	{
		var run = RunIorg("--version");
		Assert.Equal(0, run.exitCode);
		Assert.Equal("iorg v4.2.0", run.output.Trim());
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

		var startInfo = new ProcessStartInfo("dotnet")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false
		};
		startInfo.ArgumentList.Add(dll.FullName);
		foreach (var arg in args)
			startInfo.ArgumentList.Add(arg);

		using var process = Process.Start(startInfo)!;
		var stdout = process.StandardOutput.ReadToEnd();
		var stderr = process.StandardError.ReadToEnd();
		process.WaitForExit();
		return (process.ExitCode, stdout + stderr);
	}
}
