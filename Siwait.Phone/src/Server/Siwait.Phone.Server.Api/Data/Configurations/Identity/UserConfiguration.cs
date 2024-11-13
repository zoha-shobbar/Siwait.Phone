﻿using Siwait.Phone.Server.Api.Models.Identity;

namespace Siwait.Phone.Server.Api.Data.Configurations.Identity;

public partial class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        const string userName = "test";
        const string email = "test@bitplatform.dev";

        builder.HasData([new()
        {
            Id = Guid.Parse("8ff71671-a1d6-4f97-abb9-d87d7b47d6e7"),
            EmailConfirmed = true,
            LockoutEnabled = true,
            Gender = Gender.Other,
            BirthDate = new DateTimeOffset(new DateOnly(2023, 1, 1), default, default),
            FullName = "Siwait.Phone test account",
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailTokenRequestedOn = new DateTimeOffset(new DateOnly(2023, 1, 1), default, default),
            PhoneNumber = "+31684207362",
            PhoneNumberConfirmed = true,
            SecurityStamp = "959ff4a9-4b07-4cc1-8141-c5fc033daf83",
            ConcurrencyStamp = "315e1a26-5b3a-4544-8e91-2760cd28e231",
            PasswordHash = "AQAAAAIAAYagAAAAEP0v3wxkdWtMkHA3Pp5/JfS+42/Qto9G05p2mta6dncSK37hPxEHa3PGE4aqN30Aag==", // 123456
        }]);

        builder.Property(u => u.Sessions)
               .HasConversion(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                              v => JsonSerializer.Deserialize<List<UserSession>>(v, (JsonSerializerOptions?)null)!);
        // You can also use builder.OwnsMany(u => u.Sessions, navBuilder => navBuilder.ToJson());


        builder
            .HasIndex(b => b.Email)
            .HasFilter($"[{nameof(User.Email)}] IS NOT NULL")
            .IsUnique();

        builder
            .HasIndex(b => b.PhoneNumber)
            .HasFilter($"[{nameof(User.PhoneNumber)}] IS NOT NULL")
            .IsUnique();
    }
}