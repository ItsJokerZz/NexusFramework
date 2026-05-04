using System.Linq;
using System.Threading.Tasks;
using NexusCheatFramework.Memory;
using Xunit;

namespace NexusCheatFramework.Tests;

public class AobScannerTests
{
    private static byte[] BuildBuffer(int length, params (int offset, byte[] needle)[] inserts)
    {
        var b = new byte[length];
        for (int i = 0; i < length; i++) b[i] = (byte)(i * 13);
        foreach (var (off, n) in inserts) System.Array.Copy(n, 0, b, off, n.Length);
        return b;
    }

    [Fact]
    public async Task Finds_first_match()
    {
        var needle = new byte[] { 0x48, 0x8B, 0x05, 0xDE, 0xAD };
        var buf = BuildBuffer(4096, (1234, needle));
        var client = new FakeNexusClient(buf);
        var scanner = new AobScanner(client);
        var hit = await scanner.FindFirstAsync(AobPattern.Parse("48 8B ?? DE AD"), new AobScanOptions());
        Assert.NotNull(hit);
        Assert.Equal(client.Base + 1234, hit!.Value);
    }

    [Fact]
    public async Task Finds_all_matches()
    {
        var needle = new byte[] { 0xAB, 0xCD };
        var buf = BuildBuffer(8192, (10, needle), (200, needle), (5000, needle));
        var client = new FakeNexusClient(buf);
        var scanner = new AobScanner(client);
        var hits = await scanner.FindAllAsync(AobPattern.Parse("AB CD"), new AobScanOptions());
        Assert.Contains(client.Base + 10UL, hits);
        Assert.Contains(client.Base + 200UL, hits);
        Assert.Contains(client.Base + 5000UL, hits);
    }

    [Fact]
    public async Task Match_at_chunk_boundary_is_found()
    {
        // Place the needle right at the chunk boundary so it crosses the
        // 0x40000-byte read window. Use a small chunk size to make the test fast.
        var needle = new byte[] { 0x11, 0x22, 0x33, 0x44 };
        int chunk = 128;
        int bufLen = chunk * 4;
        // Place needle straddling chunk boundary at offset chunk - 2
        var buf = BuildBuffer(bufLen, (chunk - 2, needle));
        var client = new FakeNexusClient(buf);
        var scanner = new AobScanner(client);
        var opts = new AobScanOptions { ChunkSize = (uint)chunk };
        var hit = await scanner.FindFirstAsync(AobPattern.Parse("11 22 33 44"), opts);
        Assert.NotNull(hit);
        Assert.Equal(client.Base + (ulong)(chunk - 2), hit!.Value);
    }

    [Fact]
    public async Task No_match_returns_null()
    {
        var client = new FakeNexusClient(new byte[1024]);
        var scanner = new AobScanner(client);
        var hit = await scanner.FindFirstAsync(AobPattern.Parse("DE AD BE EF"), new AobScanOptions());
        Assert.Null(hit);
    }

    [Fact]
    public async Task BufferSearch_wildcard_works()
    {
        var buf = new byte[] { 0x00, 0x48, 0x8B, 0x05, 0x89, 0x48, 0x8B, 0x10, 0x89 };
        var p = AobPattern.Parse("48 8B ?? 89");
        var hits = BufferAobSearch.AllIndexesOf(buf, p);
        Assert.Equal(new[] { 1, 5 }, hits.ToArray());
    }
}
