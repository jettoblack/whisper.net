// Licensed under the MIT license: https://opensource.org/licenses/MIT

using System.Runtime.InteropServices;
using Whisper.net.Internals.Native;
using Whisper.net.LibraryLoader;
using Whisper.net.Native;

namespace Whisper.net.Internals.ModelLoader;

internal class WhisperProcessorModelBufferLoader(byte[] buffer) : IWhisperProcessorModelLoader
{
    private readonly GCHandle pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
    private GCHandle aheadsHandle;

    public void Dispose()
    {
        pinnedBuffer.Free();
        if (aheadsHandle.IsAllocated)
        {
            aheadsHandle.Free();
        }
    }

    public IntPtr LoadNativeContext(INativeWhisper nativeWhisper)
    {
        var bufferLength = new UIntPtr((uint)buffer.Length);

        var aHeads = WhisperProcessorModelFileLoader.GetWhisperAlignmentHeads(RuntimeOptions.Instance.CustomAlignmentHeads, ref aheadsHandle);

        return nativeWhisper.Whisper_Init_From_Buffer_With_Params_No_State(pinnedBuffer.AddrOfPinnedObject(), bufferLength,
            new WhisperContextParams()
            {
                UseGpu = RuntimeOptions.Instance.UseGpu ? (byte)1 : (byte)0,
                FlashAttention = RuntimeOptions.Instance.UseFlashAttention ? (byte)1 : (byte)0,
                GpuDevice = RuntimeOptions.Instance.GpuDevice,
                DtwTokenLevelTimestamp = RuntimeOptions.Instance.UseDtwTimeStamps ? (byte)1 : (byte)0,
                HeadsPreset = (WhisperAlignmentHeadsPreset)RuntimeOptions.Instance.HeadsPreset,
                DtwNTop = -1,
                WhisperAheads = aHeads,
                Dtw_mem_size = 1024 * 1024 * 128,
            });
    }
}
