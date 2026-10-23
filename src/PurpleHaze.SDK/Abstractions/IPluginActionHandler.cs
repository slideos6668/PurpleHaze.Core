namespace PurpleHaze.SDK.Abstractions;

/// <summary>
/// Optional extension of a plugin that can handle capability actions invoked by the manager/host.
/// Plugins that only register static status indicators may omit this interface.
/// </summary>
public interface IPluginActionHandler
{
    /// <summary>
    /// Handles an action against a previously registered capability.
    /// Implementations must validate <paramref name="argument"/> and throw on invalid input.
    /// </summary>
    /// <param name="capabilityId">Capability ID as registered (without plugin prefix).</param>
    /// <param name="argument">Value from the manager (bool, number, string, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured result payload (success/failure details).</returns>
    Task<object?> HandleActionAsync(string capabilityId, object? argument, CancellationToken cancellationToken = default);
}
