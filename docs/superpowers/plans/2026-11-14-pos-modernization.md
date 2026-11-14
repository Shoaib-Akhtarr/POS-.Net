# POS Modernization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform the WPF .NET 10 POS application with a modern dashboard, high-ergonomics POS sales terminal, MaterialDesignThemes toolkit integration, smooth animated sidebar, toast notifications, product image upload & rendering, streamlined optional forms, and improved profile role system.

**Architecture:** MVVM architecture using CommunityToolkit.Mvvm and MaterialDesignThemes in XAML. Entity Framework Core with SQLite auto-migrations for new Product image schema. Centralized NotificationService for toasts, native WPF DoubleAnimation Storyboard for smooth sidebar sliding, and modular WPF UserControls with responsive layout grids.

**Tech Stack:** .NET 10 WPF, C# 13, CommunityToolkit.Mvvm 8.4.2, MaterialDesignThemes 5.4.0, MaterialDesignColors 5.4.0, Microsoft.EntityFrameworkCore.Sqlite 10.0.6.

**Spec:** file:///home/goharabbas/.gemini/antigravity/brain/897be989-d21c-4bbf-a0a4-dcfdcac61961/pos-modernization-plan.md

## Global Constraints
- Target Framework: `net10.0-windows` with `<EnableWindowsTargeting>true</EnableWindowsTargeting>`.
- All database queries must run without losing existing SQLite data (`DatabaseInitializer.EnsureColumns`).
- Product images stored in `%LocalAppData%/KarobarPOS/ProductImages/`.
- Maintain full keyboard accessibility (F1 for Dashboard, F2 for POS, F8 for Hold, F9/Ctrl+Enter for Checkout, Esc for modal closing).
- All modified code must build with 0 errors and 0 warnings.

## Review Focus
1. Product Image loading when image file path is invalid, missing, or empty: Fallback smoothly to department monogram/icon without crashing or throwing XAML image binding errors.
2. Sidebar slider toggle while rapid clicking: Storyboard animation must avoid jerky jumps or conflicting states.
3. Fast Walk-in POS checkout with empty optional fields: Checkout must complete instantaneously with default customer "Walk-in" and no blocking validations.
4. Stock procurement without supplier name: Default supplier to "General Supplier" and persist cleanly.
5. Role switching / Logout: Current active session must clear, navigate to PIN lock overlay, and prevent unauthorized views until valid PIN is verified.

---

### Task 1: Core Foundation & MaterialDesign Integration
**Files:**
- Modify: `POS/App.xaml`
- Create: `POS/Services/NotificationService.cs`
- Create: `POS/Converters/ImageFallbackConverter.cs`
- Modify: `POS/Models/Product.cs`
- Modify: `POS/Services/DatabaseInitializer.cs`

- [ ] **Step 1: Update Product Model and Database Initializer for ImagePath**
Add `public string? ImagePath { get; set; }` to `POS/Models/Product.cs` and add `["ImagePath"] = "TEXT NULL"` in `DatabaseInitializer.EnsureColumns(conn, "Products", ...)`.

- [ ] **Step 2: Create NotificationService and ImageFallbackConverter**
Implement `NotificationService` with `ShowSuccess(string)`, `ShowError(string)`, `ShowWarning(string)` using event or messenger messaging to trigger toast notifications. Implement `ImageFallbackConverter` for resilient image resolution.

- [ ] **Step 3: Integrate MaterialDesign Resources in App.xaml**
Merge MaterialDesignThemes ResourceDictionaries in `POS/App.xaml` while preserving custom application brushes and converters.

- [ ] **Step 4: Verify Build**
Run `dotnet build POS/POS.csproj` and confirm 0 errors.

---

### Task 2: Fix Sidebar Slider & Smooth Animation & Logout
**Files:**
- Modify: `POS/MainWindow.xaml`
- Modify: `POS/MainWindow.xaml.cs`
- Modify: `POS/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Fix Sidebar Width in MainWindow.xaml**
Remove local `Width="240"` from `<ColumnDefinition>` to allow dynamic width binding / animation.

- [ ] **Step 2: Add Smooth Storyboard Animation for Sidebar Slide**
Create a `Storyboard` with `DoubleAnimation` on sidebar Border width from 240px to 72px with `CubicEase`.

- [ ] **Step 3: Add Explicit Logout Feature**
Add `[RelayCommand] private void LogOut()` in `MainViewModel.cs` calling `SessionService.SignOut()` and opening the login overlay. Add a visible "Log Out" button with icon in the sidebar footer and header.

- [ ] **Step 4: Add Toast Notification Host Overlay**
Add a floating Toast Notification Card at the top-right of `MainWindow.xaml` bound to `NotificationService`.

- [ ] **Step 5: Verify Build & Shortcuts**
Run `dotnet build POS/POS.csproj` and verify build succeeds.

---

### Task 3: Modern Dashboard Redesign (HomeView & HomeViewModel)
**Files:**
- Modify: `POS/Views/HomeView.xaml`
- Modify: `POS/ViewModels/HomeViewModel.cs`

- [ ] **Step 1: Update HomeViewModel for Quick Actions and Top Products**
Ensure `HomeViewModel` exposes top selling products (with images, names, sales counts, progress percentages), revenue trends, and quick navigation commands.

- [ ] **Step 2: Redesign HomeView.xaml Header & Quick Actions**
Create modern greeting banner ("Welcome back, {CurrentUserName} 👋"), period selector pill group, and 4 quick action cards (`New Sale`, `Stock In`, `Party Khata`, `Installment Records`).

- [ ] **Step 3: Modernize KPI Cards & Visual Trend Chart**
Implement modern elevated cards with circular icon badges and trend badges. Polish the 7-day/monthly sales trend chart with rounded bars and peak labels.

- [ ] **Step 4: Add Top Selling Products List with Image Thumbnails**
Add a sleek right-hand column displaying top products with images, order count, and horizontal visual progress bars.

- [ ] **Step 5: Verify Build**
Run `dotnet build POS/POS.csproj` and ensure 0 warnings/errors.

---

### Task 4: High-Quality POS Sales Terminal Redesign (POSView & POSViewModel)
**Files:**
- Modify: `POS/Views/POSView.xaml`
- Modify: `POS/ViewModels/POSViewModel.cs`

- [ ] **Step 1: Update POSViewModel for Product Image Support & Enhanced Filtering**
Ensure products loaded into `FilteredProducts` display `ImagePath`, category chips include counts, and quick cash tender commands update `AmountPaid` and `ChangeReturned`.

- [ ] **Step 2: Redesign Left Catalog Section (65% width)**
Implement omnibox search with scanner icon, category filter pills slider, and modern card grid featuring product images, department chips, stock pills, and "+ Add" action.

- [ ] **Step 3: Redesign Right Order Summary & Checkout Panel (35% width)**
Create structured order ticket header, cart items with miniature image thumbnails and stepper quantity controls, financial breakdown with prominent Grand Total, cash denomination chips, and large "Process Checkout" button.

- [ ] **Step 4: Integrate Toast Notifications upon Checkout / Hold**
Trigger `NotificationService.ShowSuccess` on sale completion and cart holds.

- [ ] **Step 5: Verify Build**
Run `dotnet build POS/POS.csproj` and test.

---

### Task 5: Stock / Products Section Picture Upload & Optional Fields
**Files:**
- Modify: `POS/Views/ProductsView.xaml`
- Modify: `POS/ViewModels/ProductsViewModel.cs`
- Modify: `POS/Views/PurchasesView.xaml`
- Modify: `POS/ViewModels/PurchasesViewModel.cs`

- [ ] **Step 1: Add Image Upload Logic to ProductsViewModel**
Add `EditImagePath`, `BrowseImageCommand` using `OpenFileDialog`, copy selected image to `%LocalAppData%/KarobarPOS/ProductImages/`, and `RemoveImageCommand`.

- [ ] **Step 2: Add Image Preview & Upload Controls to Add/Edit Product Modal**
In `ProductsView.xaml`, add image box with preview and upload/remove buttons. Add thumbnail image column to the Products DataGrid.

- [ ] **Step 3: Streamline Forms and Make Fields Optional**
In `PurchasesViewModel.cs`, remove required validation for `SupplierName`, defaulting to `"General Supplier"`. In `ProductsViewModel.cs`, make `Brand`, `Barcode`, `PackUnitName`, and `DefaultWarranty` optional with clear labels and `(Optional)` tags.

- [ ] **Step 4: Verify Build**
Run `dotnet build POS/POS.csproj` and ensure 0 errors.

---

### Task 6: Role-Based Profile Improvement & Form Labeling Polish
**Files:**
- Modify: `POS/Views/ProfileView.xaml`
- Modify: `POS/ViewModels/ProfileViewModel.cs`

- [ ] **Step 1: Enhance ProfileViewModel with Roles & Shift Stats**
Add shift summary metrics for Cashier and clear role indicator.

- [ ] **Step 2: Organize ProfileView into Clear Modern Cards with Proper Labels**
Organize into distinct cards: User Profile & Shift Info, Store Branding, Thermal/A4 Printer, FBR Fiscal Invoicing, Staff Directory (Owner), and Backups (Owner). Ensure all form inputs have standard `FormLabelStyle` and clear optional tags.

- [ ] **Step 3: Add Profile Logout Action**
Add prominent Log Out button in the Profile view header.

- [ ] **Step 4: Full System Verification**
Run `dotnet build POS/POS.csproj` and `dotnet test`.
