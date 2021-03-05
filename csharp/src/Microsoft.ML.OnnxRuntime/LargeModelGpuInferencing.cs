// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Buffers;

namespace Microsoft.ML.OnnxRuntime
{
    public class RequestBatch : SafeHandle
    {
        public RequestBatch()
            : base(IntPtr.Zero, true)
        {
            NativeApiStatus.VerifySuccess(NativeMethods.OrtCreateRequestBatch(out handle));
        }

        public void AddToBatch(string[] inputNames, OrtValue[] inputs)
        {
            if (inputs.Length != inputNames.Length)
            {
                throw new ArgumentException($"Length of {nameof(inputNames)} ({inputNames.Length}) must match that of {nameof(inputs)} ({inputs.Length}).");
            }

            IntPtr[] inputHandles = new IntPtr[inputs.Length];
            for (int i = 0; i < inputs.Length; ++i)
            {
                inputHandles[i] = inputs[i].Handle;
            }

            using (var cleanupList = new DisposableList<IDisposable>())
            {
                var inputNamesPinned = NativeOnnxValueHelper.ConvertToUtf8AndPin(inputNames, inputName => inputName, cleanupList);
                NativeApiStatus.VerifySuccess(NativeMethods.OrtAddRequestToBatch(handle, (UIntPtr)inputNames.Length, inputNamesPinned, inputHandles));

            }
        }
        public void Clear()
        {
            NativeMethods.OrtClearRequestBatch(handle);
        }
        internal IntPtr Pointer
        {
            get
            {
                return handle;
            }
        }

        #region SafeHandle

        /// <summary>
        /// Overrides SafeHandle.IsInvalid
        /// </summary>
        /// <value>returns true if handle is equal to Zero</value>
        public override bool IsInvalid { get { return handle == IntPtr.Zero; } }

        /// <summary>
        /// Overrides SafeHandle.ReleaseHandle() to properly dispose of
        /// the native instance of OrtEnv
        /// </summary>
        /// <returns>always returns true</returns>
        protected override bool ReleaseHandle()
        {
            NativeMethods.OrtReleaseRequestBatch(handle);
            handle = IntPtr.Zero;
            return true;
        }

        #endregion

    }

    public class ResponseBatch : SafeHandle
    {
        public ResponseBatch()
            : base(IntPtr.Zero, true)
        {
            NativeApiStatus.VerifySuccess(NativeMethods.OrtCreateResponseBatch(out handle));
        }

        public void AddToBatch(string[] outputNames, OrtValue[] outputs, OrtMemoryInfo[] memInfo)
        {
            if (outputs.Length != outputNames.Length)
            {
                throw new ArgumentException($"Length of {nameof(outputNames)} ({outputNames.Length}) must match that of {nameof(outputs)} ({outputs.Length}).");
            }

            if (outputs.Length != memInfo.Length)
            {
                throw new ArgumentException($"Length of {nameof(memInfo)} ({memInfo.Length}) must match that of {nameof(outputs)} ({outputs.Length}).");
            }

            IntPtr[] outputHandles = new IntPtr[outputs.Length];
            IntPtr[] memInfoHandles = new IntPtr[outputs.Length];

            for (int i = 0; i < outputs.Length; ++i)
            {
                outputHandles[i] = outputs[i].Handle;
                memInfoHandles[i] = memInfo[i].Pointer;
            }

            using (var cleanupList = new DisposableList<IDisposable>())
            {
                var outputNamesPinned = NativeOnnxValueHelper.ConvertToUtf8AndPin(outputNames, outputName => outputName, cleanupList);
                NativeApiStatus.VerifySuccess(NativeMethods.OrtAddResponseToBatch(handle, (UIntPtr)outputNames.Length,
                    outputNamesPinned, outputHandles, memInfoHandles));

            }
        }

        public IDisposableReadOnlyCollection<OrtValue> GetOutputValues(UIntPtr batchIdx, OrtAllocator allocator)
        {
            UIntPtr count = UIntPtr.Zero;
            IntPtr ortValues = IntPtr.Zero;
            NativeApiStatus.VerifySuccess(NativeMethods.OrtGetOutputValues(handle, batchIdx, allocator.Pointer, out ortValues, out count));

            if (count.Equals(UIntPtr.Zero))
            {
                return new DisposableList<OrtValue>();
            }

            using (var ortValuesAllocation = new OrtMemoryAllocation(allocator, ortValues, 0))
            {
                int outputCount = (int)count;
                var ortList = new DisposableList<OrtValue>(outputCount);
                try
                {
                    for (int i = 0; i < outputCount; ++i)
                    {
                        IntPtr ortValue = Marshal.ReadIntPtr(ortValues, IntPtr.Size * i);
                        ortList.Add(new OrtValue(ortValue));
                    }
                }
                catch (Exception e)
                {
                    ortList.Dispose();
                    throw e;
                }
                return ortList;
            }
        }

        public void Clear()
        {
            NativeMethods.OrtClearResponseBatch(handle);
        }


        internal IntPtr Pointer
        {
            get
            {
                return handle;
            }
        }

        #region SafeHandle

        /// <summary>
        /// Overrides SafeHandle.IsInvalid
        /// </summary>
        /// <value>returns true if handle is equal to Zero</value>
        public override bool IsInvalid { get { return handle == IntPtr.Zero; } }

        /// <summary>
        /// Overrides SafeHandle.ReleaseHandle() to properly dispose of
        /// the native instance of OrtEnv
        /// </summary>
        /// <returns>always returns true</returns>
        protected override bool ReleaseHandle()
        {
            NativeMethods.OrtReleaseResponseBatch(handle);
            handle = IntPtr.Zero;
            return true;
        }

        #endregion

    }

    public class PipelineSession : SafeHandle
    {
        public PipelineSession(string ensembleConfigFilePath)
            : base(IntPtr.Zero, true)
        {
            var ensembleConfigFilePathPinned = GCHandle.Alloc(NativeOnnxValueHelper.StringToZeroTerminatedUtf8(ensembleConfigFilePath), GCHandleType.Pinned);
            using (var ensembleConfigFilePathPinnedHandle = new PinnedGCHandle(ensembleConfigFilePathPinned))
            {
                NativeApiStatus.VerifySuccess(NativeMethods.OrtCreatePipelineSession(OrtEnv.Handle, ensembleConfigFilePathPinnedHandle.Pointer, out handle));
            }
        }

        public void Run(RequestBatch requestBatch, ResponseBatch responseBatch, int numSteps)
        {
            NativeApiStatus.VerifySuccess(NativeMethods.OrtPipelineSessionRun(handle, requestBatch.Pointer, responseBatch.Pointer, numSteps));
        }

        internal IntPtr Pointer
        {
            get
            {
                return handle;
            }
        }

        #region SafeHandle

        /// <summary>
        /// Overrides SafeHandle.IsInvalid
        /// </summary>
        /// <value>returns true if handle is equal to Zero</value>
        public override bool IsInvalid { get { return handle == IntPtr.Zero; } }

        /// <summary>
        /// Overrides SafeHandle.ReleaseHandle() to properly dispose of
        /// the native instance of OrtEnv
        /// </summary>
        /// <returns>always returns true</returns>
        protected override bool ReleaseHandle()
        {
            NativeMethods.OrtReleasePipelineSession(handle);
            handle = IntPtr.Zero;
            return true;
        }

        #endregion

    }

}