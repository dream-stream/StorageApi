using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace Dream_Stream_StorageApi
{
    public static class FileStreamHandler
    {
        private static readonly ConcurrentDictionary<string, (Timer timer, FileStream fileStream)> FileStreams = new ConcurrentDictionary<string, (Timer timer, FileStream fileStream)>();
        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1, 1);

        public static FileStream GetFileStream(string key, string filePath)
        {
            if(!File.Exists(filePath)) CreateFile(filePath);

            var (timer, fileStream) = FileStreams.GetOrAdd(key,
                (new Timer(x =>
                    {
                        if (!FileStreams.TryRemove(key, out var tuple)) return;
                        tuple.fileStream.Close();
                        tuple.fileStream.Dispose();
                        tuple.timer.Dispose();
                    }, null, 10000, 1000),
                    new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite)));
            
            timer.Change(10000, 1000);

            return fileStream;
        }

        private static void CreateFile(string path)
        {
            Lock.Wait();
            if (File.Exists(path)) return;

            var directories = path.Substring(0, path.LastIndexOf("/", StringComparison.Ordinal));
            Directory.CreateDirectory(directories);
            var stream = File.Create(path);
            stream.Close();
            stream.Dispose();
            
            Lock.Release();
        }
    }
}
