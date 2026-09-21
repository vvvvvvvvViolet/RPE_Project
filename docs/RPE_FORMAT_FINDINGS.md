# RPE file format — analysis findings

**Analysed:** 2026-09-21 · **Samples:** 3 files (10 KB, 31 KB, 44 KB) supplied by the requester
**Status:** format identified with high confidence; container and document schema fully readable

---

## 1. Summary

The `.rpe` files supplied are **project/template exports from Inray OPC Router 4**.

The extension is not a proprietary binary format. Each file is a **standard ZIP
archive** holding exactly one entry, `OpcRouter4.xml`, which is a UTF-8 XML
document rooted at `<OpcRouter4Export>`.

| Question asked | Finding |
|---|---|
| Text, XML, JSON, ZIP, database, binary or proprietary? | **ZIP container** wrapping a single **XML** document |
| Compressed? | Yes — DEFLATE, via the ZIP entry |
| Encrypted? | **No.** Secret fields are omitted at export time (see §6) |
| Checksum present? | Yes — the ZIP per-entry **CRC-32** (no application-level checksum) |
| Layout guessed anywhere? | **No.** Every statement below is backed by bytes in the samples |

Because the format resolved cleanly, `OpcRouter4Parser` reads it structurally.
`GenericRpeParser` remains for any `.rpe` revision that does not match.

---

## 2. Evidence: container layer

All three files begin with the ZIP local file header `50 4B 03 04` (`PK\x03\x04`)
and validate as ZIP archives with no errors.

| Sample | File size | Entry | Compressed | Uncompressed | Ratio | CRC-32 | Method |
|---|---|---|---|---|---|---|---|
| `project.rpe`   | 10,427 B | `OpcRouter4.xml` | 10,301 B | 189,187 B | 18.4:1 | `6C8AEBA8` | 8 (DEFLATE) |
| `project_1.rpe` | 31,238 B | `OpcRouter4.xml` | 31,112 B | 528,881 B | 17.0:1 | `D35706F4` | 8 (DEFLATE) |
| `project_2.rpe` | 45,141 B | `OpcRouter4.xml` | 45,015 B | 709,155 B | 15.8:1 | `43B48D43` | 8 (DEFLATE) |

Consistent across all samples:

* Exactly **one** entry per archive, always named `OpcRouter4.xml`.
* No archive comment, no entry comment, no ZIP extra field.
* General-purpose bit flag `0x0000` — **not encrypted**, sizes in the local header.
* No ZIP64 records; no directory entries; no nested archives.

The entry's DOS timestamp records when the export was written (for example
`2026-09-21 15:30:30` in `project_2.rpe`).

---

## 3. Evidence: document layer

The inner XML declares `<?xml version="1.0" encoding="utf-8"?>` and carries no
BOM. Element and attribute names are ASCII; values may contain non-ASCII text.

### 3.1 Root element

```xml
<OpcRouter4Export ExportType="Templates"
                  Version="5.6.5002.211"
                  FileVersion="Version_3"
                  EncryptedFieldHandling="Skip"
                  Type="OPCRouter"
                  LicenseId="…"
                  DisplayName="…"
                  InstanceId="…">
```

| Attribute | Meaning | Value in all 3 samples |
|---|---|---|
| `ExportType` | What was exported | `Templates` |
| `Version` | OPC Router version that wrote the file | `5.6.5002.211` |
| `FileVersion` | **Export schema revision** | `Version_3` |
| `EncryptedFieldHandling` | How secret fields were treated | `Skip` |
| `Type` | Product family | `OPCRouter` |
| `LicenseId` | Licence of the exporting installation | identical across samples |
| `DisplayName` | Instance/cluster name | identical across samples |
| `InstanceId` | Opaque instance identifier | identical across samples |

`FileVersion` is the field to branch on when a future revision appears — it is
the schema version, whereas `Version` is the product build.

### 3.2 Top-level sections

The root has eleven children, always in this order, present even when empty:

```
Options · Plugins · Certificates · Connections · ConnectionGroups
TransferObjects · ConnectionLines · TransferObjectTemplateVariables
Files · NotificationEMailSenders · NotificationGroups
```

In the `Templates` exports supplied, `Connections`, `TransferObjects`,
`ConnectionLines`, `TransferObjectTemplateVariables`, `Files`,
`NotificationEMailSenders`, `NotificationGroups` and `Options` are all empty at
the top level — the real content sits nested inside `ConnectionGroups`. A
different `ExportType` would be expected to populate the top-level lists, so the
reader walks every section rather than assuming this shape.

### 3.3 Content model

```
OpcRouter4Export
└── ConnectionGroups
    └── ConnectionGroup            (Name, GroupType, Id — path-like, e.g. "Templates/Tier1")
        ├── Groups                 (nested ConnectionGroup — arbitrary depth)
        └── Connections
            └── Connection         (Name, Enabled, ConnectionType, TriggerConjunction, Id)
                ├── TransferObjects
                │   └── <*TransferObjectConfig> / <*TriggerConfig>
                │       ├── LocalId, PosX, PosY, TransferStep, PlugInType, ConfigType
                │       ├── Groups → <*ItemGroup> → Items → <*TransferObjectItem>
                │       │                                   (Name, LocalId, Direction, …)
                │       └── TemplateVariableMappings → TemplateVariableMapping
                ├── ConnectionLines
                │   └── ConnectionLine (ItemStart, ItemStop)
                └── TemplateVariables
                    └── TemplateVariable (Name, VariableTyp, LocalId)
```

**`LocalId` is the graph key.** Every transfer object and every item carries a
`LocalId`. A `ConnectionLine` wires two of them together via `ItemStart` and
`ItemStop`, which is how OPC Router represents the data flow drawn on its
canvas. `PosX`/`PosY` give the canvas coordinates and `TransferStep` the
execution order.

### 3.4 Observed content per sample

| Sample | Plug-ins | Groups | Connections | Lines | Template vars | Elements |
|---|---|---|---|---|---|---|
| `project.rpe`   | 1 | 2 | 1 | 0   | 106 | 2,187 |
| `project_1.rpe` | 5 | 2 | 6 | 187 | 26  | 7,206 |
| `project_2.rpe` | 7 | 1 | 7 | 282 | 34  | 9,874 |

---

## 4. Typed values

Elements that hold a non-string value carry a `Type` attribute naming a .NET
type, sometimes assembly-qualified:

```xml
<ChangedUTCTimestamp Type="System.DateTime">5250883024082017904</ChangedUTCTimestamp>
<VariantValue Type="System.String, System.Private.CoreLib, …">example</VariantValue>
<MqttTransferObjectConfig Type="inray.OPCRouter.MqttPlugIn.MqttTransferObjectConfig, inray.OPCRouter4.MqttPlugIn, Version=5.6.0.0, …">
```

> **The reader treats every `Type` attribute as an opaque display string.** It is
> never passed to `Type.GetType`, never resolved and never instantiated. Doing so
> would turn a data file into a type-loading instruction, which is precisely the
> deserialisation weakness this application is required to avoid.

### 4.1 Timestamp encoding — decoded

Values under `Type="System.DateTime"` are **`DateTime.ToBinary()`** output: a
64-bit value holding 62 bits of ticks plus a 2-bit `DateTimeKind` in the high
bits. They are *not* raw tick counts — plain tick interpretation overflows.

Verified decodings from the samples:

| Raw value | Hex | Kind bits | Decoded |
|---|---|---|---|
| `5250645522852368705` | `48DE0A48AF1C5141` | 1 (UTC) | 2025-10-13 11:07:22.498 UTC |
| `5250941535738654137` | `48DF17818E0BEDB9` | 1 (UTC) | 2026-09-21 01:42:11.126 UTC |
| `5250878696463792138` | `48DEDE5AA563F80A` | 1 (UTC) | 2026-07-10 08:10:03.640 UTC |
| `4611686018427387904` | `4000000000000000` | 1 (UTC) | ticks = 0 → **"not set" sentinel** |

`4611686018427387904` is `DateTime.MinValue` with `Kind=Utc` and appears wherever
a timestamp is unset (consistently in `PublishedUtcTimestamp`). The reader shows
it as `(not set)` rather than as the year 1.

Elements observed carrying this encoding: `Changed`, `ChangedUTCTimestamp`,
`ChangedUtcTimestamp` (both casings occur), `PublishedUtcTimestamp`, `ValidTo`.

### 4.2 Enumerated values observed

| Element | Values seen |
|---|---|
| `Direction` | `input`, `output`, `both`, `none` |
| `JsonType` | `NodeItem`, `DocItem`, `TransformInput`, `TransformOutput` |
| `VarType` | `String`, `Auto` |
| `ConnectionType` | `Template`, `MS_SQL` |
| `GroupType` | `Template`, plus numeric codes (`1052`, `1053`, `2004`, `3052`, `3053`, `20101`, `20102`) |
| `PlugInType` | `1000` base · `2000` OPC · `3100` flow · `5000` variables · `20000` MQTT · `36000` database |
| `TriggerConjunction` | `OR` |

`ConfigType` is a plug-in-scoped sub-type code (`1001`, `1003`, `1015`, `1016`,
`1019`, `1027`, `2001`, `2005`, `3001`, `5001`, `20001`). The samples do not
establish a complete mapping, so the reader shows these codes verbatim and
annotates only `PlugInType`, where the mapping is unambiguous.

---

## 5. Persistence bookkeeping

Nearly every element carries these fields, which belong to OPC Router's own
persistence layer rather than to the engineering data:

```
ForceIdentityInsert · ForceDBInsert · SupportsDBNull · LoadedFromDB
LoadedSchemaFromDB · Loaded · SavedInVersion · Id
```

`SavedInVersion` is per-element and can differ from the root `Version` — in the
samples the root says `5.6.5002.211` while individual elements say
`5.6.5002.212`, i.e. the project was last touched by a slightly newer build than
the one that wrote the export header. The reader shows these in the detail grid
but does not promote them into tree labels.

---

## 6. Security-relevant findings

### 6.1 No secrets in the file — by design

`EncryptedFieldHandling="Skip"` on the root means the exporter **omitted**
encrypted fields. Password elements hold a *reference*, never a value:

```xml
<PasswordKey>
  <Key>MQTT_UNS_PASSWORD_OPEX</Key>
  <Store>internal</Store>
</PasswordKey>
```

The credential itself lives in the OPC Router installation's internal store. No
sample contains a password, key material or certificate private key —
`<Certificates>` held only an empty `<PrivateKeys><CertificateConfig/>` shell.

### 6.2 The files are still sensitive

Although they hold no secrets, the samples **do** carry information that
describes a production OT environment and should be handled accordingly:

* OPC UA endpoint URLs and server host names
* MQTT broker host names, ports and TLS settings
* Database server IP addresses, ports, database names and **user names**
* Service account names used for OPC UA and MQTT authentication
* The installation's licence identifier and instance identifier
* Full OPC tag browse paths, which map out plant equipment

Two consequences were designed into this application:

1. **The sample files are not committed to this repository.** The test suite
   builds its own synthetic fixtures (`tests/…/TestData/SampleBuilder.cs`) that
   contain no real endpoint, account, tag path or licence identifier.
2. **The logger never writes file content.** It records file names, sizes,
   parser identity and error text only, so a log can be attached to a support
   request without disclosing the above.

### 6.3 Parser attack surface

| Risk | Mitigation in this build |
|---|---|
| XXE / external entity | `DtdProcessing = Prohibit`, `XmlResolver = null`, `MaxCharactersFromEntities = 0` |
| Billion laughs | Entity expansion disabled outright by the above |
| Decompression bomb | Per-entry, total-size and 200:1 ratio ceilings before any read |
| Path traversal in entry names | Absolute, drive-rooted and `..` names rejected; nothing is ever extracted to disk |
| Unbounded memory | File-size cap, node cap, field cap, value-length cap, raw-text cap |
| Deep nesting | XML depth measured and capped at 200 |
| Type confusion / unsafe deserialisation | No `BinaryFormatter`; no `Type.GetType`; `Type` attributes are display text only |
| Embedded executable content | Nothing in a file is executed; macros and scripts are not a concept this reader acts on |

---

## 7. What is *not* established

Stated plainly, since guessing here would be worse than an honest gap:

* **Only `ExportType="Templates"` was sampled.** Other export types (full
  project, single connection, runtime configuration) are handled by the same
  generic section walk, but their specific shapes are unverified.
* **Only `FileVersion="Version_3"` was sampled.** `Version_1` and `Version_2`
  presumably exist; no sample was available.
* **`ConfigType` and numeric `GroupType` codes are not fully mapped.** The
  samples cover a subset; the reader shows the raw codes rather than inventing
  labels.
* **`EncryptedFieldHandling` values other than `Skip` were not seen.** An export
  taken with encryption preserved would contain ciphertext this build does not
  attempt to decrypt — by design, since it has no key material and should not.
* **Multi-entry `.rpe` containers were not observed.** The reader lists any extra
  entries it finds and reports them instead of ignoring them silently.

### What would close these gaps

If you can supply any of the following, support can be extended with the same
evidence-first approach:

1. A `.rpe` exported with **`ExportType`** other than `Templates` (a full project export).
2. A `.rpe` from an **older OPC Router build**, to see `FileVersion="Version_1"` or `Version_2`.
3. A `.rpe` exported **without** `EncryptedFieldHandling="Skip"`.
4. A screenshot of the same project inside OPC Router, to confirm the tree labels
   this reader derives match what the engineering tool displays.

Until then, unmatched files fall through to the generic inspector, which reports
only measured facts and states that the layout is unknown.

---

## 8. Reproducing this analysis

The findings above can be re-derived without this application:

```bash
# Container layer
unzip -l  sample.rpe          # one entry: OpcRouter4.xml
unzip -v  sample.rpe          # method 8 (DEFLATE), CRC-32, sizes

# Document layer
unzip -p  sample.rpe OpcRouter4.xml | head -3
unzip -p  sample.rpe OpcRouter4.xml | xmllint --format - | less
```

```python
# Timestamp decoding
raw = 5250883024082017904
ticks, kind = raw & 0x3FFFFFFFFFFFFFFF, (raw >> 62) & 3
import datetime
print(datetime.datetime(1, 1, 1) + datetime.timedelta(microseconds=ticks // 10), "kind", kind)
```
