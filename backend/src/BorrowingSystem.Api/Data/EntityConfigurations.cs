using BorrowingSystem.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BorrowingSystem.Api.Data;

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.Property(d => d.Code).HasMaxLength(20);
        b.Property(d => d.Name).HasMaxLength(120);
        b.HasIndex(d => d.Code).IsUnique();
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(u => u.FullName).HasMaxLength(120);
        b.Property(u => u.Email).HasMaxLength(160);
        b.Property(u => u.PasswordHash).HasMaxLength(200);
        b.HasIndex(u => u.Email).IsUnique();
        b.HasOne(u => u.Department).WithMany().HasForeignKey(u => u.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(u => u.IsAdmin);
    }
}

internal sealed class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> b)
    {
        b.ToTable("Equipment");
        b.Property(e => e.AssetTag).HasMaxLength(30);
        b.Property(e => e.Name).HasMaxLength(120);
        b.Property(e => e.Category).HasMaxLength(60);
        b.Property(e => e.Description).HasMaxLength(500);
        b.HasIndex(e => e.AssetTag).IsUnique();
        b.HasIndex(e => e.Category);
        b.Ignore(e => e.IsServiceable);
    }
}

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> b)
    {
        b.Property(r => r.EventName).HasMaxLength(150);
        b.Property(r => r.Venue).HasMaxLength(120);
        b.Property(r => r.Purpose).HasMaxLength(500);
        b.Property(r => r.RejectionReason).HasMaxLength(300);
        b.Property(r => r.ReturnRemarks).HasMaxLength(500);

        // The scheduling queries always filter on status + time window.
        b.HasIndex(r => new { r.Status, r.StartAt, r.EndAt });

        b.HasOne(r => r.Department).WithMany().HasForeignKey(r => r.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.RequestedBy).WithMany().HasForeignKey(r => r.RequestedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.ReviewedBy).WithMany().HasForeignKey(r => r.ReviewedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.ReleasedBy).WithMany().HasForeignKey(r => r.ReleasedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.ReceivedBy).WithMany().HasForeignKey(r => r.ReceivedById).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(r => r.Items).WithOne(i => i.Reservation).HasForeignKey(i => i.ReservationId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(r => r.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.Ignore(r => r.Period);
        b.Ignore(r => r.Code);
    }
}

internal sealed class ReservationItemConfiguration : IEntityTypeConfiguration<ReservationItem>
{
    public void Configure(EntityTypeBuilder<ReservationItem> b)
    {
        b.HasKey(i => new { i.ReservationId, i.EquipmentId });
        b.HasOne(i => i.Equipment).WithMany().HasForeignKey(i => i.EquipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
