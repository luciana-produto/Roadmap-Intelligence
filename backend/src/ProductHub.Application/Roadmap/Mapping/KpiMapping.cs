using ProductHub.Application.Roadmap.DTOs;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Application.Roadmap.Mapping;

public static class KpiMapping
{
    public static IReadOnlyList<KpiUnit> ParseUnits(IEnumerable<string>? units)
    {
        if (units is null)
            return [];

        var result = new List<KpiUnit>();
        foreach (var raw in units)
        {
            if (Enum.TryParse<KpiUnit>(raw, true, out var unit) && !result.Contains(unit))
                result.Add(unit);
        }
        return result;
    }

    /// <summary>
    /// Direção do impacto do vínculo derivada da operação do KPI (fonte de verdade):
    /// "Quanto maior melhor" =&gt; Aumentar; "Quanto menor melhor" =&gt; Reduzir.
    /// </summary>
    public static ImpactType ImpactTypeFromOperation(KpiOperation operation) =>
        operation == KpiOperation.LowerIsBetter ? ImpactType.Decrease : ImpactType.Increase;

    public static KpiDto ToDto(Kpi kpi, int linkedDemandsCount) =>
        new(
            kpi.Id,
            kpi.ProjectId,
            kpi.Name,
            kpi.Type.ToString(),
            kpi.Category.ToString(),
            kpi.Indicator.ToString(),
            kpi.Operation.ToString(),
            kpi.AllowedUnits.Select(u => u.ToString()).ToList(),
            kpi.Description,
            linkedDemandsCount,
            kpi.CreatedAt,
            kpi.UpdatedAt);
}
