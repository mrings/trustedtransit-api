namespace TrustedTransit.Api.Models
{
    public record Plan(string Key, string Name, int MonthlyPriceCents, string Description, string[] Features)
    {
        /// <summary>Stripe Price lookup_key — how StripeService finds/creates the recurring price.</summary>
        public string StripeLookupKey => $"tt_{Key}_monthly";
    }

    /// <summary>The subscription plan catalog. Placeholder pricing — edit here.</summary>
    public static class Plans
    {
        public const int TrialDays = 30;

        public static readonly IReadOnlyList<Plan> All = new[]
        {
            new Plan("starter", "Starter", 9900,
                "For a single facility getting started.",
                new[] { "Up to 50 residents", "Ride scheduling & tracking", "Monthly ride billing", "Email support" }),
            new Plan("pro", "Pro", 29900,
                "For busy facilities with recurring transport.",
                new[] { "Unlimited residents", "Recurring rides", "Staff roles & permissions", "Priority support" }),
            new Plan("enterprise", "Enterprise", 69900,
                "For multi-facility operators.",
                new[] { "Everything in Pro", "Multiple facilities", "Custom reporting", "Dedicated onboarding" }),
        };

        public static Plan? Get(string? key) =>
            key == null ? null : All.FirstOrDefault(p => p.Key == key);

        public static Plan? ByLookupKey(string? lookupKey) =>
            lookupKey == null ? null : All.FirstOrDefault(p => p.StripeLookupKey == lookupKey);

        public static bool IsValid(string? key) => Get(key) != null;
    }
}
