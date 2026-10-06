using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>
/// The port through which the conductor finds work and talks to humans about
/// it. GitHub Issues is one adapter; any tracker that can hold a conversation
/// and mark an item as waiting can be another.
/// </summary>
/// <remarks>
/// Being "ready" means a maintainer (or the triage worker, on a maintainer's
/// behalf) has vouched that the item is clear enough to work on. Waiting on a
/// human is visible: an item with an open question is not ready, and is shown
/// to the person whose answer it needs.
/// </remarks>
public interface IWorkSupplying
{
    /// <summary>Items marked ready and not waiting on a human.</summary>
    Task<Fin<IReadOnlyList<WorkItemId>>> ListReady(CancellationToken cancellationToken);

    /// <summary>One item, with its whole conversation.</summary>
    Task<Fin<WorkItem>> Read(WorkItemId id, CancellationToken cancellationToken);

    /// <summary>
    /// Posts a question on the item and marks it as waiting on a maintainer,
    /// assigning it to them so it appears in their own list.
    /// </summary>
    Task<Fin<Unit>> Ask(WorkItemId id, string question, CancellationToken cancellationToken);

    /// <summary>Marks a question answered: the item no longer waits on anyone.</summary>
    Task<Fin<Unit>> Resolve(WorkItemId id, CancellationToken cancellationToken);
}