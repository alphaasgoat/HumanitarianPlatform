using System.Text.Json;
using Humanitarian.Application.Common;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Volunteers;

public interface IVolunteerTaskService
{
    Task<TaskDto> CreateAsync(CreateTaskDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<TaskDto>> ListAsync(Guid? crisisEventId, VolunteerTaskStatus? status, CancellationToken ct = default);
    Task<TaskDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<VolunteerMatchDto>> RecommendAsync(Guid taskId, CancellationToken ct = default);
    Task<AssignmentDto> AssignAsync(Guid taskId, AssignDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<AssignmentDto>> ListAssignmentsAsync(Guid taskId, CancellationToken ct = default);
    Task<TaskDto> CancelTaskAsync(Guid taskId, string? reason, CancellationToken ct = default);

    Task<IReadOnlyList<AssignmentDto>> MyAssignmentsAsync(CancellationToken ct = default);
    Task<AssignmentDto> AcceptAsync(Guid assignmentId, CancellationToken ct = default);
    Task<AssignmentDto> DeclineAsync(Guid assignmentId, string? reason, CancellationToken ct = default);
    Task<AssignmentDto> CompleteAsync(Guid assignmentId, CompleteAssignmentDto dto, CancellationToken ct = default);
    Task<AssignmentDto> CancelAssignmentAsync(Guid assignmentId, string? reason, CancellationToken ct = default);
}

/// <summary>
/// Tasks, volunteer recommendations and assignments. The matcher only RECOMMENDS; a coordinator assigns.
/// A volunteer sees and answers only their own assignments.
/// </summary>
public class VolunteerTaskService(
    IUnitOfWork uow, IAuditService audit, ICurrentUser current, TimeProvider time, IVolunteerMatcher matcher) : IVolunteerTaskService
{
    private DateTime Now => time.GetUtcNow().UtcDateTime;
    private bool CanCoordinate => current.Role is Roles.VolunteerCoordinator or Roles.EmergencyCoordinator;
    private bool CanView => CanCoordinate || current.Role == Roles.CaseManager;

    // =====================================================================
    // Tasks
    // =====================================================================
    public async Task<TaskDto> CreateAsync(CreateTaskDto dto, CancellationToken ct = default)
    {
        RequireCoordinate();
        var start = TimeRules.AsUtc(dto.StartUtc);
        var end = TimeRules.AsUtc(dto.EndUtc);
        if (end <= start) throw new AppValidationException("The task must end after it starts.");
        if (dto.Latitude.HasValue != dto.Longitude.HasValue)
            throw new AppValidationException("Latitude and longitude must be provided together.");

        var crisis = await uow.Repository<CrisisEvent>().GetByIdAsync(dto.CrisisEventId, ct)
                     ?? throw new NotFoundException("Crisis event not found.");
        if (crisis.Status == CrisisStatus.Closed) throw new ConflictException("This crisis event is closed.");

        if (dto.RequestId is { } requestId)
        {
            var request = await uow.Repository<AssistanceRequest>().GetByIdAsync(requestId, ct)
                          ?? throw new NotFoundException("Request not found.");
            if (request.CrisisEventId != dto.CrisisEventId)
                throw new AppValidationException("The request belongs to a different crisis event.");
        }

        var task = new VolunteerTask
        {
            CrisisEventId = dto.CrisisEventId,
            RequestId = dto.RequestId,
            Title = dto.Title.Trim(),
            Description = dto.Description,
            RequiredSkill = dto.RequiredSkill,
            VolunteersNeeded = dto.VolunteersNeeded,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            StartUtc = start,
            EndUtc = end,
            CreatedByUserId = current.UserId ?? Guid.Empty,
            CreatedAtUtc = Now
        };
        await uow.Repository<VolunteerTask>().AddAsync(task, ct);
        await audit.RecordAsync("VolunteerTaskCreated", nameof(VolunteerTask), task.Id.ToString(),
            $"{task.Title}; needs {task.VolunteersNeeded} x {task.RequiredSkill}", ct: ct);
        await uow.SaveChangesAsync(ct);
        return (await ToTaskDtosAsync(new[] { task }, ct))[0];
    }

    public async Task<IReadOnlyList<TaskDto>> ListAsync(Guid? crisisEventId, VolunteerTaskStatus? status, CancellationToken ct = default)
    {
        RequireView();
        var cid = crisisEventId;
        var st = status;
        var tasks = await uow.Repository<VolunteerTask>().ListAsync(
            t => (cid == null || t.CrisisEventId == cid) && (st == null || t.Status == st),
            q => q.OrderBy(t => t.StartUtc), ct: ct);
        return await ToTaskDtosAsync(tasks, ct);
    }

    public async Task<TaskDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        RequireView();
        return (await ToTaskDtosAsync(new[] { await LoadTaskAsync(id, ct) }, ct))[0];
    }

    public async Task<TaskDto> CancelTaskAsync(Guid taskId, string? reason, CancellationToken ct = default)
    {
        RequireCoordinate();
        var task = await LoadTaskAsync(taskId, ct);
        if (task.Status is VolunteerTaskStatus.Completed or VolunteerTaskStatus.Cancelled)
            throw new ConflictException($"This task is already {task.Status}.");

        var now = Now;
        var assignments = await uow.Repository<TaskAssignment>().ListAsync(a => a.TaskId == taskId, ct: ct);
        foreach (var a in assignments.Where(a => a.Status is AssignmentStatus.Assigned or AssignmentStatus.Accepted))
        {
            a.Status = AssignmentStatus.Cancelled;
            a.UpdatedAtUtc = now;
            uow.Repository<TaskAssignment>().Update(a);
        }

        task.Status = VolunteerTaskStatus.Cancelled;
        task.UpdatedAtUtc = now;
        uow.Repository<VolunteerTask>().Update(task);
        await audit.RecordAsync("VolunteerTaskCancelled", nameof(VolunteerTask), taskId.ToString(), reason, ct: ct);
        await uow.SaveChangesAsync(ct);
        return (await ToTaskDtosAsync(new[] { task }, ct))[0];
    }

    // =====================================================================
    // Recommendation and assignment (human decision)
    // =====================================================================
    public async Task<IReadOnlyList<VolunteerMatchDto>> RecommendAsync(Guid taskId, CancellationToken ct = default)
    {
        RequireCoordinate();
        var task = await LoadTaskAsync(taskId, ct);
        var ranked = await RankAsync(task, ct);
        return ranked.Matches.Take(10).Select(m => new VolunteerMatchDto(
            m.Volunteer.Id, m.Volunteer.FullName, SkillSet.Names(m.Volunteer.Skills),
            m.DistanceKm, m.Score, m.Rationale)).ToList();
    }

    public async Task<AssignmentDto> AssignAsync(Guid taskId, AssignDto dto, CancellationToken ct = default)
    {
        RequireCoordinate();
        var task = await LoadTaskAsync(taskId, ct);
        if (task.Status != VolunteerTaskStatus.Open)
            throw new ConflictException(task.Status == VolunteerTaskStatus.Filled
                ? "This task already has enough volunteers."
                : $"This task is {task.Status} and cannot take more volunteers.");

        var volunteer = await uow.Repository<Volunteer>().GetByIdAsync(dto.VolunteerId, ct)
                        ?? throw new NotFoundException("Volunteer not found.");
        if (!volunteer.IsActive) throw new ConflictException("This volunteer is not active.");
        if (!volunteer.IsVerified) throw new ConflictException("Only verified volunteers can be assigned to tasks.");

        var taskAssignments = await uow.Repository<TaskAssignment>().ListAsync(a => a.TaskId == taskId, ct: ct);
        var active = taskAssignments.Where(a => a.Status is AssignmentStatus.Assigned or AssignmentStatus.Accepted).ToList();
        if (active.Any(a => a.VolunteerId == volunteer.Id))
            throw new ConflictException("This volunteer is already assigned to the task.");
        if (active.Count >= task.VolunteersNeeded)
            throw new ConflictException("This task already has enough volunteers.");

        if (!VolunteerMatcher.HasSkill(volunteer, task.RequiredSkill))
            throw new ConflictException($"This volunteer does not have the required skill ({task.RequiredSkill}).");

        var ranked = await RankAsync(task, ct);
        var candidate = ranked.Candidates.First(c => c.Volunteer.Id == volunteer.Id);
        if (!VolunteerMatcher.IsAvailable(candidate, task))
            throw new ConflictException("This volunteer is not available for the whole shift.");
        if (VolunteerMatcher.IsBusy(candidate, task))
            throw new ConflictException("This volunteer already has another task at that time.");

        var position = ranked.Matches.ToList().FindIndex(m => m.Volunteer.Id == volunteer.Id);
        var score = position >= 0 ? ranked.Matches[position].Score : 0;

        var now = Now;
        var assignment = new TaskAssignment
        {
            TaskId = taskId,
            VolunteerId = volunteer.Id,
            Status = AssignmentStatus.Assigned,
            MatchScore = score,
            AssignedByUserId = current.UserId ?? Guid.Empty,
            AssignedAtUtc = now,
            Note = dto.Note,
            CreatedAtUtc = now
        };
        await uow.Repository<TaskAssignment>().AddAsync(assignment, ct);

        var completed = taskAssignments.Count(a => a.Status == AssignmentStatus.Completed);
        if (active.Count + 1 + completed >= task.VolunteersNeeded) task.Status = VolunteerTaskStatus.Filled;
        task.UpdatedAtUtc = now;
        uow.Repository<VolunteerTask>().Update(task);

        await audit.RecordAsync("VolunteerAssigned", nameof(VolunteerTask), taskId.ToString(),
            JsonSerializer.Serialize(new
            {
                assignmentId = assignment.Id,
                volunteerId = volunteer.Id,
                matcherScore = score,
                recommendedFirst = ranked.Matches.Count > 0 ? ranked.Matches[0].Volunteer.Id : (Guid?)null,
                humanChoiceRank = position >= 0 ? position + 1 : (int?)null,
                note = dto.Note
            }), ct: ct);

        await uow.SaveChangesAsync(ct);
        return ToDto(assignment, task);
    }

    public async Task<IReadOnlyList<AssignmentDto>> ListAssignmentsAsync(Guid taskId, CancellationToken ct = default)
    {
        RequireView();
        var task = await LoadTaskAsync(taskId, ct);
        var list = await uow.Repository<TaskAssignment>().ListAsync(
            a => a.TaskId == taskId, q => q.OrderBy(a => a.AssignedAtUtc), ct: ct);
        return list.Select(a => ToDto(a, task)).ToList();
    }

    // =====================================================================
    // The volunteer answers their own assignments
    // =====================================================================
    public async Task<IReadOnlyList<AssignmentDto>> MyAssignmentsAsync(CancellationToken ct = default)
    {
        var volunteer = await LoadMyProfileAsync(ct);
        var id = volunteer.Id;
        var list = await uow.Repository<TaskAssignment>().ListAsync(
            a => a.VolunteerId == id, q => q.OrderByDescending(a => a.AssignedAtUtc), ct: ct);

        var taskIds = list.Select(a => a.TaskId).Distinct().ToList();
        var tasks = taskIds.Count == 0
            ? new List<VolunteerTask>()
            : (await uow.Repository<VolunteerTask>().ListAsync(t => taskIds.Contains(t.Id), ct: ct)).ToList();
        var byId = tasks.ToDictionary(t => t.Id);
        return list.Select(a => ToDto(a, byId.TryGetValue(a.TaskId, out var found) ? found : null)).ToList();
    }

    public async Task<AssignmentDto> AcceptAsync(Guid assignmentId, CancellationToken ct = default)
    {
        var (assignment, job) = await LoadMyAssignmentAsync(assignmentId, ct);
        if (assignment.Status != AssignmentStatus.Assigned)
            throw new ConflictException($"Only a new assignment can be accepted (current status: {assignment.Status}).");

        var now = Now;
        assignment.Status = AssignmentStatus.Accepted;
        assignment.RespondedAtUtc = now;
        assignment.UpdatedAtUtc = now;
        uow.Repository<TaskAssignment>().Update(assignment);
        await audit.RecordAsync("AssignmentAccepted", nameof(TaskAssignment), assignment.Id.ToString(), ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(assignment, job);
    }

    public async Task<AssignmentDto> DeclineAsync(Guid assignmentId, string? reason, CancellationToken ct = default)
    {
        var (assignment, job) = await LoadMyAssignmentAsync(assignmentId, ct);
        if (assignment.Status is not (AssignmentStatus.Assigned or AssignmentStatus.Accepted))
            throw new ConflictException($"This assignment is already {assignment.Status}.");

        var now = Now;
        assignment.Status = AssignmentStatus.Declined;
        assignment.RespondedAtUtc = now;
        assignment.Note = string.IsNullOrWhiteSpace(reason) ? assignment.Note : reason.Trim();
        assignment.UpdatedAtUtc = now;
        uow.Repository<TaskAssignment>().Update(assignment);

        await RecalculateTaskStatusAsync(job, ct);
        await audit.RecordAsync("AssignmentDeclined", nameof(TaskAssignment), assignment.Id.ToString(), reason, ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(assignment, job);
    }

    // =====================================================================
    // Coordinators close the loop
    // =====================================================================
    public async Task<AssignmentDto> CompleteAsync(Guid assignmentId, CompleteAssignmentDto dto, CancellationToken ct = default)
    {
        RequireCoordinate();
        var assignment = await LoadAssignmentAsync(assignmentId, ct);
        var task = await LoadTaskAsync(assignment.TaskId, ct);
        if (assignment.Status != AssignmentStatus.Accepted)
            throw new ConflictException($"Only an accepted assignment can be completed (current status: {assignment.Status}).");

        var now = Now;
        assignment.Status = AssignmentStatus.Completed;
        assignment.CompletedAtUtc = now;
        assignment.HoursWorked = dto.HoursWorked;
        assignment.Note = string.IsNullOrWhiteSpace(dto.Note) ? assignment.Note : dto.Note.Trim();
        assignment.UpdatedAtUtc = now;
        uow.Repository<TaskAssignment>().Update(assignment);

        await RecalculateTaskStatusAsync(task, ct);
        await audit.RecordAsync("AssignmentCompleted", nameof(TaskAssignment), assignment.Id.ToString(),
            $"{dto.HoursWorked} hours", ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(assignment, task);
    }

    public async Task<AssignmentDto> CancelAssignmentAsync(Guid assignmentId, string? reason, CancellationToken ct = default)
    {
        RequireCoordinate();
        var assignment = await LoadAssignmentAsync(assignmentId, ct);
        var task = await LoadTaskAsync(assignment.TaskId, ct);
        if (assignment.Status is not (AssignmentStatus.Assigned or AssignmentStatus.Accepted))
            throw new ConflictException($"This assignment is already {assignment.Status}.");

        assignment.Status = AssignmentStatus.Cancelled;
        assignment.UpdatedAtUtc = Now;
        uow.Repository<TaskAssignment>().Update(assignment);

        await RecalculateTaskStatusAsync(task, ct);
        await audit.RecordAsync("AssignmentCancelled", nameof(TaskAssignment), assignment.Id.ToString(), reason, ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(assignment, task);
    }

    // =====================================================================
    // Helpers
    // =====================================================================
    private sealed record RankedResult(IReadOnlyList<VolunteerCandidate> Candidates, IReadOnlyList<VolunteerMatch> Matches);

    private async Task<RankedResult> RankAsync(VolunteerTask task, CancellationToken ct)
    {
        var volunteers = await uow.Repository<Volunteer>().ListAsync(v => v.IsActive && v.IsVerified, ct: ct);
        var ids = volunteers.Select(v => v.Id).ToList();

        IReadOnlyList<VolunteerAvailability> slots = Array.Empty<VolunteerAvailability>();
        IReadOnlyList<TaskAssignment> active = Array.Empty<TaskAssignment>();
        if (ids.Count > 0)
        {
            slots = await uow.Repository<VolunteerAvailability>().ListAsync(s => ids.Contains(s.VolunteerId), ct: ct);
            active = await uow.Repository<TaskAssignment>().ListAsync(
                a => ids.Contains(a.VolunteerId) && (a.Status == AssignmentStatus.Assigned || a.Status == AssignmentStatus.Accepted), ct: ct);
        }

        var taskIds = active.Select(a => a.TaskId).Distinct().ToList();
        var otherTasks = taskIds.Count == 0
            ? new Dictionary<Guid, VolunteerTask>()
            : (await uow.Repository<VolunteerTask>().ListAsync(t => taskIds.Contains(t.Id), ct: ct)).ToDictionary(t => t.Id);

        var candidates = volunteers.Select(v => new VolunteerCandidate(
            v,
            slots.Where(s => s.VolunteerId == v.Id).ToList(),
            active.Where(a => a.VolunteerId == v.Id && otherTasks.ContainsKey(a.TaskId))
                  .Select(a => new BusyWindow(otherTasks[a.TaskId].StartUtc, otherTasks[a.TaskId].EndUtc)).ToList())).ToList();

        return new RankedResult(candidates, matcher.Rank(task, candidates));
    }

    /// <summary>Open / Filled / Completed follows the assignments; a cancelled task stays cancelled.</summary>
    private async Task RecalculateTaskStatusAsync(VolunteerTask task, CancellationToken ct)
    {
        if (task.Status == VolunteerTaskStatus.Cancelled) return;
        var all = await uow.Repository<TaskAssignment>().ListAsync(a => a.TaskId == task.Id, ct: ct);
        var active = all.Count(a => a.Status is AssignmentStatus.Assigned or AssignmentStatus.Accepted);
        var completed = all.Count(a => a.Status == AssignmentStatus.Completed);

        if (active == 0 && completed >= task.VolunteersNeeded) task.Status = VolunteerTaskStatus.Completed;
        else if (active + completed >= task.VolunteersNeeded) task.Status = VolunteerTaskStatus.Filled;
        else task.Status = VolunteerTaskStatus.Open;

        task.UpdatedAtUtc = Now;
        uow.Repository<VolunteerTask>().Update(task);
    }

    private async Task<VolunteerTask> LoadTaskAsync(Guid id, CancellationToken ct) =>
        await uow.Repository<VolunteerTask>().GetByIdAsync(id, ct) ?? throw new NotFoundException("Task not found.");

    private async Task<TaskAssignment> LoadAssignmentAsync(Guid id, CancellationToken ct) =>
        await uow.Repository<TaskAssignment>().GetByIdAsync(id, ct) ?? throw new NotFoundException("Assignment not found.");

    private async Task<Volunteer> LoadMyProfileAsync(CancellationToken ct)
    {
        if (current.Role != Roles.Citizen || current.UserId is null)
            throw new ForbiddenException("Only registered volunteers can do this.");
        var userId = current.UserId.Value;
        return await uow.Repository<Volunteer>().FirstOrDefaultAsync(v => v.UserId == userId, ct)
               ?? throw new NotFoundException("You have not registered as a volunteer yet.");
    }

    /// <summary>A volunteer can only reach their own assignments; others look like "not found".</summary>
    private async Task<(TaskAssignment Assignment, VolunteerTask Job)> LoadMyAssignmentAsync(Guid id, CancellationToken ct)
    {
        var volunteer = await LoadMyProfileAsync(ct);
        var assignment = await LoadAssignmentAsync(id, ct);
        if (assignment.VolunteerId != volunteer.Id) throw new NotFoundException("Assignment not found.");
        return (assignment, await LoadTaskAsync(assignment.TaskId, ct));
    }

    private async Task<IReadOnlyList<TaskDto>> ToTaskDtosAsync(IReadOnlyList<VolunteerTask> tasks, CancellationToken ct)
    {
        if (tasks.Count == 0) return Array.Empty<TaskDto>();
        var ids = tasks.Select(t => t.Id).ToList();
        var active = await uow.Repository<TaskAssignment>().ListAsync(
            a => ids.Contains(a.TaskId) && (a.Status == AssignmentStatus.Assigned || a.Status == AssignmentStatus.Accepted), ct: ct);

        return tasks.Select(t => new TaskDto(
            t.Id, t.CrisisEventId, t.RequestId, t.Title, t.Description, t.RequiredSkill.ToString(), t.VolunteersNeeded,
            active.Count(a => a.TaskId == t.Id), t.Latitude, t.Longitude, t.StartUtc, t.EndUtc, t.Status.ToString())).ToList();
    }

    private static AssignmentDto ToDto(TaskAssignment a, VolunteerTask? task) => new(
        a.Id, a.TaskId, a.VolunteerId, a.Status.ToString(), a.MatchScore, a.AssignedAtUtc, a.RespondedAtUtc,
        a.CompletedAtUtc, a.HoursWorked, a.Note, task?.Title, task?.StartUtc, task?.EndUtc);

    private void RequireCoordinate()
    {
        if (!CanCoordinate) throw new ForbiddenException("Only volunteer coordinators and emergency coordinators can do this.");
    }

    private void RequireView()
    {
        if (!CanView) throw new ForbiddenException("You are not allowed to view volunteer tasks.");
    }
}
