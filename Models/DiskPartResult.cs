namespace DiskPartUI.Models;

///<summary>
///The outcome of running a diskpart script: whether it exited cleanly and the
///combined console output.
///</summary>
public sealed record DiskPartResult(bool Success, string Output);
