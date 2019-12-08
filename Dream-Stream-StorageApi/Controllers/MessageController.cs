using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dream_Stream_StorageApi.ExtensionMethods;
using MessagePack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace Dream_Stream_StorageApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MessageController : ControllerBase
    {
        private readonly ILogger<MessageController> _logger;
        private const string BasePath = "/mnt/data";
        private static readonly SemaphoreSlim MessageLock = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim OffsetLock = new SemaphoreSlim(1, 1);
        private static readonly Counter MessagesWrittenSizeInBytes = Metrics.CreateCounter("messages_written_size_in_bytes", "", new CounterConfiguration
        {
            LabelNames = new[] { "TopicPartition" }
        });

        private static readonly Counter MessagesReadSizeInBytes = Metrics.CreateCounter("messages_read_size_in_bytes", "", new CounterConfiguration
        {
            LabelNames = new[] { "TopicPartition" }
        });

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

            if (!stream.CanRead || !stream.CanSeek) throw new Exception("AArgghh Stream");
            if (!await StoreOffset(consumerGroup, topic, partition, offset)) throw new Exception("AArgghh Offset");

            var size = (int)Math.Min(amount, stream.Length - offset);

            Response.Headers.Add("Content-Length", size.ToString());
            
            stream.Seek(offset, SeekOrigin.Begin);
            await stream.MyCopyToAsync(Response.Body, size);
            
            MessagesReadSizeInBytes.WithLabels($"{topic}/{partition}").Inc(size);
        }

        [HttpPost]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> Store([FromQuery]string topic, int partition, int length, int recursiveCount = 0)
        {
            var filePath = $"{BasePath}/{topic}/{partition}.txt";
            var streamKey = $"{topic}/{partition}";

            var stream = FileStreamHandler.GetFileStream(streamKey, filePath);

            if (!stream.CanSeek || !stream.CanWrite) return StatusCode(500);

            var lengthInBytes = new byte[10];
            BitConverter.GetBytes(length).CopyTo(lengthInBytes, 0);

            await MessageLock.WaitAsync();
            var offset = -1L;
            try
            {
                stream.Seek(0, SeekOrigin.End);
                offset = stream.Position;

                await stream.WriteAsync(lengthInBytes);
                await Request.Body.CopyToAsync(stream);
                MessageLock.Release();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                if(offset != -1L)
                    stream.SetLength(offset);
                MessageLock.Release();
                return StatusCode(500);
            }

            MessagesWrittenSizeInBytes.WithLabels($"{topic}/{partition}").Inc(length);
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

        private async Task<bool> StoreOffset(string consumerGroup, string topic, int partition, long offset, int recursiveCount = 0)
        {
            var filePath = $"{BasePath}/offsets/{topic}/{partition}.txt";
            var streamKey = $"offset/{consumerGroup}/{topic}/{partition}";
            var stream = FileStreamHandler.GetFileStream(streamKey, filePath);

            if (!stream.CanWrite || !stream.CanRead) return false;

            await OffsetLock.WaitAsync();
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
                await stream.WriteAsync(LZ4MessagePackSerializer.Serialize(offset));
                OffsetLock.Release();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                OffsetLock.Release();
                
                if (recursiveCount > 2) return false;
                
                return await StoreOffset(consumerGroup, topic, partition, offset, ++recursiveCount);
            }
        }
    }
}
