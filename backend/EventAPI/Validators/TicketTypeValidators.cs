using EventAPI.DTOs;
using FluentValidation;

namespace EventAPI.Validators
{
    public class CreateTicketTypeValidator : AbstractValidator<CreateTicketTypeDTO>
    {
        public CreateTicketTypeValidator()
        {
            RuleFor(x => x.TypeName)
                .NotEmpty().WithMessage("Ticket type name is required")
                .MaximumLength(100);

            RuleFor(x => x.Price)
                .GreaterThanOrEqualTo(0).WithMessage("Price must be >= 0");

            RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be > 0");

            RuleFor(x => x.MinPerOrder)
                .GreaterThan(0).WithMessage("MinPerOrder must be > 0");

            RuleFor(x => x.MaxPerOrder)
                .GreaterThanOrEqualTo(x => x.MinPerOrder)
                .WithMessage("MaxPerOrder must be >= MinPerOrder");

            RuleFor(x => x.SalesEndsAt)
                .GreaterThan(x => x.SalesStartsAt!.Value)
                .When(x => x.SalesStartsAt.HasValue && x.SalesEndsAt.HasValue)
                .WithMessage("Sales end must be after sales start");
        }
    }

    public class CreatePricingRuleValidator : AbstractValidator<CreatePricingRuleDTO>
    {
        private static readonly string[] ValidRuleTypes = { "EarlyBird", "LastMinute", "QuantityBased", "TimeBased" };

        public CreatePricingRuleValidator()
        {
            RuleFor(x => x.TicketTypeId)
                .GreaterThan(0).WithMessage("TicketTypeId is required");

            RuleFor(x => x.RuleName)
                .NotEmpty().WithMessage("Rule name is required")
                .MaximumLength(100);

            RuleFor(x => x.RuleType)
                .NotEmpty().WithMessage("Rule type is required")
                .Must(x => ValidRuleTypes.Contains(x))
                .WithMessage($"Rule type must be one of: {string.Join(", ", ValidRuleTypes)}");

            RuleFor(x => x)
                .Must(x => x.AdjustedPrice.HasValue || x.DiscountPercent.HasValue)
                .WithMessage("Either AdjustedPrice or DiscountPercent must be specified");

            RuleFor(x => x.DiscountPercent)
                .InclusiveBetween(0, 100)
                .When(x => x.DiscountPercent.HasValue)
                .WithMessage("Discount percent must be between 0 and 100");
        }
    }

    public class CreateRefundPolicyValidator : AbstractValidator<CreateRefundPolicyDTO>
    {
        public CreateRefundPolicyValidator()
        {
            RuleFor(x => x.PolicyName)
                .NotEmpty().WithMessage("Policy name is required")
                .MaximumLength(150);

            RuleFor(x => x.DeadlineBeforeEventHours)
                .GreaterThan(0).WithMessage("Deadline hours must be > 0");

            RuleFor(x => x.RefundPercent)
                .InclusiveBetween(0, 100)
                .WithMessage("Refund percent must be between 0 and 100");
        }
    }
}
