// -------------------------------------------------------------------------------------------------------------------
// Code taken from https://blogs.msdn.microsoft.com/pfxteam/2013/01/13/cooperatively-pausing-async-methods/#comments
// Copyright © Stephen Toub. All rights reserved
// -------------------------------------------------------------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;

public class PauseTokenSource
{
    private volatile TaskCompletionSource<bool> m_paused;
    internal static readonly Task s_completedTask = Task.FromResult(true);

    public bool IsPaused
    {
        get => m_paused != null;
        set
        {
            if (value)
            {
                Interlocked.CompareExchange(ref m_paused, new TaskCompletionSource<bool>(), null);
            }
            else
            {
                while (true)
                {
                    var tcs = m_paused;

                    if (tcs == null) return;
                    if (Interlocked.CompareExchange(ref m_paused, null, tcs) != tcs) continue;

                    tcs.SetResult(true);
                    break;
                }
            }
        }
    }

    public PauseToken Token => new PauseToken(this);

    internal Task WaitWhilePausedAsync()
    {
        var cur = m_paused;
        return cur != null ? cur.Task : s_completedTask;
    }
}

public struct PauseToken
{
    private readonly PauseTokenSource m_source;
    internal PauseToken(PauseTokenSource source) { m_source = source; }

    public bool IsPaused => m_source != null && m_source.IsPaused;

    public Task WaitWhilePausedAsync()
    {
        return IsPaused ? m_source.WaitWhilePausedAsync() : PauseTokenSource.s_completedTask;
    }
}
