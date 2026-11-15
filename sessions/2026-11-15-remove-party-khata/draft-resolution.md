# Roast Room Draft Resolution: Party Khata Screen

**Resolution:** Adopt **Option C (Progressive Disclosure / Streamlined Architecture)**. Do NOT delete the underlying B2B supplier and cheque clearing engine, but immediately remove the "Party Khata" button from the main navigation sidebar to eliminate counter friction.

## Verdict Label: RESHAPE & STREAMLINE (Option C)

### The Core Decision:
1. **Remove from Sidebar:** Delete the `<Button Command="{Binding NavigateToKhataCommand}" ToolTip="Party Khata">` from `MainWindow.xaml` so that counter staff and cashiers only see clean, essential operational workflows (`POS`, `Inventory`, `Customers`, `Purchases`, `Installments`, `Records`).
2. **Preserve the Engine:** Keep `KhataService.cs`, `KhataModels.cs`, and `KhataView.xaml` intact in the codebase.
3. **Consolidate with Purchases / Settings:** Expose supplier ledger and cheque tracking either as a sub-tab inside `Purchases` (where B2B vendor transactions actually occur) or as an Owner-only toggle in Profile/Settings for wholesale stores.

## Checkable Conditions
1. **Condition 1 (Immediate UI De-clutter):** The sidebar no longer displays "Party Khata" alongside "Customers / Khata".
2. **Condition 2 (Zero DB Breakage):** No database tables, migrations, or models are dropped or corrupted.
3. **Condition 3 (Owner Access):** If a shop owner writes post-dated cheques to suppliers, the cheque clearing module remains accessible under Owner tools or Purchases.

## Next Steps
- Remove the Party Khata button from `MainWindow.xaml`.
- Verify the build compiles cleanly with zero warnings or broken bindings.
- Test that `Customers` (Retail Udhaar) and `Purchases` continue functioning seamlessly.
