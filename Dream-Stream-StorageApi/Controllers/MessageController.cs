using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Dream_Stream_StorageApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MessageController : ControllerBase
    {
        private readonly ILogger<MessageController> _logger;
        //private const string BasePath = "/Users/Nicklas Nielsen/Desktop/data";
        private const string BasePath = "/mnt/data";
        private static readonly SemaphoreSlim MessageLock = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim OffsetLock = new SemaphoreSlim(1, 1);

        public MessageController(ILogger<MessageController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public async Task Read([FromQuery]string consumerGroup, string topic, int partition, long offset, int amount)
        {
            var filePath = $"{BasePath}/{topic}/{partition}.txt";
            var streamKey = $"{consumerGroup}/{topic}/{partition}";
            var stream = FileStreamHandler.GetFileStream(streamKey, filePath);

            if (!stream.CanRead || !stream.CanSeek) throw new Exception("AArgghh");

            await StoreOffset(consumerGroup, topic, partition, offset);
            var buffer = new byte[amount];
            stream.Seek(offset, SeekOrigin.Begin);
            await stream.ReadAsync(buffer);

            await Response.Body.WriteAsync(buffer);
        }

        [HttpPost]
        public async Task<IActionResult> Store([FromQuery]string topic, int partition, int length)
        {
            var filePath = $"{BasePath}/{topic}/{partition}.txt";
            var streamKey = $"{topic}/{partition}";

            var stream = FileStreamHandler.GetFileStream(streamKey, filePath);

            if (!stream.CanSeek || !stream.CanWrite) return StatusCode(500);

            var lengthInBytes = new byte[10];
            BitConverter.GetBytes(length).CopyTo(lengthInBytes, 0);
            var buffer = new byte[1024 * 1000];
            await Request.Body.ReadAsync(buffer);

            await MessageLock.WaitAsync();
            stream.Seek(0, SeekOrigin.End);
            var offset = stream.Position;

            await stream.WriteAsync(lengthInBytes);
            await stream.WriteAsync(buffer.Take(length).ToArray());
            MessageLock.Release();

            return Ok(offset);
        }

        [HttpGet("offset")]
        public async Task<IActionResult> ReadOffset([FromQuery]string consumerGroup, string topic, int partition)
        {
            var filePath = $"{BasePath}/offsets/{topic}/{partition}.txt";
            var streamKey = $"offset/{consumerGroup}/{topic}/{partition}";
            var stream = FileStreamHandler.GetFileStream(streamKey, filePath);

            if (!stream.CanSeek || !stream.CanRead) return StatusCode(500);

            var buffer = new byte[8];
            stream.Seek(0, SeekOrigin.Begin);
            await stream.ReadAsync(buffer);

            return Ok(LZ4MessagePackSerializer.Deserialize<long>(buffer));
        }

        private async Task StoreOffset(string consumerGroup, string topic, int partition, long offset)
        {
            var filePath = $"{BasePath}/offsets/{topic}/{partition}.txt";
            var streamKey = $"offset/{consumerGroup}/{topic}/{partition}";
            var stream = FileStreamHandler.GetFileStream(streamKey, filePath);

            if (!stream.CanWrite || !stream.CanRead) return;

            await OffsetLock.WaitAsync();
            stream.Seek(0, SeekOrigin.Begin);
            await stream.WriteAsync(LZ4MessagePackSerializer.Serialize(offset));
            OffsetLock.Release();
        }
    }
}
