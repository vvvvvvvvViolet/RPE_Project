# Third-party notices

RPE Reader 1.0.0

## Summary

**The shipped application has no third-party runtime dependencies.**

`RPEReader.exe` and `RPEReader.Core.dll` are built exclusively against the
.NET 8 base class library and WPF. No NuGet package is referenced by
`src/RPEReader.App` or `src/RPEReader.Core`, so nothing beyond Microsoft's own
runtime is redistributed.

The packages listed under *Build and test tooling* are used only to compile and
test the application. They are not part of the published output.

---

## Redistributed components

### .NET 8 runtime and WPF (self-contained deployment)

Because the application is published self-contained, the .NET 8 runtime,
`Microsoft.WindowsDesktop.App` (WPF) and their native support libraries are
copied into the `Release` folder and redistributed with the application.

| Item | Detail |
|---|---|
| Component | .NET 8 runtime, WPF (`Microsoft.WindowsDesktop.App`), ASP.NET Core shared framework files where present |
| Copyright | © Microsoft Corporation |
| Licence | MIT License |
| Source | https://github.com/dotnet/runtime · https://github.com/dotnet/wpf |
| Licence text | https://github.com/dotnet/runtime/blob/main/LICENSE.TXT |
| Redistribution | Permitted under the MIT License and the .NET Library redistribution terms |

Native libraries redistributed as part of WPF include `wpfgfx_cor3.dll`,
`PresentationNative_cor3.dll`, `D3DCompiler_47_cor3.dll`, `PenImc_cor3.dll` and
`vcruntime140_cor3.dll`. All are Microsoft components covered by the same terms.

The full third-party notice that ships with the .NET runtime is reproduced in
`ThirdPartyNotices.txt` inside the published `Release` folder. It covers upstream
components vendored into the runtime itself (among them zlib, Brotli, ICU, RyuJIT
and Unicode data).

---

## Build and test tooling

These are developer-time dependencies. They are restored during a build and are
**not** redistributed with the application.

| Package | Version | Licence | Project |
|---|---|---|---|
| `xunit` | 2.9.2 | Apache-2.0 | https://github.com/xunit/xunit |
| `xunit.runner.visualstudio` | 2.8.2 | Apache-2.0 | https://github.com/xunit/visualstudio.xunit |
| `Microsoft.NET.Test.Sdk` | 17.11.1 | MIT | https://github.com/microsoft/vstest |

All three are open source. Apache-2.0 and MIT both permit this use without
imposing obligations on the shipped application.

---

## Optional external tooling

Not bundled, not required to run the application, and not redistributed here.

| Tool | Purpose | Licence |
|---|---|---|
| Inno Setup 6 | Compiles `build/installer.iss` into a setup executable | Inno Setup License (free, permits commercial use) — https://jrsoftware.org/files/is/license.txt |
| .NET 8 SDK | Builds the solution | MIT |

---

## Licence texts

### MIT License

Applies to the .NET runtime, WPF and `Microsoft.NET.Test.Sdk`.

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### Apache License 2.0

Applies to xUnit.net and its Visual Studio runner. Full text:
https://www.apache.org/licenses/LICENSE-2.0

Required notice: this product includes software developed by the xUnit.net
project (https://xunit.net/). Licensed under the Apache License, Version 2.0.
You may not use those files except in compliance with that licence. Unless
required by applicable law or agreed to in writing, software distributed under
the Licence is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
OF ANY KIND, either express or implied.

---

## Trademarks

"OPC Router" is a product of Inray Industriesoftware GmbH. "OPC UA" is a
trademark of the OPC Foundation. "Windows", ".NET" and "Visual Studio" are
trademarks of Microsoft Corporation. This project is an independent, read-only
file viewer. It is **not** affiliated with, endorsed by, or supported by any of
these organisations, and it contains no code from any of their products.

---

## Verifying this inventory

```bash
# Every package reference in the solution
dotnet list RPEReader.sln package --include-transitive

# The shipped projects should report no packages at all
dotnet list src/RPEReader.Core/RPEReader.Core.csproj package
dotnet list src/RPEReader.App/RPEReader.App.csproj package
```
