using GoMigo.Application.Places;
using Microsoft.AspNetCore.Mvc;

namespace GoMigo.Api.Controllers;

[ApiController]
[Route("api/places")]
[Produces("application/json")]
public sealed class PlacesController(PlacesService placesService) : ControllerBase
{
    /// <summary>
    /// Type-ahead suggestions for the location field (Bengaluru neighbourhoods first, then OpenStreetMap places).
    /// Pass the user's coordinates, when known, to bias results toward them.
    /// </summary>
    [HttpGet("autocomplete")]
    [ProducesResponseType<IReadOnlyList<PlaceSuggestionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<IReadOnlyList<PlaceSuggestionDto>> Autocomplete(
        [FromQuery] string? q, [FromQuery] double? latitude, [FromQuery] double? longitude, CancellationToken cancellationToken) =>
        placesService.AutocompleteAsync(q, latitude, longitude, cancellationToken);

    /// <summary>Human-readable name for a position, e.g. the user's current location.</summary>
    [HttpGet("reverse")]
    [ProducesResponseType<PlaceSuggestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PlaceSuggestionDto> Reverse([FromQuery] double latitude, [FromQuery] double longitude, CancellationToken cancellationToken) =>
        placesService.ReverseAsync(latitude, longitude, cancellationToken);
}
