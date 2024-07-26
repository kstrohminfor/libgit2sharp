using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LibGit2Sharp.Core;

namespace LibGit2Sharp
{
    public class RefdbIterator : IDisposable
    {
        private IEnumerator<ReferenceData> enumerator;

        public RefdbIterator(IEnumerator<ReferenceData> enumerator)
        {
            this.enumerator = enumerator;
        }

        public ReferenceData GetNext()
        {
            if (this.enumerator.MoveNext())
            {
                return this.enumerator.Current;
            }

            return null;
        }

        public void Dispose()
        {
            if (this.enumerator != null)
            {
                this.enumerator.Dispose();
                this.enumerator = null;
            }
        }

        private IntPtr nativePointer;

        internal IntPtr RefdbIteratorPtr
        {
            get
            {
                if (IntPtr.Zero == nativePointer)
                {
                    var nativeIterator = new GitRefdbIterator();

                    nativeIterator.Next = ReferenceIteratorEntryPoints.NextCallback;
                    nativeIterator.NextName = ReferenceIteratorEntryPoints.NextNameCallback;
                    nativeIterator.Free = ReferenceIteratorEntryPoints.FreeCallback;

                    nativeIterator.GCHandle = GCHandle.ToIntPtr(GCHandle.Alloc(this));
                    nativePointer = Marshal.AllocHGlobal(Marshal.SizeOf(nativeIterator));
                    Marshal.StructureToPtr(nativeIterator, nativePointer, false);
                }

                return nativePointer;
            }
        }

        /// <summary>
        /// Frees the iterator pointer, if one has been allocated.
        /// </summary>
        private void Free()
        {
            if (IntPtr.Zero == nativePointer)
            {
                return;
            }

            GCHandle.FromIntPtr(Marshal.ReadIntPtr(nativePointer, GitRefdbIterator.GCHandleOffset)).Free();
            Marshal.FreeHGlobal(nativePointer);
            nativePointer = IntPtr.Zero;
        }

        private static class ReferenceIteratorEntryPoints
        {
            public static readonly GitRefdbIterator.next_callback NextCallback = Next;
            public static readonly GitRefdbIterator.next_name_callback NextNameCallback = NextName;
            public static readonly GitRefdbIterator.free_callback FreeCallback = Free;

            public static int Next(
                out IntPtr referencePtr,
                IntPtr iteratorPtr)
            {
                referencePtr = IntPtr.Zero;
                var iterator = PtrToIterator(iteratorPtr);
                if (iterator == null)
                {
                    return (int)GitErrorCode.Error;
                }

                ReferenceData data;
                try
                {
                    data = iterator.GetNext();
                }
                catch (Exception ex)
                {
                    return RefdbBackendException.GetCode(ex);
                }

                if (data == null)
                {
                    return (int)GitErrorCode.IterOver;
                }

                referencePtr = data.MarshalToPtr();
                return (int)GitErrorCode.Ok;
            }

            public static int NextName(
                out string refNamePtr,
                IntPtr iteratorPtr)
            {
                refNamePtr = null;
                var iterator = PtrToIterator(iteratorPtr);
                if (iterator == null)
                {
                    return (int)GitErrorCode.Error;
                }

                ReferenceData data;
                try
                {
                    data = iterator.GetNext();
                }
                catch (Exception ex)
                {
                    return RefdbBackendException.GetCode(ex);
                }

                if (data == null)
                {
                    return (int)GitErrorCode.IterOver;
                }

                refNamePtr = data.RefName;
                return (int)GitErrorCode.Ok;
            }

            public static void Free(IntPtr iteratorPtr)
            {
                var iterator = PtrToIterator(iteratorPtr);
                if (iterator == null)
                {
                    return;
                }

                try
                {
                    iterator.Free();

                    iterator.Dispose();
                }
                catch (Exception ex)
                {
                    Proxy.git_error_set_str(GitErrorCategory.Reference, ex);
                }
            }

            private static RefdbIterator PtrToIterator(IntPtr pointer)
            {
                var intPtr = Marshal.ReadIntPtr(pointer, GitRefdbIterator.GCHandleOffset);
                var interator = GCHandle.FromIntPtr(intPtr).Target as RefdbIterator;

                if (interator == null)
                {
                    Proxy.git_error_set_str(GitErrorCategory.Reference, "Cannot retrieve the managed RefdbIterator");
                    return null;
                }

                return interator;
            }
        }
    }
}