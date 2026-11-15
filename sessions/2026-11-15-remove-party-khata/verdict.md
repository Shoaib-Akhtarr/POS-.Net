# Roast Room: Remove or Keep Party Khata Screen

2026-11-15 · Seats: Wickman, Buffett, Graham, Hormozi, Musk · Rounds: 3 · Mode: Full Council

## Verdict: RESHAPE (Option C)

**The call:** Do not permanently delete the underlying supplier and cheque clearing engine, but immediately remove "Party Khata" from the main sidebar. It is creating visual drag and operational confusion at the front counter, while the backend debt tracking can be cleanly accessed under Purchases or Owner settings when needed.

Option scores: Option A (Flat Delete) 1.6 · Option B (Keep on Sidebar) 2.4 · Option C (Remove from Sidebar / Progressive Disclosure) 9.8 · Support for the resolution 10.0/10 · Confidence 5/5 · Spread 0 · Vote: 5 endorse, 0 amend, 0 dissent

## Scorecard

| Seat | Weight | Final stance | Opt A | Opt B | Opt C | Pillar score | Moved? | Their line |
|---|---|---|---|---|---|---|---|---|
| Wickman (ops) | 3 | Endorse | 2 | 2 | 10 | 9 | yes, to C | "Give your counter staff one clean path to follow and keep the back-office books in the back office." |
| Buffett (money) | 3 | Endorse | 0 | 3 | 10 | 10 | yes, to C | "Never fly blind on who you owe money to, but don't let your supplier cheques clutter the front counter." |
| Graham (product) | 2 | Endorse | 3 | 2 | 10 | 9 | yes, to C | "Great software feels effortless: hide what isn't used today and build only what users scream for." |
| Hormozi (offers) | 2 | Endorse | 1 | 2 | 10 | 10 | yes, to C | "Make the daily experience fast and friction-free, and keep your wholesale power in reserve." |
| Musk (speed) | 2 | Endorse | 2 | 2 | 10 | 10 | yes, to C | "Deleting the sidebar button removes 100% of the UI friction today while keeping your balance sheet intact." |

Math:
- Option A: (2*3 + 0*3 + 3*2 + 1*2 + 2*2) / 12 = 1.5
- Option B: (2*3 + 3*3 + 2*2 + 2*2 + 2*2) / 12 = 2.3
- Option C: (10*3 + 10*3 + 10*2 + 10*2 + 10*2) / 12 = 10.0

## Why the council landed here

- **Retail counter vs Supplier back-office:** "Customers / Khata" is for daily counter retail (who took Rs 2,000 paint on Udhaar). "Party Khata" is for wholesale suppliers (paying Rs 300,000 to Berger or Fast Cables with post-dated cheques). Putting both on the same sidebar makes cashiers confused and creates cognitive clutter (Wickman, Core Process).
- **The risk of flat deletion (Option A):** If you delete Party Khata completely, you delete your record of post-dated cheques (PDCs) and supplier credit. When a distributor deposits a 30-day cheque and it bounces, you risk supplier boycotts and frozen accounts (Buffett, Never Risk Ruin).
- **The algorithm's rule (Option C):** "Delete the part or process." If the user doesn't use Party Khata daily, delete it from the main UI immediately. But don't delete the database tables—keep the capability in reserve (Musk, The Algorithm).
- **Speed to value:** Progressive disclosure wins. Cashiers get a clean 5-button sidebar, while the shop owner keeps supplier books safe (Graham & Hormozi).

## Where they fought

- **Sidebar presence vs Total deletion:** Buffett initially wanted Party Khata kept visible because supplier debt can kill a store. Musk and Graham pushed back that visible clutter kills user adoption. The resolution satisfied both: remove the sidebar button immediately, preserve the database logic, and link supplier payables through Purchases.

## Minority report

There was no dissent on the final vote. The only unverified assumption is whether the store currently pays 100% cash upfront for all wholesale inventory. If the founder confirms they never write cheques and never take supplier credit, even the backend PDC engine can eventually be archived.

## Conditions

1. Remove the "Party Khata" button from `MainWindow.xaml` sidebar today.
2. Keep `KhataService.cs` and `KhataModels.cs` in the project so no database tables or migrations break.
3. Keep retail customer credit ("Customers / Khata") untouched.

## What to do next

1. This week: Remove the "Party Khata" navigation item from `MainWindow.xaml` to streamline the sidebar.
2. Verify: Ensure counter staff experience zero confusion between customer credit and vendor procurement.

---
Simulated advisors built from each person's public work. They are not the people and do not speak for them.
