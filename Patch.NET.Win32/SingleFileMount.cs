using PatchDotNet;
using FileAccess = DokanNet.FileAccess;
using DokanNet.Logging;
using DokanNet;
using System.Runtime.InteropServices;
using static DokanNet.FormatProviders;
using System.Security.AccessControl;

namespace PatchDotNet.Win32
{
    public class SingleFileMount : IDokanOperations
    {
        private const FileAccess DataAccess = FileAccess.ReadData | FileAccess.WriteData | FileAccess.AppendData |
                                              FileAccess.Execute |
                                              FileAccess.GenericExecute | FileAccess.GenericWrite |
                                              FileAccess.GenericRead;
        private const FileAccess DataWriteAccess = FileAccess.WriteData | FileAccess.AppendData |
                                                   FileAccess.Delete |
                                                   FileAccess.GenericWrite;

        private readonly ILogger _logger;
        private readonly FileProvider _provider;
        private readonly string _path;
        public SingleFileMount(FileProvider provider, string fileName, ILogger logger = null)
        {
            _logger = logger;
            _provider = provider;
            _path = '\\' + fileName;
        }
        #region TRACE
        protected NtStatus Trace(string method, string fileName, IDokanFileInfo info, NtStatus result,
            params object[] parameters)
        {
#if TRACE
            if (result != DokanResult.Success) { Console.ForegroundColor = ConsoleColor.Red; }
            else { Console.ForegroundColor = ConsoleColor.Gray; }
            var extraParameters = parameters != null && parameters.Length > 0
                ? ", " + string.Join(", ", parameters.Select(x => string.Format(DefaultFormatProvider, "{0}", x)))
                : string.Empty;

            _logger?.Debug(DokanFormat($"{method}('{fileName}', {info}{extraParameters}) -> {result}"));
#endif

            return result;
        }

        #region NOT-IMPLEMENTED
        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files,
            IDokanFileInfo info)
        {
            files = null;
            return DokanResult.NotImplemented;

        }

        public NtStatus FindStreams(string fileName, IntPtr enumContext, out string streamName, out long streamSize,
            IDokanFileInfo info)
        {
            streamName = string.Empty;
            streamSize = 0;
            return DokanResult.NotImplemented;
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
        {
            streams = new FileInformation[0];
            return DokanResult.NotImplemented;
        }


        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime,
            DateTime? lastWriteTime, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;

        }

        public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {

            return DokanResult.NotImplemented;
        }
        public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections,
            IDokanFileInfo info)
        {
            security = null;
            return DokanResult.NotImplemented;

        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections,
            IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;

        }

        #endregion


        private NtStatus Trace(string method, string fileName, IDokanFileInfo info,
            FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes,
            NtStatus result)
        {
#if TRACE
            if (result != DokanResult.Success) { Console.ForegroundColor = ConsoleColor.Red; }
            else { Console.ForegroundColor = ConsoleColor.Gray; }
            _logger?.Debug(
                DokanFormat(
                    $"{method}('{fileName}', {info}, [{access}], [{share}], [{mode}], [{options}], [{attributes}]) -> {result}"));

#endif

            return result;
        }
        #endregion
        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {

            return Trace(nameof(DeleteFile), fileName, info, DokanResult.AccessDenied);

        }
        public NtStatus CreateFile(string fileName, FileAccess access, FileShare share, FileMode mode,
            FileOptions options, FileAttributes attributes, IDokanFileInfo info)
        {
            if (access == FileAccess.Delete)
            {
                return Trace(nameof(CreateFile), fileName, info, access, share, mode, options,
           attributes, DokanResult.AccessDenied);
            }
            var result = DokanResult.Success;

            var readWriteAttributes = (access & DataAccess) == 0;
            var readAccess = (access & DataWriteAccess) == 0;

            var pathExists = fileName == "\\" || fileName == _path;
            if (info.IsDirectory)
            {
                switch (mode)
                {
                    case FileMode.Open:
                        if (fileName != "\\")
                        {
                            if (fileName == _path)
                                return Trace(nameof(CreateFile), fileName, info, access, share, mode, options,
                                    attributes, DokanResult.NotADirectory);
                            return Trace(nameof(CreateFile), fileName, info, access, share, mode, options,
                                attributes, DokanResult.PathNotFound);

                        }
                        if (!_provider.CanWrite && access.HasFlag(FileAccess.WriteData))
                        {
                            return Trace(nameof(CreateFile), fileName, info, access, share, mode, options,
                                attributes, DokanResult.AccessDenied);
                        }
                        break;

                    case FileMode.CreateNew:
                        return Trace(nameof(CreateFile), fileName, info, access, share, mode, options,
                                    attributes, DokanResult.AccessDenied);

                }
            }
            else
            {

                switch (mode)
                {
                    case FileMode.Open:

                        if (pathExists)
                        {
                            if (fileName == "\\" || readWriteAttributes)
                            {
                                if ((access & FileAccess.Delete) == FileAccess.Delete
                                        && (access & FileAccess.Synchronize) != FileAccess.Synchronize)
                                    //It is a DeleteFile request on a directory
                                    return Trace(nameof(CreateFile), fileName, info, access, share, mode, options,
                                        attributes, DokanResult.AccessDenied);

                                // check if driver only wants to read attributes, security info, or open directory
                                if (readWriteAttributes)
                                {


                                    info.IsDirectory = fileName == "\\";
                                    info.Context = new object();
                                    // must set it to something if you return DokanError.Success

                                    return Trace(nameof(CreateFile), fileName, info, access, share, mode, options,
                                        attributes, DokanResult.Success);
                                }
                            }
                        }
                        else
                        {
                            return Trace(nameof(CreateFile), fileName, info, access, share, mode, options, attributes,
                                DokanResult.FileNotFound);
                        }
                        break;

                    case FileMode.CreateNew:
                        return Trace(nameof(CreateFile), fileName, info, access, share, mode, options,
                                        attributes, DokanResult.AccessDenied);

                    case FileMode.Truncate:
                        if (fileName != _path)
                            return Trace(nameof(CreateFile), fileName, info, access, share, mode, options, attributes,
                                DokanResult.FileNotFound);
                        break;
                }

                try
                {
                    info.Context = _provider.GetStream(access.HasFlag(FileAccess.WriteData)||access.HasFlag(FileAccess.WriteAttributes)||access.HasFlag(FileAccess.GenericWrite) ? System.IO.FileAccess.ReadWrite : System.IO.FileAccess.Read);
                    if (pathExists && (mode == FileMode.OpenOrCreate
                                       || mode == FileMode.Create))
                        result = DokanResult.AlreadyExists;

                    bool fileCreated = mode == FileMode.CreateNew || mode == FileMode.Create || (!pathExists && mode == FileMode.OpenOrCreate);
                    if (fileCreated)
                    {
                        FileAttributes new_attributes = attributes;
                        new_attributes |= FileAttributes.Archive; // Files are always created as Archive
                        // FILE_ATTRIBUTE_NORMAL is override if any other attribute is set.
                        new_attributes &= ~FileAttributes.Normal;
                    }
                }
                catch (UnauthorizedAccessException) // don't have access rights
                {
                    if (info.Context is RoWStream stream)
                    {
                        // returning AccessDenied cleanup and close won't be called,
                        // so we have to take care of the stream now
                        stream.Dispose();
                        info.Context = null;
                    }
                    return Trace(nameof(CreateFile), fileName, info, access, share, mode, options, attributes,
                        DokanResult.AccessDenied);
                }
                catch (DirectoryNotFoundException)
                {
                    return Trace(nameof(CreateFile), fileName, info, access, share, mode, options, attributes,
                        DokanResult.PathNotFound);
                }
                catch (Exception ex)
                {
                    var hr = (uint)Marshal.GetHRForException(ex);
                    switch (hr)
                    {
                        case 0x80070020: //Sharing violation
                            return Trace(nameof(CreateFile), fileName, info, access, share, mode, options, attributes,
                                DokanResult.SharingViolation);
                        default:
                            throw;
                    }
                }
            }
            return Trace(nameof(CreateFile), fileName, info, access, share, mode, options, attributes,
                result);
        }


        public void CloseFile(string fileName, IDokanFileInfo info)
        {
            lock (_provider)
            {

#if TRACE
                if (info.Context != null)
                    Console.WriteLine(DokanFormat($"{nameof(CloseFile)}('{fileName}', {info} - entering"));
#endif

                (info.Context as RoWStream)?.Dispose();
                info.Context = null;
                Trace(nameof(CloseFile), fileName, info, DokanResult.Success);
                // could recreate cleanup code here but this is not called sometimes
            }
        }


        public void Cleanup(string fileName, IDokanFileInfo info)
        {
            lock (_provider)
            {

#if TRACE
                if (info.Context != null)
                    Console.WriteLine(DokanFormat($"{nameof(Cleanup)}('{fileName}', {info} - entering"));
#endif

                (info.Context as RoWStream)?.Dispose();
                info.Context = null;

                if (info.DeleteOnClose)
                {
                    Trace(nameof(Cleanup), fileName, info, DokanResult.NotImplemented);
                    return;
                }
                Trace(nameof(Cleanup), fileName, info, DokanResult.Success);
            }
        }
        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
        {

            // Console.ForegroundColor = ConsoleColor.Green;
            // Console.WriteLine($"[Dokan] Reading {fileName}, {offset}, {buffer.Length}");
            if (fileName != _path)
            {
                bytesRead = 0;
                return Trace(nameof(ReadFile), fileName, info, DokanResult.AccessDenied);
            }
            if (info.Context == null) // memory mapped read
            {
                using (var stream = _provider.GetStream(System.IO.FileAccess.Read))
                {
                    lock (stream)
                    {
                        stream.Position = offset;
                        bytesRead = stream.Read(buffer, 0, buffer.Length);
                    }
                }
            }
            else // normal read
            {
                var stream = info.Context as RoWStream;
                lock (stream) //Protect from overlapped read
                {
                    stream.Position = offset;
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                }
            }
            return Trace(nameof(ReadFile), fileName, info, DokanResult.Success, "out " + bytesRead.ToString(),
                offset.ToString());
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, IDokanFileInfo info)
        {
            if (!_provider.CanWrite)
            {
                bytesWritten = 0;
                return Trace(nameof(WriteFile), fileName, info, DokanResult.AccessDenied, "out " + bytesWritten.ToString(),
                offset.ToString());
            }
            // Console.ForegroundColor = ConsoleColor.Yellow;
            // Console.WriteLine($"[Dokan] Writing {fileName}, {offset}, {buffer.Length}");
            if (fileName != _path)
            {
                bytesWritten = 0;
                return Trace(nameof(Cleanup), fileName, info, DokanResult.AccessDenied);
            }
            var append = offset == -1;
            if (info.Context == null)
            {
                using (var stream = info.Context as RoWStream ?? _provider.GetStream(System.IO.FileAccess.Write))
                {
                    lock (stream)
                    {

                        if (!append) // Offset of -1 is an APPEND: https://docs.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-writefile
                        {
                            stream.Position = offset;
                        }
                        else
                        {
                            stream.Position = stream.Length;
                        }
                        stream.Write(buffer, 0, buffer.Length);
                        bytesWritten = buffer.Length;
                    }
                }
            }
            else
            {
                var stream = info.Context as RoWStream;
                lock (stream)
                {

                    if (append)
                    {
                        if (stream.CanSeek)
                        {
                            stream.Seek(0, SeekOrigin.End);
                        }
                        else
                        {
                            bytesWritten = 0;
                            return Trace(nameof(WriteFile), fileName, info, DokanResult.Error, "out " + bytesWritten,
                                offset.ToString());
                        }
                    }
                    else
                    {
                        stream.Position = offset;
                    }
                    stream.Write(buffer, 0, buffer.Length);
                }
                bytesWritten = buffer.Length;
            }
            return Trace(nameof(WriteFile), fileName, info, DokanResult.Success, "out " + bytesWritten.ToString(),
                offset.ToString());
        }

        public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
        {
            try
            {
                ((RoWStream)(info.Context)).Flush();
                return Trace(nameof(FlushFileBuffers), fileName, info, DokanResult.Success);
            }
            catch (IOException)
            {
                return Trace(nameof(FlushFileBuffers), fileName, info, DokanResult.DiskFull);
            }
        }
        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
        {
            fileInfo = new FileInformation
            {
                FileName = fileName,
                Attributes = _provider.Attributes,
                CreationTime = _provider.CreationTime,
                LastAccessTime = _provider.LastAccessTime,
                LastWriteTime = _provider.LastWriteTime,
                Length = _provider.Length,
            };
            return Trace(nameof(GetFileInformation), fileName, info, DokanResult.Success);
        }
        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
        {
            if (_path == fileName && _provider.CanWrite)
            {
                _provider.Attributes = attributes;
                return DokanResult.Success;
            }
            return DokanResult.AccessDenied;

        }
        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            return DokanResult.AccessDenied;
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            return Trace(nameof(Cleanup), oldName, info, DokanResult.AccessDenied);
        }

        public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
        {

            try
            {
                ((RoWStream)(info.Context)).SetLength(length);
                return Trace(nameof(SetAllocationSize), fileName, info, DokanResult.Success,
                    length.ToString());
            }
            catch (IOException)
            {
                return Trace(nameof(SetAllocationSize), fileName, info, DokanResult.DiskFull,
                    length.ToString());
            }
        }

        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, IDokanFileInfo info)
        {
            //1EB
            freeBytesAvailable = 1152921504606846976L;
            totalNumberOfBytes = 1152921504606846976L;
            totalNumberOfFreeBytes = 1152921504606846976L;
            return Trace(nameof(GetDiskFreeSpace), null, info, DokanResult.Success, "out " + freeBytesAvailable.ToString(),
                "out " + totalNumberOfBytes.ToString(), "out " + totalNumberOfFreeBytes.ToString());
        }
        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features,
            out string fileSystemName, out uint maximumComponentLength, IDokanFileInfo info)
        {
            volumeLabel = "RoW Mount";
            fileSystemName = "NTFS";
            maximumComponentLength = 256;

            features = FileSystemFeatures.CasePreservedNames | FileSystemFeatures.CaseSensitiveSearch |
                       FileSystemFeatures.PersistentAcls | FileSystemFeatures.SupportsRemoteStorage |
                       FileSystemFeatures.UnicodeOnDisk;

            return Trace(nameof(GetVolumeInformation), null, info, DokanResult.Success, "out " + volumeLabel,
                "out " + features.ToString(), "out " + fileSystemName);
        }
        public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
        {

            try
            {
                ((RoWStream)(info.Context)).SetLength(length);
                return Trace(nameof(SetEndOfFile), fileName, info, DokanResult.Success,
                    length.ToString());
            }
            catch (IOException)
            {
                return Trace(nameof(SetEndOfFile), fileName, info, DokanResult.DiskFull,
                    length.ToString());
            }
        }
        public NtStatus Mounted(string mountPoint, IDokanFileInfo info)
        {
            return Trace(nameof(Mounted), null, info, DokanResult.Success);
        }

        public NtStatus Unmounted(IDokanFileInfo info)
        {
            return Trace(nameof(Unmounted), null, info, DokanResult.Success);
        }
        public NtStatus FindFiles(string fileName,
            out IList<FileInformation> files,
            IDokanFileInfo info)
        {
            files = null;
            if (fileName != "\\")
            {
                return Trace(nameof(FindFiles), fileName, info, DokanResult.PathNotFound);
            }
            files = new List<FileInformation>(){
                new FileInformation{
                    Attributes = _provider.Attributes,
                    CreationTime = _provider.CreationTime,
                    LastAccessTime = _provider.LastAccessTime,
                    LastWriteTime = _provider.LastWriteTime,
                    FileName = Path.GetFileName(_path),
                    Length=_provider.Length
                }
            };
            return Trace(nameof(FindFiles), fileName, info, DokanResult.Success);
        }
    }

}