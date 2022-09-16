namespace PatchDotNet{
    public struct FileInfo
    {
        public FileAttributes Attributes { get; set; }
        public DateTime? CreationTime { get; set; }
        public DateTime? LastAccessTime { get; set; }
        public DateTime? LastWriteTime { get; set; }
    }
}