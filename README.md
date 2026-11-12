# 🛒 POS-.Net — Aizaz Fareed Center & Al-Kuwait Electronics

![POS Header](header.png)

A desktop Point of Sale (POS), inventory, and **Khata (credit ledger)** management system built with **C#**, **.NET 10 WPF**, and **SQLite (Entity Framework Core)**. Tailored for multi-department retail and wholesale operations across **Electric, Sanitary, Hardware, Tiles, Paint, Solar Systems, and Home Appliances**.

---

## ✨ Key Features

- **Multi-Department POS & Fast Catalog (`POSView`)**
  - Supports **7 core departments** (`Electric`, `Sanitary`, `Hardware`, `Tiles`, `Paint`, `Solar System`, `Electronics`) plus custom categories.
  - Switch between **Card Grid** and **Compact Table** views for large SKU catalogs.
  - **Barcode / SKU** search with `Enter`-to-add, brand filtering, and stock-level badges.
  - **Fractional Quantities & Units** (`pcs`, `m`, `ft`, `coil`, `box`, `sqft`, `kg`, `L`) plus a built-in **Box / Coil ↔ Loose Unit Pack Calculator**.
- **Flexible Counter Checkout**
  - Cart `+` / `-` steppers, editable decimal quantities, inline negotiated unit prices (with **Owner PIN override** if priced below cost), per-item & bill-level discounts, and **Cash Tendered / Change Return**.
  - Optional **Serial Number & Warranty** fields on electronics/solar items and **Contractor / Site Notes** on bills.
  - **Hold / Resume Bills** to pause an order while serving another customer.
- **Estimates (*Kacha Bill*) & Sale Returns (`RecordsView`)**
  - Save and print itemized **Estimates** without deducting stock or updating Khata, and convert them to a finalized **Sale** in one click.
  - Process partial or full **Sale Returns** that restore stock quantities and credit customer Khata balances while preventing duplicate over-returns.
- **Customer & Contractor Khata Ledger (`CustomersView`)**
  - Track customer/contractor balances, record full or partial payments (`Cash`, `Bank`, `JazzCash`, `EasyPaisa`), inspect itemized receipts, and print **Khata Ledger Statements**.
- **Dual Printing & Configurable Branding (`PrintService` + `ProfileView`)**
  - **80mm Thermal ESC/POS** receipt printing (with optional WPF bitmap raster mode for Urdu script) and **A4/A5 Invoice** printing via Windows `PrintDialog` (supports *Microsoft Print to PDF*).
  - Editable shop name, subtitle, address, phone numbers, receipt footer, and default printer in **Settings**.
- **Dashboard & Inventory Control (`HomeView`, `ProductsView`, `PurchasesView`)**
  - Period filters (`Today`, `Last 7 Days`, `Last 30 Days`, `All Time`), 7-day net sales chart (`LiveCharts.Wpf`), net profit calculation using sale-time `UnitCost` snapshots, and low-stock alert modals.
  - Full inventory add/edit/soft-delete and supplier restocking audit log.
- **Unified Themes, Bilingual UI & Data Safety**
  - Live **Light / Dark** Slate+Emerald theme switching.
  - Live **English / Urdu (`RightToLeft`)** language switching.
  - Automatic SQLite schema migration and one-click `.db` **Backup & Restore** (stored under `%LocalAppData%\KarobarPOS\`), plus automatic JSON sale/inventory archives.

---

## 🔐 Security, Roles & Default PINs

Sign-in is managed via PBKDF2-hashed PINs in [`SessionService`](POS/Services/SessionService.cs). Every sale, purchase, return, and Khata payment records the active staff member (`CreatedBy`).

| Seeded Profile | Role | Default PIN | Access Level |
| :--- | :--- | :--- | :--- |
| **Dr. Ehsan** | `Owner` | `1234` | Full access: Profit & Purchase metrics, Cost Prices, Product Add/Edit/Delete, Below-Cost Override, Staff PIN Management, DB Restore |
| **Sameer Younis** | `Owner` | `1234` | Full access (same as above) |
| **Counter Cashier** | `Cashier` | `0000` | Billing, Hold/Estimates, Returns, Customer Khata Payments, and Inventory view (Cost Price, Profit, Delete & Settings Admin hidden) |

> **Important:** Change the default Owner and Cashier PINs under **Settings → Staff & Owner PINs** before deploying to the shop counter.

---

## 🛠️ Tech Stack

- **Language:** C# (.NET 10)
- **UI Framework:** WPF (`net10.0-windows`, MVVM via `CommunityToolkit.Mvvm`)
- **Database:** SQLite with Entity Framework Core 10
- **Charts:** `LiveCharts.Wpf`
- **Architecture:** Single-project WPF MVVM (`POS.slnx` / `POS/POS.csproj`)

---

## 🚀 Getting Started

### Running on Windows (Recommended for Runtime & Printing)
**Prerequisites:**
- Windows 10 / 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (with **.NET Desktop Runtime**) or Visual Studio 2022/2026 with `.NET desktop development`

**Steps:**
1. Clone the repository:
   ```bash
   git clone https://github.com/Shoaib-Akhtarr/POS-.Net.git
   cd POS-.Net
   ```
2. Open `POS.slnx` in Visual Studio and press **F5**, **or** run from terminal:
   ```bash
   dotnet run --project POS/POS.csproj
   ```
3. On startup, select a staff profile and enter your PIN (or click **Quick Start as Counter Cashier**).
4. Configure your printer (`XP-80` or installed printer) and store details under **Settings**.

> **Database Location:** Active data is stored in `%LocalAppData%\KarobarPOS\pos.db`. If a legacy `pos.db` exists next to the executable, it is automatically imported and schema-upgraded on first launch.

### Building / Developing on Linux (Fedora / Ubuntu)
`POS/POS.csproj` enables `<EnableWindowsTargeting>true</EnableWindowsTargeting>` so you can edit and compile-check C# and XAML directly on Linux with `.NET 10 SDK` installed:
```bash
dotnet build POS/POS.csproj
```
*(WPF execution and Windows print spooler testing require Windows.)*

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `F1` | Open Dashboard |
| `F2` | Open POS & focus Product Search (Barcode `Enter` adds directly) |
| `F4` | Open POS & focus Customer Search |
| `F8` | Hold / Park current cart |
| `F9` or `Ctrl + Enter` | Complete & Print Sale |
| `Esc` | Close active modal or dropdown |

---

## 🤝 Contributing

Contributions and pull requests are welcome.

*Built with ❤️ by [Shoaib Akhtarr](https://github.com/Shoaib-Akhtarr)*
