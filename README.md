# Soenneker.QrCodes.Generator

**Cross-platform QR codes in pure managed C#.** Generate PNG images, scalable SVGs, and embeddable data URIs from text, URLs, and Unicode content.

[![NuGet version](https://img.shields.io/nuget/v/soenneker.qrcodes.generator.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.qrcodes.generator/)
[![Build and test](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.qrcodes.generator/build-and-test.yml?style=for-the-badge&label=build%20%26%20test)](https://github.com/soenneker/soenneker.qrcodes.generator/actions/workflows/build-and-test.yml)
[![NuGet downloads](https://img.shields.io/nuget/dt/soenneker.qrcodes.generator.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.qrcodes.generator/)

The encoder and renderers are implemented in this project using managed .NET APIs.

- **Windows, Linux, and macOS** — targets .NET 11.
- **Four output formats** — PNG bytes, SVG text, Base64 PNG, and PNG data URIs.
- **QR Model 2** — versions 1–40, all four error correction levels, and automatic mask selection.
- **Unicode support** — UTF-8 byte encoding with an ECI marker.
- **Concurrent use** — stateless generation with immutable options and optional dependency injection.

## Installation

```shell
dotnet add package Soenneker.QrCodes.Generator
```

## Quick start

```csharp
using System.IO;
using Soenneker.QrCodes.Generator;

var generator = new QrCodeGenerator();

byte[] png = generator.GeneratePng("https://soenneker.com");
await File.WriteAllBytesAsync("qr.png", png);
```

Defaults produce a black-and-white QR code with Quartile error correction, eight pixels per module, and the white border needed for scanning. The smallest version that fits the text is selected automatically.

## Output formats

Every method accepts `string text` and an optional `QrCodeOptions` instance.

| Method | Returns | Use for |
| --- | --- | --- |
| `GeneratePng` | `byte[]` | Files, downloads, and image responses |
| `GenerateSvg` | `string` | Scalable images for web and print |
| `GeneratePngBase64` | `string` | A Base64 PNG without a URI prefix |
| `GeneratePngDataUri` | `string` | An HTML image source starting with `data:image/png;base64,` |

```csharp
string svg = generator.GenerateSvg("Hello 世界 🌍");
await File.WriteAllTextAsync("qr.svg", svg);

string base64 = generator.GeneratePngBase64("https://soenneker.com");
string imageSource = generator.GeneratePngDataUri("https://soenneker.com");
```

Generation is synchronous and performs no file or network I/O. Returned arrays and strings belong to the caller.

## Options

```csharp
var options = new QrCodeOptions
{
    PixelsPerModule = 12,
    ErrorCorrection = QrCodeErrorCorrection.High,
    DrawQuietZones = true,
    Version = 0
};

byte[] png = generator.GeneratePng("https://soenneker.com", options);
```

| Option | Default | Behavior |
| --- | --- | --- |
| `PixelsPerModule` | `8` | Integer scale from `1` to `64` |
| `ErrorCorrection` | `Quartile` | `Low`, `Medium`, `Quartile`, or `High` |
| `DrawQuietZones` | `true` | Adds a four-module white border on every side |
| `Version` | `0` | `0` selects the smallest fitting version; `1`–`40` requires that exact version |

Higher error correction uses more capacity and can require a larger QR code. A fixed version that cannot hold the text at the requested correction level causes an `ArgumentException`.

A module is one square in the QR grid. The grid is `17 + 4 × version` modules wide. With the default border, the image width and height are:

```text
(17 + 4 × version + 8) × PixelsPerModule
```

For example, version 1 at the default scale produces a 232 × 232 PNG. SVG output uses the same dimensions with a scalable `viewBox`.

Keep `DrawQuietZones` enabled unless your surrounding layout supplies an equivalent white border.

## Dependency injection

Register the generator in your application's service collection:

```csharp
using Soenneker.QrCodes.Generator.Registrars;

builder.Services.AddQrCodeGeneratorAsSingleton();
```

Then inject `IQrCodeGenerator` from `Soenneker.QrCodes.Generator.Abstract`:

```csharp
using Microsoft.AspNetCore.Http;
using Soenneker.QrCodes.Generator.Abstract;

app.MapGet("/qr", (IQrCodeGenerator generator) =>
    Results.File(
        generator.GeneratePng("https://soenneker.com"),
        "image/png"));
```

A singleton supports concurrent calls. Use `AddQrCodeGeneratorAsScoped()` if you prefer one instance per scope. Both registrars preserve an existing registration.

## Input and capacity

Text is encoded in UTF-8 byte mode with ECI assignment 26. Whitespace is preserved, and characters such as accented letters, CJK text, and emoji are supported. Capacity depends on the **UTF-8 byte count**, version, and correction level.

| Invalid input | Exception |
| --- | --- |
| Null text | `ArgumentNullException` |
| Empty text or text exceeding capacity | `ArgumentException` |
| Invalid UTF-16, such as an unpaired surrogate | `EncoderFallbackException` |
| Unsupported scale, version, or correction value | `ArgumentOutOfRangeException` |

Numeric and alphanumeric content is supported through byte mode; specialized numeric, alphanumeric, and Kanji compression modes are not implemented. Micro QR, structured append, logos, and custom colors are not supported.

## Dependencies and verification

The production package references `Microsoft.Extensions.DependencyInjection.Abstractions` for its registrars. QR encoding, Reed–Solomon error correction, PNG writing, and SVG writing are implemented locally using .NET APIs.

The test project uses **ZXing.Net** to independently decode QR codes and **StbImageSharp** to read PNGs. These are test-only dependencies. Coverage includes all 160 version/correction combinations at capacity, automatic sizing, Unicode, SVG/PNG geometry, validation, and concurrent generation.

The CI workflow is configured for Windows, Linux, and macOS.

## License

[MIT](LICENSE)
