// Admin/AdminData.cs
// Data store for the admin console: extra models, JSON persistence,
// pricing engine (seasonal rates, length-of-stay discounts, promos, tax),
// date-based availability, folios, e-mail rendering and the audit trail.
//
// The guest-side lists (rooms/bookings/transactions/tickets) are created in
// admin/Program.cs (a copy of the original Program.cs) and handed over here,
// so the admin console and the guest site share the same live data.
using System.Net;
using System.Text.Json;

public class AdminUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Front Desk";
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime? LastLoginAt { get; set; }
    public string LastLoginIp { get; set; } = "";
    public bool MustChangePassword { get; set; }
    public int FailedLogins { get; set; }
    public DateTime? LockoutUntil { get; set; }
    public string Theme { get; set; } = "light"; // light | dark (admin console appearance)
}

public class AuditEntry
{
    public int Id { get; set; }
    public DateTime Time { get; set; }
    public string Username { get; set; } = "";
    public string Role { get; set; } = "";
    public string Action { get; set; } = "";
    public string Details { get; set; } = "";
    public string Ip { get; set; } = "";
}

public class FolioItem
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = "";
    public string Type { get; set; } = "charge"; // charge | discount | tax | extra | payment | refund
    public int Amount { get; set; }              // always stored positive
}

public class Folio
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public int InvoiceNo { get; set; }
    public List<FolioItem> Items { get; set; } = new();
}

public class Offer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "percent"; // percent | fixed
    public int Value { get; set; }
    public int RoomId { get; set; }               // 0 = every room
    public string Code { get; set; } = "";        // empty = auto-applied when eligible
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public int MinNights { get; set; } = 1;
    public bool Enabled { get; set; } = true;
}

public class SeasonalRate
{
    public int Id { get; set; }
    public int RoomId { get; set; }               // 0 = every room
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int PercentAdjust { get; set; }        // +20 = peak season, -15 = low season
}

public class LosDiscount   // length-of-stay discount
{
    public int Id { get; set; }
    public int MinNights { get; set; }
    public int PercentOff { get; set; }
}

public class BlackoutDate
{
    public int Id { get; set; }
    public int RoomId { get; set; }               // 0 = every room
    public DateTime Date { get; set; }
}

public class HousekeepingTask
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public string Description { get; set; } = "";
    public string Assignee { get; set; } = "";
    public DateTime Due { get; set; }
    public string Status { get; set; } = "Pending"; // Pending | In progress | Done
}

public class EmailTemplate
{
    public int Id { get; set; }
    public string Key { get; set; } = "";      // booking_confirmation | cancellation | reminder
    public string Name { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
}

public class OutboxEmail
{
    public int Id { get; set; }
    public string Template { get; set; } = "";
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime Created { get; set; }
}

public class SystemSettings
{
    public string HotelName { get; set; } = "Azure Cove Hotel";
    public string ContactEmail { get; set; } = "frontdesk@azurecovehotel.com";
    public string ContactPhone { get; set; } = "+63 917 000 0000";
    public string Address { get; set; } = "Cove Bay, Philippines";
    public string TaxName { get; set; } = "VAT";
    public int TaxPercent { get; set; } = 12;
    public string CurrencyCode { get; set; } = "PHP";
    public string CurrencySymbol { get; set; } = "PHP ";
    public int BookingCutoffHour { get; set; } = 18;
    public int FreeCancelHours { get; set; } = 48;
    public int CancelFeePercent { get; set; } = 0;
    public string CheckInTime { get; set; } = "14:00";
    public string CheckOutTime { get; set; } = "12:00";
    public string CancellationPolicy { get; set; } = "Free cancellation up to 48 hours before check-in. Cancellations after that may be charged a fee. Refunds go back to the original payment method.";
}

public class QuoteLine
{
    public DateTime Date { get; set; }
    public int Rate { get; set; }
}

public class Quote
{
    public List<QuoteLine> Lines { get; set; } = new();
    public int Subtotal { get; set; }
    public int LosPercent { get; set; }
    public int LosAmount { get; set; }
    public string OfferName { get; set; } = "";
    public int OfferAmount { get; set; }
    public int Tax { get; set; }
    public int Total { get; set; }
    public string Error { get; set; } = "";
    public int Discount => LosAmount + OfferAmount;
}

public class SavedData
{
    public List<Room> Rooms { get; set; } = new();
    public List<Booking> Bookings { get; set; } = new();
    public List<Transaction> Transactions { get; set; } = new();
    public List<Ticket> Tickets { get; set; } = new();
    public List<string> PaymentMethods { get; set; } = new();
    public List<AdminUser> Admins { get; set; } = new();
    public List<AuditEntry> AuditLog { get; set; } = new();
    public List<Folio> Folios { get; set; } = new();
    public List<Offer> Offers { get; set; } = new();
    public List<SeasonalRate> SeasonalRates { get; set; } = new();
    public List<LosDiscount> LosDiscounts { get; set; } = new();
    public List<BlackoutDate> Blackouts { get; set; } = new();
    public List<HousekeepingTask> Tasks { get; set; } = new();
    public List<EmailTemplate> Templates { get; set; } = new();
    public List<OutboxEmail> Outbox { get; set; } = new();
    public SystemSettings Settings { get; set; } = new();
    public Dictionary<string, bool> Gateways { get; set; } = new();
    public List<int> DismissedDupes { get; set; } = new();
    public int NextInvoiceNo { get; set; } = 1;
}

public static class AdminData
{
    public static string DataPath = "azure-cove-data.json";

    // Shared with the guest site (same list objects as admin/Program.cs)
    public static List<Room> Rooms = new();
    public static List<Booking> Bookings = new();
    public static List<Transaction> Transactions = new();
    public static List<Ticket> Tickets = new();
    public static List<string> PaymentMethods = new();

    // Admin-only state
    public static List<AdminUser> Admins = new();
    public static List<AuditEntry> AuditLog = new();
    public static List<Folio> Folios = new();
    public static List<Offer> Offers = new();
    public static List<SeasonalRate> SeasonalRates = new();
    public static List<LosDiscount> LosDiscounts = new();
    public static List<BlackoutDate> Blackouts = new();
    public static List<HousekeepingTask> Tasks = new();
    public static List<EmailTemplate> Templates = new();
    public static List<OutboxEmail> Outbox = new();

    public static SystemSettings Settings = new();
    public static Dictionary<string, bool> Gateways = new();
    public static List<int> DismissedDupes = new();
    public static int NextInvoiceNo = 1;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static void Initialize(string contentRoot, List<Room> rooms, List<Booking> bookings,
        List<Transaction> transactions, List<Ticket> tickets, List<string> paymentMethods)
    {
        Rooms = rooms;
        Bookings = bookings;
        Transactions = transactions;
        Tickets = tickets;
        PaymentMethods = paymentMethods;
        DataPath = Path.Combine(contentRoot, "azure-cove-data.json");
        Load();
    }

    // ---------- persistence ----------

    public static void Load()
    {
        if (!File.Exists(DataPath)) { Seed(); Save(); return; }
        try
        {
            SavedData? d = JsonSerializer.Deserialize<SavedData>(File.ReadAllText(DataPath), JsonOpts);
            if (d == null) { Seed(); Save(); return; }

            CopyInto(Rooms, d.Rooms);
            CopyInto(Bookings, d.Bookings);
            CopyInto(Transactions, d.Transactions);
            CopyInto(Tickets, d.Tickets);
            CopyInto(PaymentMethods, d.PaymentMethods.Count > 0 ? d.PaymentMethods : PaymentMethods);
            CopyInto(Admins, d.Admins);
            CopyInto(AuditLog, d.AuditLog);
            CopyInto(Folios, d.Folios);
            CopyInto(Offers, d.Offers);
            CopyInto(SeasonalRates, d.SeasonalRates);
            CopyInto(LosDiscounts, d.LosDiscounts);
            CopyInto(Blackouts, d.Blackouts);
            CopyInto(Tasks, d.Tasks);
            CopyInto(Templates, d.Templates);
            CopyInto(Outbox, d.Outbox);
            CopyInto(DismissedDupes, d.DismissedDupes);

            Settings = d.Settings ?? new SystemSettings();
            Gateways = d.Gateways ?? new Dictionary<string, bool>();
            foreach (string m in PaymentMethods) if (!Gateways.ContainsKey(m)) Gateways[m] = true;
            NextInvoiceNo = d.NextInvoiceNo < 1 ? 1 : d.NextInvoiceNo;
            FixUps();
        }
        catch
        {
            // If the file is unreadable we fall back to fresh seed data.
            Rooms.Clear(); Bookings.Clear(); Transactions.Clear(); Tickets.Clear();
            Folios.Clear(); Offers.Clear(); SeasonalRates.Clear(); LosDiscounts.Clear();
            Blackouts.Clear(); Tasks.Clear(); Templates.Clear(); Outbox.Clear();
            Admins.Clear(); AuditLog.Clear(); DismissedDupes.Clear();
            Settings = new SystemSettings();
            Seed();
        }
        Save();
    }

    public static void Save()
    {
        try
        {
            SavedData d = new()
            {
                Rooms = Rooms, Bookings = Bookings, Transactions = Transactions, Tickets = Tickets,
                PaymentMethods = PaymentMethods, Admins = Admins, AuditLog = AuditLog, Folios = Folios,
                Offers = Offers, SeasonalRates = SeasonalRates, LosDiscounts = LosDiscounts,
                Blackouts = Blackouts, Tasks = Tasks, Templates = Templates, Outbox = Outbox,
                Settings = Settings, Gateways = Gateways, DismissedDupes = DismissedDupes,
                NextInvoiceNo = NextInvoiceNo
            };
            File.WriteAllText(DataPath, JsonSerializer.Serialize(d, JsonOpts));
        }
        catch (Exception ex)
        {
            Console.WriteLine("AdminData.Save failed: " + ex.Message);
        }
    }

    private static void CopyInto<T>(List<T> target, List<T>? source)
    {
        target.Clear();
        if (source != null) target.AddRange(source);
    }

    private static void Seed()
    {
        Settings = new SystemSettings();
        SeedAdmin();
        foreach (string m in PaymentMethods) Gateways[m] = true;
        Templates.Add(new EmailTemplate
        {
            Id = 1, Key = "booking_confirmation", Name = "Booking confirmation",
            Subject = "Your booking #{{booking_id}} at {{hotel_name}} is confirmed",
            Body = "Hi {{guest_name}},\n\nYour booking at {{hotel_name}} is confirmed.\n\nBooking number: #{{booking_id}}\nRoom: {{room}}\nCheck-in: {{checkin}} (from {{checkin_time}})\nCheck-out: {{checkout}}\nNights: {{nights}}\nTotal: {{total}}\nStatus: {{status}}\n\nNeed help? Contact us at {{contact}}.\n\n- The {{hotel_name}} team"
        });
        Templates.Add(new EmailTemplate
        {
            Id = 2, Key = "cancellation", Name = "Booking cancellation",
            Subject = "Your booking #{{booking_id}} at {{hotel_name}} was cancelled",
            Body = "Hi {{guest_name}},\n\nYour booking #{{booking_id}} ({{room}}, {{checkin}}) has been cancelled.\n\nIf you paid, refunds go back to the original payment method.\n\nQuestions? Contact us at {{contact}}.\n\n- The {{hotel_name}} team"
        });
        Templates.Add(new EmailTemplate
        {
            Id = 3, Key = "reminder", Name = "Check-in reminder",
            Subject = "See you soon, {{guest_name}} - check-in at {{hotel_name}} on {{checkin}}",
            Body = "Hi {{guest_name}},\n\nThis is a friendly reminder of your upcoming stay.\n\nBooking number: #{{booking_id}}\nRoom: {{room}}\nCheck-in: {{checkin}} (from {{checkin_time}})\nCheck-out: {{checkout}}\nTotal: {{total}}\n\nWe look forward to welcoming you!\n- The {{hotel_name}} team"
        });
    }

    private static void SeedAdmin()
    {
        Admins.Add(new AdminUser
        {
            Id = 1, Username = "admin", PasswordHash = AdminAuth.HashPassword("admin123"),
            Role = "Admin", Active = true, CreatedAt = DateTime.Now, CreatedBy = "system",
            MustChangePassword = true
        });
    }

    private static void FixUps()
    {
        foreach (Booking b in Bookings)
        {
            if (b.CheckIn == DateTime.MinValue) b.CheckIn = DateTime.Today;
            if (b.CheckOut == DateTime.MinValue) b.CheckOut = b.CheckIn.AddDays(b.Nights < 1 ? 1 : b.Nights);
            if (b.Source == "") b.Source = "Direct";
            if (b.Subtotal == 0 && b.Total > 0) { b.Subtotal = b.Total; b.Discount = 0; b.Tax = 0; }
        }
        foreach (Room r in Rooms)
        {
            if (r.Units < 1) r.Units = 1;
            if (r.RoomStatus == "") r.RoomStatus = "Clean";
        }
        if (Admins.Count == 0) SeedAdmin();
        if (Templates.Count == 0)
        {
            Templates.Add(new EmailTemplate { Id = 1, Key = "booking_confirmation", Name = "Booking confirmation", Subject = "Your booking #{{booking_id}} at {{hotel_name}} is confirmed", Body = "Hi {{guest_name}}, your booking #{{booking_id}} is confirmed." });
        }
    }

    // ---------- small helpers ----------

    public static int NextId<T>(List<T> list, Func<T, int> key) => list.Count == 0 ? 1 : list.Max(key) + 1;

    public static string H(string? text) => WebUtility.HtmlEncode(text ?? "");

    public static string Money(int amount) => Settings.CurrencySymbol + amount.ToString("N0");

    public static string D(DateTime d) => d.ToString("ddd, MMM d, yyyy");

    public static string Ds(DateTime d) => d.ToString("yyyy-MM-dd");

    public static string Chip(string text, string color) => "<span class='chip " + color + "'>" + H(text) + "</span>";

    public static string StatusChip(string status)
    {
        string color = status switch
        {
            "Paid" or "Completed" or "Available" or "Clean" or "Done" or "Checked-in" => "ok",
            "Pay at hotel" or "Due at check-in" or "Pending" or "Dirty" or "In progress" => "warn",
            "Cancelled" or "Refunded" or "Voided" or "Sold out" or "Maintenance" or "Out of Order" => "bad",
            _ => "info"
        };
        return Chip(status, color);
    }

    // ---------- audit ----------

    public static void Audit(HttpContext? ctx, string action, string details)
    {
        AdminUser? u = AdminAuth.UserOf(ctx);
        AuditLog.Add(new AuditEntry
        {
            Id = NextId(AuditLog, x => x.Id),
            Time = DateTime.Now,
            Username = u?.Username ?? "system",
            Role = u?.Role ?? "-",
            Action = action,
            Details = details,
            Ip = ctx?.Connection.RemoteIpAddress?.ToString() ?? "-"
        });
        Save();
    }

    // ---------- gateways ----------

    public static bool IsGatewayOnline(string method) => Gateways.TryGetValue(method, out bool ok) && ok;

    public static List<string> OnlineMethods()
    {
        var list = new List<string>();
        foreach (string m in PaymentMethods) if (IsGatewayOnline(m)) list.Add(m);
        return list;
    }

    // ---------- pricing engine ----------

    public static int RateFor(Room room, DateTime night)
    {
        int rate = room.Price;
        foreach (SeasonalRate s in SeasonalRates)
            if ((s.RoomId == 0 || s.RoomId == room.Id) && night.Date >= s.From.Date && night.Date <= s.To.Date)
                rate = (int)Math.Round(room.Price * (1 + s.PercentAdjust / 100.0));
        return Math.Max(0, rate);
    }

    public static int LosPercentFor(int nights)
    {
        int best = 0;
        foreach (LosDiscount d in LosDiscounts)
            if (nights >= d.MinNights && d.PercentOff > best) best = d.PercentOff;
        return best;
    }

    public static Offer? MatchOffer(string? code, Room room, int nights, DateTime checkin)
    {
        string c = (code ?? "").Trim();
        foreach (Offer o in Offers)
        {
            if (!o.Enabled) continue;
            if (o.RoomId != 0 && o.RoomId != room.Id) continue;
            if (checkin.Date < o.ValidFrom.Date || checkin.Date > o.ValidTo.Date) continue;
            if (nights < Math.Max(1, o.MinNights)) continue;
            if (c == "")
            {
                if (o.Code == "") return o; // no code typed -> only auto-applied offers
                continue;
            }
            if (string.Equals(o.Code, c, StringComparison.OrdinalIgnoreCase)) return o;
        }
        return null;
    }

    public static Quote Quote(Room room, DateTime checkin, int nights, string? promoCode)
    {
        Quote q = new();
        if (nights < 1) nights = 1;
        for (int i = 0; i < nights; i++)
        {
            DateTime night = checkin.Date.AddDays(i);
            int rate = RateFor(room, night);
            q.Lines.Add(new QuoteLine { Date = night, Rate = rate });
            q.Subtotal += rate;
        }
        q.LosPercent = LosPercentFor(nights);
        q.LosAmount = (int)Math.Round(q.Subtotal * q.LosPercent / 100.0);
        Offer? offer = MatchOffer(promoCode, room, nights, checkin);
        if (offer != null)
        {
            q.OfferName = offer.Name;
            int net = q.Subtotal - q.LosAmount;
            q.OfferAmount = offer.Type == "percent" ? (int)Math.Round(net * offer.Value / 100.0) : offer.Value;
            if (q.OfferAmount > net) q.OfferAmount = net;
            if (q.OfferAmount < 0) q.OfferAmount = 0;
        }
        int taxed = q.Subtotal - q.LosAmount - q.OfferAmount;
        q.Tax = (int)Math.Round(taxed * Settings.TaxPercent / 100.0);
        q.Total = taxed + q.Tax;
        return q;
    }

    // ---------- date-based availability ----------

    public static bool StatusBlocks(string status) => status == "Cancelled" || status == "Refunded";

    public static int BookedUnits(Room room, DateTime date)
    {
        int count = 0;
        foreach (Booking b in Bookings)
            if (b.RoomId == room.Id && !StatusBlocks(b.Status) && date.Date >= b.CheckIn.Date && date.Date < b.CheckOut.Date)
                count++;
        return count;
    }

    public static bool IsBlackout(int roomId, DateTime date)
    {
        foreach (BlackoutDate b in Blackouts)
            if ((b.RoomId == 0 || b.RoomId == roomId) && b.Date.Date == date.Date) return true;
        return false;
    }

    public static bool RoomFree(Room room, DateTime checkin, int nights)
    {
        if (!room.IsAvailable) return false;
        for (int i = 0; i < nights; i++)
        {
            DateTime night = checkin.Date.AddDays(i);
            if (IsBlackout(room.Id, night)) return false;
            if (BookedUnits(room, night) >= Math.Max(1, room.Units)) return false;
        }
        return true;
    }

    public static string AvailabilityLabel(Room room, DateTime checkin, int nights)
    {
        if (!room.IsAvailable) return "Sold out";
        for (int i = 0; i < nights; i++)
        {
            DateTime night = checkin.Date.AddDays(i);
            if (IsBlackout(room.Id, night) || BookedUnits(room, night) >= Math.Max(1, room.Units)) return "Fully booked";
        }
        return "Available";
    }

    // ---------- folios ----------

    public static Folio? FolioFor(int bookingId) => Folios.Find(f => f.BookingId == bookingId);

    public static int NextItemId(Folio f) => f.Items.Count == 0 ? 1 : f.Items.Max(x => x.Id) + 1;

    public static Folio CreateFolio(Booking b, Room room, Quote q)
    {
        Folio f = new() { Id = NextId(Folios, x => x.Id), BookingId = b.Id, InvoiceNo = NextInvoiceNo++ };
        foreach (QuoteLine l in q.Lines)
            f.Items.Add(new FolioItem { Id = NextItemId(f), Date = l.Date, Description = "Room charge - " + room.Name + " (" + D(l.Date) + ")", Type = "charge", Amount = l.Rate });
        if (q.LosAmount > 0)
            f.Items.Add(new FolioItem { Id = NextItemId(f), Date = b.CheckIn, Description = "Length-of-stay discount (" + q.LosPercent + "%)", Type = "discount", Amount = q.LosAmount });
        if (q.OfferAmount > 0)
            f.Items.Add(new FolioItem { Id = NextItemId(f), Date = b.CheckIn, Description = "Promotion: " + q.OfferName, Type = "discount", Amount = q.OfferAmount });
        if (q.Tax > 0)
            f.Items.Add(new FolioItem { Id = NextItemId(f), Date = b.CheckIn, Description = Settings.TaxName + " (" + Settings.TaxPercent + "%)", Type = "tax", Amount = q.Tax });
        if (b.Status == "Paid")
            f.Items.Add(new FolioItem { Id = NextItemId(f), Date = DateTime.Now, Description = "Payment received - " + b.PaymentMethod, Type = "payment", Amount = b.Total });
        Folios.Add(f);
        return f;
    }

    public static int FolioBalance(Folio f)
    {
        int owed = 0;
        foreach (FolioItem i in f.Items)
        {
            if (i.Type == "charge" || i.Type == "tax" || i.Type == "extra") owed += i.Amount;
            else owed -= i.Amount; // discount, payment, refund reduce the balance
        }
        return owed;
    }

    // ---------- cancellation policy ----------

    public static (int amount, bool full) RefundableFor(Booking b)
    {
        if (DateTime.Now <= b.CheckIn.Date.AddHours(-Settings.FreeCancelHours)) return (b.Total, true);
        int fee = (int)Math.Round(b.Total * Settings.CancelFeePercent / 100.0);
        return (Math.Max(0, b.Total - fee), false);
    }

    // ---------- e-mails (demo outbox, no SMTP) ----------

    public static string Fill(string text, Booking b)
    {
        Room? room = Rooms.Find(r => r.Id == b.RoomId);
        return (text ?? "")
            .Replace("{{hotel_name}}", Settings.HotelName)
            .Replace("{{guest_name}}", b.GuestName)
            .Replace("{{booking_id}}", b.Id.ToString())
            .Replace("{{room}}", room?.Name ?? "-")
            .Replace("{{checkin}}", D(b.CheckIn))
            .Replace("{{checkout}}", D(b.CheckOut))
            .Replace("{{checkin_time}}", Settings.CheckInTime)
            .Replace("{{checkout_time}}", Settings.CheckOutTime)
            .Replace("{{nights}}", b.Nights.ToString())
            .Replace("{{total}}", Money(b.Total))
            .Replace("{{status}}", b.Status)
            .Replace("{{contact}}", Settings.ContactEmail + " | " + Settings.ContactPhone);
    }

    public static void QueueEmail(string key, Booking b)
    {
        EmailTemplate? t = Templates.Find(x => x.Key == key);
        if (t == null) return;
        Outbox.Add(new OutboxEmail
        {
            Id = NextId(Outbox, x => x.Id),
            Template = t.Name,
            To = b.Email,
            Subject = Fill(t.Subject, b),
            Body = Fill(t.Body, b),
            Created = DateTime.Now
        });
        Save();
    }

    // ---------- duplicate payment detection ----------

    public static List<Transaction> FlaggedDuplicates()
    {
        var flagged = new List<Transaction>();
        foreach (Transaction t in Transactions)
        {
            if (t.Status == "Voided" || t.RelatedId != 0 || DismissedDupes.Contains(t.Id)) continue;
            bool dupe = false;
            foreach (Transaction o in Transactions)
            {
                if (o.Id == t.Id || o.Status == "Voided" || o.RelatedId != 0) continue;
                if (o.Type != t.Type || o.Amount != t.Amount) continue;
                if (o.BookingId == t.BookingId && Math.Abs((o.Date - t.Date).TotalMinutes) <= 10) { dupe = true; break; }
                if (o.BookingId != t.BookingId && o.Method == t.Method && o.Date.Date == t.Date.Date)
                {
                    Booking? bt = Bookings.Find(b => b.Id == t.BookingId);
                    Booking? bo = Bookings.Find(b => b.Id == o.BookingId);
                    if (bt != null && bo != null && bt.Email != "" && bt.Email == bo.Email) { dupe = true; break; }
                }
            }
            if (dupe) flagged.Add(t);
        }
        return flagged;
    }
}
