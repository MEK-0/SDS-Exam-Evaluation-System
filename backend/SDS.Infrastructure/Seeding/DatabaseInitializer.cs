// Üretim başlangıcında veritabanı migration'larını uygular ve zorunlu başlangıç verisini oluşturur.
using Microsoft.EntityFrameworkCore;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Seeding;

public sealed class DatabaseInitializer(SdsDbContext dbContext, OpticalTemplateSeeder opticalTemplateSeeder)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        await opticalTemplateSeeder.SeedAsync(cancellationToken);
    }
}
