# Patch.NET
Patch.NET is a Redirect-on-Write implementation in C#

## Redirect-on-Write (RoW)
As the name suggests, when data is written to a RoW stream, it's not actually written to the base stream, but instead redirected to a patch stream/file.
Each patch can be individually transferred and attached, allowing version control and efficient data sync.

## FileProvider & Patch chain
To start using this library, you need to initialize a FileProvider, specifying the base stream and patches.
One FileProvider can have multiple patches, it'll try to verify the patch chain with unique GUID and file length during initialisation. 
If writing is enabled, new data will be written to the last patch, while reading from base stream and all patches.
You can change the redirect target by calling ``FileProvider.ChangeCurrent()``, all subsequent write operation well be redirected to the new patch, useful for creating snapshot without re-initialization.

## RoWStream
FileProvider does not provide any public method for reading, writing data. 
You must call ``FileProvider.GetStream()`` to get a ``RoWStream`` instance that can be used to access the data, the instance is thread-safe and should be disposed when no longer needed.

## Patch defragmentation/merge
Write records will accumulate over time and could have a significant impact on performance and startup time. You can defragment a patch or merge multiple patches to optimize it.

## BlockMap
``FileProvider`` needs to read through all records in each patch and map them to the memory during initialization, which could have significant impact on startup time. The ``BlockMap`` class allows you to store the in-memory mapping to a output stream when the provider is about to be disposed, then read it from input stream next time to initialize a provider without needing to read through all records, which would dramatically improve the startup performancce.

## FileStore
This class provides an directory-based abstraction for simplified patch hierarchy management.
