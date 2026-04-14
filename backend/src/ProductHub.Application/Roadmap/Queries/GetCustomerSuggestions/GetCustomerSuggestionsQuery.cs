using MediatR;

namespace ProductHub.Application.Roadmap.Queries.GetCustomerSuggestions;

public sealed record GetRoadmapCustomerSuggestionsQuery() : IRequest<IEnumerable<string>>;