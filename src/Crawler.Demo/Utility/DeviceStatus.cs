namespace Async.Programming;

/// <summary>
/// The device status.
/// </summary>
public enum DeviceStatus
{
    /// <summary>
    /// The device is in ready.
    /// </summary>
    Ready,

    /// <summary>
    /// The device is in use meanwhile that data can be captured.
    /// </summary>
    InUse,

    /// <summary>
    /// The device has been deactivated.
    /// </summary>
    Deactivated
}