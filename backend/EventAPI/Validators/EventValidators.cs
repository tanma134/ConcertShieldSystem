using EventAPI.DTOs;
using FluentValidation;

namespace EventAPI.Validators
{
    public class CreateEventValidator : AbstractValidator<CreateEventDTO>
    {
        public CreateEventValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required")
                .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

            RuleFor(x => x.ShortDescription)
                .MaximumLength(500).When(x => x.ShortDescription != null);

            RuleFor(x => x.LocationName)
                .MaximumLength(200).When(x => x.LocationName != null);

            RuleFor(x => x.Address)
                .MaximumLength(300).When(x => x.Address != null);

            RuleFor(x => x.City)
                .MaximumLength(100).When(x => x.City != null);

            RuleFor(x => x.StartsAt)
                .NotEmpty().WithMessage("Start date is required")
                .GreaterThan(DateTime.UtcNow).WithMessage("Start date must be in the future");

            RuleFor(x => x.EndsAt)
                .NotEmpty().WithMessage("End date is required")
                .GreaterThan(x => x.StartsAt).WithMessage("End date must be after start date");

            RuleFor(x => x.Timezone)
                .MaximumLength(50).When(x => x.Timezone != null);

            RuleFor(x => x.MinTicketsPerAccount)
                .GreaterThan(0).When(x => x.MinTicketsPerAccount.HasValue)
                .WithMessage("Minimum tickets per account must be greater than 0");

            RuleFor(x => x.MaxTicketsPerAccount)
                .GreaterThan(0).When(x => x.MaxTicketsPerAccount.HasValue)
                .WithMessage("Maximum tickets per account must be greater than 0");

            RuleFor(x => x)
                .Must(x => !x.MinTicketsPerAccount.HasValue || !x.MaxTicketsPerAccount.HasValue ||
                           x.MinTicketsPerAccount.Value <= x.MaxTicketsPerAccount.Value)
                .WithMessage("MinTicketsPerAccount must be <= MaxTicketsPerAccount");
        }
    }

    public class UpdateEventValidator : AbstractValidator<UpdateEventDTO>
    {
        public UpdateEventValidator()
        {
            RuleFor(x => x.Title)
                .MaximumLength(200).When(x => x.Title != null);

            RuleFor(x => x.ShortDescription)
                .MaximumLength(500).When(x => x.ShortDescription != null);

            RuleFor(x => x.LocationName)
                .MaximumLength(200).When(x => x.LocationName != null);

            RuleFor(x => x.Address)
                .MaximumLength(300).When(x => x.Address != null);

            RuleFor(x => x.City)
                .MaximumLength(100).When(x => x.City != null);

            RuleFor(x => x.StartsAt)
                .GreaterThan(DateTime.UtcNow)
                .When(x => x.StartsAt.HasValue)
                .WithMessage("Start date must be in the future");

            RuleFor(x => x.EndsAt)
                .GreaterThan(x => x.StartsAt!.Value)
                .When(x => x.StartsAt.HasValue && x.EndsAt.HasValue)
                .WithMessage("End date must be after start date");
        }
    }
}
