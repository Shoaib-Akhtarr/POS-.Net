# Roast Room Brief: Remove or Keep Party Khata Page

**Decision:** Should Karobar POS permanently delete the "Party Khata" page, or does it serve an indispensable business function?

## Options

- **Option A (Delete entirely):** Remove the Party Khata view, viewmodel, and sidebar link completely. Only keep the retail "Customers / Khata" page.
- **Option B (Keep as-is):** Keep Party Khata as a dedicated module for B2B supplier payables, distributor credit ledgers, Roznamcha (cash book), and post-dated cheque (PDC) clearing/bouncing.
- **Option C (Unified & Progressive Disclosure):** Keep the underlying supplier payable and PDC logic, but streamline it: integrate supplier tracking into Purchases, or hide Party Khata behind an optional "Supplier Ledger / PDC" feature toggle in Settings so small cash-only retail users don't see clutter, but credit/wholesale stores don't lose supplier debt tracking.

## The Business & Context

- **Business:** Karobar POS (`Aizaz Fareed Center - POS`), a desktop POS built for wholesale & retail shops (Electric, Sanitary, Hardware, Tiles, Paint, Solar & Electronics in Pakistan).
- **Core Workflows:**
  1. Retail sales (Cash, Udhaar/Credit to customers, Installment plans).
  2. Inventory & Stock (SKUs, units, cost vs selling prices).
  3. Purchases from distributors (Berger, Clipsal, Dawlance, Diamond Paints, Fast Cables).
- **Current State of "Party Khata":**
  - Manages B2B supplier/distributor payables (Adaigi / Cash Out).
  - Handles customer credit recovery (Wasooli / Cash In) and Roznamcha daily cash book.
  - Manages Post-Dated Cheques (PDCs) with clearing dates, bank names, and status (Cleared / Bounced).
- **Founder's Lean:** The founder asks: *"is the party khata page have any work? i don't think i need it!"* They are leaning toward deleting it because they feel it may be unnecessary bloat or duplicate work alongside the Customers page.

## The Board & Weights

| Seat | Pillar | Weight | Reason |
|---|---|---|---|
| Wickman | Operations and scaling | 3 | Owns core operational process, reducing cashier confusion, and avoiding redundant screens |
| Buffett | Money and capital | 3 | Owns supplier debt, payables float, PDC risk, and knowing who you owe money to |
| Graham | Product and startups | 2 | Owns feature creep vs real demand; tests if users actually want or use this page |
| Hormozi | Offers and cash flow | 2 | Owns speed to cash, receivables vs payables cycle, and operational focus |
| Musk | First principles & execution speed | 2 | Owns aggressive deletion of unnecessary requirements and questioning every screen |
