using Microsoft.EntityFrameworkCore;
using ProductHub.Domain.Roadmap;
using ProductHub.Infrastructure.Persistence;

namespace ProductHub.Infrastructure.Persistence.Seed;

public static class RoadmapSeeder
{
    private sealed record MockDemandSeed(
        int QuarterYear,
        int QuarterNumber,
        string Title,
        string? Description,
        DemandStatus Status,
        DemandType Type,
        DemandClassification Classification,
        decimal Hours,
        string[] ProductNames,
        string? Observation = null,
        string? JiraIssue = null,
        string[]? Customers = null,
        bool IsBlocked = false,
        string? BlockedReason = null,
        DateOnly? DeliveryDate = null);

    private sealed record MockDependencySeed(string DemandKey, string DependsOnDemandKey);

    private static readonly (string Name, string Slug, string[] Products)[] Data =
    [
        ("Cross",       "cross",       ["SVO", "Taste", "HUB CRM", "SVC"]),
        ("Neemo",       "neemo",       ["Neemo"]),
        ("Taste",       "taste",       ["Taste PDV", "Taste AA"]),
        ("Retaguarda",  "retaguarda",  ["Retaguarda"]),
        ("Degust",      "degust",      ["Degust PDV", "Hub Delivery"]),
        ("Menew",       "menew",       ["Menew"])
    ];

    private static readonly MockDemandSeed[] CrossQ22026Demands =
    [
        new(
            2026,
            2,
            "Padronizar autenticação SSO no ecossistema Cross",
            "Unifica o fluxo de autenticação entre SVO, HUB CRM e SVC para reduzir retrabalho operacional.",
            DemandStatus.InProgress,
            DemandType.Planned,
            DemandClassification.Strategic,
            120,
            ["SVO", "HUB CRM", "SVC"],
            Observation: "Integração técnica priorizada pelo time de plataforma.",
            JiraIssue: "CROSS-201",
            Customers: ["Operações", "CS"]),
        new(
            2026,
            2,
            "Adequar trilha de auditoria LGPD do HUB CRM",
            "Reforça logs sensíveis e políticas de retenção para auditoria e conformidade.",
            DemandStatus.Backlog,
            DemandType.Planned,
            DemandClassification.Mandatory,
            64,
            ["HUB CRM"],
            Observation: "Escopo alinhado com jurídico e segurança.",
            JiraIssue: "CROSS-214",
            Customers: ["Jurídico"]),
        new(
            2026,
            2,
            "Reduzir latência de sincronização do cardápio Taste",
            "Melhora o tempo de propagação do cardápio em integrações do Cross.",
            DemandStatus.Done,
            DemandType.Spillover,
            DemandClassification.ImprovementGap,
            40,
            ["Taste"],
            Observation: "Entrega concluída e validada em homologação.",
            JiraIssue: "CROSS-176",
            Customers: ["Implantação"],
            DeliveryDate: new DateOnly(2026, 5, 18)),
        new(
            2026,
            2,
            "Atualizar dependências críticas do gateway SVC",
            "Pacote de atualização para corrigir vulnerabilidades e manter compatibilidade com o gateway.",
            DemandStatus.Backlog,
            DemandType.Unplanned,
            DemandClassification.TechnicalDebtSecurity,
            52,
            ["SVC"],
            Observation: "Janela de deploy depende de aprovação de infraestrutura.",
            JiraIssue: "CROSS-223",
            Customers: ["Infra", "Segurança"],
            IsBlocked: true,
            BlockedReason: "Aguardando liberação da janela de manutenção do balanceador."),
        new(
            2026,
            2,
            "Criar dashboard executivo de funil comercial",
            "Consolida indicadores do HUB CRM para acompanhamento executivo.",
            DemandStatus.Deprioritized,
            DemandType.Additional,
            DemandClassification.Evolution,
            24,
            ["HUB CRM"],
            Observation: "Demanda deslocada para acomodar iniciativas mandatórias.",
            JiraIssue: "CROSS-231",
            Customers: ["Comercial"]),
        new(
            2026,
            2,
            "Homologar fluxo omnichannel de pedidos do Cross",
            "Valida a jornada fim a fim entre SVO e Taste com cenários de regressão.",
            DemandStatus.InProgress,
            DemandType.Additional,
            DemandClassification.Homologation,
            36,
            ["SVO", "Taste"],
            Observation: "Homologação compartilhada com operações de loja.",
            JiraIssue: "CROSS-239",
            Customers: ["Suporte", "Operações"])
    ];

    private static readonly MockDemandSeed[] CrossBacklogDemands =
    [
        new(
            Quarter.BacklogYear,
            Quarter.BacklogNumber,
            "Mapear nova esteira de onboarding self-service no Cross",
            "Descoberta funcional para estruturar a entrada autônoma de novos clientes no ecossistema Cross.",
            DemandStatus.Backlog,
            DemandType.Planned,
            DemandClassification.Strategic,
            48,
            ["HUB CRM", "SVO"],
            Observation: "Aguardando refinamento final com produto e operações.",
            JiraIssue: "CROSS-245",
            Customers: ["Produto", "Operações"]),
        new(
            Quarter.BacklogYear,
            Quarter.BacklogNumber,
            "Preparar base para centralização de notificações transacionais",
            "Spike técnico para consolidar notificações críticas entre HUB CRM, Taste e SVC.",
            DemandStatus.Backlog,
            DemandType.Unplanned,
            DemandClassification.Evolution,
            32,
            ["HUB CRM", "Taste", "SVC"],
            Observation: "Backlog técnico-comercial sem quarter comprometido.",
            JiraIssue: "CROSS-246",
            Customers: ["Comercial", "Suporte"])
    ];

    private static readonly MockDemandSeed[] CrossDemands = [.. CrossQ22026Demands, .. CrossBacklogDemands];

    private static readonly MockDemandSeed[] RetaguardaQ22026Demands =
    [
        new(
            2026,
            2,
            "Modernizar motor de conciliação fiscal da Retaguarda",
            "Atualiza a base transacional usada pela retaguarda para suportar integrações mais estáveis com o ecossistema Cross.",
            DemandStatus.InProgress,
            DemandType.Planned,
            DemandClassification.Mandatory,
            96,
            ["Retaguarda"],
            Observation: "Entrega acompanhada pelo time financeiro e arquitetura.",
            JiraIssue: "RET-101",
            Customers: ["Financeiro", "Arquitetura"]),
        new(
            2026,
            2,
            "Aprimorar fila de publicação de cadastros mestres",
            "Prepara a retaguarda para publicar eventos mais confiáveis consumidos por Cross e canais satélites.",
            DemandStatus.Backlog,
            DemandType.Spillover,
            DemandClassification.Evolution,
            44,
            ["Retaguarda"],
            Observation: "Refinamento pendente com o time de plataforma.",
            JiraIssue: "RET-102",
            Customers: ["Plataforma"])
    ];

    private static readonly MockDependencySeed[] CrossProjectDependencies =
    [
        new("CROSS-201", "RET-101"),
        new("CROSS-214", "RET-101"),
        new("CROSS-245", "RET-102")
    ];

    public static async Task SeedAsync(AppDbContext context)
    {
        var projects = await context.RoadmapProjects
            .Include(project => project.Products)
            .ToListAsync();

        var projectsBySlug = projects.ToDictionary(project => project.Slug, StringComparer.OrdinalIgnoreCase);
        var hasProjectChanges = false;

        foreach (var (name, slug, products) in Data)
        {
            if (!projectsBySlug.TryGetValue(slug, out var project))
            {
                project = RoadmapProject.Create(name, slug);
                foreach (var productName in products)
                    project.AddProduct(productName);

                await context.RoadmapProjects.AddAsync(project);
                projectsBySlug[slug] = project;
                hasProjectChanges = true;
                continue;
            }

            var existingProductNames = project.Products
                .Select(product => product.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var productName in products.Where(productName => !existingProductNames.Contains(productName)))
            {
                project.AddProduct(productName);
                hasProjectChanges = true;
            }
        }

        if (hasProjectChanges)
            await context.SaveChangesAsync();

        var seededDemandIdsByKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        var crossProject = await context.RoadmapProjects
            .Include(project => project.Products)
            .FirstOrDefaultAsync(project => project.Slug == "cross");
        var retaguardaProject = await context.RoadmapProjects
            .Include(project => project.Products)
            .FirstOrDefaultAsync(project => project.Slug == "retaguarda");

        if (crossProject is null || retaguardaProject is null)
            return;

            var crossDemandChanges = await SeedProjectDemandsAsync(context, crossProject, CrossDemands, seededDemandIdsByKey);
            var retaguardaDemandChanges = await SeedProjectDemandsAsync(context, retaguardaProject, RetaguardaQ22026Demands, seededDemandIdsByKey);
            var hasDemandChanges = crossDemandChanges || retaguardaDemandChanges;

        if (hasDemandChanges)
            await context.SaveChangesAsync();

        await LoadExistingDemandIdsAsync(context, crossProject.Id, seededDemandIdsByKey);
        await LoadExistingDemandIdsAsync(context, retaguardaProject.Id, seededDemandIdsByKey);

        var existingDependencies = await context.RoadmapDemandDependencies
            .Select(link => new { link.DemandId, link.DependsOnDemandId })
            .ToListAsync();

        var existingDependencySet = existingDependencies
            .Select(link => $"{link.DemandId}:{link.DependsOnDemandId}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasDependencyChanges = false;

        foreach (var dependency in CrossProjectDependencies)
        {
            if (!seededDemandIdsByKey.TryGetValue(dependency.DemandKey, out var demandId))
                continue;

            if (!seededDemandIdsByKey.TryGetValue(dependency.DependsOnDemandKey, out var dependsOnDemandId))
                continue;

            var dependencyKey = $"{demandId}:{dependsOnDemandId}";
            if (existingDependencySet.Contains(dependencyKey))
                continue;

            await context.RoadmapDemandDependencies.AddAsync(
                RoadmapDemandDependency.FromRepository(demandId, dependsOnDemandId));
            existingDependencySet.Add(dependencyKey);
            hasDependencyChanges = true;
        }

        if (hasDependencyChanges)
            await context.SaveChangesAsync();
    }

    private static async Task<bool> SeedProjectDemandsAsync(
        AppDbContext context,
        RoadmapProject project,
        MockDemandSeed[] seeds,
        IDictionary<string, Guid> seededDemandIdsByKey)
    {
        var productIdsByName = project.Products
            .ToDictionary(product => product.Name, product => product.Id, StringComparer.OrdinalIgnoreCase);

        var existingDemands = await context.RoadmapDemands
            .Where(demand => demand.ProjectId == project.Id)
            .Select(demand => new { demand.Id, Key = demand.JiraIssue ?? demand.Title })
            .ToListAsync();

        var existingDemandKeySet = existingDemands
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(item => item.Key!, item => item.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var existingDemand in existingDemands.Where(item => !string.IsNullOrWhiteSpace(item.Key)))
            seededDemandIdsByKey[existingDemand.Key!] = existingDemand.Id;

        var hasDemandChanges = false;

        for (var index = 0; index < seeds.Length; index++)
        {
            var seed = seeds[index];
            var demandKey = seed.JiraIssue ?? seed.Title;
            if (existingDemandKeySet.ContainsKey(demandKey))
                continue;

            var demand = RoadmapDemand.Create(
                seed.Title,
                seed.Description,
                project.Id,
                seed.QuarterYear,
                seed.QuarterNumber,
                seed.Type,
                seed.Classification,
                seed.ProductNames.Select(productName => productIdsByName[productName]),
                (index + 1) * 10,
                seed.JiraIssue,
                seed.Hours,
                seed.Customers,
                seed.IsBlocked,
                seed.BlockedReason);

            demand.Update(
                seed.Title,
                seed.Description,
                seed.QuarterYear,
                seed.QuarterNumber,
                seed.Status,
                seed.Type,
                seed.Classification,
                sortOrder: null,
                observation: seed.Observation,
                jiraIssue: seed.JiraIssue,
                hours: seed.Hours,
                customers: seed.Customers,
                isBlocked: seed.IsBlocked,
                blockedReason: seed.BlockedReason,
                deliveryDate: seed.DeliveryDate);

            await context.RoadmapDemands.AddAsync(demand);
            existingDemandKeySet[demandKey] = demand.Id;
            seededDemandIdsByKey[demandKey] = demand.Id;
            hasDemandChanges = true;
        }

        return hasDemandChanges;
    }

    private static async Task LoadExistingDemandIdsAsync(
        AppDbContext context,
        Guid projectId,
        IDictionary<string, Guid> seededDemandIdsByKey)
    {
        var existingDemands = await context.RoadmapDemands
            .Where(demand => demand.ProjectId == projectId)
            .Select(demand => new { demand.Id, Key = demand.JiraIssue ?? demand.Title })
            .ToListAsync();

        foreach (var demand in existingDemands.Where(item => !string.IsNullOrWhiteSpace(item.Key)))
            seededDemandIdsByKey[demand.Key!] = demand.Id;
    }
}
