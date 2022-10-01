# Patch.NET
Patch.NET is a Redirect-on-Write implementation in c#

## Redirect-on-Write (RoW)
Inferred by it's name, when data is written to a RoW stream, it's not actually written to the base stream, but instead redirected to a patch stream/file.
Each patch can be individually transferred and attached, allowing version control and efficient data sync.

## FileProvider & Patch chain
To start using this library, you need to initialize a FileProvider, specifying the base stream and patches.
One FileProvider can have multiple patches, it'll try to verify the patch chain with unique GUID and file length during initialisation. 
If writing is enabled, new data will be written to the last patch, while reading from base stream and all patches.
You can change the redirect target by calling FileProvider.ChangeCurrent(), all subsequent write operation well be redirected to the new patch, useful for creating snapshot without re-initialization.

## RoWStream
FileProvider does not provide any public method for reading, writing data. 
You must call FileProvider.GetStream() to get a RoWStream instance that can be used to access the data, the instance is thread-safe and should be disposed when no longer needed.

## Patch defragmentation/merge
Write records will accumulate over time and could have a significant impact on performance and startup time. You can defragment a patch or merge multiple patches to optimize it.

## FileStore
This class provides an directory-based abstraction for simplified patch hierarchy management.

## BlockMap
You can save the mapping to drastically improve startup performance
