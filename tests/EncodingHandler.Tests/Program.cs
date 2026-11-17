using System.Collections;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MinorShift.Emuera.Runtime.Utils;

if (args.Length != 0 && (args.Length != 2 || args[0] != "--game-dir"))
{
    Console.Error.WriteLine("Usage: EncodingHandler.Tests [--game-dir <game-directory>]");
    return 2;
}
Console.OutputEncoding = new UTF8Encoding(false);
var fixtures = Path.Combine(AppContext.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtures);
var assembly = typeof(EncodingHandler).Assembly;
var programType = assembly.GetType("MinorShift.Emuera.Program", throwOnError: true)!;
var imageType = assembly.GetType("MinorShift.Emuera.UI.Game.Image.AppContents", throwOnError: true)!;
var resourceType = assembly.GetType("MinorShift.Emuera.Runtime.Utils.ResourceStore", throwOnError: true)!;
var processType = assembly.GetType("MinorShift.Emuera.GameProc.Process", throwOnError: true)!;
int passed = 0, failed = 0;
Emit(new
{
    kind = "environment", runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    engine = assembly.Location,
    version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    note = "Calls the built engine assembly, including AppContents.LoadContents; does not start Program.Main or a GUI."
});
try
{
    var examples = new (string Name, Encoding Encoding, string Text)[]
    {
        ("utf8", new UTF8Encoding(false, true), "\u7edf\u4e00,\u65e5\u672c\u8a9e\nsecond,line"),
        ("utf8-bom", new UTF8Encoding(true, true), "\u7edf\u4e00,\u65e5\u672c\u8a9e"),
        ("cp932", EncodingHandler.GetEncoding(932), "\u65e5\u672c\u8a9e,\u753b\u50cf"),
        ("utf16-le", new UnicodeEncoding(false, true, true), "\u7edf\u4e00,\u753b\u50cf"),
        ("utf16-be", new UnicodeEncoding(true, true, true), "\u7edf\u4e00,\u753b\u50cf"),
        ("utf32-le", new UTF32Encoding(false, true, true), "\u7edf\u4e00,\u753b\u50cf"),
        ("utf32-be", new UTF32Encoding(true, true, true), "\u7edf\u4e00,\u753b\u50cf"),
        ("empty", new UTF8Encoding(false, true), ""),
        ("one-byte", new UTF8Encoding(false, true), "x"),
        ("two-bytes", new UTF8Encoding(false, true), "xy")
    };
    foreach (var example in examples)
    {
        byte[] bytes = example.Encoding.GetPreamble().Concat(example.Encoding.GetBytes(example.Text)).ToArray();
        string path = Path.Combine(fixtures, example.Name + ".csv");
        File.WriteAllBytes(path, bytes);
        Run("path/" + example.Name, () =>
        {
            var encoding = EncodingHandler.DetectEncoding(path);
            Equal(example.Encoding.CodePage, encoding.CodePage, "code page");
            Equal(example.Text, File.ReadAllText(path, encoding), "decoded text");
        });
        Run("stream/" + example.Name, () =>
        {
            using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 }.Concat(bytes).ToArray());
            stream.Position = 4;
            var encoding = EncodingHandler.DetectEncoding(stream);
            Equal(example.Encoding.CodePage, encoding.CodePage, "code page");
            Equal(4L, stream.Position, "position");
            Require(stream.CanRead, "caller stream was closed");
            using var reader = new StreamReader(stream, encoding, true, leaveOpen: true);
            Equal(example.Text, reader.ReadToEnd(), "decoded text");
        });
    }
    Run("path/read-only-attribute", () =>
    {
        string path = Path.Combine(fixtures, "readonly.csv");
        File.WriteAllText(path, "\u7edf\u4e00,42", new UTF8Encoding(false, true));
        var attributes = File.GetAttributes(path);
        File.SetAttributes(path, attributes | FileAttributes.ReadOnly);
        try { Equal(65001, EncodingHandler.DetectEncoding(path).CodePage, "read-only code page"); }
        finally { File.SetAttributes(path, attributes); }
    });
    Run("path/missing-file-propagates", () =>
        Throws<FileNotFoundException>(() => EncodingHandler.DetectEncoding(Path.Combine(fixtures, "missing.csv"))));
    Run("path/directory-propagates", () =>
        Throws<UnauthorizedAccessException>(() => EncodingHandler.DetectEncoding(fixtures)));
    Run("path/locked-file-propagates", () =>
    {
        string path = Path.Combine(fixtures, "locked.csv");
        File.WriteAllText(path, "test,42");
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        Throws<IOException>(() => EncodingHandler.DetectEncoding(path));
    });
    foreach (Exception error in new Exception[] { new IOException("read failed"), new UnauthorizedAccessException("read denied") })
    {
        Run("stream/" + error.GetType().Name + "-preserves-error-position", () =>
        {
            using var stream = new FaultStream(error, failOnSeek: false);
            stream.Position = 4;
            var thrown = Catch(() => EncodingHandler.DetectEncoding(stream));
            Require(ReferenceEquals(error, thrown), "original exception was replaced or swallowed");
            Equal(4L, stream.Position, "position after failed read");
            Require(stream.CanRead, "caller stream was closed");
        });
    }
    Run("stream/seek-error-propagates", () =>
    {
        var error = new IOException("seek failed");
        using var stream = new FaultStream(error, failOnSeek: true);
        Require(ReferenceEquals(error, Catch(() => EncodingHandler.DetectEncoding(stream))), "seek failure was hidden");
    });
    Run("decode/invalid-in-both-encodings", () =>
    {
        string path = Path.Combine(fixtures, "invalid.csv");
        File.WriteAllBytes(path, new byte[] { 0x81 });
        var encoding = EncodingHandler.DetectEncoding(path);
        Equal(932, encoding.CodePage, "invalid UTF8 should fall back to CP932");
        Throws<DecoderFallbackException>(() => File.ReadAllText(path, encoding));
    });
    Run("decode/invalid-utf8-with-bom", () =>
    {
        string path = Path.Combine(fixtures, "invalid-bom.csv");
        File.WriteAllBytes(path, new byte[] { 0xef, 0xbb, 0xbf, 0xff });
        var encoding = EncodingHandler.DetectEncoding(path);
        Equal(65001, encoding.CodePage, "BOM must select UTF8");
        Throws<DecoderFallbackException>(() => encoding.GetString(new byte[] { 0xff }));
    });

    string diskGame = Path.Combine(fixtures, "disk-game");
    Directory.CreateDirectory(Path.Combine(diskGame, "resources"));
    Directory.CreateDirectory(Path.Combine(diskGame, "csv"));
    File.WriteAllText(Path.Combine(diskGame, "resources", "probe.csv"), "test_sprite,image.webp,0,0,1,1", new UTF8Encoding(false));
    Run("integration/disk-csv-load", () =>
    {
        Configure(diskGame);
        Require(LoadImages() == null, "disk CSV load returned an error");
        Equal(1, RowCount(), "disk metadata count");
    });
    Run("integration/csv-error-source-directory", () =>
    {
        Configure(diskGame);
        string text = "\u7edf\u4e00,42";
        File.WriteAllText(Path.Combine(diskGame, "csv", "probe.csv"), text, new UTF8Encoding(false));
        var positionType = assembly.GetTypes().Single(t => t.Name == "ScriptPosition");
        var position = Activator.CreateInstance(positionType, "probe.csv", 0);
        var actual = Invoke(processType, "getRawTextFormFilewithLine", position);
        Equal(text, (string)actual!, "CSV error source line");
    });
    Run("integration/unreadable-csv-remains-fatal", () =>
    {
        Configure(diskGame);
        using var held = new FileStream(Path.Combine(diskGame, "resources", "probe.csv"), FileMode.Open, FileAccess.Read, FileShare.None);
        Require(LoadImages() is IOException, "unreadable CSV was skipped or converted to an encoding error");
    });
    string pakGame = Path.Combine(fixtures, "pak-game");
    Directory.CreateDirectory(pakGame);
    using (var zip = ZipFile.Open(Path.Combine(pakGame, "resources.pak"), ZipArchiveMode.Create))
    {
        using var writer = new StreamWriter(zip.CreateEntry("resources/probe.csv").Open(), new UTF8Encoding(false));
        writer.Write("pak_sprite,image.webp,0,0,1,1");
    }
    Run("integration/pak-only-csv-load", () =>
    {
        Configure(pakGame);
        Require(LoadImages() == null, "pak CSV load returned an error");
        Equal(1, RowCount(), "pak metadata count");
    });

    if (args.Length == 2)
    {
        string game = Path.GetFullPath(args[1]);
        string root = Path.Combine(game, "resources");
        string[] files = Directory.EnumerateFiles(root, "*.csv", SearchOption.AllDirectories).ToArray();
        Require(files.Length > 0, "live CSV scope is empty");
        long totalLines = 0, totalBytes = 0;
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var expectedMeta = new Dictionary<string, int>(StringComparer.Ordinal);
        var diskCsvNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in files)
        {
            Run("live/" + Path.GetRelativePath(root, path), () =>
            {
                byte[] before = File.ReadAllBytes(path);
                string hash = Convert.ToHexString(SHA256.HashData(before));
                var encoding = EncodingHandler.DetectEncoding(path);
                string[] lines = File.ReadAllLines(path, encoding);
                Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), "content changed");
                hashes.Add(path, hash);
                diskCsvNames.Add(Path.GetRelativePath(root, path).Replace('\\', '/'));
                AddExpectedRows(expectedMeta, lines);
                totalLines += lines.Length;
                totalBytes += before.LongLength;
                Emit(new { kind = "csv", path, bytesRead = before.LongLength, linesRead = lines.Length, codePage = encoding.CodePage, sha256 = hash });
            });
        }
        int pakOnlyCsvs = 0;
        string pak = File.Exists(Path.Combine(game, "resources.pak")) ? Path.Combine(game, "resources.pak") : Path.Combine(game, "resources.zip");
        if (File.Exists(pak))
        {
            using var zip = ZipFile.OpenRead(pak);
            foreach (var entry in zip.Entries)
            {
                if (!entry.FullName.StartsWith("resources/", StringComparison.Ordinal) || !entry.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) continue;
                if (diskCsvNames.Contains(entry.FullName["resources/".Length..])) continue;
                using var source = entry.Open();
                using var memory = new MemoryStream();
                source.CopyTo(memory);
                memory.Position = 0;
                using var reader = new StreamReader(memory, EncodingHandler.DetectEncoding(memory), true);
                var lines = new List<string>();
                while (reader.ReadLine() is { } line) lines.Add(line);
                AddExpectedRows(expectedMeta, lines);
                pakOnlyCsvs++;
            }
        }
        Emit(new { kind = "coverage", enumeratedCsvs = files.Length, loadedCsvs = hashes.Count, pakOnlyCsvs, totalLines, totalBytes });
        Run("integration/live-AppContents-load", () =>
        {
            Configure(game);
            Exception? error = LoadImages();
            if (error != null) throw error;
            var actual = (IDictionary)imageType.GetField("metaRows", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            Require(expectedMeta.Count > 0, "expected metadata scope is empty");
            Equal(expectedMeta.Count, actual.Count, "sprite name coverage");
            foreach (var entry in expectedMeta)
            {
                Require(actual.Contains(entry.Key), "missing sprite: " + entry.Key);
                Equal(entry.Value, ((ICollection)actual[entry.Key]!).Count, "metadata rows: " + entry.Key);
            }
            foreach (var entry in hashes)
                Equal(entry.Value, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(entry.Key))), "live resource changed");
            Emit(new { kind = "image-load", game, metaRowCount = RowCount(), expectedMetaRows = expectedMeta.Values.Sum(), spriteNames = actual.Count, expectedSpriteNames = expectedMeta.Count, loadedCsvs = hashes.Count, expectedCsvs = files.Length });
            Equal(files.Length, hashes.Count, "CSV coverage");
        });
        foreach (string name in new[] { "通用浴室_0_0", "別立ち3_服_通常_11" })
        {
            if (!expectedMeta.ContainsKey(name.ToUpperInvariant())) continue;
            Run("integration/live-sprite-decode/" + name, () =>
            {
                var sprite = Invoke(imageType, "GetSprite", name);
                Require(sprite != null, "sprite was not created");
                Require((bool)sprite!.GetType().GetProperty("IsCreated")!.GetValue(sprite)!, "sprite reports not created");
                var image = sprite.GetType().GetField("BaseImage")!.GetValue(sprite)!;
                var bitmap = image.GetType().GetProperty("SKBitmap")!.GetValue(image);
                Require(bitmap != null, "bitmap decoding failed");
                int width = (int)bitmap!.GetType().GetProperty("Width")!.GetValue(bitmap)!;
                int height = (int)bitmap.GetType().GetProperty("Height")!.GetValue(bitmap)!;
                Require(width > 0 && height > 0, "decoded bitmap is empty");
                Emit(new { kind = "sprite-decode", name, width, height });
            });
        }
    }
}
catch (Exception ex)
{
    failed++;
    Emit(new { kind = "fatal", error = ex.ToString() });
}
finally
{
    Invoke(resourceType, "Configure", fixtures, Path.Combine(fixtures, "resources"));
    Directory.Delete(fixtures, recursive: true);
}
Emit(new { kind = "summary", passed, failed, guiTested = false });
return failed == 0 ? 0 : 1;

void Configure(string game) => Invoke(programType, "SetDirPaths", game);
Exception? LoadImages() => (Exception?)Invoke(imageType, "LoadContents", true);
int RowCount()
{
    var rows = (IDictionary)imageType.GetField("metaRows", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
    return rows.Values.Cast<ICollection>().Sum(list => list.Count);
}
void Run(string name, Action test)
{
    try { test(); passed++; Emit(new { kind = "test", name, result = "PASS" }); }
    catch (Exception ex) { failed++; Emit(new { kind = "test", name, result = "FAIL", error = ex.ToString() }); }
}
static object? Invoke(Type type, string name, params object?[] args) =>
    type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);
static void AddExpectedRows(Dictionary<string, int> expected, IEnumerable<string> lines)
{
    foreach (string raw in lines)
    {
        string line = raw.Trim();
        if (line.Length == 0 || line.StartsWith(';')) continue;
        string[] fields = line.Split(',');
        if (fields.Length < 2) continue;
        if (fields[1].Trim().Equals("ANIME", StringComparison.OrdinalIgnoreCase) &&
            (fields.Length < 4 || !int.TryParse(fields[2], out int width) || !int.TryParse(fields[3], out int height) || width <= 0 || height <= 0)) continue;
        string name = fields[0].Trim().ToUpperInvariant();
        expected[name] = expected.GetValueOrDefault(name) + 1;
    }
}
static void Emit(object value) => Console.WriteLine(JsonSerializer.Serialize(value));
static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
static void Equal<T>(T expected, T actual, string reason) => Require(EqualityComparer<T>.Default.Equals(expected, actual), $"{reason}: expected {expected}, got {actual}");
static Exception Catch(Action action)
{
    try { action(); }
    catch (Exception ex) { return ex; }
    throw new InvalidOperationException("Expected an exception.");
}
static void Throws<T>(Action action) where T : Exception => Require(Catch(action) is T, $"Expected {typeof(T).Name}");

sealed class FaultStream(Exception error, bool failOnSeek) : MemoryStream(new byte[16])
{
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (failOnSeek) return base.Read(buffer, offset, count);
        Position++;
        throw error;
    }
    public override int Read(Span<byte> buffer)
    {
        if (failOnSeek) return base.Read(buffer);
        Position++;
        throw error;
    }
    public override long Seek(long offset, SeekOrigin origin) => failOnSeek ? throw error : base.Seek(offset, origin);
}
