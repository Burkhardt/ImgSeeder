# Getting Started with ImgSeeder (`iorg`)

`iorg` is the RAIkeep command-line tool for organizing, validating, and managing images and video containers within an **ImageTree**.

This guide covers ImgSeeder 4.5.15, including CR060 filename handling,
CR061 idempotent imports, and CR067 single-file media ingestion (`mp4`, `mov`,
`webm`, `mkv`, and `avi`) with optional verified scratch-file cleanup.

This guide walks through setting up cloud storage integration, installing the tools, performing bulk imports from a ZIP archive, and managing/cleaning images (e.g. for the sample order-sheet photo archive).

---

## Prerequisites & Installation

### Step 1: Set Up Cloud Storage
Ensure your cloud storage provider (e.g. **Microsoft OneDrive**) is installed and syncing locally:
- macOS: Available via Mac App Store or Microsoft direct download.
- Verify that your local synchronization folder is active (typically under `~/Library/CloudStorage/OneDrive-...` on macOS).

### Step 2: Install CLI Tools
Install both **`Amafu`** (cloud drive discovery engine) and **`ImgSeeder`** (ImageTree organizer) as .NET global tools:

```bash
dotnet tool install --global Amafu
dotnet tool install --global ImgSeeder
```

*(Ensure `~/.dotnet/tools` is in your `$PATH`.)*

### Step 3: Initialize Cloud Discovery & Shortcuts
Run `amafu` to detect your cloud roots and generate stable POSIX shortcuts under `~/.CloudStorage/`:

```bash
# Check version and detect providers
amafu -v
amafu detect --json

# Create shortcuts and write shared ~/.config/RAIkeep.json5 configuration
amafu init --create-links
```

Verify that the stable shortcut is created and your tenant roots are visible:

```bash
ls ~/.CloudStorage/OneDrive/
```

For the **SampleApp** application root, you will see the application structure:
```bash
ls ~/.CloudStorage/OneDrive/SampleApp
# Output: Activity  Image  Object  Person  Place
```

---

## Preflight: Verify ImageTree Path Resolution

Before copying or unzipping, use `-c` (`--cloud`) and `-a` (`--app`) with `-h` to confirm the target ImageTree location:

```bash
iorg -c OneDrive -a SampleApp -h
```

`iorg` displays the resolved base path where the image tree resides (e.g. `~/.CloudStorage/OneDrive/SampleApp/Image`).

## Ingest one video from scratch storage

After a media encoder has completed successfully, hand its output file to
`iorg`. The `--move` option deletes that one source file only after the
destination has passed SHA-256 verification. An archive or directory cannot
be used with `--move`.

```bash
iorg organize --source /tmp/Server-Clip-001.mp4 \
  --cloud OneDrive --app SampleApp --subscriber Customer \
  --move --json |
  jq '.Files[0] | {ItemId, Ext, Status, SizeBytes, ItemTreePath}'
```

The `ItemTreePath` is relative to the configured cloud root, so callers can
use it without calculating the `ItemIdTree8x2` directories. `--json` alone
writes the receipt to stdout; `-o` is optional. A repeat import with identical
bytes reports `Unchanged`, while changed bytes report `Updated`.

---

## Bulk Importing Images from a ZIP Archive

When receiving photo archives from photographers or cameras (e.g. from `/Volumes/NVMe/GooglePhotos/Photos-1-001.zip`), use `iorg organize` to unpack, normalize names into PascalCase, deduplicate, and stream directly into the ImageTree.

### Step 4: Run the Import

```bash
iorg organize \
  --cloud OneDrive \
  --app SampleApp \
  --subscriber Customer \
  --source /Volumes/NVMe/GooglePhotos/Photos-1-001.zip \
  -o import-receipt.json \
  --json
```

**Options explained:**
- `--cloud OneDrive` (or `-c OneDrive`): Uses the configured OneDrive cloud root.
- `--app SampleApp` (or `-a SampleApp`): Scopes the target to the application's `Image` branch.
- `--subscriber Customer` (or `-t Customer`): The tenant/subscriber partition owning these photos.
- `--source <file.zip>` (or `-s <file.zip>`): Path to the source archive.
- `-o <file.json>` (or `--out` / `--receipt`): Writes the import receipt directly to the specified file.
- `--json`: Emits compact `ReceiptVersion 3` structured JSON output. (Note: `--import-id` is optional; an ID is generated automatically if omitted).
- `--no-overwrite`: Optional guard that rejects an archive before copying if any existing destination has different bytes. Without it, a rerun leaves identical images alone and updates changed images in place.

---

## Inspecting Import Results

### Step 5: Query the Receipt with `jq`

1. **Check summary counts:**
   ```bash
   jq '.Summary' import-receipt.json
   # Output: {"Copied": 193, "Updated": 0, "Unchanged": 0, "Skipped": 0, "Failed": 0}
   ```

2. **Inspect compact contiguous ranges:**
   ```bash
   jq -r '(.Ranges // [(.Range + {BaseItemId, PathConvention, NamingConvention})])[] |
     select(.Start != null) | [.BaseItemId, .Start, .End, .Count, .Ext] | @tsv' import-receipt.json
   ```

3. **Check for any exceptions or collisions:**
   ```bash
   jq '.Exceptions[]?' import-receipt.json
   ```

### Step 6: Verify Imported Images in the ImageTree

Verify that images are correctly placed and searchable using `iorg list`:

```bash
# List all imported images for the item
iorg list 'CustomerOrderSheet26*' --cloud OneDrive --app SampleApp --subscriber Customer

# Filter listing to a single image number (-i or --image)
iorg list CustomerOrderSheet26 -i 100 --cloud OneDrive --app SampleApp --subscriber Customer

# Inspect metadata with EXIF extraction
iorg list 'CustomerOrderSheet26*' --cloud OneDrive --app SampleApp --subscriber Customer \
  --exif 'DateTimeOriginal,Exposure*' --json | jq '.[] | {FileName, Exif}'
```

---

## Cleaning & Managing Images (`iorg clean`)

If an import needs to be rolled back or individual images removed, use `iorg clean`.

### Dry-run by default
Running `iorg clean` without `--force` prints a preview of files selected for deletion without touching disk:

```bash
# Preview cleaning all images for this ItemId
iorg clean CustomerOrderSheet26 -a SampleApp -t Customer
```

### Clean all images for an item
Passing `--force` deletes all images and derivatives under that ItemId (e.g. `_001` through `_193`):

```bash
iorg clean CustomerOrderSheet26 -a SampleApp -t Customer --force
```

### Clean a specific image number only (`-i` / `--image`)
To remove only one specific image number (for instance, image `100` and its derivatives `_100.jpg`, `_100_Large.jpg`), supply `-i` or `--image`:

```bash
iorg clean CustomerOrderSheet26 -i 100 -a SampleApp -t Customer --force
```

---

## Filename Normalization Rules

`iorg organize` applies `ImageFile.EasyFileName` rules to source filenames:
- Hyphenated or delimited words are converted to **PascalCase**.
- Intermediate numeric tokens (such as `26` in `Customer-Order-Sheet-26-10.jpg`) are preserved as part of the `ItemId` stem (`CustomerOrderSheet26`).
- The final numeric token is treated as the sequence number (`ImageNumber`) and padded to a minimum of 3 digits (`D3`):
  - `Customer-Order-Sheet-26-1.jpg` $\rightarrow$ `CustomerOrderSheet26_001.jpg`
  - `Customer-Order-Sheet-26-10.jpg` $\rightarrow$ `CustomerOrderSheet26_010.jpg`
  - `Customer-Order-Sheet-26-100.jpg` $\rightarrow$ `CustomerOrderSheet26_100.jpg`
  - `Customer-Order-Sheet-26-101.jpg` $\rightarrow$ `CustomerOrderSheet26_101.jpg`
