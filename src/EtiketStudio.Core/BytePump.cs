// SPDX-License-Identifier: GPL-3.0-only
namespace EtiketStudio.Core;

/// <summary>Completes short writes without retrying data that was already accepted.</summary>
public static class BytePump
{
    public static void WriteAll(int length, Func<int, int, int> write, CancellationToken token = default)
    {
        if (length <= 0) throw new DomainException("InvalidFile");
        int offset = 0;
        while (offset < length)
        {
            token.ThrowIfCancellationRequested();
            int requested = Math.Min(64 * 1024, length - offset);
            int written = write(offset, requested);
            if (written <= 0 || written > requested) throw new DomainException("SpoolError", 29);
            offset += written;
        }
    }
}
