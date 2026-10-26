using System.Threading.Channels;
using Tot.Integrations.Runtime.Redaction;

namespace Tot.Integrations.Runtime.Handlers;

/// <summary>
/// Singleton bounded channel that <see cref="JournalHandler"/> writes <see cref="CallRecord"/>s into.
/// The <c>JournalWriter</c> background service (INT-18) drains this channel to the DB.
/// Capacity is 10,000. When full, records are dropped and the drop counter increments
/// (surfaced as a metric in INT-14).
/// </summary>
internal sealed class JournalChannel
{
    private readonly Channel<CallRecord> _channel;

    internal long DroppedCount;

    public JournalChannel()
    {
        _channel = Channel.CreateBounded<CallRecord>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    internal bool TryWrite(CallRecord record)
    {
        if (_channel.Writer.TryWrite(record))
            return true;
        System.Threading.Interlocked.Increment(ref DroppedCount);
        return false;
    }

    internal ChannelReader<CallRecord> Reader => _channel.Reader;
}
