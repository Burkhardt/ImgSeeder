# ImgSeeder

## 4.5.6

Coordinated 4.5.6 release; public behavior is aligned with the synchronized platform.

Release notes: [ImgSeeder_RELEASE_NOTES_4.5.6.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.5.6.md).

## 4.5.5

Coordinated 4.5.5 release; public behavior is aligned with the synchronized platform.

Release notes: [ImgSeeder_RELEASE_NOTES_4.5.5.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.5.5.md).

## 4.5.4

Coordinated 4.5.4 release; public behavior is aligned with the synchronized platform.

Release notes: [ImgSeeder_RELEASE_NOTES_4.5.4.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.5.4.md).

## 4.5.3

Coordinated 4.5.3 release; public behavior is aligned with the synchronized platform.

Release notes: [ImgSeeder_RELEASE_NOTES_4.5.3.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.5.3.md).

## 4.5.2

Coordinated 4.5.2 release; public behavior is aligned with the synchronized platform.

Release notes: [ImgSeeder_RELEASE_NOTES_4.5.2.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.5.2.md).

## 4.5.0

Coordinated 4.5.0 dependencies including JsonPit live references; iorg reports version 4.5.0.

Release notes: [ImgSeeder_RELEASE_NOTES_4.5.0.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.5.0.md).

## 4.4.8

Coordinated 4.4.8 dependencies including JsonPit live references; iorg reports version 4.4.8.

Release notes: [ImgSeeder_RELEASE_NOTES_4.4.8.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.4.8.md).


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

## 4.4.5

- Participates unchanged in the synchronized nine-package CR047 release and reports `iorg v4.4.5`.
- Fallback package dependencies align to 4.4.5.
- Current release notes: [ImgSeeder_RELEASE_NOTES_4.4.5.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.4.5.md)

## 4.4.4

- Participates unchanged in the synchronized nine-package CR044 release and reports `iorg v4.4.4`.
- Fallback package dependencies align to 4.4.4.
- Current release notes: [ImgSeeder_RELEASE_NOTES_4.4.4.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.4.4.md)

## 4.4.2

- Participates unchanged in the synchronized eight-package CR040/CR041 release and reports `iorg v4.4.2`.
- Fallback package dependencies align to 4.4.2.
- Current release notes: [ImgSeeder_RELEASE_NOTES_4.4.2.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.4.2.md)

## 4.4.1

- Participates in synchronized RAIkeep v4.4.1 CR037/CR037.1 and reports `iorg v4.4.1`.
- Misplaced reserved verbs fail before ImageTree access with exit code `2` and
  an actionable verb-first correction; version flags take immediate precedence.
- Fallback package dependencies align to 4.4.1.
- Current release notes: [ImgSeeder_RELEASE_NOTES_4.4.1.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.4.1.md)

## 4.3.2

- Participates unchanged in coordinated RAIkeep v4.3.2.
- Aligns fallback package dependencies to 4.3.2 and reports `iorg v4.3.2`.
- Current release notes: [ImgSeeder_RELEASE_NOTES_4.3.2.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.3.2.md)

## 4.3.1

- Participates unchanged in coordinated RAIkeep v4.3.1.
- Aligns fallback package dependencies to 4.3.1 and reports `iorg v4.3.1`.
- Current release notes: [ImgSeeder_RELEASE_NOTES_4.3.1.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.3.1.md)

## 4.3.0

- Participates unchanged in coordinated RAIkeep v4.3.0.
- Aligns fallback package dependencies to 4.3.0 and reports `iorg v4.3.0`.
- Current release notes: [ImgSeeder_RELEASE_NOTES_4.3.0.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.3.0.md)

## 4.2.11 (superseded before publication)

- This prepared line was not published; its coordinated changes are carried by v4.3.0.
- Aligns fallback package dependencies to 4.2.11 and reports `iorg v4.2.11`.
- Current release notes: [ImgSeeder_RELEASE_NOTES_4.2.11.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.2.11.md)

## 4.2.10

- Adds `-a, --app` for application-root addressing; iorg appends the conventional `Image` segment.
- Adds preferred `-t, --tenant`; `--subscriber` remains a compatibility alias.
- Keeps `-r, --root` as the exact ImageTree-root alternative. `--root` and `--app` are mutually exclusive, and app-root addressing requires a tenant.
- Corrects root and contextual help alignment, documents the legacy unnamed subscriber only when it was actually used, and consistently shows `(-p|--pathconv)`.
- Aligns fallback package dependencies to 4.2.10 and reports `iorg v4.2.10`.
- Current release notes: [ImgSeeder_RELEASE_NOTES_4.2.10.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.2.10.md)

## 4.2.9

- Implements accepted incident corrective action CR022 by removing the TempDir subscriber staging tree.
- `iorg` organization writes each image directly to its final ItemTree pathname through RaiFile; it never moves a staged temporary file into a CloudDrive.
- The optional `tempRoot` API parameter remains source/binary compatible but is intentionally behaviorally inert.
- Aligns all fallback package dependencies to 4.2.9 and reports `iorg v4.2.9`.
- Current release notes: [ImgSeeder_RELEASE_NOTES_4.2.9.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.2.9.md)
- Mandatory storage contract: [Cloud-Storage-In-Place-Invariant.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/Cloud-Storage-In-Place-Invariant.md)

## 4.2.8

- Aligns ImgSeeder with the coordinated seven-package RAIkeep 4.2.8 release implementing accepted CR021.
- Existing `iorg` behavior remains unchanged from 4.2.7.
- Aligns all fallback package dependencies to 4.2.8 and reports `iorg v4.2.8`.
- Current release notes: [ImgSeeder_RELEASE_NOTES_4.2.8.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.2.8.md)

## 4.2.7

- Implements accepted CR020 with read-only recursive `iorg list <FileNamePattern>` discovery across image, `.puml`, and `.raid` artifacts.
- Adds exact-ItemId `iorg move <SourceItemId> [TargetItemId]` for relocation, rename, and path-convention migration without disturbing bucket siblings.
- Adds `iorg clean --cache` for explicit derivative cleanup and makes `iorg clean <ItemId> --force` remove the complete item file family.
- Removes the misleading Legacy row from root help; `-r` remains the supported short spelling of `--root`.
- Aligns all fallback package dependencies to 4.2.7 and reports `iorg v4.2.7`.
- Current release notes: [ImgSeeder_RELEASE_NOTES_4.2.7.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.2.7.md)

## 4.2.6

- Aligns ImgSeeder with the coordinated seven-package RAIkeep 4.2.6 release implementing accepted CR019.
- Aligns fallback dependencies on `JsonPit 4.2.6`, `OsLibCore 4.2.6`, `RaiUtils 4.2.6`, and `RaiImage 4.2.6`; `iorg` behavior is unchanged.
- Reports `iorg v4.2.6` through the CLI version boundary.
- Current release notes: [ImgSeeder_RELEASE_NOTES_4.2.6.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.2.6.md)

## 4.2.5

- Aligns ImgSeeder with the coordinated seven-package RAIkeep 4.2.5 release implementing accepted CR017.
- Aligns fallback dependencies on `JsonPit 4.2.5`, `OsLibCore 4.2.5`, `RaiUtils 4.2.5`, and `RaiImage 4.2.5`; `iorg` behavior is unchanged.
- Reports `iorg v4.2.5` through the CLI version boundary.
- Current release notes: [ImgSeeder_RELEASE_NOTES_4.2.5.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.2.5.md)

## 4.2.4

- Adopts RaiImage 4.2.4 so `iorg` destinations use accepted CR016 NFC normalization and Unicode text-element bucketing.
- Aligns fallback dependencies on `JsonPit 4.2.4`, `OsLibCore 4.2.4`, `RaiUtils 4.2.4`, and `RaiImage 4.2.4`.
- CLI behavior and the established Nerd Font help contract remain unchanged.
- 4.2.4 release notes: [ImgSeeder_RELEASE_NOTES_4.2.4.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.2.4.md)

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

## release notes

- Latest release notes: [ImgSeeder_RELEASE_NOTES_4.5.6.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/ImgSeeder_RELEASE_NOTES_4.5.6.md)

## Validation

- `dotnet test ImgSeeder.slnx --nologo -v minimal`
