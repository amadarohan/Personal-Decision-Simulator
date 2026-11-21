namespace DecisionSimulator.Web.Localization;

/// <summary>
/// Every piece of user-facing text in the application.
/// <para>
/// The catalogue is a strongly typed class rather than a key/value lookup on
/// purpose. A missing translation becomes a compile error instead of a silent
/// fallback to English, and renaming a string is a rename rather than a search
/// for a magic key. Each supported language supplies one complete instance.
/// </para>
/// </summary>
public sealed class AppStrings
{
    // Shell and navigation.
    public required string BrandName { get; init; }
    public required string NavOverview { get; init; }
    public required string NavDecisions { get; init; }
    public required string NavScenarios { get; init; }
    public required string NavSimulations { get; init; }
    public required string NavRisks { get; init; }
    public required string NavData { get; init; }
    public required string NavAssistant { get; init; }
    public required string NavSettings { get; init; }
    public required string Disclaimer { get; init; }
    public required string LanguageName { get; init; }
    public required string SwitchLanguage { get; init; }

    // Shared vocabulary.
    public required string Loading { get; init; }
    public required string Saving { get; init; }
    public required string Creating { get; init; }
    public required string Running { get; init; }
    public required string Thinking { get; init; }
    public required string Cancel { get; init; }
    public required string Edit { get; init; }
    public required string Remove { get; init; }
    public required string Yes { get; init; }
    public required string No { get; init; }
    public required string Never { get; init; }
    public required string Dash { get; init; }
    public required string Months { get; init; }
    public required string Paths { get; init; }
    public required string Messages { get; init; }

    // Overview page.
    public required string OverviewTitle { get; init; }
    public required string OverviewLede { get; init; }
    public required string StatDecisions { get; init; }
    public required string StatStillOpen { get; init; }
    public required string StatModels { get; init; }
    public required string StatAssistant { get; init; }
    public required string AssistantReady { get; init; }
    public required string AssistantOffline { get; init; }
    public required string RecentDecisions { get; init; }
    public required string NoDecisionsYet { get; init; }
    public required string CreateFirstOne { get; init; }
    public required string AvailableModels { get; init; }
    public required string ColTitle { get; init; }
    public required string ColCategory { get; init; }
    public required string ColStatus { get; init; }
    public required string ColAlternatives { get; init; }
    public required string ColScenarios { get; init; }
    public required string ColUpdated { get; init; }

    // Decisions page.
    public required string DecisionsTitle { get; init; }
    public required string DecisionsLede { get; init; }
    public required string NewDecision { get; init; }
    public required string FieldTitle { get; init; }
    public required string TitlePlaceholder { get; init; }
    public required string FieldCategory { get; init; }
    public required string FieldImportance { get; init; }
    public required string FieldHorizon { get; init; }
    public required string FieldDescription { get; init; }
    public required string CreateDecision { get; init; }
    public required string AllDecisions { get; init; }
    public required string NothingRecorded { get; init; }
    public required string ColImportance { get; init; }
    public required string ErrorTitleRequired { get; init; }

    // Decision detail page.
    public required string DecisionFallbackTitle { get; init; }
    public required string Created { get; init; }
    public required string Alternatives { get; init; }
    public required string NoAlternatives { get; init; }
    public required string Baseline { get; init; }
    public required string InitialCost { get; init; }
    public required string RecurringCost { get; init; }
    public required string FieldName { get; init; }
    public required string AlternativeNamePlaceholder { get; init; }
    public required string AddAlternative { get; init; }
    public required string ScenariosHeading { get; init; }
    public required string NoScenarios { get; init; }
    public required string Reviews { get; init; }
    public required string NoReviews { get; init; }
    public required string Lesson { get; init; }
    public required string ErrorAlternativeNameRequired { get; init; }
    public required string ColHorizon { get; init; }
    public required string ColVariables { get; init; }
    public required string ColRuns { get; init; }

    // Scenarios page.
    public required string ScenariosTitle { get; init; }
    public required string ScenariosLede { get; init; }
    public required string NewScenario { get; init; }
    public required string FieldDecision { get; init; }
    public required string SelectDecision { get; init; }
    public required string ScenarioNamePlaceholder { get; init; }
    public required string FieldType { get; init; }
    public required string FieldHorizonMonths { get; init; }
    public required string CreateScenario { get; init; }
    public required string AllScenarios { get; init; }
    public required string NoScenariosYet { get; init; }
    public required string ErrorChooseDecision { get; init; }
    public required string ErrorScenarioNameRequired { get; init; }

    // Scenario detail page.
    public required string ScenarioFallbackTitle { get; init; }
    public required string AssumptionSet { get; init; }
    public required string AssumptionsNotice { get; init; }
    public required string Assumptions { get; init; }
    public required string NoAssumptions { get; init; }
    public required string ColVariable { get; init; }
    public required string ColValue { get; init; }
    public required string ColDistribution { get; init; }
    public required string ColRange { get; init; }
    public required string ColConfidence { get; init; }
    public required string ColSource { get; init; }
    public required string AddAssumption { get; init; }
    public required string EditAssumption { get; init; }
    public required string NoVariableDefinitions { get; init; }
    public required string DataPageLink { get; init; }
    public required string FieldVariable { get; init; }
    public required string SelectVariable { get; init; }
    public required string FieldValue { get; init; }
    public required string FieldDistribution { get; init; }
    public required string FieldConfidence { get; init; }
    public required string FieldSource { get; init; }
    public required string FieldSourceReference { get; init; }
    public required string SourceReferencePlaceholder { get; init; }
    public required string FieldMinimum { get; init; }
    public required string FieldMaximum { get; init; }
    public required string FieldStandardDeviation { get; init; }
    public required string DistributionNeedsRange { get; init; }
    public required string DistributionNeedsSigma { get; init; }
    public required string SaveAssumption { get; init; }
    public required string RunHistory { get; init; }
    public required string NotRunYet { get; init; }
    public required string ColStarted { get; init; }
    public required string ColType { get; init; }
    public required string ColIterations { get; init; }
    public required string ColSeed { get; init; }
    public required string ColAssumptions { get; init; }
    public required string ColDetail { get; init; }
    public required string RunThisScenario { get; init; }
    public required string ErrorChooseVariable { get; init; }
    public required string ErrorRangeRequired { get; init; }
    public required string ErrorSigmaRequired { get; init; }
    public required string AssumptionSaved { get; init; }
    public required string RemovedPrefix { get; init; }

    // Simulations page.
    public required string SimulationsTitle { get; init; }
    public required string SimulationsLede { get; init; }
    public required string RunScenarioHeading { get; init; }
    public required string FieldScenario { get; init; }
    public required string SelectScenario { get; init; }
    public required string FieldModel { get; init; }
    public required string FieldIterations { get; init; }
    public required string FieldSeed { get; init; }
    public required string SeedPlaceholder { get; init; }
    public required string RunSimulation { get; init; }
    public required string IterationsLabel { get; init; }
    public required string SeedLabel { get; init; }
    public required string AssumptionSetShort { get; init; }
    public required string MissingAssumptionsPrefix { get; init; }
    public required string ColMetric { get; init; }
    public required string ColMean { get; init; }
    public required string ColStdDev { get; init; }
    public required string ColMin { get; init; }
    public required string ColMax { get; init; }
    public required string ColPercentiles { get; init; }
    public required string NegativeCashNote { get; init; }
    public required string ModelAssumptions { get; init; }
    public required string Warnings { get; init; }
    public required string ErrorChooseScenario { get; init; }
    public required string ErrorChooseModel { get; init; }

    // Risks page.
    public required string RisksTitle { get; init; }
    public required string RisksLede { get; init; }
    public required string NewRiskFactor { get; init; }
    public required string RiskNamePlaceholder { get; init; }
    public required string FieldScope { get; init; }
    public required string FieldDirection { get; init; }
    public required string FieldLikelihood { get; init; }
    public required string SourceReferencePlaceholderRisk { get; init; }
    public required string AddRiskFactor { get; init; }
    public required string RecordedRisks { get; init; }
    public required string NoRisks { get; init; }
    public required string ColScope { get; init; }
    public required string ColDirection { get; init; }
    public required string ColLikelihood { get; init; }
    public required string ColVerified { get; init; }
    public required string ColActive { get; init; }
    public required string ErrorRiskNameRequired { get; init; }

    // Data page.
    public required string DataTitle { get; init; }
    public required string DataLede { get; init; }
    public required string DataSources { get; init; }
    public required string NoSources { get; init; }
    public required string ColProvider { get; init; }
    public required string ColReliability { get; init; }
    public required string ColUpdateFrequency { get; init; }
    public required string FieldBaseUrl { get; init; }
    public required string AddSource { get; init; }
    public required string Indicators { get; init; }
    public required string NoIndicators { get; init; }
    public required string ColCode { get; init; }
    public required string ColCountry { get; init; }
    public required string ColUnit { get; init; }
    public required string ColLatestValue { get; init; }
    public required string ColObserved { get; init; }
    public required string ColFreshness { get; init; }
    public required string FieldCode { get; init; }
    public required string FieldCountryCode { get; init; }
    public required string FieldUnit { get; init; }
    public required string SourceNamePlaceholder { get; init; }
    public required string IndicatorNamePlaceholder { get; init; }
    public required string IndicatorCodePlaceholder { get; init; }
    public required string UnitPlaceholder { get; init; }
    public required string AddIndicator { get; init; }
    public required string ErrorSourceNameRequired { get; init; }
    public required string ErrorIndicatorRequired { get; init; }

    // Settings page.
    public required string SettingsTitle { get; init; }
    public required string SettingsLede { get; init; }
    public required string Profile { get; init; }
    public required string FieldDisplayName { get; init; }
    public required string FieldCountryName { get; init; }
    public required string FieldCity { get; init; }
    public required string FieldTimeZone { get; init; }
    public required string FieldLanguage { get; init; }
    public required string FieldCalendar { get; init; }
    public required string FieldCurrencyCode { get; init; }
    public required string FieldCurrencyUnit { get; init; }

    /// <summary>Unit label for amounts displayed in Toman.</summary>
    public required string CurrencyToman { get; init; }

    /// <summary>Unit label for amounts displayed in the base currency unit.</summary>
    public required string CurrencyRial { get; init; }

    public required string SaveProfile { get; init; }
    public required string ProfileSaved { get; init; }
    public required string ApplicationSettings { get; init; }
    public required string SecretsNote { get; init; }
    public required string NoSettings { get; init; }
    public required string ColKey { get; init; }
    public required string SaveSetting { get; init; }
    public required string ErrorDisplayNameRequired { get; init; }
    public required string ErrorSettingKeyRequired { get; init; }

    // Assistant page.
    public required string AssistantTitle { get; init; }
    public required string AssistantLede { get; init; }
    public required string NoModelConfigured { get; init; }
    public required string EverythingElseWorks { get; init; }
    public required string Conversations { get; init; }
    public required string ConversationTitlePlaceholder { get; init; }
    public required string FieldContext { get; init; }
    public required string StartConversation { get; init; }
    public required string YourMessage { get; init; }
    public required string FieldRole { get; init; }
    public required string FieldSensitivity { get; init; }
    public required string Send { get; init; }
    public required string ErrorConversationTitleRequired { get; init; }
    public required string ErrorWriteSomething { get; init; }
}
