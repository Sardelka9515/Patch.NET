using PatchDotNet;
using FileAccess=DokanNet.FileAccess;
using DokanNet.Logging;
using DokanNet;
using static DokanNet.FormatProviders;

namespace PatchDotNet.Win32
{
    public class SingleFileMount
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
            _path = '\\'+fileName;
        }
        #region TRACE
        protected NtStatus Trace(string method, string fileName, IDokanFileInfo info, NtStatus result,
            params object[] parameters)
        {
#if TRACE
            var extraParameters = parameters != null && parameters.Length > 0
                ? ", " + string.Join(", ", parameters.Select(x => string.Format(DefaultFormatProvider, "{0}", x)))
                : string.Empty;

            _logger.Debug(DokanFormat($"{method}('{fileName}', {info}{extraParameters}) -> {result}"));
#endif

            return result;
        }

        private NtStatus Trace(string method, string fileName, IDokanFileInfo info,
            FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes,
            NtStatus result)
        {
#if TRACE
            _logger.Debug(
                DokanFormat(
                    $"{method}('{fileName}', {info}, [{access}], [{share}], [{mode}], [{options}], [{attributes}]) -> {result}"));
#endif

            return result;
        }
#endregion

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

            var pathExists = fileName=="\\" || fileName==_path;
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

                        if (fileName=="\\"||readWriteAttributes)
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
                        if (fileName!=_path)
                            return Trace(nameof(CreateFile), fileName, info, access, share, mode, options, attributes,
                                DokanResult.FileNotFound);
                        break;
                }

                try
                {
                    info.Context = new FileStream(filePath, mode,
                        readAccess ? System.IO.FileAccess.Read : System.IO.FileAccess.ReadWrite, share, 4096, options);

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
                        File.SetAttributes(filePath, new_attributes);
                    }
                }
                catch (UnauthorizedAccessException) // don't have access rights
                {
                    if (info.Context is FileStream fileStream)
                    {
                        // returning AccessDenied cleanup and close won't be called,
                        // so we have to take care of the stream now
                        fileStream.Dispose();
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
    }
}