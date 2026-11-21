namespace TourPlatformApi.Models;

public enum AccountType { Agency, Intermediary }
public enum SubscriptionPlan { Base, Medium, Lux, Unlimited }
public enum UserRole
{
    AgencyGuide,
    AgencyManager,
    AgencyAccountant,
    AgencyOwner,
    IntermediaryDriver,
    IntermediaryDispatcher,
    IntermediaryOwner
}
public enum Currency { USD, EUR, GBP, TRY }
public enum BookingStatus { Draft, Confirmed, Cancelled }
