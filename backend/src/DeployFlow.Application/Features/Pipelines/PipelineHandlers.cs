using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;
using AutoMapper;

namespace DeployFlow.Application.Features.Pipelines;

// â”€â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public record GetPipelinesQuery(
    Guid? ProjectId = null,
    int Page = 1, int PageSize = 20
) : IRequest<Result<PaginatedResponse<PipelineDto>>>;

public class GetPipelinesQueryHandler : IRequestHandler<GetPipelinesQuery, Result<PaginatedResponse<PipelineDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetPipelinesQueryHandler(IUnitOfWork uow, ICurrentUser cu, IMapper mapper)
    { _uow = uow; _currentUser = cu; _mapper = mapper; }

    public async Task<Result<PaginatedResponse<PipelineDto>>> Handle(GetPipelinesQuery request, CancellationToken ct)
    {
        IReadOnlyList<Pipeline> items;
        if (request.ProjectId.HasValue)
            items = await _uow.Pipelines.GetByProjectAsync(request.ProjectId.Value, ct);
        else
            items = await _uow.Pipelines.GetByTenantAsync(_currentUser.TenantId, ct);

        var filtered = items.Where(p => p.TenantId == _currentUser.TenantId).ToList();
        var dtos = filtered.Select(p => _mapper.Map<PipelineDto>(p)).ToList();
        return Result<PaginatedResponse<PipelineDto>>.Success(
            PaginatedResponse<PipelineDto>.Create(dtos, filtered.Count, request.Page, request.PageSize));
    }
}

public record GetPipelineByIdQuery(Guid Id) : IRequest<Result<PipelineDto>>;

public class GetPipelineByIdQueryHandler : IRequestHandler<GetPipelineByIdQuery, Result<PipelineDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public GetPipelineByIdQueryHandler(IUnitOfWork uow, ICurrentUser cu, IMapper mapper)
    { _uow = uow; _currentUser = cu; _mapper = mapper; }

    public async Task<Result<PipelineDto>> Handle(GetPipelineByIdQuery request, CancellationToken ct)
    {
        var pipeline = await _uow.Pipelines.GetByIdAsync(request.Id, ct);
        if (pipeline is null || pipeline.TenantId != _currentUser.TenantId)
            return Result<PipelineDto>.Failure("Pipeline not found.", 404);
        return Result<PipelineDto>.Success(_mapper.Map<PipelineDto>(pipeline));
    }
}

// â”€â”€â”€ Create Pipeline â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public record CreatePipelineCommand(
    string Name,
    string? Description,
    Guid ProjectId,
    string Trigger,
    string? CronExpression
) : IRequest<Result<PipelineDto>>;

public class CreatePipelineCommandValidator : AbstractValidator<CreatePipelineCommand>
{
    public CreatePipelineCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Trigger)
            .Must(IsValidTrigger)
            .WithMessage("Invalid trigger type.");
    }

    private static bool IsValidTrigger(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant() switch
        {
            "pr" => "pullrequest",
            _ => value
        };

        return Enum.TryParse<PipelineTriggerType>(normalized, true, out _);
    }
}

public class CreatePipelineCommandHandler : IRequestHandler<CreatePipelineCommand, Result<PipelineDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public CreatePipelineCommandHandler(IUnitOfWork uow, ICurrentUser cu, IMapper mapper)
    { _uow = uow; _currentUser = cu; _mapper = mapper; }

    public async Task<Result<PipelineDto>> Handle(CreatePipelineCommand request, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<PipelineDto>.Failure("Project not found.", 404);

        var triggerValue = request.Trigger.Trim().ToLowerInvariant() switch
        {
            "pr" => "pullrequest",
            _ => request.Trigger
        };
        Enum.TryParse<PipelineTriggerType>(triggerValue, true, out var trigger);

        var pipeline = new Pipeline
        {
            TenantId = _currentUser.TenantId,
            ProjectId = request.ProjectId,
            Name = request.Name,
            Description = request.Description,
            Trigger = trigger,
            CronExpression = request.CronExpression,
            IsEnabled = true
        };

        await _uow.Pipelines.AddAsync(pipeline, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<PipelineDto>.Success(_mapper.Map<PipelineDto>(pipeline));
    }
}

public record UpdatePipelineCommand(
    Guid Id,
    string Name,
    string? Description,
    string Trigger,
    string? CronExpression,
    bool IsEnabled
) : IRequest<Result<PipelineDto>>;

public class UpdatePipelineCommandValidator : AbstractValidator<UpdatePipelineCommand>
{
    public UpdatePipelineCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Trigger)
            .Must(IsValidTrigger)
            .WithMessage("Invalid trigger type.");
    }

    private static bool IsValidTrigger(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant() switch
        {
            "pr" => "pullrequest",
            _ => value
        };

        return Enum.TryParse<PipelineTriggerType>(normalized, true, out _);
    }
}

public class UpdatePipelineCommandHandler : IRequestHandler<UpdatePipelineCommand, Result<PipelineDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public UpdatePipelineCommandHandler(IUnitOfWork uow, ICurrentUser cu, IMapper mapper)
    {
        _uow = uow;
        _currentUser = cu;
        _mapper = mapper;
    }

    public async Task<Result<PipelineDto>> Handle(UpdatePipelineCommand request, CancellationToken ct)
    {
        var pipeline = await _uow.Pipelines.GetByIdAsync(request.Id, ct);
        if (pipeline is null || pipeline.TenantId != _currentUser.TenantId)
            return Result<PipelineDto>.Failure("Pipeline not found.", 404);

        var triggerValue = request.Trigger.Trim().ToLowerInvariant() switch
        {
            "pr" => "pullrequest",
            _ => request.Trigger
        };
        Enum.TryParse<PipelineTriggerType>(triggerValue, true, out var trigger);

        pipeline.Name = request.Name;
        pipeline.Description = request.Description;
        pipeline.Trigger = trigger;
        pipeline.CronExpression = request.CronExpression;
        pipeline.IsEnabled = request.IsEnabled;
        pipeline.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync(ct);
        return Result<PipelineDto>.Success(_mapper.Map<PipelineDto>(pipeline));
    }
}

// â”€â”€â”€ Delete Pipeline â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public record DeletePipelineCommand(Guid Id) : IRequest<Result>;

public class DeletePipelineCommandHandler : IRequestHandler<DeletePipelineCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeletePipelineCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result> Handle(DeletePipelineCommand request, CancellationToken ct)
    {
        var pipeline = await _uow.Pipelines.GetByIdAsync(request.Id, ct);
        if (pipeline is null || pipeline.TenantId != _currentUser.TenantId)
            return Result.Failure("Pipeline not found.", 404);

        pipeline.SoftDelete(_currentUser.UserId);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// â”€â”€â”€ Trigger Pipeline â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public record TriggerPipelineCommand(Guid Id) : IRequest<Result>;

public class TriggerPipelineCommandHandler : IRequestHandler<TriggerPipelineCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public TriggerPipelineCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result> Handle(TriggerPipelineCommand request, CancellationToken ct)
    {
        var pipeline = await _uow.Pipelines.GetByIdAsync(request.Id, ct);
        if (pipeline is null || pipeline.TenantId != _currentUser.TenantId)
            return Result.Failure("Pipeline not found.", 404);

        if (!pipeline.IsEnabled)
            return Result.Failure("Pipeline is disabled.");

        pipeline.Status = PipelineStatus.Running;
        pipeline.LastRunAt = DateTime.UtcNow;
        pipeline.TotalRuns++;
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
