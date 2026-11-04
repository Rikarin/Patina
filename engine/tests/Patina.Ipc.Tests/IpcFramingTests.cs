using CsCheck;
using System.Buffers.Binary;

namespace Patina.Ipc.Tests;

public sealed class IpcFramingTests {
    CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_message_survives_the_round_trip() {
        await using var stream = new MemoryStream();
        var hello = new WorkerHello(IpcMessage.ProtocolVersion, "1.2.3", 42);

        await IpcFraming.WriteAsync(stream, new() { Type = IpcTypes.Hello, Id = 7, Worker = hello }, Token);
        stream.Position = 0;
        var read = await IpcFraming.ReadAsync(stream, Token);

        Assert.NotNull(read);
        Assert.Equal(IpcTypes.Hello, read.Type);
        Assert.Equal(7, read.Id);
        Assert.Equal(hello, read.Worker);
    }

    [Fact]
    [Trait("Category", "Property")]
    public void Any_error_text_survives_the_round_trip() =>
        Gen.String.Sample(static text => {
                using var stream = new MemoryStream();
                IpcFraming.WriteAsync(stream, IpcMessage.Fail(1, text)).GetAwaiter().GetResult();
                stream.Position = 0;
                return IpcFraming.ReadAsync(stream).GetAwaiter().GetResult()?.Error == text;
            }
        );

    [Fact]
    public async Task The_frame_is_a_little_endian_length_and_utf8_json() {
        await using var stream = new MemoryStream();

        await IpcFraming.WriteAsync(stream, IpcMessage.Ok(1), Token);

        var frame = stream.ToArray();
        Assert.Equal(frame.Length - 4, BinaryPrimitives.ReadInt32LittleEndian(frame));
        Assert.Equal((byte)'{', frame[4]);
    }

    [Fact]
    public async Task A_clean_end_reads_as_null() {
        await using var stream = new MemoryStream();

        Assert.Null(await IpcFraming.ReadAsync(stream, Token));
    }

    [Fact]
    public async Task An_end_in_the_middle_of_a_frame_is_an_error() {
        await using var stream = new MemoryStream();
        await IpcFraming.WriteAsync(stream, IpcMessage.Ok(1), Token);
        stream.SetLength(stream.Length - 1);
        stream.Position = 0;

        await Assert.ThrowsAsync<EndOfStreamException>(async () => await IpcFraming.ReadAsync(stream, Token));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(IpcFraming.MaxFrameBytes + 1)]
    public async Task A_length_out_of_range_is_refused(int length) {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, length);
        await using var stream = new MemoryStream(header);

        await Assert.ThrowsAsync<InvalidDataException>(async () => await IpcFraming.ReadAsync(stream, Token));
    }
}
