using System;
using FitnessClub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace FitnessClub.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(FitnessClubDbContext))]
    [Migration("20261009122902_ClientEmailRequired")]
    partial class ClientEmailRequired
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("FitnessClub.Domain.Clients.Client", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateOnly>("DateOfBirth")
                        .HasColumnType("date");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(254)
                        .HasColumnType("nvarchar(254)");

                    b.Property<string>("Phone")
                        .HasMaxLength(16)
                        .HasColumnType("nvarchar(16)");

                    b.Property<DateTimeOffset>("RegisteredAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.ToTable("Clients", (string)null);
                });

            modelBuilder.Entity("FitnessClub.Domain.MembershipPlans.MembershipPlan", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<bool>("IsActive")
                        .HasColumnType("bit");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<decimal>("Price")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("ValidityDays")
                        .HasColumnType("int");

                    b.Property<Guid>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("uniqueidentifier");

                    b.Property<int?>("VisitLimit")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("MembershipPlans", (string)null);
                });

            modelBuilder.Entity("FitnessClub.Domain.Notifications.Notification", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Channel")
                        .IsRequired()
                        .HasMaxLength(10)
                        .HasColumnType("nvarchar(10)");

                    b.Property<Guid>("ClientId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("FailureReason")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<string>("HtmlBody")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid?>("MembershipId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Message")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<string>("Recipient")
                        .IsRequired()
                        .HasMaxLength(254)
                        .HasColumnType("nvarchar(254)");

                    b.Property<DateTimeOffset?>("SentAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(10)
                        .HasColumnType("nvarchar(10)");

                    b.Property<string>("Subject")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("nvarchar(40)");

                    b.Property<Guid>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ClientId", "CreatedAt");

                    b.HasIndex("MembershipId", "Type")
                        .IsUnique()
                        .HasFilter("[MembershipId] IS NOT NULL");

                    b.HasIndex("Status", "CreatedAt");

                    b.ToTable("Notifications", (string)null);
                });

            modelBuilder.Entity("FitnessClub.Domain.Payments.Payment", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("Amount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<Guid>("ClientId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("MembershipId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Method")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.Property<DateTimeOffset>("PaidAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ClientId");

                    b.HasIndex("MembershipId")
                        .IsUnique();

                    b.HasIndex("PaidAt");

                    b.ToTable("Payments", (string)null);
                });

            modelBuilder.Entity("FitnessClub.Domain.Rooms.Room", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("Capacity")
                        .HasColumnType("int");

                    b.Property<bool>("IsActive")
                        .HasColumnType("bit");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<Guid>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("Rooms", (string)null);
                });

            modelBuilder.Entity("FitnessClub.Domain.Trainers.Trainer", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Email")
                        .HasMaxLength(254)
                        .HasColumnType("nvarchar(254)");

                    b.Property<string>("IdentityUserId")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("bit");

                    b.Property<string>("Phone")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("nvarchar(16)");

                    b.Property<string>("Specialization")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<Guid>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("IdentityUserId")
                        .IsUnique()
                        .HasFilter("[IdentityUserId] IS NOT NULL");

                    b.HasIndex("Phone")
                        .IsUnique();

                    b.ToTable("Trainers", (string)null);
                });

            modelBuilder.Entity("FitnessClub.Domain.Training.TrainingSession", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("Capacity")
                        .HasColumnType("int");

                    b.Property<Guid>("RoomId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<Guid>("TrainerId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.Property<Guid>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("RoomId");

                    b.HasIndex("TrainerId");

                    b.ToTable("TrainingSessions", (string)null);
                });

            modelBuilder.Entity("FitnessClub.Domain.Visits.Visit", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CheckedInAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("ClientId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("MembershipId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ClientId", "CheckedInAt");

                    b.ToTable("Visits", (string)null);
                });

            modelBuilder.Entity("FitnessClub.Domain.Clients.Client", b =>
                {
                    b.OwnsOne("FitnessClub.Domain.SharedKernel.PersonName", "Name", b1 =>
                        {
                            b1.Property<Guid>("ClientId")
                                .HasColumnType("uniqueidentifier");

                            b1.Property<string>("FirstName")
                                .IsRequired()
                                .HasMaxLength(100)
                                .HasColumnType("nvarchar(100)")
                                .HasColumnName("FirstName");

                            b1.Property<string>("LastName")
                                .IsRequired()
                                .HasMaxLength(100)
                                .HasColumnType("nvarchar(100)")
                                .HasColumnName("LastName");

                            b1.Property<string>("MiddleName")
                                .HasMaxLength(100)
                                .HasColumnType("nvarchar(100)")
                                .HasColumnName("MiddleName");

                            b1.HasKey("ClientId");

                            b1.ToTable("Clients");

                            b1.WithOwner()
                                .HasForeignKey("ClientId");
                        });

                    b.OwnsMany("FitnessClub.Domain.Clients.Membership", "Memberships", b1 =>
                        {
                            b1.Property<Guid>("Id")
                                .HasColumnType("uniqueidentifier");

                            b1.Property<DateTimeOffset?>("CancelledAt")
                                .HasColumnType("datetimeoffset");

                            b1.Property<Guid>("ClientId")
                                .HasColumnType("uniqueidentifier");

                            b1.Property<DateOnly>("EndsOn")
                                .HasColumnType("date");

                            b1.Property<DateOnly?>("LastVisitOn")
                                .HasColumnType("date");

                            b1.Property<Guid>("PlanId")
                                .HasColumnType("uniqueidentifier");

                            b1.Property<string>("PlanName")
                                .IsRequired()
                                .HasMaxLength(100)
                                .HasColumnType("nvarchar(100)");

                            b1.Property<decimal>("Price")
                                .HasPrecision(18, 2)
                                .HasColumnType("decimal(18,2)");

                            b1.Property<DateTimeOffset>("PurchasedAt")
                                .HasColumnType("datetimeoffset");

                            b1.Property<DateOnly>("StartsOn")
                                .HasColumnType("date");

                            b1.Property<int?>("VisitLimit")
                                .HasColumnType("int");

                            b1.Property<int>("VisitsUsed")
                                .HasColumnType("int");

                            b1.HasKey("Id");

                            b1.HasIndex("ClientId");

                            b1.HasIndex("EndsOn");

                            b1.HasIndex("PlanId");

                            b1.ToTable("Memberships", (string)null);

                            b1.WithOwner()
                                .HasForeignKey("ClientId");

                            b1.HasOne("FitnessClub.Domain.MembershipPlans.MembershipPlan", null)
                                .WithMany()
                                .HasForeignKey("PlanId")
                                .OnDelete(DeleteBehavior.Restrict)
                                .IsRequired();
                        });

                    b.Navigation("Memberships");

                    b.Navigation("Name")
                        .IsRequired();
                });

            modelBuilder.Entity("FitnessClub.Domain.Notifications.Notification", b =>
                {
                    b.HasOne("FitnessClub.Domain.Clients.Client", null)
                        .WithMany()
                        .HasForeignKey("ClientId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("FitnessClub.Domain.Payments.Payment", b =>
                {
                    b.HasOne("FitnessClub.Domain.Clients.Client", null)
                        .WithMany()
                        .HasForeignKey("ClientId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("FitnessClub.Domain.Trainers.Trainer", b =>
                {
                    b.OwnsMany("FitnessClub.Domain.Trainers.ClientAssignment", "Clients", b1 =>
                        {
                            b1.Property<Guid>("TrainerId")
                                .HasColumnType("uniqueidentifier");

                            b1.Property<Guid>("ClientId")
                                .HasColumnType("uniqueidentifier");

                            b1.Property<DateTimeOffset>("AssignedAt")
                                .HasColumnType("datetimeoffset");

                            b1.HasKey("TrainerId", "ClientId");

                            b1.HasIndex("ClientId");

                            b1.ToTable("TrainerClients", (string)null);

                            b1.HasOne("FitnessClub.Domain.Clients.Client", null)
                                .WithMany()
                                .HasForeignKey("ClientId")
                                .OnDelete(DeleteBehavior.Restrict)
                                .IsRequired();

                            b1.WithOwner()
                                .HasForeignKey("TrainerId");
                        });

                    b.OwnsOne("FitnessClub.Domain.SharedKernel.PersonName", "Name", b1 =>
                        {
                            b1.Property<Guid>("TrainerId")
                                .HasColumnType("uniqueidentifier");

                            b1.Property<string>("FirstName")
                                .IsRequired()
                                .HasMaxLength(100)
                                .HasColumnType("nvarchar(100)")
                                .HasColumnName("FirstName");

                            b1.Property<string>("LastName")
                                .IsRequired()
                                .HasMaxLength(100)
                                .HasColumnType("nvarchar(100)")
                                .HasColumnName("LastName");

                            b1.Property<string>("MiddleName")
                                .HasMaxLength(100)
                                .HasColumnType("nvarchar(100)")
                                .HasColumnName("MiddleName");

                            b1.HasKey("TrainerId");

                            b1.ToTable("Trainers");

                            b1.WithOwner()
                                .HasForeignKey("TrainerId");
                        });

                    b.OwnsMany("FitnessClub.Domain.Trainers.WorkingHours", "WorkingHours", b1 =>
                        {
                            b1.Property<int>("Id")
                                .ValueGeneratedOnAdd()
                                .HasColumnType("int");

                            SqlServerPropertyBuilderExtensions.UseIdentityColumn(b1.Property<int>("Id"));

                            b1.Property<string>("Day")
                                .IsRequired()
                                .HasMaxLength(10)
                                .HasColumnType("nvarchar(10)");

                            b1.Property<TimeOnly>("End")
                                .HasColumnType("time");

                            b1.Property<TimeOnly>("Start")
                                .HasColumnType("time");

                            b1.Property<Guid>("TrainerId")
                                .HasColumnType("uniqueidentifier");

                            b1.HasKey("Id");

                            b1.HasIndex("TrainerId");

                            b1.ToTable("TrainerWorkingHours", (string)null);

                            b1.WithOwner()
                                .HasForeignKey("TrainerId");
                        });

                    b.Navigation("Clients");

                    b.Navigation("Name")
                        .IsRequired();

                    b.Navigation("WorkingHours");
                });

            modelBuilder.Entity("FitnessClub.Domain.Training.TrainingSession", b =>
                {
                    b.HasOne("FitnessClub.Domain.Rooms.Room", null)
                        .WithMany()
                        .HasForeignKey("RoomId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("FitnessClub.Domain.Trainers.Trainer", null)
                        .WithMany()
                        .HasForeignKey("TrainerId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.OwnsOne("FitnessClub.Domain.SharedKernel.TimeSlot", "Slot", b1 =>
                        {
                            b1.Property<Guid>("TrainingSessionId")
                                .HasColumnType("uniqueidentifier");

                            b1.Property<DateTimeOffset>("End")
                                .HasColumnType("datetimeoffset")
                                .HasColumnName("EndsAt");

                            b1.Property<DateTimeOffset>("Start")
                                .HasColumnType("datetimeoffset")
                                .HasColumnName("StartsAt");

                            b1.HasKey("TrainingSessionId");

                            b1.HasIndex("Start");

                            b1.ToTable("TrainingSessions");

                            b1.WithOwner()
                                .HasForeignKey("TrainingSessionId");
                        });

                    b.OwnsMany("FitnessClub.Domain.Training.Booking", "Bookings", b1 =>
                        {
                            b1.Property<Guid>("Id")
                                .HasColumnType("uniqueidentifier");

                            b1.Property<DateTimeOffset>("BookedAt")
                                .HasColumnType("datetimeoffset");

                            b1.Property<DateTimeOffset?>("CancelledAt")
                                .HasColumnType("datetimeoffset");

                            b1.Property<Guid>("ClientId")
                                .HasColumnType("uniqueidentifier");

                            b1.Property<Guid>("TrainingSessionId")
                                .HasColumnType("uniqueidentifier");

                            b1.HasKey("Id");

                            b1.HasIndex("ClientId");

                            b1.HasIndex("TrainingSessionId");

                            b1.ToTable("Bookings", (string)null);

                            b1.HasOne("FitnessClub.Domain.Clients.Client", null)
                                .WithMany()
                                .HasForeignKey("ClientId")
                                .OnDelete(DeleteBehavior.Restrict)
                                .IsRequired();

                            b1.WithOwner()
                                .HasForeignKey("TrainingSessionId");
                        });

                    b.Navigation("Bookings");

                    b.Navigation("Slot")
                        .IsRequired();
                });

            modelBuilder.Entity("FitnessClub.Domain.Visits.Visit", b =>
                {
                    b.HasOne("FitnessClub.Domain.Clients.Client", null)
                        .WithMany()
                        .HasForeignKey("ClientId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
