using Estimo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;

namespace Estimo.Application.Service;

public sealed class QuotePdfService(EstimoDbContext _context)
{
    public async Task<byte[]> GeneratePdfAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        var quote = await _context.Quotes.AsNoTracking()
                     .FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken)
                    ?? throw new Exception("Quote not found!");

        var client = await _context.Clients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == quote.ClientId, cancellationToken)
                     ?? throw new Exception("Client not found!");

        // Simulate PDF generation
        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Header().Text($"Quote #{quote.Name}").SemiBold().FontSize(20);
                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Text($"Client: {client.Name}");
                    col.Item().LineHorizontal(1);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.ConstantColumn(140); cd.RelativeColumn(); });
                        t.Cell().Text("Amount"); t.Cell().Text($"{quote.Amount:0.00}");
                        t.Cell().Text("VAT %"); t.Cell().Text($"{quote.VatPercent:0.##}%");
                        t.Cell().Text("Total"); t.Cell().Text($"{quote.Total:0.00}");
                        t.Cell().Text("Created UTC"); t.Cell().Text($"{quote.CreatedAt:yyyy-MM-dd HH:mm}");
                    });
                });
                page.Footer().AlignRight().Text($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(10);
            });
        }).GeneratePdf();
        
        return pdf;
    }
}
