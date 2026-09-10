namespace TrustedTransit.Api.Models
{
    public record Plan(
        string Key, string Name, int MonthlyPriceCents, string Description, string[] Features,
        int ResidentLimit, bool RecurringRides)
    {
        /// <summary>Stripe Price lookup_key — how StripeService finds/creates the recurring price.</summary>
        public string StripeLookupKey => $"tt_{Key}_monthly";

        public bool UnlimitedResidents => ResidentLimit >= int.MaxValue;
    }

    /// <summary>The subscription plan catalog + entitlements. Placeholder pricing — edit here.</summary>
    public static class Plans
    {
        public const int TrialDays = 30;
        private const int Unlimited = int.MaxValue;

        public static readonly IReadOnlyList<Plan> All = new[]
        {
            new Plan("starter", "Starter", 9900,
                "For a single facility getting started.",
                new[] { "Up to 50 residents", "Ride scheduling & tracking", "Monthly ride billing", "Email support" },
                ResidentLimit: 50, RecurringRides: false),
            new Plan("pro", "Pro", 29900,
                "For busy facilities with recurring transport.",
                new[] { "Unlimited residents", "Recurring rides", "Staff roles & permissions", "Priority support" },
                ResidentLimit: Unlimited, RecurringRides: true),
            new Plan("enterprise", "Enterprise", 69900,
                "For multi-facility operators.",
                new[] { "Everything in Pro", "Multiple facilities", "Custom reporting", "Dedicated onboarding" },
                ResidentLimit: Unlimited, RecurringRides: true),
        };

        public static readonly Plan Default = All[0];

        public static Plan Resolve(string? key) => Get(key) ?? Default;

        public static Plan? Get(string? key) =>
            key == null ? null : All.FirstOrDefault(p => p.Key == key);

        public static Plan? ByLookupKey(string? lookupKey) =>
            lookupKey == null ? null : All.FirstOrDefault(p => p.StripeLookupKey == lookupKey);

        public static bool IsValid(string? key) => Get(key) != null;
    }

    /// <summary>What a facility is allowed to do right now, given its plan + subscription state.</summary>
    public static class Entitlements
    {
        /// <summary>
        /// True when the facility can create new records: an active/past-due subscription, a live
        /// trial, or a canceled subscription still within its paid period.
        /// </summary>
        public static bool CanWrite(Facility f)
        {
            var now = DateTime.UtcNow;
            return f.SubscriptionStatus switch
            {
                "active" or "past_due" => true,
                "trial" => f.TrialEndsAt == null || f.TrialEndsAt > now,
                "canceled" => f.SubscriptionRenewsAt != null && f.SubscriptionRenewsAt > now,
                _ => false,
            };
        }

        public static string BlockedReason(Facility f) => f.SubscriptionStatus switch
        {
            "trial" => "Your free trial has ended.",
            "canceled" => "Your subscription has ended.",
            _ => "Your subscription is inactive.",
        };
    }
}
