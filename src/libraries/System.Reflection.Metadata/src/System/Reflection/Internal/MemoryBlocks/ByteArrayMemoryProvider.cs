// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace System.Reflection.Internal
{
    internal sealed class ByteArrayMemoryProvider : MemoryBlockProvider
    {
        private readonly ImmutableArray<byte> _array;
        private PinnedObject? _pinned;

        public ByteArrayMemoryProvider(ImmutableArray<byte> array)
        {
            Debug.Assert(!array.IsDefault);
            _array = array;
        }

        protected override void Dispose(bool disposing)
        {
            Debug.Assert(disposing);
            Interlocked.Exchange(ref _pinned, null)?.Dispose();
        }

        public override int Size => _array.Length;
        public ImmutableArray<byte> Array => _array;

        protected override AbstractMemoryBlock GetMemoryBlockImpl(int start, int size)
        {
            return new ByteArrayMemoryBlock(this, start, size);
        }

        internal unsafe byte* Pointer
        {
            get
            {
                if (_pinned == null)
                {
                    var newPinned = new PinnedObject(ImmutableCollectionsMarshal.AsArray(_array)!);

                    if (Interlocked.CompareExchange(ref _pinned, newPinned, null) != null)
                    {
                        // another thread has already allocated the handle:
                        newPinned.Dispose();
                    }
                }

                return _pinned.Pointer;
            }
        }
    }
}
