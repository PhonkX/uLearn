﻿// <auto-generated />
using AntiPlagiarism.Api.Models;
using AntiPlagiarism.Web.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;

namespace AntiPlagiarism.Web.Migrations
{
    [DbContext(typeof(AntiPlagiarismDb))]
    partial class AntiPlagiarismDbModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("antiplagiarism")
                .HasAnnotation("ProductVersion", "2.0.1-rtm-125")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("AntiPlagiarism.Web.Database.Models.Client", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<bool>("IsEnabled");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200);

                    b.Property<Guid>("Token");

                    b.HasKey("Id");

                    b.HasIndex("Token")
                        .IsUnique();

                    b.ToTable("Clients");
                });

            modelBuilder.Entity("AntiPlagiarism.Web.Database.Models.Code", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Text");

                    b.HasKey("Id");

                    b.ToTable("Codes");
                });

            modelBuilder.Entity("AntiPlagiarism.Web.Database.Models.Snippet", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int>("Hash");

                    b.Property<short>("SnippetType");

                    b.Property<int>("TokensCount");

                    b.HasKey("Id");

                    b.HasIndex("TokensCount", "SnippetType", "Hash")
                        .IsUnique();

                    b.ToTable("Snippets");
                });

            modelBuilder.Entity("AntiPlagiarism.Web.Database.Models.SnippetOccurence", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int>("FirstTokenIndex");

                    b.Property<int>("SnippetId");

                    b.Property<int>("SubmissionId");

                    b.HasKey("Id");

                    b.HasIndex("SnippetId");

                    b.HasIndex("SubmissionId", "FirstTokenIndex");

                    b.ToTable("SnippetsOccurences");
                });

            modelBuilder.Entity("AntiPlagiarism.Web.Database.Models.SnippetStatistics", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int>("AuthorsCount");

                    b.Property<int>("ClientId");

                    b.Property<int>("SnippetId");

                    b.Property<Guid>("TaskId");

                    b.HasKey("Id");

                    b.HasIndex("ClientId");

                    b.HasIndex("SnippetId", "TaskId", "ClientId")
                        .IsUnique();

                    b.ToTable("SnippetsStatistics");
                });

            modelBuilder.Entity("AntiPlagiarism.Web.Database.Models.Submission", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<DateTime>("AddingTime");

                    b.Property<string>("AdditionalInfo")
                        .HasMaxLength(500);

                    b.Property<Guid>("AuthorId");

                    b.Property<int>("ClientId");

                    b.Property<short>("Language");

                    b.Property<int>("ProgramId");

                    b.Property<Guid>("TaskId");

                    b.Property<int>("TokensCount");

                    b.HasKey("Id");

                    b.HasIndex("ProgramId");

                    b.HasIndex("ClientId", "TaskId");

                    b.HasIndex("ClientId", "TaskId", "AuthorId");

                    b.ToTable("Submissions");
                });

            modelBuilder.Entity("AntiPlagiarism.Web.Database.Models.TaskStatisticsParameters", b =>
                {
                    b.Property<Guid>("TaskId")
                        .ValueGeneratedOnAdd();

                    b.Property<double>("Deviation");

                    b.Property<double>("Mean");

                    b.HasKey("TaskId");

                    b.ToTable("TasksStatisticsParameters");
                });

            modelBuilder.Entity("AntiPlagiarism.Web.Database.Models.SnippetOccurence", b =>
                {
                    b.HasOne("AntiPlagiarism.Web.Database.Models.Snippet", "Snippet")
                        .WithMany()
                        .HasForeignKey("SnippetId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("AntiPlagiarism.Web.Database.Models.Submission", "Submission")
                        .WithMany()
                        .HasForeignKey("SubmissionId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("AntiPlagiarism.Web.Database.Models.SnippetStatistics", b =>
                {
                    b.HasOne("AntiPlagiarism.Web.Database.Models.Client", "Client")
                        .WithMany()
                        .HasForeignKey("ClientId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("AntiPlagiarism.Web.Database.Models.Snippet", "Snippet")
                        .WithMany()
                        .HasForeignKey("SnippetId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("AntiPlagiarism.Web.Database.Models.Submission", b =>
                {
                    b.HasOne("AntiPlagiarism.Web.Database.Models.Client", "Client")
                        .WithMany()
                        .HasForeignKey("ClientId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("AntiPlagiarism.Web.Database.Models.Code", "Program")
                        .WithMany()
                        .HasForeignKey("ProgramId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
#pragma warning restore 612, 618
        }
    }
}
