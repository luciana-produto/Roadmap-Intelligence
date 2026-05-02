# ProductHub

Sistema interno de gestão de roadmap de produto e indicadores estratégicos, voltado para o segmento **FOOD** (restaurantes) com retaguarda e PDV.

## Visão Geral

O ProductHub é uma plataforma orientada a resultado que conecta o planejamento de roadmap com KPIs estratégicos, trade-offs de decisão e saúde do roadmap. O objetivo central é permitir que o time de produto responda:

- **Estamos construindo as coisas certas?**
- **Essas iniciativas geraram impacto?**

## Contexto de Negócio

O sistema atende squads de produto que gerenciam múltiplos projetos no ecossistema FOOD:

| Projeto | Produtos |
|---------|----------|
| Cross | SVO, Taste, HUB CRM, SVC |
| Neemo | Neemo |
| Taste | Taste PDV, Taste AA |
| Retaguarda | Retaguarda |
| Degust | Degust PDV, Hub Delivery |
| Menew | Menew |

As áreas de foco estratégico incluem **receita**, **churn**, **compliance** e **experiência do cliente**.

## Funcionalidades

### Roadmap por Quarter (implementado)

- Planejamento de demandas (épicos) por quarter (Q1–Q4) e backlog
- Priorização por drag-and-drop dentro de cada quarter
- Movimentação em lote entre quarters
- Dependências cross-project entre demandas
- Gestão de capacidade (horas) por quarter/projeto
- Filtros por status, tipo, classificação, produto e cliente
- Status: Backlog → Em Progresso → Concluído / Despriorizado

### KPIs Estratégicos (em desenvolvimento)

Gerenciamento de indicadores-chave de performance vinculados a cada projeto:

- **Tipo**: Business, Product, Quality, Usability
- **Alavanca**: Growth, Efficiency, Customer
- CRUD completo com nome, descrição, fórmula de cálculo, meta e valor atual
- Vínculo entre demandas e KPIs:
  - Tipo de impacto (Increase/Decrease)
  - Impacto estimado
  - Nível de confiança (High/Medium/Low)
  - Suporte a demandas "sem KPI vinculado" como sinal de atenção

### Trade-offs de Decisão (em desenvolvimento)

Registro formal de decisões de despriorização:

- Demanda despriorizada + motivo (Cancelamento, Pedido de Cliente, Mudança Estratégica, Substituição Mandatória)
- Demanda substituta (opcional)
- Observação de contexto
- Histórico consultável para accountability

### Medição Pós-entrega (em desenvolvimento)

Acompanhamento do impacto real após entrega de demandas:

- Registro de medições vinculadas a KPI e opcionalmente a demanda
- Data da medição, valor medido, classificação (Positivo/Negativo/Neutro)
- Múltiplos registros por KPI ao longo do tempo

### Clareza do Problema (em desenvolvimento)

Campo de 0 a 10 em cada demanda indicando o quanto o problema a ser resolvido está bem definido:

- 0 = problema vago/mal definido
- 10 = problema validado com dados e evidências
- Usado como insumo no cálculo do Health Score

### Roadmap Health Score (em desenvolvimento)

Score composto de 0 a 100 que avalia a saúde do roadmap por quarter, baseado em 5 dimensões:

| Dimensão | O que mede |
|----------|-----------|
| Cobertura de KPI | % de demandas vinculadas a pelo menos um KPI |
| Clareza do Problema | Média do campo ProblemClarity das demandas |
| Taxa de Entrega | % de demandas concluídas vs. total |
| Documentação de Trade-offs | % de demandas despriorizadas com trade-off registrado |
| Saúde de Dependências | % de dependências não bloqueadas |

### Dashboard (em desenvolvimento)

Visão consolidada por projeto com:

- Health Score com breakdown por dimensão
- Lista de KPIs com valores atuais vs. metas
- Distribuição de status das demandas
- Trade-offs recentes
- Medições recentes de KPIs

## Arquitetura

### Backend

- **ASP.NET Core 10** com Clean Architecture (Domain → Application → Infrastructure → API)
- **CQRS** via MediatR com pipeline behaviors (Logging, Validation, SlowRequest)
- **EF Core** com SQL Server (fallback para InMemory em desenvolvimento)
- **FluentValidation** para validação de commands
- Domain Driven Design: Aggregate Roots, Value Objects, Domain Events
- Correlation ID end-to-end para rastreabilidade

### Frontend

- **Nuxt 3** com Vue 3 Composition API + TypeScript
- **Pinia** para state management
- **Nuxt UI** como design system
- **SortableJS** para drag-and-drop
- Layout responsivo com sidebar colapsável e tema customizável

### Infraestrutura

- **Docker Compose** com SQL Server, Seq (logs), API e Frontend
- Observabilidade via Seq com structured logging
- Seed automático com dados mock realistas

## Configuração SQL Server

Para ambiente persistente, o backend lê a connection string pela chave `ConnectionStrings__SqlServer`.

Exemplo PowerShell:

```powershell
$env:ConnectionStrings__SqlServer = "Server=20.51.132.203,1433;Database=ProductHub;User ID=product_team;Password=<senha>;Encrypt=True;TrustServerCertificate=True;Connection Timeout=30"
```

Observações:

- Em banco persistente, o startup agora prioriza EF Migrations quando elas existirem.
- Enquanto não houver migrations versionadas no repositório, o backend aplica o script idempotente `backend/sql/producthub-schema.sql` como bootstrap inicial do schema.
- Para desenvolvimento sem SQL Server configurado, o sistema continua com fallback para InMemory.

## Estrutura do Projeto

```
ProductHub/
├── backend/
│   └── src/
│       ├── ProductHub.API/          # Controllers, Middleware
│       ├── ProductHub.Application/  # Commands, Queries, DTOs, Validators
│       ├── ProductHub.Domain/       # Entities, Value Objects, Interfaces
│       ├── ProductHub.Infrastructure/ # EF Core, Repositories, Seed
│       └── ProductHub.Shared/       # Constants, Models compartilhados
│   └── tests/
│       ├── ProductHub.API.Tests/
│       ├── ProductHub.Application.Tests/
│       ├── ProductHub.Domain.Tests/
│       └── ProductHub.Infrastructure.Tests/
├── frontend/
│   └── app/
│       ├── components/    # Componentes Vue reutilizáveis
│       ├── composables/   # Hooks (useApi, useAuth, useLogger)
│       ├── layouts/       # Layout padrão com sidebar
│       ├── pages/         # Páginas da aplicação
│       ├── stores/        # Pinia stores
│       ├── types/         # TypeScript types
│       └── utils/         # Formatadores e constantes
└── docker-compose.yml
```

## Regras de Design

- **Orientado a resultado**, não apenas controle de tarefas
- Telas com foco em **visão de produto**, evitando excesso técnico
- KPIs como **parte central** da navegação e tomada de decisão
- Toda decisão de despriorização deve ser documentada
- Health Score como termômetro contínuo da qualidade do planejamento
