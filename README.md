# ImgSeeder

## Terminal font

> **Font note:** The `iorg` help screen uses glyph icons from Nerd Fonts. Most
> Nerd Font-patched fonts render correctly in most terminal environments. Blink
> on iPadOS showed clipping and character-width problems with some choices; the
> tested solution was Blink's
> [Jet Brains Mono Nerd Font stylesheet](https://github.com/blinksh/patched-fonts/blob/main/Jet%20Brains%20Mono%20Nerd%20Font.css).
> See the RAIkeep
> [terminal font guide](https://github.com/Burkhardt/RAIkeep/blob/main/doc/TERMINAL_FONTS.md)
> for Blink, macOS, and Ubuntu setup.

ImgSeeder change requests and release notes are centralized in the RAIkeep [`doc/`](https://github.com/Burkhardt/RAIkeep/tree/main/doc) directory under `ImgSeeder_...` filenames; they are not stored separately in this child repository.

The NuGet tool package includes the Burkhardt `HardCastle.png` package icon, matching the other RAIkeep packages.

ImgSeeder uses the shared RAIkeep configured cloud-root contract: `Dropbox`, `OneDrive`, `GoogleDrive`, and `ICloudDrive`.

`ImgSeeder` is the RAIkeep image organizer package. It installs the `iorg` CLI, which copies source images, normalizes filenames with RaiImage naming rules, and places the final files into an `ImageTreeFile` directory layout such as `ItemIdTree8x2`.

## 4.2.4

- Adopts RaiImage 4.2.4 so `iorg` destinations use accepted CR016 NFC normalization and Unicode text-element bucketing.
- Aligns fallback dependencies on `JsonPit 4.2.4`, `OsLibCore 4.2.4`, `RaiUtils 4.2.4`, and `RaiImage 4.2.4`.
- CLI behavior and the established Nerd Font help contract remain unchanged.
- Current release notes: [ImgSeeder_RELEASE_NOTES_4.2.4.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.2.4.md)

## 4.2.3

- Aligns fallback dependencies on `JsonPit 4.2.3`, `OsLibCore 4.2.3`, `RaiUtils 4.2.3`, and `RaiImage 4.2.3` for the coordinated CR015 release.
- Retains the `4.2.1` Nerd Font glyphs, corrected option alignment, Blink guidance, and terminal clipping tolerance unchanged.
- Current release notes: [ImgSeeder_RELEASE_NOTES_4.2.3.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.2.3.md)

## 4.2.1

- Uses glyphs embedded in `JetBrainsMonoNLNerdFontPropo-Regular` for cloud-provider and numbered help options, avoiding fallback-font width differences.
- Aligns contextual option descriptions consistently and reserves two terminal cells at the end of help lines for renderers such as Blink.
- Continues to depend on `JsonPit 4.2.0`, `OsLibCore 4.2.0`, `RaiUtils 4.2.0`, and `RaiImage 4.2.0`; no library package version changes are part of this CLI-only patch.
- Terminal setup guidance: [TERMINAL_FONTS.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/TERMINAL_FONTS.md)
- Current release notes: [ImgSeeder_RELEASE_NOTES_4.2.1.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.2.1.md)

## 4.2.0

- Retains the command-first `organize` and `clean` syntax introduced for CR006.
- Keeps established flat `iorg` invocations working throughout `4.x`; the legacy parser is scheduled for removal in `5.x.x`.
- Fallback package defaults are aligned on `JsonPit 4.2.0`, `OsLibCore 4.2.0`, `RaiUtils 4.2.0`, and `RaiImage 4.2.0`.
- No ImgSeeder CLI behavior changes from 4.1.0.
- Help is contextual per command and startup banners use decorative glyph rules instead of repeated equals signs.
- Release notes: [ImgSeeder_RELEASE_NOTES_4.2.0.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.2.0.md)

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
iorg organize -c OneDrive --root LiveAfricaStageImage/nomsa \
  --source /Users/Shared/ServerData/GDriveData/TestAfricaStage/Images/NOMSA.net/ \
  --pathconv 3 --nameconv 3
```

The command resolves `-c` through `Os.Config.Cloud`. When `--subscriber` is
omitted, `--root` is the complete subscriber destination and its final directory
name supplies the subscriber identity. Alternatively, provide a parent image root
and `--subscriber <name>` explicitly.

When `-c`/`--cloud` is omitted, `iorg` selects the first provider in the
configured `Os.Config.DefaultCloudOrder` that also has a non-empty `Cloud` path.
The configured order is preserved in help, and providers outside that filtered
list are rejected. An explicit absolute root (or `.`) remains local when no cloud
option was explicitly supplied.

To inspect the resolved values without copying files, add `-h`:

```bash
iorg organize --help
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

To inspect image deletion for an item without deleting files, use `clean` with a
required `ShortName`:

```bash
iorg clean NomsaConcert_11 -c OneDrive --root LiveAfricaStageImage/nomsa
```

`ShortName` can be either `ItemId` or `ItemId_Nr`. By default, `clean` matches all
images for that short name; `--cache` limits the operation to cached/rendered
variants such as files with a template/name extension:

```bash
iorg clean NomsaConcert_11 -c OneDrive --root LiveAfricaStageImage/nomsa --cache
```

Delete commands are dry-run by default and list what would be deleted. Add `--force` to actually delete the matched files:

```bash
iorg clean NomsaConcert_11 -c OneDrive --root LiveAfricaStageImage/nomsa --cache --force
```

An unbounded clean is not supported: omitting `ShortName` is a validation error,
including when `--force` is present.

Useful options:

- `-h`, `--help`: print help
- `-v`, `--version`: print version
- `-l`, `--nologo`: hide banner
- `-d`, `--debug`: enable debug output
- `-c`, `--cloud`: configured provider from `Os.Config.DefaultCloudOrder`; defaults to its first available entry
- `-r`, `--root`: destination root; complete subscriber destination unless `--subscriber` is supplied
- `--subscriber`: explicit subscriber identity when `--root` is the parent image root
- `--source`: source image directory for `organize`
- `--cache`: restrict `clean` to cached/rendered images
- `--force`: perform the otherwise dry-run clean
- `-p`, `--pathconv`: `1` CanonicalByName, `2` ItemIdTree3x3, or `3` ItemIdTree8x2 (default)
- `-n`, `--nameconv`: `1` Legacy, `2` ItemTemplate, or `3` Structured (default)

Run `iorg <command> --help` for contextual options.

## 4.x legacy transition

The existing flat `-s`, `-rm`, `-rmc`/`--rm-cache`, positional subscriber,
`-p`, and `-n` forms remain supported throughout `4.x` and invoke the same
handlers as command syntax. New scripts should use subcommands. The `5.x.x` line
will require `organize` or `clean`. The root option is not deprecated:
`-r` and `--root` are both supported by the new command parser. The `-p` and
`-n` convention aliases likewise remain available, scoped to `organize`.

## Standalone Binaries

Tagged releases also publish self-contained `iorg` workflow artifacts for:

- `linux-x64`
- `osx-arm64`
- `osx-x64`
- `win-x64`

These binaries can be deployed without a separate .NET runtime installation.

## Validation

- `dotnet test ImgSeeder.slnx --nologo -v minimal`
