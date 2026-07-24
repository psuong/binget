namespace BinGet.Data;

/// <summary>
/// Security levels for when a package is downloaded.
/// </summary>
public enum SecurityLevels {
    /// <summary>
    /// Do not do any checks.
    /// </summary>
    None,
    /// <summary>
    /// Do some checks when available, but warn when there is none available.
    /// </summary>
    Laxed,
    /// <summary>
    /// Always perform checks. No exceptions.
    /// </summary>
    Strict
}