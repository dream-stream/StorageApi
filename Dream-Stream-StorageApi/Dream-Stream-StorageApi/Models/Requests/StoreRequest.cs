namespace Dream_Stream_StorageApi.Models.Requests
{
    public class StoreRequest
    {
        public string Topic { get; set; }
        
        public int Partition { get; set; }
        
        public byte[] Message { get; set; }
    }
}
