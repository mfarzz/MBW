# MBW — MailBlast Workspace

**MBW** adalah aplikasi desktop Windows untuk membuat, mem-preview, dan mengirim email massal (mail merge) berbasis **workspace lokal**. Setiap workspace mewakili satu kampanye email blast: template HTML, data penerima dari Excel, lampiran, dan konfigurasi pengiriman disimpan bersama dalam satu folder portable.

## Fitur

### Workspace
- Buat, buka, dan simpan workspace sebagai folder lokal
- Daftar proyek terbaru di layar Welcome
- Auto-save pengaturan halaman Send ke workspace

### Email Editor
- Editor HTML berbasis **WebView2**
- Subject dan isi email dengan variabel template dari kolom Excel (`{{Nama}}`, dll.)
- Preview email per baris penerima

### Database
- Import data penerima dari file **Excel** (`.xlsx`)
- Pilih sheet dan baris header
- Preview tabel data di dalam aplikasi

### Attachments
- Lampiran **shared** (sama untuk semua penerima)
- Lampiran **individual** dengan matching otomatis per baris
- Pola rename file lampiran dengan variabel template

### Send
- Kirim ke semua penerima atau **range baris** tertentu
- **Delay** antar email (throttling)
- Dialog konfirmasi sebelum kirim
- Progress per baris (sending / sukses / gagal / skip)
- **Retry failed** — kirim ulang hanya baris yang gagal
- **Export log** ke CSV/TXT
- Ringkasan akhir: `12 sent · 2 failed · 1 skipped`
- Dukungan **cancel** saat pengiriman berjalan

### SMTP
- Konfigurasi SMTP global (bukan per workspace)
- Pengiriman nyata via **MailKit**
- Password disimpan aman di **Windows Credential Manager**
- Tes koneksi SMTP dari aplikasi

## Tech Stack

| Lapisan | Teknologi |
|---------|-----------|
| UI | WinUI 3, Windows App SDK, WebView2 |
| Arsitektur | MVVM (CommunityToolkit.Mvvm) |
| Runtime | .NET 10 |
| Email | MailKit, MimeKit |
| Excel | ClosedXML |
| Platform | Windows 10/11 (x64) |

## Prasyarat

- Windows 10 (build 17763+) atau Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 dengan workload:
  - **.NET desktop development**
  - **Windows application development**

Verifikasi:

```powershell
dotnet --version
dotnet --list-sdks
```

## Memulai

### Clone & build

```powershell
git clone <url-repo> MBW
cd MBW
dotnet restore
dotnet build MBW.App/MBW.App.csproj
```

### Menjalankan aplikasi

**Visual Studio:** buka `MBW.slnx`, set `MBW.App` sebagai startup project, tekan F5.

**CLI:**

```powershell
dotnet run --project MBW.App/MBW.App.csproj
```

### Menjalankan tes

```powershell
dotnet test MBW.Tests/MBW.Tests.csproj
```

## Alur Kerja

```
Welcome → Buat/Buka Workspace
    ↓
Email      → Tulis template HTML + subject
Database   → Import Excel penerima
Attachments→ Kelola lampiran shared & individual
Configuration → Pengaturan workspace
SMTP       → Atur akun pengirim (menu Workspace → SMTP)
Send       → Preview, atur range/delay, kirim
```

### Pintasan keyboard

| Pintasan | Aksi |
|----------|------|
| `Ctrl+N` | Workspace baru |
| `Ctrl+O` | Buka workspace |
| `Ctrl+S` | Simpan workspace |
| `Alt+F4` | Keluar |

## Format Workspace

Workspace disimpan sebagai folder (bukan file tunggal). Contoh struktur:

```text
Seminar-FTI/
├── workspace.json          # Metadata workspace & konfigurasi
├── email.html              # Isi template email
├── data/
│   └── recipients.xlsx     # Data penerima (opsional, path di metadata)
├── attachments/
│   ├── shared/             # Lampiran untuk semua penerima
│   └── individual/         # Lampiran per penerima
└── logs/
    └── sending.log         # Log pengiriman (jika ada)
```

> Password SMTP **tidak** disimpan di dalam workspace. Hanya disimpan di Windows Credential Manager.

## Struktur Solution

```text
MBW/
├── MBW.App/              # WinUI 3 — Views, ViewModels, Controls
├── MBW.Core/             # Model domain, interface, layanan koordinasi
├── MBW.Infrastructure/   # Excel, MailKit, storage, SMTP
├── MBW.Tests/            # Unit test (MSTest)
├── MBW.slnx
├── SETUP.md              # Panduan setup solution dari awal
├── DESIGN-GUIDE.md       # Panduan UI WinUI 3
└── IDEA.md               # Dokumen konsep produk
```

### Dependency

```text
MBW.App → MBW.Infrastructure → MBW.Core
MBW.Tests → MBW.Infrastructure, MBW.Core
```

`MBW.Core` tidak bergantung pada WinUI, MailKit, atau ClosedXML.

## Arsitektur Singkat

| Project | Tanggung jawab |
|---------|----------------|
| **MBW.Core** | `WorkspaceModel`, `EmailTemplate`, `RecipientRow`, `SendConfiguration`, interface `IEmailSender`, `IStorageService`, dll. |
| **MBW.Infrastructure** | `ExcelImporter`, `MailKitEmailSender`, `StorageService`, `AttachmentService`, `SmtpSettingsService` |
| **MBW.App** | Shell navigasi, halaman UI, gateway dialog WinUI (`WinUiSendGateway`, dll.) |

## Pengembangan

Dokumentasi tambahan:

- [SETUP.md](SETUP.md) — setup solution, package, dan urutan implementasi MVP
- [DESIGN-GUIDE.md](DESIGN-GUIDE.md) — token warna, layout shell, konvensi UI
- [IDEA.md](IDEA.md) — visi produk dan keputusan arsitektur

### Build platform

Default platform: **x64** (`Directory.Build.props`). Publish profile tersedia di `MBW.App/Properties/PublishProfiles/`.

## Catatan Deliverability

Jika email masuk folder spam, periksa konfigurasi **SPF/DKIM/DMARC** di domain pengirim, hindari konten yang memicu filter spam, dan uji dengan beberapa provider (Gmail, Outlook, dll.). Ini terkait infrastruktur email, bukan bug aplikasi.

## Lisensi

Belum ditentukan. Tambahkan file `LICENSE` jika proyek ini akan dipublikasikan.
