// Licensed under the MIT license: https://opensource.org/licenses/MIT

using System.Runtime.InteropServices;
using Whisper.net.Internals.ModelLoader;
using Whisper.net.LibraryLoader;
using Whisper.net.Logger;

namespace Whisper.net;

/// <summary>
/// A factory for creating <seealso cref="WhisperProcessorBuilder"/> used to initialize the process.
/// </summary>
/// <remarks>
/// The factory is loading the model and it is reusing it across all the processors.
/// </remarks>
public sealed class WhisperFactory : IDisposable
{
    private readonly IWhisperProcessorModelLoader loader;
    private readonly Lazy<IntPtr> contextLazy;
    private bool wasDisposed;

    private static readonly Lazy<LoadResult> libraryLoaded = new(() =>
    {
        var libraryLoaded = NativeLibraryLoader.LoadNativeLibrary();
        if (libraryLoaded.IsSuccess)
        {
            LogProvider.InitializeLogging(libraryLoaded.NativeWhisper!);
        }
        return libraryLoaded;
    }, true);

    private WhisperFactory(IWhisperProcessorModelLoader loader, bool delayInit)
    {
        if (!libraryLoaded.Value.IsSuccess)
        {
            throw new Exception($"Failed to load native whisper library. Error: {libraryLoaded.Value.ErrorMessage}");
        }

        this.loader = loader;
        if (!delayInit)
        {
            var nativeContext = loader.LoadNativeContext(libraryLoaded.Value.NativeWhisper!);
            contextLazy = new Lazy<IntPtr>(() => nativeContext);
        }
        else
        {
            contextLazy = new Lazy<IntPtr>(() => loader.LoadNativeContext(libraryLoaded.Value.NativeWhisper!), isThreadSafe: false);
        }
    }

    /// <summary>
    /// Returns the information about the loaded native runtime.
    /// </summary>
    /// <remarks>
    /// This information includes support of the features like AVX, AVX2, AVX512, CUDA, etc.
    /// </remarks>
    /// <exception cref="Exception"></exception>
    public static string? GetRuntimeInfo()
    {
        if (!libraryLoaded.Value.IsSuccess)
        {
            throw new Exception($"Failed to load native whisper library. Error: {libraryLoaded.Value.ErrorMessage}");
        }

        var systemInfoPtr = libraryLoaded.Value.NativeWhisper!.WhisperPrintSystemInfo();
        var systemInfoStr = Marshal.PtrToStringAnsi(systemInfoPtr);
        Marshal.FreeHGlobal(systemInfoPtr);
        return systemInfoStr;
    }

    /// <summary>
    /// Creates a factory that uses the ggml model from a path in order to create <seealso cref="WhisperProcessorBuilder"/>.
    /// </summary>
    /// <param name="path">The path to the model.</param>
    /// <param name="delayInitialization">A value indicating if the model should be loaded right away or during the first <see cref="CreateBuilder"/> call.</param>
    /// <returns>An instance to the same builder.</returns>
    /// <remarks>
    /// If you don't know where to find a ggml model, you can use <seealso cref="Ggml.WhisperGgmlDownloader"/> which is downloading a model from huggingface.co.
    /// </remarks>
    public static WhisperFactory FromPath(string path, bool delayInitialization = false)
    {
        return new WhisperFactory(new WhisperProcessorModelFileLoader(path), delayInitialization);
    }

    /// <summary>
    /// Creates a factory that uses the ggml model from a buffer in order to create <seealso cref="WhisperProcessorBuilder"/>.
    /// </summary>
    /// <param name="buffer">The buffer with the model.</param>
    /// <param name="delayInitialization">A value indicating if the model should be loaded right away or during the first <see cref="CreateBuilder"/> call.</param>
    /// <returns>An instance to the same builder.</returns>
    /// <remarks>
    /// If you don't know where to find a ggml model, you can use <seealso cref="Ggml.WhisperGgmlDownloader"/> which is downloading a model from huggingface.co.
    /// </remarks>
    public static WhisperFactory FromBuffer(byte[] buffer, bool delayInitialization = false)
    {
        return new WhisperFactory(new WhisperProcessorModelBufferLoader(buffer), delayInitialization);
    }

    /// <summary>
    /// Creates a builder that can be used to initialize the whisper processor.
    /// </summary>
    /// <returns>An instance to a new builder.</returns>
    /// <exception cref="ObjectDisposedException">Throws if the factory was already disposed.</exception>
    /// <exception cref="WhisperModelLoadException">Throws if the model couldn't be loaded.</exception>
    public WhisperProcessorBuilder CreateBuilder()
    {
        if (wasDisposed)
        {
            throw new ObjectDisposedException(nameof(WhisperFactory));
        }

        var context = contextLazy.Value;
        if (context == IntPtr.Zero)
        {
            throw new WhisperModelLoadException("Failed to load the whisper model.");
        }

        return new WhisperProcessorBuilder(contextLazy.Value, libraryLoaded.Value.NativeWhisper!);
    }

    public void Dispose()
    {
        if (wasDisposed)
        {
            return;
        }
        if (contextLazy.IsValueCreated && contextLazy.Value != IntPtr.Zero)
        {
            libraryLoaded.Value.NativeWhisper!.Whisper_Free(contextLazy.Value);
        }
        loader.Dispose();
        wasDisposed = true;
    }
}
