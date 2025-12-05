using MauiVersion.Models;

namespace MauiVersion.Services;

public interface IProjectLocator
{
    Task<MauiProject?> FindMauiProjectAsync(string? projectPath, CancellationToken cancellationToken = default);
}
