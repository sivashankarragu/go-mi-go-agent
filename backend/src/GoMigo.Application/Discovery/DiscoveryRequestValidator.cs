using System.Globalization;
using GoMigo.Application.Common;
using GoMigo.Application.Discovery.Contracts;
using GoMigo.Domain.Discovery;

namespace GoMigo.Application.Discovery;

public static class DiscoveryRequestValidator
{
    /// <summary>Returns per-field errors keyed by camelCase property name; empty when valid.</summary>
    public static IReadOnlyDictionary<string, string[]> Validate(DiscoveryRequestDto dto, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list))
            {
                errors[field] = list = [];
            }
            list.Add(message);
        }

        var location = dto.LocationText?.Trim();
        var hasLat = dto.Latitude is not null;
        var hasLon = dto.Longitude is not null;

        if (hasLat != hasLon)
        {
            Add(hasLat ? "longitude" : "latitude", "Latitude and longitude must be provided together.");
        }
        if (dto.Latitude is { } lat && (double.IsNaN(lat) || lat is < -90 or > 90))
        {
            Add("latitude", "Latitude must be between -90 and 90.");
        }
        if (dto.Longitude is { } lon && (double.IsNaN(lon) || lon is < -180 or > 180))
        {
            Add("longitude", "Longitude must be between -180 and 180.");
        }

        if (string.IsNullOrEmpty(location))
        {
            if (!(hasLat && hasLon))
            {
                Add("locationText", "Enter a location or share your current location.");
            }
        }
        else if (location.Length > DiscoveryLimits.MaxLocationLength)
        {
            Add("locationText", $"Location must be at most {DiscoveryLimits.MaxLocationLength} characters.");
        }

        if (dto.DurationMinutes is not { } duration)
        {
            Add("durationMinutes", "Available time is required.");
        }
        else if (duration is < DiscoveryLimits.MinDurationMinutes or > DiscoveryLimits.MaxDurationMinutes)
        {
            Add("durationMinutes",
                $"Available time must be between {DiscoveryLimits.MinDurationMinutes} and {DiscoveryLimits.MaxDurationMinutes} minutes.");
        }

        if (dto.BudgetPerPerson is not { } budget)
        {
            Add("budgetPerPerson", "Budget per person is required.");
        }
        else if (budget < DiscoveryLimits.MinBudgetPerPerson || budget > DiscoveryLimits.MaxBudgetPerPerson)
        {
            Add("budgetPerPerson",
                $"Budget per person must be between {DiscoveryLimits.MinBudgetPerPerson} and {DiscoveryLimits.MaxBudgetPerPerson.ToString(CultureInfo.InvariantCulture)}.");
        }
        else if (decimal.Round(budget, 2) != budget)
        {
            Add("budgetPerPerson", "Budget per person can have at most two decimal places.");
        }

        if (dto.Currency is { } currency
            && !DiscoveryLimits.SupportedCurrencies.Contains(currency.Trim().ToUpperInvariant()))
        {
            Add("currency", $"Supported currencies: {string.Join(", ", DiscoveryLimits.SupportedCurrencies)}.");
        }

        if (dto.Category is { } category && (!Enum.IsDefined(category) || category == ActivityCategory.Other))
        {
            Add("category", "Unknown activity category.");
        }

        if (dto.StartTime is { } start)
        {
            if (start < nowUtc - DiscoveryLimits.MaxStartTimeInPast)
            {
                Add("startTime", "Start time cannot be in the past.");
            }
            else if (start > nowUtc + DiscoveryLimits.MaxStartTimeInFuture)
            {
                Add("startTime", "Start time must be within the next 7 days.");
            }
        }

        if (dto.AdditionalPreferences is { Length: > DiscoveryLimits.MaxPreferencesLength })
        {
            Add("additionalPreferences",
                $"Additional preferences must be at most {DiscoveryLimits.MaxPreferencesLength} characters.");
        }

        return errors.ToDictionary(e => e.Key, e => e.Value.ToArray());
    }

    /// <summary>Validates and converts to the domain model, throwing <see cref="RequestValidationException"/> when invalid.</summary>
    public static DiscoveryRequest ValidateAndMap(DiscoveryRequestDto dto, DateTimeOffset nowUtc)
    {
        var errors = Validate(dto, nowUtc);
        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        var location = dto.LocationText?.Trim();
        if (string.IsNullOrEmpty(location))
        {
            location = string.Create(CultureInfo.InvariantCulture, $"{dto.Latitude:F5}, {dto.Longitude:F5}");
        }

        return new DiscoveryRequest
        {
            LocationText = location,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            DurationMinutes = dto.DurationMinutes!.Value,
            BudgetPerPerson = dto.BudgetPerPerson!.Value,
            Currency = dto.Currency?.Trim().ToUpperInvariant() ?? Money.DefaultCurrency,
            Category = dto.Category ?? ActivityCategory.Any,
            StartTime = dto.StartTime,
            AdditionalPreferences = string.IsNullOrWhiteSpace(dto.AdditionalPreferences) ? null : dto.AdditionalPreferences.Trim()
        };
    }
}
