using System.IO;
using Android.Content;
using Android.OS;
using MinorShift.Emuera;

namespace XEmuera.Droid
{
    internal class AndroidPathProvider : IPathProvider
    {
        private readonly string _exeDir;

        public AndroidPathProvider(Context context)
        {
            var externalRoot = Environment.ExternalStorageDirectory.AbsolutePath;
            _exeDir = externalRoot + Path.DirectorySeparatorChar + "emuera" + Path.DirectorySeparatorChar;

            var sep = Path.DirectorySeparatorChar;

            ErbDir = ResolveDir(_exeDir + "erb" + sep, _exeDir + "ERB" + sep);
            CsvDir = ResolveDir(_exeDir + "csv" + sep, _exeDir + "CSV" + sep);
            DebugDir = ResolveDir(_exeDir + "debug" + sep, _exeDir + "DEBUG" + sep);
            DatDir = ResolveDir(_exeDir + "dat" + sep, _exeDir + "DAT" + sep);
            ContentDir = ResolveDir(_exeDir + "resources" + sep, _exeDir + "RESOURCES" + sep);
            MusicDir = ResolveDir(_exeDir + "sound" + sep, _exeDir + "SOUND" + sep);
            SavDir = _exeDir + "sav" + sep;
            SqlDir = SavDir + "sql" + sep;
            TempDir = context.CacheDir?.AbsolutePath + sep;
            LazyLoadingDir = _exeDir;

            try
            {
                var pkgInfo = context.PackageManager?.GetPackageInfo(context.PackageName, 0);
                ExeName = pkgInfo?.PackageName ?? "emuera";
            }
            catch
            {
                ExeName = "emuera";
            }
        }

        public string ExeDir => _exeDir;
        public string ErbDir { get; }
        public string CsvDir { get; }
        public string SavDir { get; }
        public string DatDir { get; }
        public string DebugDir { get; }
        public string ContentDir { get; }
        public string MusicDir { get; }
        public string SqlDir { get; }
        public string TempDir { get; }
        public string LazyLoadingDir { get; }
        public string ExeName { get; }

        private static string ResolveDir(string primary, string fallback)
        {
            return Directory.Exists(primary) ? primary : fallback;
        }
    }
}
