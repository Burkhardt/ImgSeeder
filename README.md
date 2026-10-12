# ImgSeeder

## Latest Version 4.5.8

Image organization, cloud-safe ZIP import, and compact import receipts.

Latest release notes: [ImgSeeder_RELEASE_NOTES_4.5.8.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.5.8.md).

<details>
<summary><b>Previous Versions</b></summary>

Earlier coordinated releases are recorded in the [RAIkeep release-note archive](https://github.com/Burkhardt/RAIkeep/tree/main/doc).

</details>

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

ImgSeeder organizes images and related ItemTree artifacts across local or
configured cloud-backed roots while preserving RAIkeep's cloud-safe in-place
filesystem contract.

**CLI tools:** use `iorg` to organize, list, move, and clean ImageTree artifact
families. Use [`amafu init`](https://github.com/Burkhardt/Amafu) to detect cloud
drives and create the shared RAIkeep configuration before using cloud-backed
addressing.

The NuGet tool package includes the Burkhardt `HardCastle.png` package icon, matching the other RAIkeep packages.

ImgSeeder uses the shared RAIkeep configured cloud-root contract: `Dropbox`, `OneDrive`, `GoogleDrive`, and `ICloudDrive`.

`ImgSeeder` is the RAIkeep image organizer package. It installs the `iorg` CLI, which copies source images, normalizes filenames with RaiImage naming rules, and places the final files into an `ImageTreeFile` directory layout such as `ItemIdTree8x2`.

Complete command and safety guidance: [`IORG-OPERATIONS.md`](https://github.com/Burkhardt/RAIkeep/blob/main/doc/IORG-OPERATIONS.md). Iorg has no JsonPit-style audit/event-log feature; use `iorg list` and the default dry-run form of `iorg clean <ItemId>` for read-only inspection.

## ZIP import and compact receipts (CR049 / CR054)

`iorg` inspects ZIP subdirectories for supported images, preserves the archive,
and copies directly from its temporary extraction workspace into the ImageTree.
This example imports a batch named `Customer-Order-Sheet-26-1.jpg` through
`Customer-Order-Sheet-26-193.jpg`:

```bash
iorg organize --source ~/Downloads/orders.zip \
  --root /srv/images --tenant Customer --pathconv 3 --nameconv 3 \
  --import-id Import_OrderPhotos --activity-id OrderPhotos \
  --json > import-receipt.json
import_exit=$?

jq '{Id, ReceiptVersion, BaseItemId, Range, Ranges, Summary, Exceptions, Error}' import-receipt.json

# Buffer first: a failed import must not automatically seed a receipt downstream.
if [ "$import_exit" -eq 0 ] &&
   jq -e '.Class == "ImageImport" and .Summary.Failed == 0 and .Error == null' import-receipt.json >/dev/null; then
  pits seed Object --source - -r /srv/pits/Customer < import-receipt.json
fi
```

Python consumers can use `jpit seed Object - -r /srv/pits/Customer` or
`jpit put Object - -r /srv/pits/Customer` after the same success check.
Python package delivery and validation are coordinated independently.
A direct HTTPS ZIP endpoint can replace `--source` with
`--source-url 'https://example.org/orders.zip'`; provider login/share pages are
not ZIP downloads. Source URL credentials and query strings are not persisted.

### How names and paths are derived

`ImageFile.EasyFileName` owns normalization. A terminal run of hyphen- or
underscore-separated numeric tokens is removed from the descriptive stem; the
last token supplies `ImageNumber`. Separated uppercase words become PascalCase.

```text
Source:         Customer-Order-Sheet-26-10.jpg
BaseItemId:     CustomerOrderSheet
ImageNumber:    10
Target:         Customer/CustomerOr/CustomerOrderSheet_010.jpg
```

`Order_SHEET_001.jpg` becomes `OrderSheet_001.jpg`. Underscores do not remain in
the normalized base stem. Earlier digits within descriptive words are preserved.
ZIP folder names do not determine destination folders. A normalization collision
rejects the batch before copying; different existing bytes are never overwritten.

**4.5.5 path compatibility:** Structured output now uses at least three digits
(`_001`, `_010`, `_193`); Legacy retains two. Existing two-digit source names still
parse, but recomposition uses the new Structured filename. Existing stored files
are not renamed automatically. Migrate existing Structured paths and their
references together before replaying imports into an older image tree; otherwise
an old `_01` and new `_001` can coexist. D3 is a minimum width: numbers above 999
are not truncated. Numbered diagram artifacts also use D3 consistently across `.raid`, `.puml`,
and SVG siblings; migrate existing numbered diagram paths together.

### ReceiptVersion 2

A clean homogeneous sequence produces one compact descriptor, not a per-file list:

```json
{"Id":"Import_OrderPhotos","Class":"ImageImport","ReceiptVersion":2,"BaseItemId":"CustomerOrderSheet","PathConvention":"ItemIdTree8x2","NamingConvention":"Structured","Range":{"Start":1,"End":193,"Count":193,"Ext":"jpg"},"ActivityId":"OrderPhotos","Tenant":"Customer","Source":{"Kind":"Zip","Name":"orders.zip"},"Summary":{"Copied":193,"Unchanged":0,"Skipped":0,"Failed":0}}
```

The clean 193-image regression receipt is below 500 UTF-8 bytes before Pit history
metadata. Receipt size depends on identifiers, source names, and exceptions;
arbitrary mixed archives cannot have a universal fixed byte bound.

Mixed stems, extensions, or gaps produce deterministic contiguous `Ranges`.
Each range carries `BaseItemId`, `PathConvention`, `NamingConvention`, `Start`,
`End`, `Count`, and `Ext`. A single remaining range uses the top-level form.
Runs of only one image are represented in `Exceptions` with their actual copy
status, just like skipped files and failures; a singleton is not itself a failed
import. The 500-image/two-range regression receipt is below 1 KB.

```bash
# One row per range, for either form; this does not invent filename rules.
jq -r '(.Ranges // [(.Range + {BaseItemId, PathConvention, NamingConvention})])[] |
  select(.Start != null) | [.BaseItemId, .Start, .End, .Count, .Ext] | @tsv' import-receipt.json
jq '.Exceptions[]? | {SourceEntry, ItemId, ImageNumber, RelativePath, Status, Error, Reason}' import-receipt.json
```

Consumers reconstruct paths using the existing classes and enums:

```csharp
var image = new ImageTreeFile(tenantRoot, "CustomerOrderSheet", "", "jpg",
    PathConventionType.ItemIdTree8x2, ImageNamingConvention.Structured)
{
    ImageNumber = 10
};
Console.WriteLine(image.FullName);
```

Use the receipt's convention values, not a custom padding or path pattern.
Only successfully copied or unchanged images belong to ranges. Failed copies and
skipped entries do not inflate a successful range. The receipt links to an
activity with `ActivityId` and contains no top-level lifecycle `Status`.
Activity lifecycle state remains in its own Pit. The library retains its
operational result in memory for CLI exit handling.

`--json` writes one JSON entity to stdout and diagnostics to stderr. Successful
imports exit 0; preflight, copying, or cleanup failures exit 1. A runtime failure
may leave successful copies; inspect `Summary`, `Exceptions`, and `Error`.
An early argument error may produce stderr only. A local copy does not certify
cloud synchronization. Keep receipts for retry if subsequent Pit ingestion fails.

### Native extraction and optional limits

On macOS and Linux (including Ubuntu), ZIP contents are extracted by an awaited
`unzip -q -n <archive> -d <temporary-directory>` child process through
`OsLib.UnzipCommand` and `RaiSystem.ExecAsync`. macOS supplies `/usr/bin/unzip`;
Ubuntu installations need the `unzip` package. The options used are common to
both Info-ZIP versions. Windows uses the built-in .NET ZIP reader.

The import API dispatches archive inspection and processing to a worker, and
awaits extraction. UI callers should await `ImageImport.RunAsync` or
`IorgCommand.OrganizeAsync`; synchronous `.Wait()`/`.Result` calls still block
their calling thread. Cancellation terminates the native extraction process.

Extraction needs temporary disk space for the expanded archive, plus the
downloaded ZIP for a URL source. A missing executable, insufficient disk space,
corrupt archive, or other extraction error fails the import before final image
copies. The temporary workspace is removed on success or failure. Final files
are streamed directly into their ImageTree destinations; they are never moved
from temporary storage into a CloudDrive.

There is no application-imposed size, entry-count, or download timeout by
default. Operators can opt into positive limits:

```bash
iorg organize --source photos.zip --root /srv/images --tenant Customer \
  --import-id ImportPhotos001 --json \
  --max-archive-bytes 21474836480 \
  --max-expanded-bytes 107374182400 \
  --max-entries 100000 --download-timeout-seconds 3600
```

Increase these values or omit the corresponding option to remove the limit.
Archive size, declared expanded size, and entry count are checked before
extraction; extracted file sizes are verified afterward. These are input
validation bounds, not operating-system disk reservations. Source/download and
final-copy I/O is streamed rather than buffering the complete archive in RAM.

### Structured EXIF output

Use `--exif '*'` for all EXIF properties, `--exif DateTimeOriginal` for one,
or `--exif DateTime,DateTimeOriginal` for several. Quote wildcards to prevent
shell expansion. Dotted selectors such as `--exif 'Thumbnail.*'` work too.
ImageMagick 7 (`magick`) must be available. Selected unknown/vendor fields remain
available under their EXIF names.

```bash
# Inspect existing ImageTree photos; read-only, with JSON on stdout.
iorg list 'CustomerSanDiegoState*' --root /srv/images --tenant Customer \
  --exif 'DateTimeOriginal,DateTimeDigitized,Thumbnail.*,Exposure*' --json \
  | jq '.[] | {FileName, Exif}'

# Include structured metadata in each successful import receipt row.
iorg organize --source photos.zip --root /srv/images --tenant Customer \
  --import-id ImportPhotos001 --exif '*' --json > import-receipt.json
jq '.Files[] | {RelativePath, Exif}' import-receipt.json
```

`--exif` explicitly opts into the detailed `Files` array alongside `Range`/`Ranges` and `Summary`. Without `--exif`, `Files` is omitted.

`--exif` implies JSON output. On `organize`, it therefore requires `--import-id`.
On `list`, `--quiet` cannot be combined with `--exif`; non-image artifacts are
omitted. Other verbs reject `--exif` as an unsupported option.

Dates become C# `DateTimeOffset` values and serialize as ISO 8601 strings:

| EXIF date / output property | Required companion |
| --- | --- |
| `DateTimeOriginal` | `OffsetTimeOriginal` |
| `DateTimeDigitized` | `OffsetTimeDigitized` |
| `DateTime` | `OffsetTime` |

The reader fetches the matching offset even when the selector only names the
date. It never borrows another date's offset or a computer/file/download time.
There is no synthesized `Captured` alias. A missing or malformed companion
leaves the original fields in `Unconverted`, omits the typed date, and produces
a stderr diagnostic. `DateTime` is embedded modification metadata, distinct
from filesystem timestamps. An offset defines an instant, not a geographic
zone such as America/Los_Angeles.

Related fields are grouped into `Thumbnail`, `Lens`, `FocalPlane`, and
`Exposure`. Recognized numeric codes become integers; rational values preserve
`Numerator` and `Denominator` and include a numeric `Value`. For example:

```json
{
  "DateTimeOriginal": "2026-09-28T17:40:31-07:00",
  "Thumbnail": {"Compression": 6, "XResolution": {"Numerator": 72, "Denominator": 1, "Value": 72}},
  "Lens": {"Model": "E 70-180mm F2.8 A056"},
  "Exposure": {"Time": {"Numerator": 1, "Denominator": 250, "Value": 0.004}}
}
```

```csharp
ExifMetadata metadata = await ImageExif.ReadMetadataAsync(imageFile,
    "DateTimeOriginal,DateTimeDigitized,Exposure*");
DateTimeOffset? original = metadata.DateTimeOriginal;
DateTime? originalUtc = original?.UtcDateTime;
```

The lower-level `ImageExif.ReadAsync` remains available for callers needing the
original string dictionary. See [ImageMagick properties](https://imagemagick.org/escape/)
and the [EXIF definitions](https://www.cipa.jp/std/documents/e/DC-X008-Translation-2019-E.pdf).


## Installation

### 1. User Global Tool Install

```bash
dotnet tool install --global ImgSeeder
```

To update:

```bash
dotnet tool update --global ImgSeeder
```

*(Requires `~/.dotnet/tools` in your `$PATH`)*

### 2. Direct Install to `/usr/local/bin` (Recommended for macOS & Linux)

Install directly to `/usr/local/bin` so no `$PATH` modification is needed:

```bash
sudo dotnet tool install ImgSeeder --tool-path /usr/local/bin
```

To update:

```bash
sudo dotnet tool update ImgSeeder --tool-path /usr/local/bin
```

> **Note:** `sudo` is only required during installation and updates to write to `/usr/local/bin`. Running the installed CLI command never requires `sudo` or elevated privileges.

## Usage

Typical cloud-rooted usage:

```bash
iorg organize -c OneDrive --app AIA --tenant customer \
  --source /Users/Shared/ServerData/GDriveData/TestCustomer/Images/NOMSA.net/ \
  --pathconv 3 --nameconv 3
```

The command resolves `-c` through `Os.Config.Cloud`. `--app AIA --tenant customer`
resolves `<configured cloud>/AIA/Image/customer`. Use `-r, --root` instead when the
argument is already the exact ImageTree root; an explicit `-t, --tenant` is
appended, or, when omitted, the final root segment remains the inferred tenant.
`--subscriber` remains a compatibility alias for `--tenant`.

When `-c`/`--cloud` is omitted, `iorg` selects the first provider in the
configured `Os.Config.DefaultCloudOrder` that also has a non-empty `Cloud` path.
The configured order is preserved in help, and providers outside that filtered
list are rejected. An explicit absolute root (or `.`) remains local when no cloud
option was explicitly supplied.

If cloud-backed addressing is requested before the shared configuration exists,
`iorg` reports:

```text
RAIkeep configuration was not found at '~/.config/RAIkeep.json5'. Run 'amafu init' to detect cloud providers and create it.
```

To inspect the resolved values without copying files, add `-h`:

```bash
iorg organize --help
```

The help screen shows the resolved source, destination `ImageRoot`, subscriber, supported image extensions, detected source image count, and option selections. With `-d`, it also prints debug diagnostics such as `CanRun`, `RunBlocker`, source/target existence checks, and resolved full paths. Remove `-h` to execute the copies.

Without `-d`, each copied image is printed as a compact file name:

```text
customer-concert-11.jpg
SD-State-Sony-149.jpg
```

With `-d`, each copied image is printed with full destination and source paths:

```text
/dest/customer/CustomerCon/CustomerConce/CustomerConcert_11.jpg  /source/customer-concert-11.jpg
```

The final summary reports how many detected source images were copied and groups any files that were not copied by failure reason.

To inspect every file owned by an exact ItemId without deleting it, use `clean`:

```bash
iorg clean CustomerConcert -c OneDrive --root LiveCustomerImage/customer
```

This selects the complete exact-ItemId family, including numbered sources,
rendered derivatives, `.puml`, `_config.puml`, and `.raid` files. It does not
select a bucket-sharing ItemId with a similar prefix. Item cleanup is a dry run
unless `--force` is supplied:

```bash
iorg clean CustomerConcert -c OneDrive --root LiveCustomerImage/customer --force
```

To explicitly purge rendered derivatives throughout one subscriber tree while
preserving source images and diagram artifacts, use the separate cache form:

```bash
iorg clean --cache -c OneDrive --root LiveCustomerImage/customer
```

`--cache` is itself the explicit bounded operation and does not take an ItemId.

Discover files without mutation:

```bash
iorg list 'WorkInPro*' -c OneDrive --app AIA --tenant Customer
iorg list '*.puml' -c OneDrive --root LiveCustomerImage --tenant Customer
```

Move an exact ItemId family, optionally renaming it and selecting its destination
path convention:

```bash
iorg move AfricanBrisket -c OneDrive --app AIA --tenant Customer --pathconv 3
iorg move AfricanBrisket AfricanDinner -c OneDrive --root LiveCustomerImage --tenant Customer --pathconv 4
```

Useful options:

- `-h`, `--help`: print help
- `-v`, `--version`: print version
- `-l`, `--nologo`: hide banner
- `-d`, `--debug`: enable debug output
- `-c`, `--cloud`: configured provider from `Os.Config.DefaultCloudOrder`; defaults to its first available entry
- `-r`, `--root`: exact ImageTree root; alternative to `--app`
- `-a`, `--app`: application root; iorg appends `Image`; requires a tenant
- `-t`, `--tenant`: explicit tenant/subscriber below the ImageTree root
- `--subscriber`: compatibility alias for `--tenant`
- `--source`: source image directory for `organize`
- `--cache`: explicitly delete rendered derivatives while preserving source and diagram files
- `--force`: perform the otherwise dry-run exact-ItemId clean
- `-p`, `--pathconv`: `1` CanonicalByName, `2` ItemIdTree3x3, `3` ItemIdTree8x2 (default), or `4` Flat
- `-n`, `--nameconv`: `1` Legacy, `2` ItemTemplate, or `3` Structured (default)

Run `iorg <command> --help` for contextual options.

## 4.x legacy transition

The existing flat `-s`, `-rm`, `-rmc`/`--rm-cache`, positional subscriber,
`-p`, and `-n` forms remain supported throughout `4.x` and invoke the same
handlers as command syntax. New scripts should use subcommands. The `5.x.x` line
will require the applicable named subcommand. The root option is not deprecated:
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
