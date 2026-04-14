using MediatR;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Roadmap.Queries.GetCustomerSuggestions;

public sealed class GetRoadmapCustomerSuggestionsQueryHandler(
    IRoadmapDemandRepository demandRepository)
    : IRequestHandler<GetRoadmapCustomerSuggestionsQuery, IEnumerable<string>>
{
    public async Task<IEnumerable<string>> Handle(
        GetRoadmapCustomerSuggestionsQuery request,
        CancellationToken cancellationToken)
    {
        var demands = await demandRepository.GetAllAsync(cancellationToken);

        return demands
            .SelectMany(demand => demand.Customers)
            .Select(customer => customer.Trim())
            .Where(customer => !string.IsNullOrWhiteSpace(customer))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(customer => customer, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }
}