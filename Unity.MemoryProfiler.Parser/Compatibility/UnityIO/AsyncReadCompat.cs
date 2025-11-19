using System;
using System.IO;

namespace Unity.IO.LowLevel.Unsafe
{
    public enum AssetLoadingSubsystem
    {
        FileInfo = 0,
        Other = 1,
    }

    public enum ReadStatus
    {
        InProgress,
        Complete,
        Failed,
    }

    public unsafe struct ReadCommand
    {
        public void* Buffer;
        public long Offset;
        public long Size;
    }

    public struct ReadHandle : IDisposable
    {
        internal ReadStatus _status;
        internal bool _isValid;
        internal Unity.Jobs.JobHandle _jobHandle;
        // 注意：官方 Unity 的 ReadHandle 可能包含错误信息，但为了保持 unmanaged 约束，
        // 我们移除 Exception 字段。错误状态通过 ReadStatus 传递。
        // internal Exception? _exception;

        public Unity.Jobs.JobHandle JobHandle => _jobHandle;

        public ReadStatus Status
        {
            get
            {
                return _status;
            }
        }

        public bool IsValid() => _isValid;

        public void Dispose()
        {
            _isValid = false;
        }

        internal void CompleteImmediately(ReadStatus status)
        {
            _status = status;
            _isValid = true;
            _jobHandle = default;
        }

        internal void SetFailed(Exception exception)
        {
            // Exception 信息已移除以保持 unmanaged 约束
            _status = ReadStatus.Failed;
            _isValid = true;
            _jobHandle = default;
        }
    }

    public static class AsyncReadManager
    {
        public unsafe static ReadHandle Read(string path, ReadCommand* commands, uint commandCount, AssetLoadingSubsystem subsystem = AssetLoadingSubsystem.FileInfo)
        {
            var handle = new ReadHandle { _status = ReadStatus.InProgress, _isValid = true, _jobHandle = default }; // starts valid
            try
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);

                for (uint i = 0; i < commandCount; i++)
                {
                    var cmd = commands[i];
                    if (cmd.Size == 0)
                        continue;

                    stream.Seek(cmd.Offset, SeekOrigin.Begin);
                    var destination = new Span<byte>(cmd.Buffer, checked((int)cmd.Size));

                    int totalRead = 0;
                    while (totalRead < destination.Length)
                    {
                        int read = stream.Read(destination.Slice(totalRead));
                        if (read == 0)
                            throw new EndOfStreamException($"Unexpected end of file while reading '{path}'.");
                        totalRead += read;
                    }
                }

                handle.CompleteImmediately(ReadStatus.Complete);
            }
            catch (Exception ex)
            {
                handle.SetFailed(ex);
            }

            return handle;
        }
    }
}

