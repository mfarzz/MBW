# MBW Project Setup

Panduan setup awal untuk **MBW (MailBlast Workspace)**, aplikasi desktop Windows untuk membuat, mem-preview, dan mengirim email massal berbasis workspace lokal.

## Target MVP

- Windows desktop application
- .NET 10
- WinUI 3 dengan Windows App SDK
- MVVM menggunakan CommunityToolkit.Mvvm
- Excel sebagai sumber data penerima
- HTML email editor menggunakan WebView2
- SMTP menggunakan MailKit
- SQLite untuk metadata dan send log
- Workspace portable dengan ekstensi `.mbw`

## Prasyarat

Install komponen berikut di Windows:

1. Visual Studio 2022 versi terbaru yang mendukung .NET 10.
2. Workload **.NET desktop development**.
3. Workload **Windows application development**.
4. Windows 10 SDK atau Windows 11 SDK.
5. .NET 10 SDK.
6. Git.

Verifikasi instalasi:

```powershell
dotnet --version
dotnet --list-sdks
```

Pastikan Windows App SDK dan template WinUI 3 tersedia di Visual Studio Installer.

## Struktur Solution

```text
MBW/
|-- MBW.slnx
|-- Directory.Build.props
|-- Directory.Packages.props
|-- README.md
|-- IDEA.md
|-- SETUP.md
|
|-- MBW.App/
|   |-- Views/
|   |-- ViewModels/
|   |-- Controls/
|   |-- Resources/
|   `-- MBW.App.csproj
|
|-- MBW.Core/
|   |-- Models/
|   |-- Interfaces/
|   |-- Services/
|   `-- MBW.Core.csproj
|
|-- MBW.Infrastructure/
|   |-- Excel/
|   |-- Email/
|   |-- Storage/
|   |-- Security/
|   |-- Workspace/
|   `-- MBW.Infrastructure.csproj
|
`-- MBW.Tests/
    |-- Core/
    |-- Infrastructure/
    `-- MBW.Tests.csproj
```

## Membuat Solution

Jalankan dari folder repository:

```powershell
dotnet new sln -n MBW

dotnet new classlib -n MBW.Core -f net10.0
dotnet new classlib -n MBW.Infrastructure -f net10.0
dotnet new mstest -n MBW.Tests -f net10.0
```

Project WinUI 3 sebaiknya dibuat melalui Visual Studio:

1. Buka Visual Studio.
2. Pilih **Create a new project**.
3. Cari template **Blank App, Packaged (WinUI 3 in Desktop)**.
4. Gunakan nama `MBW.App`.
5. Pilih target framework .NET 10 jika tersedia.
6. Simpan project di root repository.

Tambahkan project ke solution:

```powershell
dotnet sln MBW.sln add MBW.App\MBW.App.csproj
dotnet sln MBW.sln add MBW.Core\MBW.Core.csproj
dotnet sln MBW.sln add MBW.Infrastructure\MBW.Infrastructure.csproj
dotnet sln MBW.sln add MBW.Tests\MBW.Tests.csproj
```

## Referensi Antarproject

```powershell
dotnet add MBW.App\MBW.App.csproj reference MBW.Core\MBW.Core.csproj
dotnet add MBW.App\MBW.App.csproj reference MBW.Infrastructure\MBW.Infrastructure.csproj
dotnet add MBW.Infrastructure\MBW.Infrastructure.csproj reference MBW.Core\MBW.Core.csproj
dotnet add MBW.Tests\MBW.Tests.csproj reference MBW.Core\MBW.Core.csproj
dotnet add MBW.Tests\MBW.Tests.csproj reference MBW.Infrastructure\MBW.Infrastructure.csproj
```

Dependency direction:

```text
MBW.App -> MBW.Infrastructure -> MBW.Core
MBW.Tests -> MBW.Infrastructure dan MBW.Core
```

`MBW.Core` tidak boleh bergantung pada WinUI, MailKit, ClosedXML, atau SQLite.

## Dependensi Awal

Install package berikut pada project yang sesuai:

```powershell
dotnet add MBW.App package CommunityToolkit.Mvvm
dotnet add MBW.App package Microsoft.Web.WebView2

dotnet add MBW.Infrastructure package MailKit
dotnet add MBW.Infrastructure package ClosedXML
dotnet add MBW.Infrastructure package Microsoft.Data.Sqlite

dotnet add MBW.Tests package MSTest.TestFramework
dotnet add MBW.Tests package MSTest.TestAdapter
```

Catatan: gunakan versi stabil terbaru yang kompatibel dengan .NET 10. Versi package dikunci setelah project berhasil dibuat dan diuji.

## Batasan Arsitektur

### MBW.Core

Berisi kontrak dan logika domain yang tidak bergantung pada UI atau sistem eksternal:

- `WorkspaceModel`
- `EmailTemplate`
- `RecipientRow`
- `AttachmentMatch`
- `SendConfiguration`
- `SendResult`
- Interface untuk workspace, template, attachment, dan pengiriman email

### MBW.Infrastructure

Berisi implementasi akses eksternal:

- Import dan pembacaan Excel menggunakan ClosedXML
- Pengiriman email menggunakan MailKit
- Penyimpanan SQLite
- Penyimpanan dan pembukaan file workspace
- Integrasi Windows Credential Manager untuk credential SMTP
- Matching dan rename attachment

### MBW.App

Berisi UI dan orkestrasi interaksi pengguna:

- Navigation shell
- Workspace explorer
- Email editor
- Database preview
- Attachment panel
- Matching dan rename configuration
- Email preview
- Sending progress dan send log

## Format Workspace Awal

File `.mbw` dirancang sebagai paket workspace portable:

```text
Seminar-FTI.mbw/
|-- workspace.json
|-- email.html
|-- data/
|   `-- recipients.xlsx
|-- attachments/
`-- logs/
    `-- sending.log
```

Credential SMTP tidak disimpan di dalam workspace. Workspace hanya menyimpan identifier akun SMTP, sedangkan password disimpan melalui Windows Credential Manager.

## Urutan Implementasi MVP

1. Membuat solution dan project references.
2. Membuat model domain serta interface di `MBW.Core`.
3. Membuat workspace create/open/save.
4. Membuat import Excel dan preview data.
5. Membaca header Excel sebagai variable template.
6. Membuat editor HTML sederhana dan penyimpanan template.
7. Membuat attachment matching dan preview hasil rename.
8. Membuat email preview berdasarkan satu recipient.
9. Membuat SMTP test connection.
10. Membuat blast engine dengan delay, range, cancel, dan error handling.
11. Menyimpan send log dan mengekspor log ke CSV.
12. Menambahkan pengujian dan packaging aplikasi.

## Validasi Setup

Setelah semua project dibuat, jalankan:

```powershell
dotnet restore
dotnet build
dotnet test
```

Setup dianggap berhasil jika:

- Solution berhasil dibuka di Visual Studio.
- `MBW.App` dapat dijalankan sebagai aplikasi WinUI 3.
- Semua project berhasil di-build.
- Test project dapat dijalankan walaupun belum memiliki test case bisnis.

## Keputusan yang Ditunda

Hal berikut tidak perlu diputuskan sebelum fondasi MVP berjalan:

- Sinkronisasi workspace ke cloud
- Dukungan multi-user
- Database online
- Rich text editor tingkat lanjut
- Queue atau retry service terpisah
- Installer production
- Dukungan macOS atau Linux
