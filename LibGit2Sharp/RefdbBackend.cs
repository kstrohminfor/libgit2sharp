using LibGit2Sharp.Core;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LibGit2Sharp
{
    /// <summary>
    /// Reference database backend.
    /// </summary>
    public abstract class RefdbBackend
    {
        private IntPtr nativePointer;

        /// <summary>
        /// Gets the repository.
        /// </summary>
        protected Repository Repository { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RefdbBackend"/> class.
        /// </summary>
        /// <param name="repository">Repository that this refdb is attached to.</param>
        protected RefdbBackend(Repository repository)
        {
            Ensure.ArgumentNotNull(repository, "repository");
            this.Repository = repository;
        }

        /// <summary>
        /// Checks to see if a reference exists.
        /// </summary>
        public abstract bool Exists(string referenceName);

        /// <summary>
        /// Attempts to look up a reference.
        /// </summary>
        /// <returns>False if the reference doesn't exist.</returns>
        public abstract bool Lookup(string referenceName, out ReferenceData data);

        /// <summary>
        /// Iterates all references (if glob is null) or only references matching glob (if not null.)
        /// </summary>
        public abstract IEnumerable<ReferenceData> Iterate(string glob);

        /// <summary>
        /// Writes a reference to the database.
        /// </summary>
        /// <param name="newRef">New reference to write.</param>
        /// <param name="oldRef">Old reference (possibly null.)</param>
        /// <param name="force">True if overwrites are allowed.</param>
        /// <param name="signature">User signature.</param>
        /// <param name="message">User message.</param>
        public abstract void Write(ReferenceData newRef, ReferenceData oldRef, bool force, Signature signature, string message);

        /// <summary>
        /// Deletes a reference from the database.
        /// </summary>
        /// <param name="existingRef">Reference to delete.</param>
        public abstract void Delete(ReferenceData existingRef);

        /// <summary>
        /// Renames a reference.
        /// </summary>
        /// <param name="oldName">Old name.</param>
        /// <param name="newName">New name.</param>
        /// <param name="force">Allow overwrites.</param>
        /// <param name="signature">User signature.</param>
        /// <param name="message">User message.</param>
        /// <returns>New reference.</returns>
        public abstract ReferenceData Rename(string oldName, string newName, bool force, Signature signature, string message);

        /// <summary>
        /// Backend pointer. Accessing this lazily allocates a marshalled GitRefdbBackend, which is freed with Free().
        /// </summary>
        internal IntPtr RefdbBackendPointer
        {
            get
            {
                if (IntPtr.Zero == nativePointer)
                {
                    var nativeBackend = new GitRefdbBackend()
                    {
                        Version = 1,
                        Compress = null,
                        Lock = null,
                        Unlock = null,
                        Exists = BackendEntryPoints.ExistsCallback,
                        Lookup = BackendEntryPoints.LookupCallback,
                        Iterator = BackendEntryPoints.IteratorCallback,
                        Write = BackendEntryPoints.WriteCallback,
                        Rename = BackendEntryPoints.RenameCallback,
                        Del = BackendEntryPoints.DelCallback,
                        HasLog = BackendEntryPoints.HasLogCallback,
                        EnsureLog = BackendEntryPoints.EnsureLogCallback,
                        Free = BackendEntryPoints.FreeCallback,
                        ReflogRead = BackendEntryPoints.ReflogReadCallback,
                        ReflogWrite = BackendEntryPoints.ReflogWriteCallback,
                        ReflogRename = BackendEntryPoints.ReflogRenameCallback,
                        ReflogDelete = BackendEntryPoints.ReflogDeleteCallback,
                        GCHandle = GCHandle.ToIntPtr(GCHandle.Alloc(this))
                    };

                    nativePointer = Marshal.AllocHGlobal(Marshal.SizeOf(nativeBackend));
                    Marshal.StructureToPtr(nativeBackend, nativePointer, false);
                }

                return nativePointer;
            }
        }

        /// <summary>
        /// Frees the backend pointer, if one has been allocated.
        /// </summary>
        private void Free()
        {
            if (IntPtr.Zero == nativePointer)
            {
                return;
            }

            GCHandle.FromIntPtr(Marshal.ReadIntPtr(nativePointer, GitRefdbBackend.GCHandleOffset)).Free();
            Marshal.FreeHGlobal(nativePointer);
            nativePointer = IntPtr.Zero;
        }

        /// <summary>
        /// Static entry points that trampoline into the custom backend's implementation.
        /// </summary>
        private static unsafe class BackendEntryPoints
        {
            public static readonly GitRefdbBackend.exists_callback ExistsCallback = Exists;
            public static readonly GitRefdbBackend.lookup_callback LookupCallback = Lookup;
            public static readonly GitRefdbBackend.iterator_callback IteratorCallback = Iterator;
            public static readonly GitRefdbBackend.write_callback WriteCallback = Write;
            public static readonly GitRefdbBackend.rename_callback RenameCallback = Rename;
            public static readonly GitRefdbBackend.del_callback DelCallback = Del;
            public static readonly GitRefdbBackend.has_log_callback HasLogCallback = HasLog;
            public static readonly GitRefdbBackend.ensure_log_callback EnsureLogCallback = EnsureLog;
            public static readonly GitRefdbBackend.free_callback FreeCallback = Free;
            public static readonly GitRefdbBackend.reflog_read_callback ReflogReadCallback = ReflogRead;
            public static readonly GitRefdbBackend.reflog_write_callback ReflogWriteCallback = ReflogWrite;
            public static readonly GitRefdbBackend.reflog_rename_callback ReflogRenameCallback = ReflogRename;
            public static readonly GitRefdbBackend.reflog_delete_callback ReflogDeleteCallback = ReflogDelete;

            public static int Exists(
                ref bool exists,
                IntPtr backendPtr,
                string refName)
            {
                var backend = PtrToBackend(backendPtr);
                if (backend == null)
                {
                    return (int)GitErrorCode.Error;
                }

                try
                {
                    exists = backend.Exists(refName);
                }
                catch (Exception ex)
                {
                    return RefdbBackendException.GetCode(ex);
                }

                return (int)GitErrorCode.Ok;
            }

            public static int Lookup(
                out IntPtr referencePtr,
                IntPtr backendPtr,
                string refName)
            {
                referencePtr = IntPtr.Zero;
                var backend = PtrToBackend(backendPtr);
                if (backend == null)
                {
                    return (int)GitErrorCode.Error;
                }

                try
                {
                    ReferenceData data;
                    if (!backend.Lookup(refName, out data))
                    {
                        return (int)GitErrorCode.NotFound;
                    }

                    referencePtr = data.MarshalToPtr();
                }
                catch (Exception ex)
                {
                    return RefdbBackendException.GetCode(ex);
                }

                return (int)GitErrorCode.Ok;
            }

            public static int Iterator(
                out IntPtr iteratorPtr,
                IntPtr backendPtr,
                string glob)
            {
                iteratorPtr = IntPtr.Zero;
                var backend = PtrToBackend(backendPtr);
                if (backend == null)
                {
                    return (int)GitErrorCode.Error;
                }

                RefdbIterator iterator;
                try
                {
                    var enumerator = backend.Iterate(glob).GetEnumerator();
                    iterator = new RefdbIterator(enumerator);
                }
                catch (Exception ex)
                {
                    return RefdbBackendException.GetCode(ex);
                }

                iteratorPtr = iterator.RefdbIteratorPtr;
                return (int)GitErrorCode.Ok;
            }

            public static int Write(
                IntPtr backendPtr,
                git_reference* reference,
                bool force,
                git_signature* who,
                string message,
                IntPtr old,
                string oldTarget)
            {
                var backend = PtrToBackend(backendPtr);
                if (backend == null)
                {
                    return (int)GitErrorCode.Error;
                }

                var signature = new Signature(who);

                // New ref data is constructed directly from the reference pointer.
                var newRef = ReferenceData.MarshalFromPtr(reference);

                // Old ref value is provided as a check, so that the refdb can atomically test the old value
                // and set the new value, thereby preventing write conflicts.
                // If a write conflict is detected, we should return GIT_EMODIFIED.
                // If the ref is brand new, the "old" oid pointer is null.
                ReferenceData oldRef = null;
                if (old != IntPtr.Zero)
                {
                    oldRef = new ReferenceData(oldTarget, ObjectId.BuildFromPtr(old));
                }

                try
                {
                    // If the user returns false, we detected a conflict and aborted the write.
                    backend.Write(newRef, oldRef, force, signature, message);
                }
                catch (Exception ex)
                {
                    return RefdbBackendException.GetCode(ex);
                }

                return (int)GitErrorCode.Ok;
            }

            public static int Rename(
                out IntPtr reference,
                IntPtr backendPtr,
                string oldName,
                string newName,
                bool force,
                git_signature* who,
                string message)
            {
                reference = IntPtr.Zero;
                var backend = PtrToBackend(backendPtr);
                if (backend == null)
                {
                    return (int)GitErrorCode.Error;
                }

                var signature = new Signature(who);

                ReferenceData newRef;
                try
                {
                    newRef = backend.Rename(oldName, newName, force, signature, message);
                }
                catch (Exception ex)
                {
                    return RefdbBackendException.GetCode(ex);
                }

                reference = newRef.MarshalToPtr();
                return (int)GitErrorCode.Ok;
            }

            public static int Del(
                IntPtr backendPtr,
                string refName,
                IntPtr oldId,
                string oldTarget)
            {
                var backend = PtrToBackend(backendPtr);
                if (backend == null)
                {
                    return (int)GitErrorCode.Error;
                }

                ReferenceData existingRef;
                if (IntPtr.Zero == oldId)
                {
                    existingRef = new ReferenceData(refName, oldTarget);
                }
                else
                {
                    existingRef = new ReferenceData(refName, ObjectId.BuildFromPtr(oldId));
                }

                try
                {
                    backend.Delete(existingRef);
                }
                catch (Exception ex)
                {
                    return RefdbBackendException.GetCode(ex);
                }

                return (int)GitErrorCode.Ok;
            }

            public static int HasLog(
                IntPtr backend,
                string refName)
            {
                return (int)GitErrorCode.Error;
            }

            public static int EnsureLog(
                IntPtr backend,
                string refName)
            {
                return (int)GitErrorCode.Error;
            }

            public static void Free(IntPtr backend)
            {
                PtrToBackend(backend).Free();
            }

            public static int ReflogRead(
                out git_reflog* reflog,
                IntPtr backend,
                string name)
            {
                reflog = null;
                return (int)GitErrorCode.Error;
            }

            public static int ReflogWrite(
                IntPtr backend,
                git_reflog* reflog)
            {
                return (int)GitErrorCode.Error;
            }

            public static int ReflogRename(
                IntPtr backend,
                string oldName,
                string newName)
            {
                return (int)GitErrorCode.Error;
            }

            public static int ReflogDelete(
                IntPtr backend,
                string name)
            {
                return (int)GitErrorCode.Error;
            }

            private static RefdbBackend PtrToBackend(IntPtr pointer)
            {
                var intPtr = Marshal.ReadIntPtr(pointer, GitRefdbBackend.GCHandleOffset);
                var backend = GCHandle.FromIntPtr(intPtr).Target as RefdbBackend;

                if (backend == null)
                {
                    Proxy.git_error_set_str(GitErrorCategory.Reference, "Cannot retrieve the managed RefdbBackend");
                }

                return backend;
            }
        }
    }
}