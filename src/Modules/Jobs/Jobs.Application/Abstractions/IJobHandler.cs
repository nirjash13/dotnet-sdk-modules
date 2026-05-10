using System.Threading;
using System.Threading.Tasks;

namespace Jobs.Application.Abstractions;

/// <summary>
/// Handles the execution of a job of type <typeparamref name="TJob"/>.
/// Register one implementation per job type in DI.
/// </summary>
/// <typeparam name="TJob">The job payload type.</typeparam>
public interface IJobHandler<in TJob>
    where TJob : IJob
{
    /// <summary>Executes the job. Exceptions here will trigger the retry policy.</summary>
    /// <param name="job">The job payload.</param>
    /// <param name="ct">Cancellation token.</param>
    Task HandleAsync(TJob job, CancellationToken ct);
}
