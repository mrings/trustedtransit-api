using Stripe;
using Stripe.Checkout;
using TrustedTransit.Api.Models;
using Plan = TrustedTransit.Api.Models.Plan;

namespace TrustedTransit.Api.Services
{
    /// <summary>Thin wrapper over Stripe for facility subscriptions.</summary>
    public class StripeBillingService
    {
        private readonly ILogger<StripeBillingService> _logger;
        private readonly Dictionary<string, string> _priceCache = new();

        public bool Enabled { get; }
        public string? WebhookSecret { get; }

        public StripeBillingService(IConfiguration config, ILogger<StripeBillingService> logger)
        {
            _logger = logger;
            var key = config["Stripe:SecretKey"];
            WebhookSecret = config["Stripe:WebhookSecret"];
            Enabled = !string.IsNullOrWhiteSpace(key);
            if (Enabled)
                StripeConfiguration.ApiKey = key;
        }

        /// <summary>Price id for a plan, creating the Stripe Product + recurring Price on first use.</summary>
        public async Task<string> EnsurePriceIdAsync(Plan plan)
        {
            if (_priceCache.TryGetValue(plan.Key, out var cached))
                return cached;

            var existing = await new PriceService().ListAsync(new PriceListOptions
            {
                LookupKeys = new List<string> { plan.StripeLookupKey },
                Limit = 1,
            });
            if (existing.Data.Count > 0)
                return _priceCache[plan.Key] = existing.Data[0].Id;

            var product = await new ProductService().CreateAsync(new ProductCreateOptions
            {
                Name = $"TrustedTransit {plan.Name}",
                Description = plan.Description,
                Metadata = new Dictionary<string, string> { ["tt_plan"] = plan.Key },
            });
            var price = await new PriceService().CreateAsync(new PriceCreateOptions
            {
                Product = product.Id,
                UnitAmount = plan.MonthlyPriceCents,
                Currency = "usd",
                Recurring = new PriceRecurringOptions { Interval = "month" },
                LookupKey = plan.StripeLookupKey,
            });
            _logger.LogInformation("Created Stripe price {PriceId} for plan {Plan}", price.Id, plan.Key);
            return _priceCache[plan.Key] = price.Id;
        }

        public async Task<string> GetOrCreateCustomerAsync(Facility facility)
        {
            if (!string.IsNullOrEmpty(facility.StripeCustomerId))
                return facility.StripeCustomerId;

            var customer = await new CustomerService().CreateAsync(new CustomerCreateOptions
            {
                Name = facility.Name,
                Metadata = new Dictionary<string, string> { ["facility_id"] = facility.Id.ToString() },
            });
            facility.StripeCustomerId = customer.Id;
            return customer.Id;
        }

        public async Task<string> CreateCheckoutUrlAsync(Facility facility, Plan plan, string returnBase)
        {
            var priceId = await EnsurePriceIdAsync(plan);
            var customerId = await GetOrCreateCustomerAsync(facility);

            var session = await new SessionService().CreateAsync(new SessionCreateOptions
            {
                Mode = "subscription",
                Customer = customerId,
                ClientReferenceId = facility.Id.ToString(),
                LineItems = new List<SessionLineItemOptions>
                {
                    new() { Price = priceId, Quantity = 1 },
                },
                SuccessUrl = $"{returnBase}/?sub=success",
                CancelUrl = $"{returnBase}/?sub=cancel",
            });
            return session.Url;
        }

        public async Task<string> CreatePortalUrlAsync(Facility facility, string returnBase)
        {
            var session = await new Stripe.BillingPortal.SessionService().CreateAsync(
                new Stripe.BillingPortal.SessionCreateOptions
                {
                    Customer = facility.StripeCustomerId,
                    ReturnUrl = $"{returnBase}/?sub=portal",
                });
            return session.Url;
        }

        public async Task UpdatePlanAsync(Facility facility, Plan plan)
        {
            var priceId = await EnsurePriceIdAsync(plan);
            var sub = await new SubscriptionService().GetAsync(facility.StripeSubscriptionId);
            await new SubscriptionService().UpdateAsync(sub.Id, new SubscriptionUpdateOptions
            {
                CancelAtPeriodEnd = false,
                ProrationBehavior = "create_prorations",
                Items = new List<SubscriptionItemOptions>
                {
                    new() { Id = sub.Items.Data[0].Id, Price = priceId },
                },
            });
        }

        public async Task CancelAsync(Facility facility)
        {
            await new SubscriptionService().UpdateAsync(facility.StripeSubscriptionId,
                new SubscriptionUpdateOptions { CancelAtPeriodEnd = true });
        }

        public async Task ResumeAsync(Facility facility)
        {
            await new SubscriptionService().UpdateAsync(facility.StripeSubscriptionId,
                new SubscriptionUpdateOptions { CancelAtPeriodEnd = false });
        }

        /// <summary>Maps a Stripe subscription onto the facility's local fields.</summary>
        public static void Apply(Facility facility, Subscription sub)
        {
            facility.StripeSubscriptionId = sub.Id;
            facility.CancelAtPeriodEnd = sub.CancelAtPeriodEnd;

            var item = sub.Items?.Data?.FirstOrDefault();
            var lookupKey = item?.Price?.LookupKey;
            var plan = Plans.ByLookupKey(lookupKey);
            if (plan != null)
                facility.SubscriptionTier = plan.Key;

            var periodEnd = item?.CurrentPeriodEnd ?? sub.BillingCycleAnchor;
            facility.SubscriptionRenewsAt = periodEnd == default ? facility.SubscriptionRenewsAt : periodEnd;

            facility.SubscriptionStatus = sub.Status switch
            {
                "active" or "trialing" => "active",
                "past_due" or "unpaid" => "past_due",
                "canceled" or "incomplete_expired" => "canceled",
                _ => facility.SubscriptionStatus,
            };
            facility.UpdatedAt = DateTime.UtcNow;
        }
    }
}
