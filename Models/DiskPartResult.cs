namespace DiskPartUI.Models;

///<summary>
///The outcome of running a diskpart script: whether it exited cleanly, the
///combined console output, and the exact script that was sent.
///</summary>
public sealed record DiskPartResult(bool Success, string Output, string Script);
