using TourPlatformApi.Domain;

namespace TourPlatformApi.Notifications;

public sealed class SubscriptionNotificationPolicy
{
    public bool SendTicketToIntermediary(SubscriptionPlan plan) =>
        plan is SubscriptionPlan.Medium or SubscriptionPlan.Lux or SubscriptionPlan.Unlimited;

    public bool SendTicketToClient(SubscriptionPlan plan) =>
        plan is SubscriptionPlan.Lux or SubscriptionPlan.Unlimited;

    public bool SendWhatsAppDepartureUpdate(SubscriptionPlan plan) =>
        plan is SubscriptionPlan.Lux or SubscriptionPlan.Unlimited;

    public decimal SeasonalMonthlyPrice(decimal baseMonthlyPrice, int month) =>
        month is >= 5 and <= 10
            ? baseMonthlyPrice
            : baseMonthlyPrice / 2m;
}
