using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DecisionSimulator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIMemories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemoryType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Confidence = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsValidated = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIMemories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssumptionSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssumptionSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Actor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsRestore = table.Column<bool>(type: "bit", nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ProviderType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ReliabilityLevel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UpdateFrequency = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RequiresApiKey = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Decisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Importance = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Horizon = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DecisionDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    TargetDecisionDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Context = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    DesiredOutcome = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ConstraintsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RiskTolerance = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReviewScheduledForUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Decisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiskFactors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GeographicScope = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ImpactDirection = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ImpactMagnitude = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LikelihoodAssessment = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Confidence = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastVerifiedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskFactors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SimulationModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ModelType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FormulaDefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequiredVariablesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutputDefinitionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsBuiltIn = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ColorHex = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CountryName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PreferredLanguage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CalendarSystem = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DefaultCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CurrencyUnitConvention = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsOnboardingCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VariableDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DefaultValue = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    MinimumAllowedValue = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    MaximumAllowedValue = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    IsExternalDataCandidate = table.Column<bool>(type: "bit", nullable: false),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariableDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EconomicIndicators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IndicatorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    GeographicScope = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CurrentValue = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    PreviousValue = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    ObservationDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RetrievedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Confidence = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FreshnessWindow = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomicIndicators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EconomicIndicators_DataSources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AIConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ContextType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIConversations_Decisions_DecisionId",
                        column: x => x.DecisionId,
                        principalTable: "Decisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AIProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProposalType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Confidence = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIProposals_Decisions_DecisionId",
                        column: x => x.DecisionId,
                        principalTable: "Decisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DecisionAlternatives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsBaseline = table.Column<bool>(type: "bit", nullable: false),
                    EstimatedInitialCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EstimatedRecurringCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExpectedBenefits = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    KnownRisks = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RequiredResourcesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConstraintsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionAlternatives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DecisionAlternatives_Decisions_DecisionId",
                        column: x => x.DecisionId,
                        principalTable: "Decisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DecisionDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DecisionDocuments_Decisions_DecisionId",
                        column: x => x.DecisionId,
                        principalTable: "Decisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DecisionReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ActualOutcome = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    ExpectedOutcome = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    OutcomeDifference = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    WhatWentWell = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    WhatWentWrong = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    LessonsLearned = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DecisionReviews_Decisions_DecisionId",
                        column: x => x.DecisionId,
                        principalTable: "Decisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntityTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntityTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DataObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IndicatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VariableDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Value = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ObservationDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RetrievedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ValidFromUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ValidUntilUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    OriginalPayloadHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    VerificationStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Confidence = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SourceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataObservations_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DataObservations_EconomicIndicators_IndicatorId",
                        column: x => x.IndicatorId,
                        principalTable: "EconomicIndicators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DataObservations_VariableDefinitions_VariableDefinitionId",
                        column: x => x.VariableDefinitionId,
                        principalTable: "VariableDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AIMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TokenUsage = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIMessages_AIConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "AIConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AIExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AIProposalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InputJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutputJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIExecutionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIExecutionLogs_AIProposals_AIProposalId",
                        column: x => x.AIProposalId,
                        principalTable: "AIProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Scenarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlternativeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentScenarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TimeHorizonMonths = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProbabilityWeight = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    AssumptionSetVersion = table.Column<int>(type: "int", nullable: false),
                    IsBaseline = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scenarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scenarios_DecisionAlternatives_AlternativeId",
                        column: x => x.AlternativeId,
                        principalTable: "DecisionAlternatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Scenarios_Decisions_DecisionId",
                        column: x => x.DecisionId,
                        principalTable: "Decisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Scenarios_Scenarios_ParentScenarioId",
                        column: x => x.ParentScenarioId,
                        principalTable: "Scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScenarioDependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DependsOnScenarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationshipType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScenarioDependencies_Scenarios_DependsOnScenarioId",
                        column: x => x.DependsOnScenarioId,
                        principalTable: "Scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScenarioDependencies_Scenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalTable: "Scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScenarioVariables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariableDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    MinimumValue = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    MaximumValue = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    StandardDeviation = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    DistributionType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Confidence = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DistributionBasis = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsUserOverride = table.Column<bool>(type: "bit", nullable: false),
                    DiscreteOutcomesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioVariables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScenarioVariables_Scenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalTable: "Scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScenarioVariables_VariableDefinitions_VariableDefinitionId",
                        column: x => x.VariableDefinitionId,
                        principalTable: "VariableDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SimulationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SimulationModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RunType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IterationCount = table.Column<int>(type: "int", nullable: false),
                    RandomSeed = table.Column<int>(type: "int", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    InputSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutputSummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AssumptionSetVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SimulationRuns_Scenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalTable: "Scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SimulationResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SimulationRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetricName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MetricUnit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MeanValue = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    MedianValue = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    MinimumValue = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    MaximumValue = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    StandardDeviation = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Percentile10 = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Percentile25 = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Percentile75 = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Percentile90 = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    SampleCount = table.Column<int>(type: "int", nullable: false),
                    ResultMetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SimulationResults_SimulationRuns_SimulationRunId",
                        column: x => x.SimulationRunId,
                        principalTable: "SimulationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SimulationTimePoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SimulationRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimeIndex = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    MetricName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    LowerBound = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    UpperBound = table.Column<decimal>(type: "decimal(18,6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationTimePoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SimulationTimePoints_SimulationRuns_SimulationRunId",
                        column: x => x.SimulationRunId,
                        principalTable: "SimulationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIConversations_ContextType",
                table: "AIConversations",
                column: "ContextType");

            migrationBuilder.CreateIndex(
                name: "IX_AIConversations_DecisionId",
                table: "AIConversations",
                column: "DecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AIExecutionLogs_AIProposalId",
                table: "AIExecutionLogs",
                column: "AIProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_AIExecutionLogs_CreatedAtUtc",
                table: "AIExecutionLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AIMemories_IsActive",
                table: "AIMemories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AIMemories_MemoryType",
                table: "AIMemories",
                column: "MemoryType");

            migrationBuilder.CreateIndex(
                name: "IX_AIMessages_ConversationId_CreatedAtUtc",
                table: "AIMessages",
                columns: new[] { "ConversationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AIProposals_DecisionId",
                table: "AIProposals",
                column: "DecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AIProposals_Status",
                table: "AIProposals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AIProposals_Status_CreatedAtUtc",
                table: "AIProposals",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppSettings_Key",
                table: "AppSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssumptionSets_Name",
                table: "AssumptionSets",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAtUtc",
                table: "AuditLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_BackupHistories_CreatedAtUtc",
                table: "BackupHistories",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DataObservations_DataSourceId_ObservationDate",
                table: "DataObservations",
                columns: new[] { "DataSourceId", "ObservationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DataObservations_IndicatorId_ValidFromUtc",
                table: "DataObservations",
                columns: new[] { "IndicatorId", "ValidFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DataObservations_VariableDefinitionId_ObservationDate",
                table: "DataObservations",
                columns: new[] { "VariableDefinitionId", "ObservationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DataObservations_VerificationStatus",
                table: "DataObservations",
                column: "VerificationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_DataSources_IsActive",
                table: "DataSources",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DataSources_Name",
                table: "DataSources",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DecisionAlternatives_DecisionId",
                table: "DecisionAlternatives",
                column: "DecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionAlternatives_OneBaselinePerDecision",
                table: "DecisionAlternatives",
                columns: new[] { "DecisionId", "IsBaseline" },
                unique: true,
                filter: "[IsBaseline] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionDocuments_DecisionId",
                table: "DecisionDocuments",
                column: "DecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionDocuments_RelativePath",
                table: "DecisionDocuments",
                column: "RelativePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DecisionReviews_DecisionId_ReviewDate",
                table: "DecisionReviews",
                columns: new[] { "DecisionId", "ReviewDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Decisions_Category",
                table: "Decisions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Decisions_DecisionDate",
                table: "Decisions",
                column: "DecisionDate");

            migrationBuilder.CreateIndex(
                name: "IX_Decisions_ReviewScheduledForUtc",
                table: "Decisions",
                column: "ReviewScheduledForUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Decisions_Status",
                table: "Decisions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EconomicIndicators_Category",
                table: "EconomicIndicators",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_EconomicIndicators_Code_Country_NoCity",
                table: "EconomicIndicators",
                columns: new[] { "IndicatorCode", "CountryCode" },
                unique: true,
                filter: "[City] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EconomicIndicators_IndicatorCode_CountryCode_City",
                table: "EconomicIndicators",
                columns: new[] { "IndicatorCode", "CountryCode", "City" },
                unique: true,
                filter: "[City] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EconomicIndicators_SourceId",
                table: "EconomicIndicators",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_EntityTags_EntityType_EntityId",
                table: "EntityTags",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_EntityTags_TagId_EntityType_EntityId",
                table: "EntityTags",
                columns: new[] { "TagId", "EntityType", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskFactors_Category",
                table: "RiskFactors",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_RiskFactors_GeographicScope_Category",
                table: "RiskFactors",
                columns: new[] { "GeographicScope", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskFactors_IsActive",
                table: "RiskFactors",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioDependencies_DependsOnScenarioId",
                table: "ScenarioDependencies",
                column: "DependsOnScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioDependencies_ScenarioId_DependsOnScenarioId",
                table: "ScenarioDependencies",
                columns: new[] { "ScenarioId", "DependsOnScenarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Scenarios_AlternativeId",
                table: "Scenarios",
                column: "AlternativeId");

            migrationBuilder.CreateIndex(
                name: "IX_Scenarios_DecisionId",
                table: "Scenarios",
                column: "DecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Scenarios_DecisionId_Status",
                table: "Scenarios",
                columns: new[] { "DecisionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Scenarios_OneBaselinePerDecision",
                table: "Scenarios",
                columns: new[] { "DecisionId", "IsBaseline" },
                unique: true,
                filter: "[IsBaseline] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Scenarios_ParentScenarioId",
                table: "Scenarios",
                column: "ParentScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioVariables_ScenarioId_VariableDefinitionId",
                table: "ScenarioVariables",
                columns: new[] { "ScenarioId", "VariableDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioVariables_VariableDefinitionId",
                table: "ScenarioVariables",
                column: "VariableDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_SimulationModels_ModelType",
                table: "SimulationModels",
                column: "ModelType");

            migrationBuilder.CreateIndex(
                name: "IX_SimulationModels_Name_ModelVersion",
                table: "SimulationModels",
                columns: new[] { "Name", "ModelVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SimulationResults_SimulationRunId_MetricName",
                table: "SimulationResults",
                columns: new[] { "SimulationRunId", "MetricName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SimulationRuns_ScenarioId_StartedAtUtc",
                table: "SimulationRuns",
                columns: new[] { "ScenarioId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SimulationRuns_SimulationModelId",
                table: "SimulationRuns",
                column: "SimulationModelId");

            migrationBuilder.CreateIndex(
                name: "IX_SimulationRuns_Status",
                table: "SimulationRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SimulationTimePoints_SimulationRunId_Date",
                table: "SimulationTimePoints",
                columns: new[] { "SimulationRunId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_SimulationTimePoints_SimulationRunId_MetricName_TimeIndex",
                table: "SimulationTimePoints",
                columns: new[] { "SimulationRunId", "MetricName", "TimeIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VariableDefinitions_Category",
                table: "VariableDefinitions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_VariableDefinitions_IsExternalDataCandidate",
                table: "VariableDefinitions",
                column: "IsExternalDataCandidate");

            migrationBuilder.CreateIndex(
                name: "IX_VariableDefinitions_Key",
                table: "VariableDefinitions",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIExecutionLogs");

            migrationBuilder.DropTable(
                name: "AIMemories");

            migrationBuilder.DropTable(
                name: "AIMessages");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AssumptionSets");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BackupHistories");

            migrationBuilder.DropTable(
                name: "DataObservations");

            migrationBuilder.DropTable(
                name: "DecisionDocuments");

            migrationBuilder.DropTable(
                name: "DecisionReviews");

            migrationBuilder.DropTable(
                name: "EntityTags");

            migrationBuilder.DropTable(
                name: "RiskFactors");

            migrationBuilder.DropTable(
                name: "ScenarioDependencies");

            migrationBuilder.DropTable(
                name: "ScenarioVariables");

            migrationBuilder.DropTable(
                name: "SimulationModels");

            migrationBuilder.DropTable(
                name: "SimulationResults");

            migrationBuilder.DropTable(
                name: "SimulationTimePoints");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "AIProposals");

            migrationBuilder.DropTable(
                name: "AIConversations");

            migrationBuilder.DropTable(
                name: "EconomicIndicators");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "VariableDefinitions");

            migrationBuilder.DropTable(
                name: "SimulationRuns");

            migrationBuilder.DropTable(
                name: "DataSources");

            migrationBuilder.DropTable(
                name: "Scenarios");

            migrationBuilder.DropTable(
                name: "DecisionAlternatives");

            migrationBuilder.DropTable(
                name: "Decisions");
        }
    }
}
