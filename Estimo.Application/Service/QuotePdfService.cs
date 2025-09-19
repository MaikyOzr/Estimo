using Estimo.Application.Features.Auth;
using Estimo.Domain;
using Estimo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using System.Globalization;

namespace Estimo.Application.Service;

public sealed class QuotePdfService(EstimoDbContext _context, ICurrentUser user)
{
    public async Task<byte[]> GeneratePdfAsync(Guid quoteId, CancellationToken ct)
    {
        var data = await (from q in _context.Quotes.AsNoTracking()
                          join c in _context.Clients.AsNoTracking() on q.ClientId equals c.Id
                          where q.Id == quoteId && c.OwnerId == user.Id
                          select new { q, c })
                         .FirstOrDefaultAsync(ct)
                   ?? throw new UnauthorizedAccessException("Quote not found or access denied");

        var total = data.q.Amount + data.q.Amount * data.q.VatPercent / 100m;
        var culture = CultureInfo.GetCultureInfo("es-ES");

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);

                page.Header().Text($"Quote #{data.q.Name}").SemiBold().FontSize(20);

                page.Content().Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Text($"Client: {data.c.Name}");
                    col.Item().LineHorizontal(1);

                    // --- таблиця сум ---
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.ConstantColumn(140); cd.RelativeColumn(); });
                        t.Cell().Text("Amount"); t.Cell().Text($"{data.q.Amount.ToString("N2", culture)} €");
                        t.Cell().Text("VAT %"); t.Cell().Text($"{data.q.VatPercent.ToString("0.##", culture)}%");
                        t.Cell().Text("Total"); t.Cell().Text($"{total.ToString("N2", culture)} €");
                        t.Cell().Text("Created UTC"); t.Cell().Text($"{data.q.CreatedAt:yyyy-MM-dd HH:mm}");
                    });

                    // --- ТУТ додаємо CTA на оплату ---
                    col.Item().Text(text =>
                    {
                        var url = data.q.PaymentUrl;
                        if (!string.IsNullOrWhiteSpace(url))
                            text.Span("Pay online: ").Bold().FontSize(12);
                    });

                    col.Item().Text(text =>
                    {
                        if (!string.IsNullOrWhiteSpace(data.q.PaymentUrl))
                            text.Hyperlink("Pay Online", data.q.PaymentUrl!)
                                .FontSize(14)
                                .FontColor("#1d4ed8")
                                .Underline();
                    });
                    // --- кінець CTA ---
                });

                page.Footer().AlignRight()
                    .Text($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(10);
            });
        }).GeneratePdf();

        return pdf;
    }


    public static async Task CommitGenerateAsync(EstimoDbContext db, Guid userId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var usage = await db.UserUsages.FindAsync(new object[] { userId }, ct);

        if (usage is null)
        {
            usage = new UserUsage
            {
                UserId = userId,
                WindowDate = today,
                DailyCount = 1,
                TotalCount = 1
            };
            db.UserUsages.Add(usage);
        }
        else
        {
            if (usage.WindowDate != today)
            {
                usage.WindowDate = today;
                usage.DailyCount = 0;
            }
            usage.TotalCount += 1;
            usage.DailyCount += 1;
        }

        await db.SaveChangesAsync(ct);
    }
}
