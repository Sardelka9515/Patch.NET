namespace PatchDotNet
{
    public class FileInfo
    {
        Patch p;
        public FileInfo(Patch pa)
        {
            p = pa;
        }
        const long offset = 1024;
        public FileAttributes Attributes
        {
            get => (FileAttributes)BitConverter.ToInt32(p.GetHeader(offset, 4)) | (p.CanWrite ? 0 : FileAttributes.ReadOnly);
            set => p.SetHeader(offset, BitConverter.GetBytes((int)value));
        }
        public DateTime CreationTime
        {
            get => p.GetHeader(offset + 4, 8).GetDateTime();
            set => p.SetHeader(offset + 4, value.ToBytes());
        }
        public DateTime LastAccessTime
        {
            get => p.GetHeader(offset + 12, 8).GetDateTime();
            set => p.SetHeader(offset + 12, value.ToBytes());
        }
        public DateTime LastWriteTime
        {
            get => p.GetHeader(offset + 20, 8).GetDateTime();
            set => p.SetHeader(offset + 20, value.ToBytes());
        }
    }
}