# Azure-Cove-Hotel

Hotel reservation website for **Azure Cove Hotel** (guest site) plus a separate **admin console**.

## Run the guest site (original)

```bash
dotnet run Program.cs
```

Requires the .NET 10 SDK. The original `Program.cs` is kept exactly as it was.

## Run the site + admin console (the copy)

```bash
dotnet run --file admin/Program.cs
```

(or `dotnet run admin/Program.cs`). Needs .NET SDK **10.0.300+** (uses the `#:include` directive) or .NET 11.
Open the site at the URL shown in the console (e.g. `http://localhost:5000`) and the admin console at **`/admin`**.

The admin console lives in its own files under `admin/Admin/` and shares the same live data as the guest site
(bookings made on the guest site appear in the admin console immediately).

## Admin console

- **Login:** first account is `admin` / `admin123` - you will be asked to change it at first sign-in.
- **Dark mode:** the **Dark mode / Light mode** button in the top bar switches the console theme; the preference is saved per account and survives restarts. Receipts and invoices stay white (paper look) in dark mode.
- **Roles:** Admin (everything), Manager (everything except Accounts Database), Front Desk (reservations, payments, billing, rooms, calendar, housekeeping, guests), Housekeeping (dashboard + housekeeping only).
- **Sidebar:** Dashboard, Reservations, Rooms & Inventory, Availability Calendar, Guests/Users, Payments, Billing/Invoicing, Staff Management, Housekeeping, Offers/Promotions, Rate Management, Reports, Email Templates, System Settings, Accounts Database, Audit Logs.
- **Receipts / invoices:** every transaction has a printable digital receipt (`/admin/receipt/{id}`) and every booking an itemized folio + printable invoice.

## Data

Everything (rooms, bookings, transactions, folios, staff accounts, offers, rates, settings, audit logs)
auto-saves to `admin/azure-cove-data.json` and reloads on the next start. Delete the file to reset to factory state.
Photos are served from the `assets/` folder in the repo root (also `wwwroot`, the `admin`
folder or the repo root still work, same as the original site).
The browser-tab icon (`azure-icon.jpeg`) comes from the assets folder.

## Notes

- E-mail sending is **demo mode**: templates render into an outbox; hooking up SMTP is a future step.
- Duplicate payment detection flags same-booking payments within 10 minutes and same guest/amount/method per day - review them in **Payments**.
- If you run both the original `Program.cs` and the copy at the same time, give one a different port:
  `ASPNETCORE_URLS=http://localhost:5050 dotnet run Program.cs`
