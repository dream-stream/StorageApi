using System;
using System.IO;
using System.Threading.Tasks;

namespace Dream_Stream_StorageApi.ExtensionMethods
{
    public static class StreamExtensions
    {
        public static async Task CopyToAsync(this Stream sourceStream, Stream destinationStream, int amount)
        {
            var buffer = new byte[81920];
            int read;
            while (amount > 0 &&
                   (read = await sourceStream.ReadAsync(buffer, 0, Math.Min(buffer.Length, amount))) > 0)
            {
                await destinationStream.WriteAsync(buffer, 0, read);
                amount -= read;
            }
        }
    }
}
