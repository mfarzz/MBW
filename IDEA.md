Setuju. **MBW — MailBlast Workspace** menurut saya sudah punya konsep yang jelas. Dan **tidak perlu PostgreSQL/online database** untuk versi awal. Bahkan untuk aplikasi seperti ini, saya justru menyarankan **local-first**.

## 🏗️ Rancangan MBW

![Image](https://images.openai.com/static-rsc-4/gVEcf7fCwEM1Nb208zLVXP5qnjZsFo0T5P2SmPCQgrh-v6L9HqoUjpHOL9uWzUiHWmZ28D24tn46JpkDlQJEoX73SbDo8_j0SwPBYn92R1n3MQD9m_23qj6lHyGwl6brLIKogN1qmTWxbN0iTxOU9K2ihW-UEZs5nf9VoDK7nDX4CQWkhWrh3XC_WskvI05X?purpose=fullsize)

![Image](https://images.openai.com/static-rsc-4/j-BuO_2AzuZAHktAAxUvB4zKYnhyvedlcfhF2hv6-PUnbUCOjSI7Qw9tupR_h8RVbDfoWr2te6UaW5KcQxgEcJF2Ymvx3kx-qs3PeNkUPQF6v2XrtcP269EzZL39TLvvvj62b2aDuAur1G3ounYYjE4dBS12HRdGS4sXJczhsU3Kk4zzHuVAssiUlcIiTAXq?purpose=fullsize)

![Image](https://images.openai.com/static-rsc-4/EwIYQAy0Mi0CO5dSWG4z0CSo5gc97ta43L9kOQonUZ9e7akirLEOuXJXR4JvRCoGl5HugpA3N5waTNZEvTC4_Ptr5VnALT9yzQTCD6k9iQ9iBq6_RmdiNqTMru8QP9nxKWACOqsxDidSjtg8VD6VLdfMHgKGrLMbwNAQO8-gW091lx6tVWhKNT2ziEz9jqW3?purpose=fullsize)

![Image](https://images.openai.com/static-rsc-4/DB-O1l31qALsniP2nvm33tkZ9K1mS8wWimv6HToWfcKPCZj-smHOcC1mGYkXRsHqunsLDJLVPvD7kghxScQXFu3W22tImUDpHfqkYxDi0d8vQoo5a9Gx1ZIO68ctUfcHjXPJYb-F-0I5-Ug39-CrsKqmcLvpRkAt0VETXgmB6lZsp9OKmIU83m1-GYJD8Uek?purpose=fullsize)

### Konsep utama

**MBW = Workspace-based desktop application**

Satu workspace merepresentasikan satu pekerjaan email blast.

```text
MBW
│
├── Application Settings
│   └── SMTP
│
└── Workspace
    ├── Email
    ├── Database
    ├── Attachments
    └── Configuration
```

SMTP adalah **global application setting**, sedangkan seluruh konfigurasi lainnya menjadi bagian dari workspace.

---

# 1. Apakah perlu PostgreSQL?

**Tidak.**

Untuk MBW, PostgreSQL malah berlebihan karena:

* aplikasi berjalan di komputer user
* tidak membutuhkan server
* tidak membutuhkan banyak user yang mengakses database yang sama
* data penerima berasal dari Excel
* workspace harus portable
* konfigurasi relatif kecil

Arsitektur awal cukup:

```text
Windows
   │
   └── MBW
       │
       ├── Workspace File
       ├── Local Files
       ├── SQLite
       └── SMTP
```

### Tapi apakah perlu SQLite?

**Opsional, tetapi saya merekomendasikan SQLite.**

Bukan untuk menyimpan data Excel.

SQLite digunakan untuk hal-hal seperti:

```text
Application
├── Recent Workspaces
├── UI preferences
└── SMTP metadata

Workspace
├── configuration
├── send history
└── session metadata
```

Sedangkan data penerima tetap:

```text
Excel
 ↓
MBW
 ↓
Memory
 ↓
Email Engine
```

Jadi:

> **Excel = source data**
> **SQLite = local application/workspace metadata**

---

# 2. Struktur Workspace

Saya menyarankan MBW menggunakan format workspace sendiri.

Misalnya:

```text
Seminar-FTI.mbw
```

Secara konsep:

```text
Seminar-FTI.mbw
│
├── workspace.json
├── email.html
│
├── data/
│   └── mahasiswa.xlsx
│
├── attachments/
│   ├── 001.pdf
│   ├── 002.pdf
│   └── ...
│
└── logs/
    └── sending.log
```

Ini membuat workspace **portable**.

User bisa:

```text
Seminar-FTI.mbw
```

dibuka kembali dan seluruh konfigurasi workspace dipulihkan.

---

# 3. Application Settings

Global:

```text
Settings
└── SMTP Accounts
    └── Campus
        ├── Host
        ├── Port
        ├── Username
        ├── Security
        └── Credential
```

Password jangan disimpan plaintext di `.mbw`.

Gunakan:

**Windows Credential Manager / Windows Credential Locker**

Jadi ketika workspace diberikan kepada orang lain:

```text
Workspace
    ↓
SMTP tidak membawa password
    ↓
User memasukkan credential SMTP sendiri
```

---

# 4. UI utama

Saya akan membuatnya sangat mirip konsep VS Code.

```text
┌───────────────────────────────────────────────────────────────┐
│ MBW    Seminar-FTI.mbw                         ● Ready        │
├───────────────┬───────────────────────────────────────────────┤
│               │                                               │
│ WORKSPACE     │                 EMAIL EDITOR                  │
│               │                                               │
│ 📄 Email      │ Subject                                       │
│               │ [Undangan Seminar {Nama}]                     │
│ 📊 Database   │                                               │
│  └ mahasiswa  │ ┌─────────────────────────────────────────┐   │
│     .xlsx     │ │ B I U  14px  ≡  🔗  { }               │   │
│               │ ├─────────────────────────────────────────┤   │
│ 📎 Attachments│ │                                         │   │
│  └ 1,245 files│ │ Yth. {Nama},                            │   │
│               │ │                                         │   │
│ ⚙ Configuration│ │ Dengan hormat,                          │   │
│               │ │                                         │   │
│  ├ Matching   │ │ Kami mengundang Anda...                 │   │
│  ├ Rename     │ │                                         │   │
│  └ Sending    │ └─────────────────────────────────────────┘   │
│               │                                               │
├───────────────┴───────────────────────────────────────────────┤
│ SMTP: Connected │ DB: 1,245 │ Attachment: 1,245 │ Ready       │
└───────────────────────────────────────────────────────────────┘
```

---

# 5. Email Editor

Tidak perlu mencoba menjadi Word.

Fokus ke kebutuhan email:

* Bold
* Italic
* Underline
* Font size
* Font family
* Alignment
* Bullet
* Numbering
* Hyperlink
* Insert variable
* Undo/redo

Contoh variable:

```text
{Nama}
{NIM}
{Fakultas}
{Program_Studi}
{Email}
```

Variable berasal otomatis dari header Excel.

---

# 6. Database Panel

User drag/drop Excel:

```text
📊 Database
└── mahasiswa.xlsx
```

Klik:

```text
Database Preview

Rows: 1,245

┌───────┬──────────────┬──────────────────┐
│ NIM   │ Nama         │ Email            │
├───────┼──────────────┼──────────────────┤
│ 001   │ Fariz        │ fariz@...        │
│ 002   │ Ahmad        │ ahmad@...        │
│ ...   │ ...          │ ...              │
└───────┴──────────────┴──────────────────┘
```

Kemudian MBW mengenali:

```text
Available Variables

{NIM}
{Nama}
{Email}
{Fakultas}
```

---

# 7. Attachment Panel

```text
📎 Attachments
└── attachments/
    ├── 001.pdf
    ├── 002.pdf
    ├── 003.pdf
    └── ...
```

Ada dua mode:

### Individual

```text
● Different attachment per recipient
○ Same attachment for everyone
```

### Same

```text
○ Different attachment per recipient
● Same attachment for everyone
```

---

# 8. Matching

```text
MATCHING

Database column:
[ NIM ▼ ]

File pattern:
[ {NIM}.pdf ]

Result:

✓ 1,240 matched
⚠ 5 missing
```

Klik `5 missing`:

```text
Missing Attachments

005
023
087
421
998
```

Ini sangat membantu sebelum blast.

---

# 9. Rename

```text
RENAME ATTACHMENT

Original:
{NIM}.pdf

Rename:
Surat_{NIM}_{Nama}.pdf

Preview:

001.pdf
↓
Surat_001_Muhammad Fariz.pdf
```

---

# 10. Sending

```text
SEND CONFIGURATION

Email:
[ Email ▼ ]

Subject:
[ Undangan Seminar - {Nama} ]

Range:
From [ 1 ] To [ 1245 ]

Delay:
[ 5 ] seconds

☑ Stop on error
☑ Confirm before sending
```

---

# 11. Preview

Ini saya anggap **mandatory feature**.

```text
EMAIL PREVIEW

Recipient:
Muhammad Fariz
fariz@...

Subject:
Undangan Seminar - Muhammad Fariz

Attachment:
Surat_001_Muhammad Fariz.pdf

────────────────────────

Yth. Muhammad Fariz,

Dengan hormat,
Kami mengundang Anda...
```

Navigasi:

```text
← Previous       1 / 1,245       Next →
```

---

# 12. Blast Engine

Secara internal:

```text
Excel
 ↓
Row
 ↓
Variable Resolver
 ↓
Template Renderer
 ↓
Attachment Matcher
 ↓
Attachment Renamer
 ↓
SMTP Sender
 ↓
Delay
 ↓
Next Row
```

Contoh:

```text
Row #001
   │
   ├── Email = fariz@...
   ├── Nama = Muhammad Fariz
   ├── NIM = 001
   │
   ↓
Render
   │
   ↓
Subject
"Undangan Seminar - Muhammad Fariz"
   │
   ↓
Attachment
001.pdf
   │
   ↓
Rename
Surat_001_Muhammad Fariz.pdf
   │
   ↓
SMTP
   │
   ↓
SUCCESS ✓
```

---

# 13. Send Log

Setelah blast:

```text
SEND LOG

✓ 001  Muhammad Fariz     Sent
✓ 002  Ahmad              Sent
✓ 003  Budi               Sent
✗ 004  Citra              Failed
✓ 005  Dimas              Sent
```

Bisa ditambahkan:

```text
Export Log → CSV
```

---

# 14. Teknologi

Saya akan menggunakan:

```text
┌──────────────────────────────┐
│          MBW Desktop         │
├──────────────────────────────┤
│ C#                           │
│ .NET 10                      │
│ WinUI 3                      │
│ MVVM                         │
│ CommunityToolkit.Mvvm        │
├──────────────────────────────┤
│ MailKit                      │
│ ClosedXML                    │
│ SQLite                       │
│ WebView2                     │
└──────────────────────────────┘
```

### Kenapa WebView2?

Untuk email editor, HTML adalah format yang sangat natural.

Kita bisa:

```text
Rich Editor
    ↓
HTML
    ↓
Email
```

Jadi email yang dikirim memang menggunakan HTML.

---

# 15. Arsitektur project C#

Saya akan pisahkan:

```text
MBW/
│
├── MBW.App/
│   ├── Views/
│   ├── ViewModels/
│   └── Controls/
│
├── MBW.Core/
│   ├── Models/
│   ├── Interfaces/
│   └── Services/
│
├── MBW.Infrastructure/
│   ├── Excel/
│   ├── Email/
│   ├── Storage/
│   └── Security/
│
└── MBW.Tests/
```

Dengan prinsip:

```text
WinUI 3
   ↓
ViewModel
   ↓
Core
   ↓
Services
   ├── Excel
   ├── SMTP
   ├── Attachment
   ├── Template
   └── Workspace
```

---

# 16. Tidak perlu backend

Ini bagian yang menurut saya penting:

**MBW tidak membutuhkan:**

```text
❌ Node.js server
❌ Express
❌ Laravel
❌ PostgreSQL server
❌ Redis
❌ Cloud
❌ REST API
```

Aplikasinya:

```text
Windows PC
     │
     ↓
    MBW
     │
     ├── Excel
     ├── Local Workspace
     ├── SQLite
     └── SMTP
```

Email langsung:

```text
MBW
 ↓
SMTP Provider
 ↓
Recipient
```

Jadi **offline untuk semua proses kecuali saat mengirim email**.

---

## Rancangan final MBW

Saya akan mengunci konsepnya seperti ini:

> **MBW (MailBlast Workspace)** adalah aplikasi desktop Windows untuk membuat, mengonfigurasi, mem-preview, dan mengirim email massal berbasis workspace.

**Global:**

> SMTP saja.

**Per workspace:**

> Email + Excel + Attachment + Matching + Rename + Sending Configuration.

**Storage:**

> Workspace lokal + SQLite untuk metadata/log bila diperlukan.

**Database online:**

> **Tidak diperlukan.**

Dan menurut saya pendekatan ini jauh lebih bagus untuk MVP karena kita bisa fokus dulu pada **UX dan email-blasting engine**, tanpa membebani proyek dengan backend/server.
