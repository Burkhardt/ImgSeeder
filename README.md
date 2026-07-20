# ImgSeeder

`ImgSeeder` is the RAIkeep image organizer package. It installs the `iorg` CLI, which copies source images, normalizes filenames with RaiImage naming rules, and places the final files into an `ImageTreeFile` directory layout such as `ItemIdTree8x2`.

## 3.12.1

- Coordinated patch release: carries forward `-rmc` as the short alias for cache deletion while keeping `--rm-cache`.
- Fallback package defaults stay aligned on `JsonPit 3.12.1`, `OsLibCore 3.12.1`, `RaiUtils 3.12.1`, and `RaiImage 3.12.1`.
- Help output continues to use fixed option-column formatting so glyph icons align consistently.
- Current release notes: [RELEASE_NOTES_3.12.1.md](RELEASE_NOTES_3.12.1.md)

This tool is part of the RAIkeep package family:

- `OsLibCore`
- `RaiUtils`
- `RaiImage`
- `JsonPit`
- `ImgSeeder` (`iorg` command)
- `PitSeeder`

## Install

Install the NuGet tool with:

```bash
dotnet tool install --global ImgSeeder
```

On macOS or Linux, a practical option is to install directly into a directory on your `PATH`:

```bash
sudo dotnet tool install ImgSeeder --tool-path /usr/local/bin
```

Update an existing installation with:

```bash
dotnet tool update --global ImgSeeder
```

To update an installation in `/usr/local/bin`:

```bash
sudo dotnet tool update ImgSeeder --tool-path /usr/local/bin
```

## Usage

Typical cloud-rooted usage:

```bash
iorg -c OneDrive -r LiveAfricaStageImage nomsa -s /Users/Shared/ServerData/GDriveData/TestAfricaStage/Images/NOMSA.net/
```

The command resolves `-c` through `Os.Config.Cloud`, appends `-r` and the subscriber name, then copies each supported source image into the destination image tree.

To inspect the resolved values without copying files, add `-h`:

```bash
iorg -h -c OneDrive -r LiveAfricaStageImage nomsa -s /Users/Shared/ServerData/GDriveData/TestAfricaStage/Images/NOMSA.net/
```

The help screen shows the resolved source, destination `ImageRoot`, subscriber, supported image extensions, detected source image count, and option selections. With `-d`, it also prints debug diagnostics such as `CanRun`, `RunBlocker`, source/target existence checks, and resolved full paths. Remove `-h` to execute the copies.

Without `-d`, each copied image is printed as a compact file name:

```text
nomsa-concert-11.jpg
SD-State-Sony-149.jpg
```

With `-d`, each copied image is printed with full destination and source paths:

```text
/dest/nomsa/NomsaCon/NomsaConce/NomsaConcert_11.jpg  /source/nomsa-concert-11.jpg
```

The final summary reports how many detected source images were copied and groups any files that were not copied by failure reason.

To inspect image deletion for an item without deleting files, use `-rm` with a `ShortName`:

```bash
iorg -rm NomsaConcert_11 -c OneDrive -r LiveAfricaStageImage nomsa
```

`ShortName` can be either `ItemId` or `ItemId_Nr`. `-rm` matches all images for that short name, while `-rmc` (or `--rm-cache`) matches only cached/rendered variants such as files with a template/name extension:

```bash
iorg -rmc NomsaConcert_11 -c OneDrive -r LiveAfricaStageImage nomsa
```

Delete commands are dry-run by default and list what would be deleted. Add `--force` to actually delete the matched files:

```bash
iorg -rmc NomsaConcert_11 -c OneDrive -r LiveAfricaStageImage nomsa --force
```

Useful options:

- `-h`, `--help`: print help
- `-v`, `--version`: print version
- `-l`, `--nologo`: hide banner
- `-d`, `--debug`: enable debug output
- `-c`, `--cloud`: cloud provider name from `Os.Config.Cloud`
- `-r`, `--root`: destination image root under the cloud root
- `-s`, `--source`: source image directory
- `-rm`: list all images that would be deleted for `ShortName`; add `--force` to delete
- `-rmc`, `--rm-cache`: list cached images that would be deleted for `ShortName`; add `--force` to delete
- `--force`: perform delete actions for `-rm` or `-rmc/--rm-cache`
- `-p`, `--pathconv`: `CanonicalByName`, `ItemIdTree3x3`, or `ItemIdTree8x2`
- `-n`, `--nameconv`: `Legacy`, `ItemTemplate`, or `Structured`

## Standalone Binaries

Tagged releases also publish self-contained `iorg` workflow artifacts for:

- `linux-x64`
- `osx-arm64`
- `osx-x64`
- `win-x64`

These binaries can be deployed without a separate .NET runtime installation.

## Validation

- `dotnet test ImgSeeder.slnx --nologo -v minimal`
