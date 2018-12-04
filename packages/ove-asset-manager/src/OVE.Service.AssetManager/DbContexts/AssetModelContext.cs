using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using OVE.Service.Core.Assets;

namespace OVE.Service.AssetManager.DbContexts {
    public class AssetModelContext : DbContext {

        // enable configuration

        // ReSharper disable once MemberCanBePrivate.Global << due to DI
        public AssetModelContext(DbContextOptions<AssetModelContext> options) : base(options) {
        }

        public DbSet<OVEAssetModel> AssetModels { get; set; }

        public static void Initialize(IServiceProvider serviceProvider) {
            using (var context =
                new AssetModelContext(serviceProvider.GetRequiredService<DbContextOptions<AssetModelContext>>())) {

                if (context.AssetModels.Any()) {
                    return; // DB has been seeded
                }
                
                context.SaveChanges();
            }
        }

    }
}
