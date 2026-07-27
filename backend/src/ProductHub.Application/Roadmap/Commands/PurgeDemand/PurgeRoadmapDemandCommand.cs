using MediatR;

namespace ProductHub.Application.Roadmap.Commands.PurgeDemand;

/// <summary>Exclusão DEFINITIVA (física) de um item que está na lixeira.</summary>
public sealed record PurgeRoadmapDemandCommand(Guid Id) : IRequest;
