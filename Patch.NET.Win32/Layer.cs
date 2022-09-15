using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using DokanNet;
using DokanNet.Logging;
using static DokanNet.FormatProviders;
using FileAccess = DokanNet.FileAccess;

namespace PatchDotNet.Win32
{
    internal class Layer : IDokanOperations
    {
        public string VirtualPath = @"\base.vhdx";
        public string RealPath=@"C:\\base.vhdx";

        private readonly ILogger _logger;

        public Layer(ILogger logger, string realPath)
        {
            if (!Directory.Exists(Path.GetDirectoryName(realPath)))
                throw new ArgumentException(nameof(realPath));
            _logger = logger;
            this.RealPath = realPath;
        }

        



        protected static Int32 GetNumOfBytesToCopy(Int32 bufferLength, long offset, IDokanFileInfo info, FileStream stream)
        {
            if (info.PagingIo)
            {
                var longDistanceToEnd = stream.Length - offset;
                var isDistanceToEndMoreThanInt = longDistanceToEnd > Int32.MaxValue;
                if (isDistanceToEndMoreThanInt) return bufferLength;
                var distanceToEnd = (Int32)longDistanceToEnd;
                if (distanceToEnd < bufferLength) return distanceToEnd;
                return bufferLength;
            }
            return bufferLength;
        }

        #region Implementation of IDokanOperations


        public void Cleanup(string fileName, IDokanFileInfo info)
        {
#if TRACE
            if (info.Context != null)
                Console.WriteLine(DokanFormat($"{nameof(Cleanup)}('{fileName}', {info} - entering"));
#endif

            (info.Context as FileStream)?.Dispose();
            info.Context = null;

            if (info.DeleteOnClose)
            {
                Trace(nameof(Cleanup), fileName, info, DokanResult.NotImplemented);
                return;
            }
            Trace(nameof(Cleanup), fileName, info, DokanResult.Success);
        }

        public void CloseFile(string fileName, IDokanFileInfo info)
        {
#if TRACE
            if (info.Context != null)
                Console.WriteLine(DokanFormat($"{nameof(CloseFile)}('{fileName}', {info} - entering"));
#endif

            (info.Context as FileStream)?.Dispose();
            info.Context = null;
            Trace(nameof(CloseFile), fileName, info, DokanResult.Success);
            // could recreate cleanup code here but this is not called sometimes
        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
        {
            if(!IsValid(fileName))
            {
                bytesRead = 0;
                return Trace(nameof(Cleanup), fileName, info, DokanResult.NotImplemented);
            }
            if (info.Context == null) // memory mapped read
            {
                using (var stream = new FileStream(RealPath, FileMode.Open, System.IO.FileAccess.Read))
                {
                    stream.Position = offset;
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                }
            }
            else // normal read
            {
                var stream = info.Context as FileStream;
                lock (stream) //Protect from overlapped read
                {
                    stream.Position = offset;
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                }
            }
            return Trace(nameof(ReadFile), fileName, info, DokanResult.Success, "out " + bytesRead.ToString(),
                offset.ToString(CultureInfo.InvariantCulture));
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, IDokanFileInfo info)
        {
            if (!IsValid(fileName))
            {
                bytesWritten=0;
                return Trace(nameof(Cleanup), fileName, info, DokanResult.NotImplemented);
            }
            var append = offset == -1;
            if (info.Context == null)
            {
                using (var stream = new FileStream(RealPath, append ? FileMode.Append : FileMode.Open, System.IO.FileAccess.Write))
                {
                    if (!append) // Offset of -1 is an APPEND: https://docs.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-writefile
                    {
                        stream.Position = offset;
                    }
                    var bytesToCopy = GetNumOfBytesToCopy(buffer.Length, offset, info, stream);
                    stream.Write(buffer, 0, bytesToCopy);
                    bytesWritten = bytesToCopy;
                }
            }
            else
            {
                var stream = info.Context as FileStream;
                lock (stream) //Protect from overlapped write
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
                                offset.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                    else
                    {
                        stream.Position = offset;
                    }
                    var bytesToCopy = GetNumOfBytesToCopy(buffer.Length, offset, info, stream);
                    stream.Write(buffer, 0, bytesToCopy);
                    bytesWritten = bytesToCopy;
                }
            }
            return Trace(nameof(WriteFile), fileName, info, DokanResult.Success, "out " + bytesWritten.ToString(),
                offset.ToString(CultureInfo.InvariantCulture));
        }

        public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
        {
            try
            {
                ((FileStream)(info.Context)).Flush();
                return Trace(nameof(FlushFileBuffers), fileName, info, DokanResult.Success);
            }
            catch (IOException)
            {
                return Trace(nameof(FlushFileBuffers), fileName, info, DokanResult.DiskFull);
            }
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
        {
            // may be called with info.Context == null, but usually it isn't
            var filePath = GetPath(fileName);
            FileSystemInfo finfo = new FileInfo(filePath);
            if (!finfo.Exists)
                finfo = new DirectoryInfo(filePath);

            fileInfo = new FileInformation
            {
                FileName = fileName,
                Attributes = finfo.Attributes,
                CreationTime = finfo.CreationTime,
                LastAccessTime = finfo.LastAccessTime,
                LastWriteTime = finfo.LastWriteTime,
                Length = (finfo as FileInfo)?.Length ?? 0,
            };
            return Trace(nameof(GetFileInformation), fileName, info, DokanResult.Success);
        }

        private string GetPath(string fileName)
        {
            if(fileName == VirtualPath) { return RealPath; }
            return Path.GetDirectoryName(RealPath);
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            if (fileName != @"\")
            {
                files=new List<FileInformation>();
                return Trace(nameof(Cleanup), fileName, info, DokanResult.NotImplemented);
            }
            // This function is not called because FindFilesWithPattern is implemented
            files = FindFilesHelper();

            return Trace(nameof(FindFiles), fileName, info, DokanResult.Success);
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
        {
            if (!IsValid(fileName))
            {
                return Trace(nameof(Cleanup), fileName, info, DokanResult.NotImplemented);
            }
            try
            {
                // MS-FSCC 2.6 File Attributes : There is no file attribute with the value 0x00000000
                // because a value of 0x00000000 in the FileAttributes field means that the file attributes for this file MUST NOT be changed when setting basic information for the file
                if (attributes != 0)
                    File.SetAttributes(RealPath, attributes);
                return Trace(nameof(SetFileAttributes), fileName, info, DokanResult.Success, attributes.ToString());
            }
            catch (UnauthorizedAccessException)
            {
                return Trace(nameof(SetFileAttributes), fileName, info, DokanResult.AccessDenied, attributes.ToString());
            }
            catch (FileNotFoundException)
            {
                return Trace(nameof(SetFileAttributes), fileName, info, DokanResult.FileNotFound, attributes.ToString());
            }
            catch (DirectoryNotFoundException)
            {
                return Trace(nameof(SetFileAttributes), fileName, info, DokanResult.PathNotFound, attributes.ToString());
            }
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime,
            DateTime? lastWriteTime, IDokanFileInfo info)
        {
            if (!IsValid(fileName))
            {
                return Trace(nameof(Cleanup), fileName, info, DokanResult.NotImplemented);
            }
            try
            {
                var filePath = RealPath;

                if (creationTime.HasValue)
                    File.SetCreationTime(filePath, creationTime.Value);

                if (lastAccessTime.HasValue)
                    File.SetLastAccessTime(filePath, lastAccessTime.Value);

                if (lastWriteTime.HasValue)
                    File.SetLastWriteTime(filePath, lastWriteTime.Value);

                return Trace(nameof(SetFileTime), fileName, info, DokanResult.Success, creationTime, lastAccessTime,
                    lastWriteTime);
            }
            catch (UnauthorizedAccessException)
            {
                return Trace(nameof(SetFileTime), fileName, info, DokanResult.AccessDenied, creationTime, lastAccessTime,
                    lastWriteTime);
            }
            catch (FileNotFoundException)
            {
                return Trace(nameof(SetFileTime), fileName, info, DokanResult.FileNotFound, creationTime, lastAccessTime,
                    lastWriteTime);
            }
        }

        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {
            return Trace(nameof(Cleanup), fileName, info, DokanResult.NotImplemented);

        }

        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            return Trace(nameof(Cleanup), fileName, info, DokanResult.NotImplemented);
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            return Trace(nameof(Cleanup), oldName, info, DokanResult.NotImplemented);

        }

        public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
        {
            
            try
            {
                ((FileStream)(info.Context)).SetLength(length);
                return Trace(nameof(SetEndOfFile), fileName, info, DokanResult.Success,
                    length.ToString(CultureInfo.InvariantCulture));
            }
            catch (IOException)
            {
                return Trace(nameof(SetEndOfFile), fileName, info, DokanResult.DiskFull,
                    length.ToString(CultureInfo.InvariantCulture));
            }
        }

        public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
        {
            
            try
            {
                ((FileStream)(info.Context)).SetLength(length);
                return Trace(nameof(SetAllocationSize), fileName, info, DokanResult.Success,
                    length.ToString(CultureInfo.InvariantCulture));
            }
            catch (IOException)
            {
                return Trace(nameof(SetAllocationSize), fileName, info, DokanResult.DiskFull,
                    length.ToString(CultureInfo.InvariantCulture));
            }
        }

        public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            
#if !NETCOREAPP1_0
            try
            {
                ((FileStream)(info.Context)).Lock(offset, length);
                return Trace(nameof(LockFile), fileName, info, DokanResult.Success,
                    offset.ToString(CultureInfo.InvariantCulture), length.ToString(CultureInfo.InvariantCulture));
            }
            catch (IOException)
            {
                return Trace(nameof(LockFile), fileName, info, DokanResult.AccessDenied,
                    offset.ToString(CultureInfo.InvariantCulture), length.ToString(CultureInfo.InvariantCulture));
            }
#else
// .NET Core 1.0 do not have support for FileStream.Lock
            return DokanResult.NotImplemented;
#endif
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
#if !NETCOREAPP1_0
            try
            {
                ((FileStream)(info.Context)).Unlock(offset, length);
                return Trace(nameof(UnlockFile), fileName, info, DokanResult.Success,
                    offset.ToString(CultureInfo.InvariantCulture), length.ToString(CultureInfo.InvariantCulture));
            }
            catch (IOException)
            {
                return Trace(nameof(UnlockFile), fileName, info, DokanResult.AccessDenied,
                    offset.ToString(CultureInfo.InvariantCulture), length.ToString(CultureInfo.InvariantCulture));
            }
#else
// .NET Core 1.0 do not have support for FileStream.Unlock
            return DokanResult.NotImplemented;
#endif
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
            volumeLabel = "SAUSAGEIUM";
            fileSystemName = "NTFS";
            maximumComponentLength = 256;

            features = FileSystemFeatures.CasePreservedNames | FileSystemFeatures.CaseSensitiveSearch |
                       FileSystemFeatures.PersistentAcls | FileSystemFeatures.SupportsRemoteStorage |
                       FileSystemFeatures.UnicodeOnDisk;

            return Trace(nameof(GetVolumeInformation), null, info, DokanResult.Success, "out " + volumeLabel,
                "out " + features.ToString(), "out " + fileSystemName);
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections,
            IDokanFileInfo info)
        {
            if (!IsValid(fileName))
            {
                security=null;
                return Trace(nameof(Cleanup), fileName, info, DokanResult.NotImplemented);
            }
            try
            {
#if NET5_0_OR_GREATER
                security = info.IsDirectory
                    ? (FileSystemSecurity)new DirectoryInfo(GetPath(fileName)).GetAccessControl()
                    : new FileInfo(RealPath).GetAccessControl();
#else
                security = info.IsDirectory
                    ? (FileSystemSecurity)Directory.GetAccessControl(GetPath(fileName))
                    : File.GetAccessControl(GetPath(fileName));
#endif
                return Trace(nameof(GetFileSecurity), fileName, info, DokanResult.Success, sections.ToString());
            }
            catch (UnauthorizedAccessException)
            {
                security = null;
                return Trace(nameof(GetFileSecurity), fileName, info, DokanResult.AccessDenied, sections.ToString());
            }
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections,
            IDokanFileInfo info)
        {
            if (!IsValid(fileName))
            {
                return Trace(nameof(Cleanup), fileName, info, DokanResult.NotImplemented);
            }
            try
            {
#if NET5_0_OR_GREATER
                if (info.IsDirectory)
                {
                    return Trace(nameof(SetFileSecurity), fileName, info, DokanResult.NotImplemented, sections.ToString());

                }
                else
                {
                    new FileInfo(RealPath).SetAccessControl((FileSecurity)security);
                }
#else
                if (info.IsDirectory)
                {
                    Directory.SetAccessControl(GetPath(fileName), (DirectorySecurity)security);
                }
                else
                {
                    File.SetAccessControl(GetPath(fileName), (FileSecurity)security);
                }
#endif
                return Trace(nameof(SetFileSecurity), fileName, info, DokanResult.Success, sections.ToString());
            }
            catch (UnauthorizedAccessException)
            {
                return Trace(nameof(SetFileSecurity), fileName, info, DokanResult.AccessDenied, sections.ToString());
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

        public NtStatus FindStreams(string fileName, IntPtr enumContext, out string streamName, out long streamSize,
            IDokanFileInfo info)
        {
            streamName = string.Empty;
            streamSize = 0;
            return Trace(nameof(FindStreams), fileName, info, DokanResult.NotImplemented, enumContext.ToString(),
                "out " + streamName, "out " + streamSize.ToString());
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
        {
            streams = new FileInformation[0];
            return Trace(nameof(FindStreams), fileName, info, DokanResult.NotImplemented);
        }

        public IList<FileInformation> FindFilesHelper()
        {
            
            FileInfo finfo = new FileInfo(RealPath);
            return new List<FileInformation>() {
                new FileInformation{
                    Attributes = finfo.Attributes,
                    CreationTime = finfo.CreationTime,
                    LastAccessTime = finfo.LastAccessTime,
                    LastWriteTime = finfo.LastWriteTime,
                    Length = (finfo as FileInfo)?.Length ?? 0,
                    FileName = Path.GetFileName(VirtualPath),
                } };
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files,
            IDokanFileInfo info)
        {
            files = FindFilesHelper();

            return Trace(nameof(FindFilesWithPattern), fileName, info, DokanResult.Success);
        }

        #endregion Implementation of IDokanOperations
        private bool IsValid(string filename)
        {
            if((filename == "\\") || (filename == VirtualPath))
            {
                return true;
            }
            return false;
        }
    }
}
