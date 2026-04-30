    using appWeb2.Models;
using Microsoft.EntityFrameworkCore;

namespace appWeb2.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) 
            : base(options) { }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<VideoJuegos> VideoJuegos { get; set; }
        public DbSet<Compra> Compras { get; set; }
        public DbSet<DetalleCompra> detalle_compra { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.correo)
                .IsUnique();

            modelBuilder.Entity<VideoJuegos>()
                .HasOne(v => v.Categoria)
                .WithMany(c => c.VideoJuegos)
                .HasForeignKey(v => v.idcategoria)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Compra>()
                .HasOne(c => c.Usuario)
                .WithMany(u => u.Compras)
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            //modelBuilder.Entity<Compra>()
            //    .HasOne(c=> c.VideoJuegos)
            //    .WithMany(V => V.Compras)
            //    .HasForeignKey(c=> c.VideJuegoId)
            //    .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
