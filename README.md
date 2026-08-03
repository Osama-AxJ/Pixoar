# Pixoar

<p align="center">
  <img src="Pixoar.App/Resources/Assets/Branding/pixoar-logo.png" alt="Pixoar Logo" width="180">
</p>

<p align="center">
  A lightweight Windows application for converting, resizing, and inspecting images, with built-in DDS support and Windows Explorer integration.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-v0.2.0-blue" alt="Version">
  <img src="https://img.shields.io/badge/platform-Windows-0078D6" alt="Windows">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4" alt=".NET 8">
  <img src="https://img.shields.io/badge/license-GPL--3.0-blue" alt="GPL-3.0">
</p>

---

> Built to make image conversion as fast as possible without opening an image editor.

## Screenshots

<p align="center">
  <img src="docs/images/right-click-menu.png" alt="Pixoar Screenshot" width="900">
</p>

---

## Features

- Convert between PNG, JPG, WEBP, BMP, TIFF, and DDS
- Batch conversion and resizing
- Percentage and dimension based resizing
- DDS conversion with selectable compression
- Windows Explorer context menu integration
- Image information viewer
- Drag and drop support

---

## Supported Formats

| Format | Read | Convert | Preview |
| ------- | :--: | :-----: | :-----: |
| PNG | ✅ | ✅ | ✅ |
| JPG | ✅ | ✅ | ✅ |
| WEBP | ✅ | ✅ | ✅ |
| BMP | ✅ | ✅ | ✅ |
| TIFF | ✅ | ✅ | ✅ |
| DDS | ✅ | ✅ | ✅ |

---

## Download

Download the latest release from the **[Releases](https://github.com/Osama-AxJ/Pixoar/releases)** page.

The default release is framework dependent and requires the **.NET 8 Desktop Runtime (x64)**.

No installation is required.

Extract the .zip and run:

```text
Pixoar.exe
```

DDS support works out of the box using the bundled `texconv.exe`.

---

## DDS

Supported compression formats:

- DXT1
- DXT3
- DXT5
- BC7
- Uncompressed

Available options:

- Generate mipmaps
- Preserve alpha

---

## Windows Explorer Integration

Pixoar can add quick actions directly to the Windows right-click menu.

To enable it:

1. Open **Settings**.
2. Go to **Quick Actions**.
3. Enable **Context Menu**.
4. Select the actions you want to appear in the context menu.
5. Click **Apply Changes**.

Available actions:

- Resize
- Convert
- View image information
- Open in Pixoar