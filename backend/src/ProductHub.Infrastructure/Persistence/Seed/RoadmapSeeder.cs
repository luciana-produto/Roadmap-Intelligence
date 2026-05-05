using Microsoft.EntityFrameworkCore;
using ProductHub.Domain.Roadmap;
using ProductHub.Infrastructure.Persistence;

namespace ProductHub.Infrastructure.Persistence.Seed;

public static class RoadmapSeeder
{
    private sealed record MockRoadmapItemSeed(
        string Key,
        RoadmapItemType ItemType,
        string Title,
        string? Description,
        string? ParentKey,
        string? ProjectSlug,
        int QuarterYear,
        int QuarterNumber,
        DemandStatus Status,
        DemandType Type,
        DemandClassification Classification,
        decimal? Hours,
        string[] ProductNames,
        string? Observation = null,
        string? JiraIssue = null,
        string[]? Customers = null,
        bool IsBlocked = false,
        string? BlockedReason = null,
        int? ProblemClarity = null,
        DateOnly? PromisedDate = null,
        DateOnly? DeliveryDate = null,
        bool HasNoKpi = false,
        NoKpiClassification? NoKpiClassification = null,
        DeprioritizationReason? DeprioritizationReason = null);

    private sealed record MockDependencySeed(string ItemKey, string DependsOnItemKey);

    private static readonly (string Name, string Slug, string[] Products)[] Data =
    [
        ("Cross", "cross", ["SVO", "Taste", "HUB CRM", "SVC"]),
        ("Neemo", "neemo", ["Neemo"]),
        ("Taste", "taste", ["Taste PDV", "Taste AA"]),
        ("Retaguarda", "retaguarda", ["Retaguarda"]),
        ("Degust", "degust", ["Degust PDV", "Hub Delivery"]),
        ("Menew", "menew", ["Menew"])
    ];

    private static readonly MockRoadmapItemSeed[] Items =
    [
        new(
            "CROSS-RM-FOUNDATION",
            RoadmapItemType.Roadmap,
            "Fundação de Plataforma Cross",
            "Organiza frentes estruturantes de identidade, compliance e integrações críticas do ecossistema Cross.",
            null,
            null,
            Quarter.BacklogYear,
            Quarter.BacklogNumber,
            DemandStatus.Backlog,
            DemandType.Planned,
            DemandClassification.Strategic,
            null,
            []),
        new(
            "CROSS-EP-IDENTITY",
            RoadmapItemType.Epic,
            "Identidade e acesso unificado do ecossistema Cross",
            "Consolida autenticação, onboarding e governança de acesso entre canais e backoffice.",
            "CROSS-RM-FOUNDATION",
            null,
            Quarter.BacklogYear,
            Quarter.BacklogNumber,
            DemandStatus.InProgress,
            DemandType.Planned,
            DemandClassification.Strategic,
            null,
            [],
            Observation: "Épico priorizado para reduzir fricção operacional entre squads.",
            JiraIssue: "CROSS-EP-01",
            Customers: ["Operações", "CS"],
            ProblemClarity: 8,
            PromisedDate: new DateOnly(2026, 6, 25)),
        new(
            "CROSS-201",
            RoadmapItemType.Demand,
            "Padronizar autenticação SSO no ecossistema Cross",
            "Unifica o fluxo de autenticação entre SVO, HUB CRM e SVC para reduzir retrabalho operacional.",
            "CROSS-EP-IDENTITY",
            "cross",
            2026,
            2,
            DemandStatus.InProgress,
            DemandType.Planned,
            DemandClassification.Strategic,
            120,
            ["SVO", "HUB CRM", "SVC"],
            Observation: "Integração técnica priorizada pelo time de plataforma.",
            JiraIssue: "CROSS-201",
            Customers: ["Operações", "CS"],
            ProblemClarity: 8,
            PromisedDate: new DateOnly(2026, 6, 20)),
        new(
            "CROSS-EP-COMPLIANCE",
            RoadmapItemType.Epic,
            "Compliance e segurança operacional do Cross",
            "Agrupa entregas mandatórias e de mitigação de risco para garantir continuidade e conformidade.",
            "CROSS-RM-FOUNDATION",
            null,
            Quarter.BacklogYear,
            Quarter.BacklogNumber,
            DemandStatus.Backlog,
            DemandType.Planned,
            DemandClassification.Mandatory,
            null,
            [],
            Observation: "Coordenação compartilhada com jurídico, segurança e infraestrutura.",
            JiraIssue: "CROSS-EP-02",
            Customers: ["Jurídico", "Segurança"],
            ProblemClarity: 7,
            PromisedDate: new DateOnly(2026, 6, 18)),
        new(
            "CROSS-214",
            RoadmapItemType.Demand,
            "Adequar trilha de auditoria LGPD do HUB CRM",
            "Reforça logs sensíveis e políticas de retenção para auditoria e conformidade.",
            "CROSS-EP-COMPLIANCE",
            "cross",
            2026,
            2,
            DemandStatus.Backlog,
            DemandType.Planned,
            DemandClassification.Mandatory,
            64,
            ["HUB CRM"],
            Observation: "Escopo alinhado com jurídico e segurança.",
            JiraIssue: "CROSS-214",
            Customers: ["Jurídico"],
            ProblemClarity: 7,
            PromisedDate: new DateOnly(2026, 6, 10)),
        new(
            "CROSS-223",
            RoadmapItemType.Demand,
            "Atualizar dependências críticas do gateway SVC",
            "Pacote de atualização para corrigir vulnerabilidades e manter compatibilidade com o gateway.",
            "CROSS-EP-COMPLIANCE",
            "cross",
            2026,
            2,
            DemandStatus.Backlog,
            DemandType.Unplanned,
            DemandClassification.TechnicalDebtSecurity,
            52,
            ["SVC"],
            Observation: "Janela de deploy depende de aprovação de infraestrutura.",
            JiraIssue: "CROSS-223",
            Customers: ["Infra", "Segurança"],
            IsBlocked: true,
            BlockedReason: "Aguardando liberação da janela de manutenção do balanceador.",
            ProblemClarity: 6,
            PromisedDate: new DateOnly(2026, 6, 5)),
        new(
            "CROSS-RM-EXPERIENCE",
            RoadmapItemType.Roadmap,
            "Experiência e expansão comercial Cross",
            "Conecta ganhos de experiência, homologação e descoberta comercial em uma mesma linha de evolução.",
            null,
            null,
            Quarter.BacklogYear,
            Quarter.BacklogNumber,
            DemandStatus.Backlog,
            DemandType.Planned,
            DemandClassification.Strategic,
            null,
            []),
        new(
            "CROSS-EP-OPERATIONS",
            RoadmapItemType.Epic,
            "Eficiência operacional da jornada omnichannel",
            "Agrupa entregas que reduzem atrito na operação e melhoram a percepção do fluxo fim a fim.",
            "CROSS-RM-EXPERIENCE",
            null,
            Quarter.BacklogYear,
            Quarter.BacklogNumber,
            DemandStatus.InProgress,
            DemandType.Additional,
            DemandClassification.Evolution,
            null,
            [],
            Observation: "Épico com impacto direto em adoção e satisfação do fluxo omnichannel.",
            JiraIssue: "CROSS-EP-03",
            Customers: ["Implantação", "Suporte", "Operações"],
            ProblemClarity: 8,
            PromisedDate: new DateOnly(2026, 6, 28)),
        new(
            "CROSS-176",
            RoadmapItemType.Demand,
            "Reduzir latência de sincronização do cardápio Taste",
            "Melhora o tempo de propagação do cardápio em integrações do Cross.",
            "CROSS-EP-OPERATIONS",
            "cross",
            2026,
            2,
            DemandStatus.Done,
            DemandType.Spillover,
            DemandClassification.ImprovementGap,
            40,
            ["Taste"],
            Observation: "Entrega concluída e validada em homologação.",
            JiraIssue: "CROSS-176",
            Customers: ["Implantação"],
            ProblemClarity: 9,
            PromisedDate: new DateOnly(2026, 5, 10),
            DeliveryDate: new DateOnly(2026, 5, 18)),
        new(
            "CROSS-239",
            RoadmapItemType.Demand,
            "Homologar fluxo omnichannel de pedidos do Cross",
            "Valida a jornada fim a fim entre SVO e Taste com cenários de regressão.",
            "CROSS-EP-OPERATIONS",
            "cross",
            2026,
            2,
            DemandStatus.InProgress,
            DemandType.Additional,
            DemandClassification.Homologation,
            36,
            ["SVO", "Taste"],
            Observation: "Homologação compartilhada com operações de loja.",
            JiraIssue: "CROSS-239",
            Customers: ["Suporte", "Operações"],
            ProblemClarity: 8,
            PromisedDate: new DateOnly(2026, 6, 25)),
        new(
            "CROSS-EP-SELF-SERVICE",
            RoadmapItemType.Epic,
            "Onboarding self-service e ativação comercial",
            "Explora a jornada autônoma de entrada de novos clientes no ecossistema Cross.",
            "CROSS-RM-EXPERIENCE",
            null,
            Quarter.BacklogYear,
            Quarter.BacklogNumber,
            DemandStatus.Backlog,
            DemandType.Planned,
            DemandClassification.Strategic,
            null,
            [],
            Observation: "Épico ainda em discovery, aguardando refinamento com produto e operações.",
            JiraIssue: "CROSS-EP-04",
            Customers: ["Produto", "Operações"],
            ProblemClarity: 4,
            PromisedDate: new DateOnly(2026, 7, 10)),
        new(
            "CROSS-245",
            RoadmapItemType.Demand,
            "Mapear nova esteira de onboarding self-service no Cross",
            "Descoberta funcional para estruturar a entrada autônoma de novos clientes no ecossistema Cross.",
            "CROSS-EP-SELF-SERVICE",
            "cross",
            Quarter.BacklogYear,
            Quarter.BacklogNumber,
            DemandStatus.Backlog,
            DemandType.Planned,
            DemandClassification.Strategic,
            48,
            ["HUB CRM", "SVO"],
            Observation: "Aguardando refinamento final com produto e operações.",
            JiraIssue: "CROSS-245",
            Customers: ["Produto", "Operações"],
            ProblemClarity: 4),
        new(
            "CROSS-246",
            RoadmapItemType.Demand,
            "Preparar base para centralização de notificações transacionais",
            "Spike técnico para consolidar notificações críticas entre HUB CRM, Taste e SVC.",
            "CROSS-EP-SELF-SERVICE",
            "cross",
            Quarter.BacklogYear,
            Quarter.BacklogNumber,
            DemandStatus.Backlog,
            DemandType.Unplanned,
            DemandClassification.Evolution,
            32,
            ["HUB CRM", "Taste", "SVC"],
            Observation: "Backlog técnico-comercial sem quarter comprometido.",
            JiraIssue: "CROSS-246",
            Customers: ["Comercial", "Suporte"],
            ProblemClarity: 3),
        new(
            "RET-RM-CORE",
            RoadmapItemType.Roadmap,
            "Estabilidade transacional da Retaguarda",
            "Agrupa as frentes estruturais que sustentam fiscal, publicação de eventos e integrações do core.",
            null,
            null,
            Quarter.BacklogYear,
            Quarter.BacklogNumber,
            DemandStatus.Backlog,
            DemandType.Planned,
            DemandClassification.Mandatory,
            null,
            []),
        new(
            "RET-EP-FISCAL",
            RoadmapItemType.Epic,
            "Conciliação fiscal e publicação confiável do core",
            "Concentra as entregas que sustentam a operação fiscal e a qualidade dos eventos publicados pela Retaguarda.",
            "RET-RM-CORE",
            null,
            Quarter.BacklogYear,
            Quarter.BacklogNumber,
            DemandStatus.InProgress,
            DemandType.Planned,
            DemandClassification.Mandatory,
            null,
            [],
            Observation: "Entrega acompanhada por arquitetura e financeiro.",
            JiraIssue: "RET-EP-01",
            Customers: ["Financeiro", "Arquitetura"],
            ProblemClarity: 8,
            PromisedDate: new DateOnly(2026, 6, 28)),
        new(
            "RET-101",
            RoadmapItemType.Demand,
            "Modernizar motor de conciliação fiscal da Retaguarda",
            "Atualiza a base transacional usada pela retaguarda para suportar integrações mais estáveis com o ecossistema Cross.",
            "RET-EP-FISCAL",
            "retaguarda",
            2026,
            2,
            DemandStatus.InProgress,
            DemandType.Planned,
            DemandClassification.Mandatory,
            96,
            ["Retaguarda"],
            Observation: "Entrega acompanhada pelo time financeiro e arquitetura.",
            JiraIssue: "RET-101",
            Customers: ["Financeiro", "Arquitetura"],
            ProblemClarity: 8,
            PromisedDate: new DateOnly(2026, 6, 28)),
        new(
            "RET-102",
            RoadmapItemType.Demand,
            "Aprimorar fila de publicação de cadastros mestres",
            "Prepara a retaguarda para publicar eventos mais confiáveis consumidos por Cross e canais satélites.",
            "RET-EP-FISCAL",
            "retaguarda",
            2026,
            2,
            DemandStatus.Backlog,
            DemandType.Spillover,
            DemandClassification.Evolution,
            null,
            ["Retaguarda"],
            Observation: "Refinamento pendente com o time de plataforma.",
            JiraIssue: "RET-102",
            Customers: ["Plataforma"],
            ProblemClarity: 6,
            PromisedDate: new DateOnly(2026, 6, 18))
    ];

    private static readonly MockDependencySeed[] Dependencies =
    [
        new("CROSS-EP-IDENTITY", "RET-EP-FISCAL"),
        new("CROSS-214", "RET-101"),
        new("CROSS-239", "CROSS-245"),
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

        var itemIdsByKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var hasItemChanges = await SeedRoadmapItemsAsync(context, projectsBySlug, itemIdsByKey);
        if (hasItemChanges)
            await context.SaveChangesAsync();

        await LoadExistingItemIdsAsync(context, itemIdsByKey);

        var existingDependencies = await context.RoadmapDemandDependencies
            .Select(link => new { link.DemandId, link.DependsOnDemandId })
            .ToListAsync();

        var existingDependencySet = existingDependencies
            .Select(link => $"{link.DemandId}:{link.DependsOnDemandId}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasDependencyChanges = false;
        foreach (var dependency in Dependencies)
        {
            if (!itemIdsByKey.TryGetValue(dependency.ItemKey, out var itemId))
                continue;

            if (!itemIdsByKey.TryGetValue(dependency.DependsOnItemKey, out var dependsOnItemId))
                continue;

            var dependencyKey = $"{itemId}:{dependsOnItemId}";
            if (existingDependencySet.Contains(dependencyKey))
                continue;

            await context.RoadmapDemandDependencies.AddAsync(
                RoadmapDemandDependency.FromRepository(itemId, dependsOnItemId));
            existingDependencySet.Add(dependencyKey);
            hasDependencyChanges = true;
        }

        if (hasDependencyChanges)
            await context.SaveChangesAsync();

        if (projectsBySlug.TryGetValue("cross", out var crossProject))
            await SeedKpisAsync(context, crossProject.Id, itemIdsByKey);
    }

    private static async Task<bool> SeedRoadmapItemsAsync(
        AppDbContext context,
        IReadOnlyDictionary<string, RoadmapProject> projectsBySlug,
        IDictionary<string, Guid> itemIdsByKey)
    {
        var existingItems = await context.RoadmapDemands
            .Include(item => item.Products)
            .ToListAsync();

        var existingByKey = existingItems
            .Select(item => new { Item = item, Key = item.JiraIssue ?? item.Title })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(entry => entry.Key!, entry => entry.Item, StringComparer.OrdinalIgnoreCase);

        var existingTradeOffDemandIds = await context.DemandTradeOffs
            .Select(item => item.DeprioritizedDemandId)
            .ToListAsync();

        var hasChanges = false;

        for (var index = 0; index < Items.Length; index++)
        {
            var seed = Items[index];

            Guid? parentId = null;
            if (!string.IsNullOrWhiteSpace(seed.ParentKey) && itemIdsByKey.TryGetValue(seed.ParentKey, out var resolvedParentId))
                parentId = resolvedParentId;

            Guid? projectId = null;
            IReadOnlyList<Guid> productIds = [];
            if (!string.IsNullOrWhiteSpace(seed.ProjectSlug) && projectsBySlug.TryGetValue(seed.ProjectSlug, out var project))
            {
                projectId = project.Id;
                var productIdsByName = project.Products.ToDictionary(product => product.Name, product => product.Id, StringComparer.OrdinalIgnoreCase);
                productIds = seed.ProductNames
                    .Where(productIdsByName.ContainsKey)
                    .Select(productName => productIdsByName[productName])
                    .ToList();
            }

            if (!existingByKey.TryGetValue(seed.Key, out var item))
            {
                item = RoadmapDemand.Create(
                    seed.ItemType,
                    parentId,
                    seed.Title,
                    seed.Description,
                    projectId,
                    projectId.HasValue ? [projectId.Value] : [],
                    seed.QuarterYear,
                    seed.QuarterNumber,
                    seed.Status,
                    seed.Type,
                    seed.Classification,
                    productIds,
                    seed.ItemType == RoadmapItemType.Demand ? (index + 1) * 10 : 0,
                    seed.JiraIssue,
                    null,
                    seed.Hours,
                    seed.Customers,
                    seed.IsBlocked,
                    seed.BlockedReason,
                    seed.PromisedDate,
                    seed.ProblemClarity,
                    seed.HasNoKpi,
                    seed.NoKpiClassification);

                await context.RoadmapDemands.AddAsync(item);
                existingByKey[seed.Key] = item;
                hasChanges = true;
            }

            item.Update(
                seed.ItemType,
                parentId,
                seed.Title,
                seed.Description,
                projectId,
                projectId.HasValue ? [projectId.Value] : [],
                seed.QuarterYear,
                seed.QuarterNumber,
                seed.Status,
                seed.Type,
                seed.Classification,
                sortOrder: seed.ItemType == RoadmapItemType.Demand ? item.SortOrder : 0,
                observation: seed.Observation,
                deprioritizationReason: seed.DeprioritizationReason,
                jiraIssue: seed.JiraIssue,
                hours: seed.Hours,
                customers: seed.Customers,
                isBlocked: seed.IsBlocked,
                blockedReason: seed.BlockedReason,
                promisedDate: seed.PromisedDate,
                deliveryDate: seed.DeliveryDate,
                problemClarity: seed.ProblemClarity,
                hasNoKpi: seed.HasNoKpi,
                noKpiClassification: seed.NoKpiClassification);

            item.ReplaceProducts(productIds);
            itemIdsByKey[seed.Key] = item.Id;

            if (seed.Status == DemandStatus.Deprioritized
                && seed.DeprioritizationReason.HasValue
                && projectId.HasValue
                && !existingTradeOffDemandIds.Contains(item.Id))
            {
                await context.DemandTradeOffs.AddAsync(
                    DemandTradeOff.Create(
                        projectId.Value,
                        seed.QuarterYear,
                        seed.QuarterNumber,
                        item.Id,
                        null,
                        seed.DeprioritizationReason.Value,
                        seed.Observation));
                existingTradeOffDemandIds.Add(item.Id);
                hasChanges = true;
            }
        }

        return hasChanges;
    }

    private static async Task LoadExistingItemIdsAsync(
        AppDbContext context,
        IDictionary<string, Guid> itemIdsByKey)
    {
        var existingItems = await context.RoadmapDemands
            .Select(item => new { item.Id, Key = item.JiraIssue ?? item.Title })
            .ToListAsync();

        foreach (var item in existingItems.Where(entry => !string.IsNullOrWhiteSpace(entry.Key)))
            itemIdsByKey[item.Key!] = item.Id;
    }

    private static async Task SeedKpisAsync(
        AppDbContext context,
        Guid projectId,
        IDictionary<string, Guid> itemIdsByKey)
    {
        var existingKpis = await context.Kpis
            .Where(k => k.ProjectId == projectId)
            .ToListAsync();

        if (existingKpis.Count > 0)
            return;

        var kpis = new[]
        {
            Kpi.Create(projectId, "Taxa de Churn Mensal", KpiType.Business, KpiLever.Customer, KpiObjective.Decrease,
                "Percentual de clientes que cancelaram no mês",
                "(Clientes cancelados / Total clientes início do mês) x 100",
                2.5m, 3.1m),
            Kpi.Create(projectId, "Receita Recorrente Mensal (MRR)", KpiType.Business, KpiLever.Growth, KpiObjective.Increase,
                "Receita mensal recorrente de assinaturas ativas",
                "Soma de todas assinaturas ativas no mês",
                850000m, 780000m),
            Kpi.Create(projectId, "Taxa de Adoção da Funcionalidade", KpiType.Product, KpiLever.Customer, KpiObjective.Increase,
                "Percentual de usuários elegíveis que utilizam a funcionalidade lançada",
                "(Usuários ativos da funcionalidade / Usuários elegíveis) x 100",
                45m, 18m),
            Kpi.Create(projectId, "Tempo Médio de Onboarding", KpiType.Product, KpiLever.Efficiency, KpiObjective.Decrease,
                "Tempo médio para concluir o onboarding de novos clientes",
                "Média de dias entre assinatura e ativação completa",
                12m, 18m),
            Kpi.Create(projectId, "Taxa de Compliance Fiscal", KpiType.Business, KpiLever.Efficiency, KpiObjective.Increase,
                "Percentual de rotinas fiscais executadas sem inconsistências",
                "(Rotinas fiscais sem erro / Rotinas fiscais totais) x 100",
                98.5m, 96.9m),
            Kpi.Create(projectId, "NPS do Produto", KpiType.Product, KpiLever.Customer, KpiObjective.Increase,
                "Nível de satisfação dos clientes com a experiência do produto",
                "% promotores - % detratores",
                62m, 54m)
        };

        foreach (var kpi in kpis)
            await context.Kpis.AddAsync(kpi);

        await context.SaveChangesAsync();

        var kpiByName = kpis.ToDictionary(k => k.Name, StringComparer.OrdinalIgnoreCase);
        var linkSeeds = new (string ItemKey, string KpiName, ImpactType Impact, decimal? EstimatedImpact, ConfidenceLevel Confidence, string? Observation)[]
        {
            ("CROSS-EP-IDENTITY", "Taxa de Churn Mensal", ImpactType.Decrease, 0.3m, ConfidenceLevel.Medium, "Redução esperada com a padronização do SSO."),
            ("CROSS-EP-IDENTITY", "Tempo Médio de Onboarding", ImpactType.Decrease, 5m, ConfidenceLevel.High, "Fluxo mais simples reduz o tempo de ativação."),
            ("CROSS-EP-COMPLIANCE", "Taxa de Compliance Fiscal", ImpactType.Increase, 0.5m, ConfidenceLevel.High, null),
            ("CROSS-EP-OPERATIONS", "NPS do Produto", ImpactType.Increase, 3m, ConfidenceLevel.Medium, "A redução de fricção deve melhorar a percepção do fluxo."),
            ("CROSS-EP-OPERATIONS", "Taxa de Adoção da Funcionalidade", ImpactType.Increase, 4m, ConfidenceLevel.Medium, "Homologação mais estável acelera adoção em clientes ativos."),
            ("RET-EP-FISCAL", "Taxa de Compliance Fiscal", ImpactType.Increase, 0.7m, ConfidenceLevel.High, null),
            ("RET-EP-FISCAL", "Receita Recorrente Mensal (MRR)", ImpactType.Increase, 15000m, ConfidenceLevel.Medium, null)
        };

        foreach (var (itemKey, kpiName, impact, estimated, confidence, observation) in linkSeeds)
        {
            if (!itemIdsByKey.TryGetValue(itemKey, out var itemId))
                continue;
            if (!kpiByName.TryGetValue(kpiName, out var kpi))
                continue;

            await context.DemandKpiLinks.AddAsync(
                DemandKpiLink.FromRepository(itemId, kpi.Id, impact, estimated, confidence, observation));
        }

        await context.SaveChangesAsync();

        var measurementSeeds = new (string ItemKey, string KpiName, decimal MeasuredValue, DateOnly MeasurementDate, MeasurementResult Result, string? Observation)[]
        {
            ("CROSS-EP-OPERATIONS", "NPS do Produto", 4.2m, new DateOnly(2026, 5, 25), MeasurementResult.Positive, "Clientes perceberam melhora no fluxo após as entregas do épico."),
            ("CROSS-EP-OPERATIONS", "Taxa de Adoção da Funcionalidade", 2.1m, new DateOnly(2026, 5, 25), MeasurementResult.Positive, "Adoção inicial acima do esperado em clientes piloto.")
        };

        foreach (var (itemKey, kpiName, measuredValue, measurementDate, result, observation) in measurementSeeds)
        {
            if (!itemIdsByKey.TryGetValue(itemKey, out var itemId))
                continue;
            if (!kpiByName.TryGetValue(kpiName, out var kpi))
                continue;

            await context.KpiMeasurements.AddAsync(
                KpiMeasurement.Create(kpi.Id, itemId, measuredValue, measurementDate, result, observation));
        }

        await context.SaveChangesAsync();
    }
}
