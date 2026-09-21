# Release manifest — RPE Reader 1.0.0

Self-contained `win-x64` publish produced by `build/publish.ps1`.

- **Files:** 245
- **Total size:** 144.9 MB (151,926,100 bytes)
- **Target:** Windows 10 (1809+) / Windows 11, x64
- **Runtime:** .NET 8 bundled — no separate runtime install required

> The published binaries are **not committed to this repository**: a 146 MB
> self-contained drop does not belong in version control. Reproduce it with
> `pwsh .\build\publish.ps1`, or download the artifact from the
> `build-release` GitHub Actions workflow, which publishes it on a real
> Windows runner.

## Application files

| File | Size | SHA-256 |
|---|---:|---|
| `RPEReader.exe` | 252,416 B | `dbdff07014db1e96ee1e227f6e84f4c4ff0bfd8701108afbe1210feb3296dd99` |
| `RPEReader.dll` | 327,680 B | `0144ca58b4f0f3d19b14a7e3a9d2c3cddc671e2f80de26fb870db111c5ab1bcc` |
| `RPEReader.Core.dll` | 217,088 B | `f15685149f49f2892255fce01fb57fd5be80ef7675de4e21f215f8aa972b63b2` |
| `RPEReader.runtimeconfig.json` | 497 B | `7fd808627ff268db842207ae7022d8c35d386b1e8f5a969256cc322af9bcfc77` |
| `RPEReader.deps.json` | 34,683 B | `32b830c7cd7f472263269ec82039f37a6752f764968543077928c4dbdc3efc5a` |

> The SHA-256 values above are from one specific build. .NET builds are not
> bit-for-bit deterministic by default, so a rebuild on another machine will
> produce different hashes for the managed assemblies. Use them to verify a
> drop you were given, not to compare two independent builds.

## Bundled runtime

The remaining 240 files are the .NET 8 runtime and WPF, copied in by the
self-contained publish. Notable native components:

| File | Size | Purpose |
|---|---:|---|
| `wpfgfx_cor3.dll` | 1,955,624 B | WPF composition and rendering engine |
| `PresentationNative_cor3.dll` | 1,235,280 B | WPF native support layer |
| `D3DCompiler_47_cor3.dll` | 4,741,488 B | Direct3D shader compiler used by WPF |
| `PenImc_cor3.dll` | 158,032 B | Pen and touch input |
| `vcruntime140_cor3.dll` | 124,544 B | Visual C++ runtime used by the above |
| `hostfxr.dll` | 366,376 B | .NET host resolver |
| `hostpolicy.dll` | 406,824 B | .NET host policy |
| `coreclr.dll` | 4,995,920 B | CoreCLR runtime |
| `clrjit.dll` | 1,778,512 B | JIT compiler |
| `System.Private.CoreLib.dll` | 13,174,568 B | Base class library core |

`ThirdPartyNotices.txt` and `LICENSE.txt` from the .NET runtime are included
in the publish output as required by its redistribution terms.

## Full file list

<details><summary>All 245 files</summary>

```
      20,816  Accessibility.dll
   4,741,488  D3DCompiler_47_cor3.dll
     517,928  DirectWriteForwarder.dll
   1,005,352  Microsoft.CSharp.dll
   2,195,504  Microsoft.DiaSymReader.Native.amd64.dll
   1,247,016  Microsoft.VisualBasic.Core.dll
     247,632  Microsoft.VisualBasic.Forms.dll
      18,728  Microsoft.VisualBasic.dll
      15,184  Microsoft.Win32.Primitives.dll
      34,600  Microsoft.Win32.Registry.AccessControl.dll
     120,616  Microsoft.Win32.Registry.dll
      96,040  Microsoft.Win32.SystemEvents.dll
     158,032  PenImc_cor3.dll
   8,546,088  PresentationCore.dll
      38,736  PresentationFramework-SystemCore.dll
      34,600  PresentationFramework-SystemData.dll
      34,600  PresentationFramework-SystemDrawing.dll
      34,600  PresentationFramework-SystemXml.dll
      30,544  PresentationFramework-SystemXmlLinq.dll
     444,200  PresentationFramework.Aero.dll
     448,336  PresentationFramework.Aero2.dll
     239,400  PresentationFramework.AeroLite.dll
     272,168  PresentationFramework.Classic.dll
     669,480  PresentationFramework.Luna.dll
     333,648  PresentationFramework.Royale.dll
  16,127,824  PresentationFramework.dll
   1,235,280  PresentationNative_cor3.dll
   1,287,976  PresentationUI.dll
     217,088  RPEReader.Core.dll
      33,136  RPEReader.Core.pdb
      34,683  RPEReader.deps.json
     327,680  RPEReader.dll
     252,416  RPEReader.exe
      35,936  RPEReader.pdb
         497  RPEReader.runtimeconfig.json
   1,603,368  ReachFramework.dll
      15,144  System.AppContext.dll
      15,184  System.Buffers.dll
     489,256  System.CodeDom.dll
     276,264  System.Collections.Concurrent.dll
     837,416  System.Collections.Immutable.dll
     104,272  System.Collections.NonGeneric.dll
     104,232  System.Collections.Specialized.dll
     259,880  System.Collections.dll
     198,480  System.ComponentModel.Annotations.dll
      16,720  System.ComponentModel.DataAnnotations.dll
      46,888  System.ComponentModel.EventBasedAsync.dll
      79,656  System.ComponentModel.Primitives.dll
     747,304  System.ComponentModel.TypeConverter.dll
      30,544  System.ComponentModel.dll
   1,075,024  System.Configuration.ConfigurationManager.dll
      19,280  System.Configuration.dll
     173,904  System.Console.dll
      23,336  System.Core.dll
   2,860,840  System.Data.Common.dll
      15,656  System.Data.DataSetExtensions.dll
      24,872  System.Data.dll
      21,840  System.Design.dll
      16,168  System.Diagnostics.Contracts.dll
      15,696  System.Diagnostics.Debug.dll
     415,568  System.Diagnostics.DiagnosticSource.dll
     800,592  System.Diagnostics.EventLog.Messages.dll
     386,896  System.Diagnostics.EventLog.dll
      46,888  System.Diagnostics.FileVersionInfo.dll
     288,552  System.Diagnostics.PerformanceCounter.dll
     337,744  System.Diagnostics.Process.dll
      46,888  System.Diagnostics.StackTrace.dll
      67,368  System.Diagnostics.TextWriterTraceListener.dll
      15,144  System.Diagnostics.Tools.dll
     145,192  System.Diagnostics.TraceSource.dll
      16,168  System.Diagnostics.Tracing.dll
   1,046,312  System.DirectoryServices.dll
   1,529,640  System.Drawing.Common.dll
      15,184  System.Drawing.Design.dll
     132,944  System.Drawing.Primitives.dll
      21,288  System.Drawing.dll
      16,168  System.Dynamic.Runtime.dll
     243,536  System.Formats.Asn1.dll
     276,264  System.Formats.Tar.dll
      15,656  System.Globalization.Calendars.dll
      15,184  System.Globalization.Extensions.dll
      15,656  System.Globalization.dll
      83,752  System.IO.Compression.Brotli.dll
      15,144  System.IO.Compression.FileSystem.dll
     828,712  System.IO.Compression.Native.dll
      55,080  System.IO.Compression.ZipFile.dll
     264,016  System.IO.Compression.dll
     104,272  System.IO.FileSystem.AccessControl.dll
      55,120  System.IO.FileSystem.DriveInfo.dll
      15,144  System.IO.FileSystem.Primitives.dll
      87,848  System.IO.FileSystem.Watcher.dll
      15,656  System.IO.FileSystem.dll
      91,944  System.IO.IsolatedStorage.dll
      83,792  System.IO.MemoryMappedFiles.dll
     292,648  System.IO.Packaging.dll
      16,208  System.IO.Pipes.AccessControl.dll
     165,672  System.IO.Pipes.dll
      15,184  System.IO.UnmanagedMemoryStream.dll
      15,656  System.IO.dll
   3,675,944  System.Linq.Expressions.dll
     804,648  System.Linq.Parallel.dll
     173,904  System.Linq.Queryable.dll
     542,504  System.Linq.dll
     157,520  System.Memory.dll
     128,848  System.Net.Http.Json.dll
   1,734,480  System.Net.Http.dll
     550,736  System.Net.HttpListener.dll
     431,912  System.Net.Mail.dll
     112,424  System.Net.NameResolution.dll
     157,520  System.Net.NetworkInformation.dll
      96,040  System.Net.Ping.dll
     231,208  System.Net.Primitives.dll
     284,496  System.Net.Quic.dll
     345,936  System.Net.Requests.dll
     669,480  System.Net.Security.dll
      46,888  System.Net.ServicePoint.dll
     546,640  System.Net.Sockets.dll
     169,768  System.Net.WebClient.dll
      67,408  System.Net.WebHeaderCollection.dll
      42,792  System.Net.WebProxy.dll
     100,136  System.Net.WebSockets.Client.dll
     190,248  System.Net.WebSockets.dll
      17,232  System.Net.dll
      15,656  System.Numerics.Vectors.dll
      15,144  System.Numerics.dll
      79,656  System.ObjectModel.dll
     980,776  System.Printing.dll
  13,174,568  System.Private.CoreLib.dll
   2,082,600  System.Private.DataContractSerialization.dll
     259,880  System.Private.Uri.dll
     403,240  System.Private.Xml.Linq.dll
   7,997,224  System.Private.Xml.dll
      75,560  System.Reflection.DispatchProxy.dll
      15,656  System.Reflection.Emit.ILGeneration.dll
      15,656  System.Reflection.Emit.Lightweight.dll
     128,808  System.Reflection.Emit.dll
      15,184  System.Reflection.Extensions.dll
   1,115,944  System.Reflection.Metadata.dll
      15,696  System.Reflection.Primitives.dll
      42,792  System.Reflection.TypeExtensions.dll
      16,168  System.Reflection.dll
     137,000  System.Resources.Extensions.dll
      15,184  System.Resources.Reader.dll
      15,696  System.Resources.ResourceManager.dll
      50,984  System.Resources.Writer.dll
      15,184  System.Runtime.CompilerServices.Unsafe.dll
      30,504  System.Runtime.CompilerServices.VisualC.dll
      17,744  System.Runtime.Extensions.dll
      15,184  System.Runtime.Handles.dll
      50,984  System.Runtime.InteropServices.JavaScript.dll
      15,144  System.Runtime.InteropServices.RuntimeInformation.dll
      96,080  System.Runtime.InteropServices.dll
      16,680  System.Runtime.Intrinsics.dll
      15,656  System.Runtime.Loader.dll
     329,552  System.Runtime.Numerics.dll
     309,072  System.Runtime.Serialization.Formatters.dll
      15,656  System.Runtime.Serialization.Json.dll
      38,696  System.Runtime.Serialization.Primitives.dll
      16,680  System.Runtime.Serialization.Xml.dll
      16,680  System.Runtime.Serialization.dll
      43,304  System.Runtime.dll
     231,208  System.Security.AccessControl.dll
     100,136  System.Security.Claims.dll
      17,192  System.Security.Cryptography.Algorithms.dll
      16,168  System.Security.Cryptography.Cng.dll
      15,656  System.Security.Cryptography.Csp.dll
      15,656  System.Security.Cryptography.Encoding.dll
      15,144  System.Security.Cryptography.OpenSsl.dll
     759,592  System.Security.Cryptography.Pkcs.dll
      15,656  System.Security.Cryptography.Primitives.dll
      55,080  System.Security.Cryptography.ProtectedData.dll
      16,680  System.Security.Cryptography.X509Certificates.dll
     464,680  System.Security.Cryptography.Xml.dll
   2,049,832  System.Security.Cryptography.dll
     186,192  System.Security.Permissions.dll
     186,152  System.Security.Principal.Windows.dll
      15,144  System.Security.Principal.dll
      15,144  System.Security.SecureString.dll
      18,216  System.Security.dll
      16,680  System.ServiceModel.Web.dll
      15,696  System.ServiceProcess.dll
     861,992  System.Text.Encoding.CodePages.dll
      15,656  System.Text.Encoding.Extensions.dll
      15,696  System.Text.Encoding.dll
     132,904  System.Text.Encodings.Web.dll
   1,500,968  System.Text.Json.dll
   1,021,736  System.Text.RegularExpressions.dll
      83,752  System.Threading.AccessControl.dll
     132,904  System.Threading.Channels.dll
      15,696  System.Threading.Overlapped.dll
     489,256  System.Threading.Tasks.Dataflow.dll
      15,656  System.Threading.Tasks.Extensions.dll
     132,904  System.Threading.Tasks.Parallel.dll
      16,680  System.Threading.Tasks.dll
      15,656  System.Threading.Thread.dll
      15,696  System.Threading.ThreadPool.dll
      15,184  System.Threading.Timer.dll
      83,792  System.Threading.dll
     661,328  System.Transactions.Local.dll
      16,208  System.Transactions.dll
      15,184  System.ValueTuple.dll
      59,176  System.Web.HttpUtility.dll
      15,144  System.Web.dll
   1,451,856  System.Windows.Controls.Ribbon.dll
     116,520  System.Windows.Extensions.dll
      16,168  System.Windows.Forms.Design.Editors.dll
   5,564,200  System.Windows.Forms.Design.dll
   3,012,392  System.Windows.Forms.Primitives.dll
  13,563,688  System.Windows.Forms.dll
     137,000  System.Windows.Input.Manipulations.dll
      30,504  System.Windows.Presentation.dll
      15,696  System.Windows.dll
   1,423,184  System.Xaml.dll
      15,656  System.Xml.Linq.dll
      21,800  System.Xml.ReaderWriter.dll
      16,208  System.Xml.Serialization.dll
      15,656  System.Xml.XDocument.dll
      30,544  System.Xml.XPath.XDocument.dll
      15,656  System.Xml.XPath.dll
      15,656  System.Xml.XmlDocument.dll
      17,704  System.Xml.XmlSerializer.dll
      23,376  System.Xml.dll
      49,960  System.dll
     415,528  UIAutomationClient.dll
     870,184  UIAutomationClientSideProviders.dll
      59,176  UIAutomationProvider.dll
     313,168  UIAutomationTypes.dll
   2,254,632  WindowsBase.dll
     210,768  WindowsFormsIntegration.dll
     310,568  clretwrc.dll
     667,944  clrgc.dll
   1,778,512  clrjit.dll
   4,995,920  coreclr.dll
      71,552  createdump.exe
     366,376  hostfxr.dll
     406,824  hostpolicy.dll
   1,348,480  mscordaccore.dll
   1,348,480  mscordaccore_amd64_amd64_8.0.2926.32403.dll
   1,234,312  mscordbi.dll
      59,216  mscorlib.dll
     136,488  mscorrc.dll
     524,320  msquic.dll
     100,648  netstandard.dll
     124,544  vcruntime140_cor3.dll
   1,955,624  wpfgfx_cor3.dll
```
</details>
