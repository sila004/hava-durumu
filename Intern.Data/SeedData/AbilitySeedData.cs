using Intern.Entities.Concretes;
using Microsoft.EntityFrameworkCore;

namespace Intern.Data.SeedData
{
    public static class AbilitySeedData
    {
        // Başlangıç yetenekleri (Name, Description)
        private static readonly (string Name, string Description)[] Abilities =
        {
            ("ViewEmployeeManagement", "Çalışan yönetimi ekranını görüntüleme"),
            ("CreateEmployee", "Yeni çalışan ekleme"),
            ("EditEmployee", "Çalışan bilgilerini düzenleme"),
            ("DeleteEmployee", "Çalışan silme"),
        };

        // Tekrar çalıştırılabilir: yalnız veritabanında adı olmayanları ekler, var olanlara dokunmaz.
        public static async Task Initialize(IServiceProvider serviceProvider, AppDbContext context)
        {
            var existingNames = await context.Abilities
                .Select(a => a.Name)
                .ToListAsync();

            foreach (var (name, description) in Abilities)
            {
                if (existingNames.Contains(name))
                {
                    continue;
                }

                await context.Abilities.AddAsync(new Ability
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Description = description
                });
            }

            await context.SaveChangesAsync();
        }
    }
}
