using ImgSeeder;
using OsLib;
using RaiImage;
using Xunit;

namespace Iorg.Tests;

public class SourceImageStructuredTreeTests
{
	private const string Subscriber = "nomsa";
	private static readonly RaiRelPath SourceRelPath = new("TestAfricaStage/Images/NOMSA.net/");

	public static TheoryData<string, string, int, string, string> MixedSourceNames => new()
	{
		{ "nomsa-concert-11.jpg", "NomsaConcert", 11, "NomsaCon", "NomsaConce" },
		{ "SD-State-Sony-149.jpg", "SDStateSony", 149, "SDStateS", "SDStateSon" },
		{ "San-Diego-State-09.24-232.jpg", "SanDiegoState0924", 232, "SanDiego", "SanDiegoSt" },
		{ "SD-State-Fuji-unedited-87.jpg", "SDStateFujiUnedited", 87, "SDStateF", "SDStateFuj" },
	};

	[Theory]
	[MemberData(nameof(MixedSourceNames))]
	public void Organize_WritesStructuredNameDirectlyInto8x2TreeWithoutTempStaging(
		string sourceFileName,
		string expectedItemId,
		int expectedImageNumber,
		string expectedTopdir,
		string expectedSubdir)
	{
		var sourceRoot = GoogleDriveSourceRoot();
		var sourceFile = new RaiFile(sourceRoot, sourceFileName);
		Assert.True(sourceFile.Exists(), $"Missing test image: {sourceFile.FullName}");

		var testRoot = NewTestRoot();
		try
		{
			var testSourceRoot = testRoot / "source";
			var testSourceFile = new RaiFile(testSourceRoot, sourceFile.NameWithExtension);
			testSourceFile.mkdir();
			testSourceFile.cp(sourceFile);

			var subscriberRoot = testRoot / "dest" / Subscriber;
			var tempRoot = testRoot / "temp";
			using var output = new StringWriter();

			var count = ImageOrganizer.Organize(
				testSourceRoot,
				subscriberRoot,
				Subscriber,
				PathConventionType.ItemIdTree8x2,
				ImageNamingConvention.Structured,
				tempRoot,
				output);

			var expected = new ImageTreeFile(
				subscriberRoot,
				expectedItemId,
				string.Empty,
				sourceFile.Ext,
				PathConventionType.ItemIdTree8x2,
				ImageNamingConvention.Structured)
			{
				ImageNumber = expectedImageNumber
			};

			Assert.Equal(1, count);
			Assert.True(sourceFile.Exists(), $"Original source should remain untouched: {sourceFile.FullName}");
			Assert.True(testSourceFile.Exists(), $"Copied source should remain untouched: {testSourceFile.FullName}");
			Assert.True(expected.Exists(), $"Expected destination file: {expected.FullName}");
			Assert.Equal(expectedTopdir, Assert.Single(expected.Topdir.Segments));
			Assert.Equal(expectedSubdir, Assert.Single(expected.Subdir.Segments));
			Assert.Equal(1, ImageOrganizer.CountSourceImages(testSourceRoot));
			Assert.False(tempRoot.Exists());
			Assert.Equal($"{testSourceFile.NameWithExtension}{Environment.NewLine}", output.ToString());
		}
		finally
		{
			Cleanup(testRoot);
		}
	}

	[Fact]
	public void Organize_DebugOutputWritesDestinationAndSourceFullPaths()
	{
		var sourceRoot = GoogleDriveSourceRoot();
		var sourceFile = new RaiFile(sourceRoot, "nomsa-concert-11.jpg");
		Assert.True(sourceFile.Exists(), $"Missing test image: {sourceFile.FullName}");

		var testRoot = NewTestRoot();
		try
		{
			var testSourceRoot = testRoot / "source";
			var testSourceFile = new RaiFile(testSourceRoot, sourceFile.NameWithExtension);
			testSourceFile.mkdir();
			testSourceFile.cp(sourceFile);

			var subscriberRoot = testRoot / "dest" / Subscriber;
			var tempRoot = testRoot / "temp";
			using var output = new StringWriter();

			ImageOrganizer.Organize(
				testSourceRoot,
				subscriberRoot,
				Subscriber,
				PathConventionType.ItemIdTree8x2,
				ImageNamingConvention.Structured,
				tempRoot,
				output,
				debug: true);

			var expected = new ImageTreeFile(
				subscriberRoot,
				"NomsaConcert",
				string.Empty,
				sourceFile.Ext,
				PathConventionType.ItemIdTree8x2,
				ImageNamingConvention.Structured)
			{
				ImageNumber = 11
			};

			Assert.Equal($"{expected.FullName} {Icons.ArrowLeft} {testSourceFile.FullName}{Environment.NewLine}", output.ToString());
			Assert.False(tempRoot.Exists());
		}
		finally
		{
			Cleanup(testRoot);
		}
	}

	[Fact]
	public void Organize_ConfiguredCloudDestination_WritesFinalFileDirectlyAndNeverCreatesStagingRoot()
	{
		var testRoot = NewTestRoot();
		var cloudRoot = NewConfiguredCloudRoot();
		try
		{
			var sourceRoot = (testRoot / "source").mkdir();
			var source = new TextFile(sourceRoot, "African-Brisket-01", "png", "image bytes");
			var stagingRoot = testRoot / "forbidden-staging";
			using var output = new StringWriter();

			var count = ImageOrganizer.Organize(
				sourceRoot,
				cloudRoot,
				Subscriber,
				PathConventionType.ItemIdTree8x2,
				ImageNamingConvention.Structured,
				stagingRoot,
				output);

			var destination = new ImageTreeFile(
				cloudRoot,
				"AfricanBrisket",
				string.Empty,
				"png",
				PathConventionType.ItemIdTree8x2,
				ImageNamingConvention.Structured)
			{
				ImageNumber = 1
			};

			Assert.Equal(1, count);
			Assert.True(source.Exists());
			Assert.True(destination.Exists());
			Assert.Equal("image bytes", new TextFile(destination.FullName).ReadAllText().Trim());
			Assert.False(stagingRoot.Exists());
		}
		finally
		{
			Cleanup(testRoot);
			Cleanup(cloudRoot);
		}
	}

	[Fact]
	public void DeleteByShortName_DryRunListsMatchesWithoutDeleting()
	{
		var testRoot = NewTestRoot();
		try
		{
			var subscriberRoot = testRoot / "dest" / Subscriber;
			var source = SeedImage(subscriberRoot, "AfricanPicnic", string.Empty, "png", imageNumber: 1);
			var cache = SeedImage(subscriberRoot, "AfricanPicnic", "Small", "webp", imageNumber: 1);
			using var output = new StringWriter();

			var report = ImageOrganizer.DeleteByShortName(
				subscriberRoot,
				"AfricanPicnic_01",
				cacheOnly: false,
				force: false,
				PathConventionType.ItemIdTree8x2,
				ImageNamingConvention.Structured,
				output);

			Assert.Equal(2, report.MatchedCount);
			Assert.Equal(2, report.DeletedCount);
			Assert.True(source.Exists());
			Assert.True(cache.Exists());
			Assert.Contains($"would delete: {source.NameWithExtension}", output.ToString());
			Assert.Contains($"would delete: {cache.NameWithExtension}", output.ToString());
		}
		finally
		{
			Cleanup(testRoot);
		}
	}

	[Fact]
	public void DeleteByShortName_ForceDeletesMatches()
	{
		var testRoot = NewTestRoot();
		try
		{
			var subscriberRoot = testRoot / "dest" / Subscriber;
			var source = SeedImage(subscriberRoot, "AfricanPicnic", string.Empty, "png", imageNumber: 1);
			var other = SeedImage(subscriberRoot, "AfricanPicnic", string.Empty, "png", imageNumber: 2);
			using var output = new StringWriter();

			var report = ImageOrganizer.DeleteByShortName(
				subscriberRoot,
				"AfricanPicnic_01",
				cacheOnly: false,
				force: true,
				PathConventionType.ItemIdTree8x2,
				ImageNamingConvention.Structured,
				output);

			Assert.Equal(1, report.MatchedCount);
			Assert.Equal(1, report.DeletedCount);
			Assert.False(source.Exists());
			Assert.True(other.Exists());
			Assert.Contains($"deleted: {source.NameWithExtension}", output.ToString());
		}
		finally
		{
			Cleanup(testRoot);
		}
	}

	[Fact]
	public void DeleteByShortName_CacheOnlyKeepsSourceImages()
	{
		var testRoot = NewTestRoot();
		try
		{
			var subscriberRoot = testRoot / "dest" / Subscriber;
			var source = SeedImage(subscriberRoot, "GageElementary", string.Empty, "png");
			var cache = SeedImage(subscriberRoot, "GageElementary", "Small", "webp");
			using var output = new StringWriter();

			var report = ImageOrganizer.DeleteByShortName(
				subscriberRoot,
				"GageElementary",
				cacheOnly: true,
				force: true,
				PathConventionType.ItemIdTree8x2,
				ImageNamingConvention.Structured,
				output);

			Assert.Equal(1, report.MatchedCount);
			Assert.Equal(1, report.DeletedCount);
			Assert.True(source.Exists());
			Assert.False(cache.Exists());
			Assert.Contains($"deleted cached: {cache.NameWithExtension}", output.ToString());
		}
		finally
		{
			Cleanup(testRoot);
		}
	}

	private static RaiPath GoogleDriveSourceRoot()
	{
		var cloudDir = Os.Config?.Cloud?["GoogleDrive"];
		Assert.False(string.IsNullOrWhiteSpace((string?)cloudDir),
			"This test requires Os.Config.Cloud.GoogleDrive to be configured.");

		var root = new RaiPath((string)cloudDir) / SourceRelPath;
		Assert.True(root.Exists(), $"Missing GoogleDrive test image root: {root.FullPath}");
		return root;
	}

	private static RaiPath NewTestRoot()
	{
		var root = Os.TempDir / "RAIkeep" / "iorg-tests" / Guid.NewGuid().ToString("N");
		Cleanup(root);
		root.mkdir();
		return root;
	}

	private static RaiPath NewConfiguredCloudRoot()
	{
		foreach (var provider in new[] { "OneDrive", "Dropbox", "GoogleDrive", "ICloudDrive" })
		{
			string? configured = Os.Config?.Cloud?[provider];
			if (string.IsNullOrWhiteSpace(configured)) continue;
			var providerRoot = new RaiPath(configured);
			if (!providerRoot.Exists()) continue;
			return (providerRoot / "RAIkeep" / "iorg-tests" / "cr022" / Guid.NewGuid().ToString("N")).mkdir();
		}

		Assert.Skip("No configured CloudDrive is available for the CR022 ImgSeeder direct-write test.");
		throw new InvalidOperationException("Assert.Skip did not terminate the test.");
	}

	private static ImageTreeFile SeedImage(RaiPath subscriberRoot, string itemId, string nameExt, string ext, int imageNumber = ImageFile.NoImageNumber)
	{
		var file = new ImageTreeFile(
			subscriberRoot,
			itemId,
			nameExt,
			ext,
			PathConventionType.ItemIdTree8x2,
			ImageNamingConvention.Structured)
		{
			ImageNumber = imageNumber
		};
		file.mkdir();
		File.WriteAllText(file.FullName, file.NameWithExtension);
		return file;
	}

	private static void Cleanup(RaiPath root)
	{
		if (root.Exists())
			root.rmdir(depth: 8, deleteFiles: true);
	}
}
