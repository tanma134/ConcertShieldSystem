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

            RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue)
                .WithMessage("Latitude must be between -90 and 90");
            RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue)
                .WithMessage("Longitude must be between -180 and 180");

            RuleFor(x => x.StartsAt)
                .NotEmpty().WithMessage("Start date is required")
                .Must(value => value > DateTime.UtcNow).WithMessage("Start date and time must be later than now (including hours and minutes)");

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

            RuleFor(x => x.HoldTimeoutMinutes)
                .InclusiveBetween(1, 30).When(x => x.HoldTimeoutMinutes.HasValue)
                .WithMessage("Seat hold timeout must be between 1 and 30 minutes.");

            RuleFor(x => x.ReEntryPolicy)
                .Must(p => Common.ReEntryPolicyTypes.IsValid(p)).When(x => !string.IsNullOrEmpty(x.ReEntryPolicy))
                .WithMessage("Re-entry policy must be NoReEntry, Unlimited, or TimeLimited.");

            RuleFor(x => x.ReEntryDurationMinutes)
                .InclusiveBetween(5, 120).When(x => x.ReEntryDurationMinutes.HasValue)
                .WithMessage("Time-limited re-entry duration must be between 5 and 120 minutes.");

            RuleFor(x => x)
                .Must(x => x.ReEntryDurationMinutes.HasValue && x.ReEntryDurationMinutes.Value >= 5 && x.ReEntryDurationMinutes.Value <= 120)
                .When(x => x.ReEntryPolicy?.Equals(Common.ReEntryPolicyTypes.TimeLimited, StringComparison.OrdinalIgnoreCase) == true)
                .WithMessage("Time-limited re-entry policy requires a duration between 5 and 120 minutes.");
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

            RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue)
                .WithMessage("Latitude must be between -90 and 90");
            RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue)
                .WithMessage("Longitude must be between -180 and 180");

            RuleFor(x => x.StartsAt)
                .Must(value => value > DateTime.UtcNow)
                .When(x => x.StartsAt.HasValue)
                .WithMessage("Start date and time must be later than now (including hours and minutes)");

            RuleFor(x => x.EndsAt)
                .GreaterThan(x => x.StartsAt!.Value)
                .When(x => x.StartsAt.HasValue && x.EndsAt.HasValue)
                .WithMessage("End date must be after start date");

            RuleFor(x => x.HoldTimeoutMinutes)
                .InclusiveBetween(1, 30).When(x => x.HoldTimeoutMinutes.HasValue)
                .WithMessage("Seat hold timeout must be between 1 and 30 minutes.");

            RuleFor(x => x.ReEntryPolicy)
                .Must(p => Common.ReEntryPolicyTypes.IsValid(p)).When(x => !string.IsNullOrEmpty(x.ReEntryPolicy))
                .WithMessage("Re-entry policy must be NoReEntry, Unlimited, or TimeLimited.");

            RuleFor(x => x.ReEntryDurationMinutes)
                .InclusiveBetween(5, 120).When(x => x.ReEntryDurationMinutes.HasValue)
                .WithMessage("Time-limited re-entry duration must be between 5 and 120 minutes.");

            RuleFor(x => x)
                .Must(x => x.ReEntryDurationMinutes.HasValue && x.ReEntryDurationMinutes.Value >= 5 && x.ReEntryDurationMinutes.Value <= 120)
                .When(x => x.ReEntryPolicy?.Equals(Common.ReEntryPolicyTypes.TimeLimited, StringComparison.OrdinalIgnoreCase) == true)
                .WithMessage("Time-limited re-entry policy requires a duration between 5 and 120 minutes.");
        }
    }
}
