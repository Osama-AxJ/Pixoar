# Pixoar

<p align="center">
  <img src="Pixoar.App/Resources/Assets/Branding/pixoar-logo.png" alt="Pixoar Logo" width="180">
</p>

<p align="center">
  A lightweight native Windows utility for fast image conversion and resizing, with DDS support and Windows Explorer integration.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-v0.1.0-blue" alt="Version">
  <img src="https://img.shields.io/badge/platform-Windows-0078D6" alt="Windows">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4" alt=".NET 8">
  <img src="https://img.shields.io/badge/license-GPLv3-blue" alt="GPL v3">
</p>

---

> Built to make image conversion as fast as possible without opening an editor.

## Screenshots

<p align="center">
  <img src="docs/images/right-click-menu.png" alt="Pixoar Screenshot" width="900">
</p>

---

## Features

- Fast image conversion
- Batch conversion
- Percentage and dimension based resizing
- DDS conversion and preview
- Right click menu integration
- Image information viewer
- Drag and drop support

---

## Supported Formats

| Format | Read | Convert | Preview |
| ------ | :--: | :-----: | :-----: |
| PNG | ✅ | ✅ | ✅ |
| JPG / JPEG | ✅ | ✅ | ✅ |
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