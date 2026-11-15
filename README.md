# Azure-Cove-Hotel

Hotel reservation website for **Azure Cove Hotel** (guest site) plus a separate **admin console** —
one single-file app, one live data set.

## Run

```bash
dotnet run --file Program.cs
```

(or `dotnet run Program.cs`). Needs .NET SDK **10.0.300+** (uses the `#:include` directive) or .NET 11.
Open the site at the URL shown in the console (e.g. `http://localhost:5000`) and the admin console at **`/admin`**.

## Structure

- `Program.cs` — the whole guest website (landing page with room carousel, booking with
  confirmation prompts and per-method payment flows, my bookings, cancellation, support)
- `Admin/` — the admin console modules (wired into the same app by the `AdminConsole.Map` hook
  in `Program.cs`); bookings made on the guest site appear in the admin console immediately
- `assets/` — all site pictures (room photos, hero, logo, icon); served through `/photo/...`

## Admin console

- **Login:** first account is `admin` / `admin123` - you will be asked to change it at first sign-in.
- **Dark mode:** the **Dark mode / Light mode** button in the top bar switches the console theme; the preference is saved per account and survives restarts. Receipts and invoices stay white (paper look) in dark mode.
- **Roles:** Admin (everything), Manager (everything except Accounts Database), Front Desk (reservations, payments, billing, rooms, calendar, housekeeping, guests), Housekeeping (dashboard + housekeeping only).
- **Sidebar:** Dashboard, Reservations, Rooms & Inventory, Availability Calendar, Guests/Users, Payments, Billing/Invoicing, Staff Management, Housekeeping, Offers/Promotions, Rate Management, Reports, Email Templates, System Settings, Accounts Database, Audit Logs.
- **Receipts / invoices:** every transaction has a printable digital receipt (`/admin/receipt/{id}`) and every booking an itemized folio + printable invoice.

## Data

Everything (rooms, bookings, transactions, folios, staff accounts, offers, rates, settings, audit logs)
auto-saves to `azure-cove-data.json` in the repo root and reloads on the next start. Delete the file to reset to factory state.
Pictures are served from the `assets/` folder (also `wwwroot` or the repo root still work).
The browser-tab icon (`azure-icon.jpeg`) comes from the assets folder.

## Notes

- E-mail sending is **demo mode**: templates render into an outbox; hooking up SMTP is a future step.
- Duplicate payment detection flags same-booking payments within 10 minutes and same guest/amount/method per day - review them in **Payments**.
- If the port is taken, run on another one:
  `ASPNETCORE_URLS=http://localhost:5050 dotnet run --file Program.cs`
