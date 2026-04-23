// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET9_0_OR_GREATER
using System;
using Microsoft.Coyote.Runtime;
using SynchronizedBlock = Microsoft.Coyote.Rewriting.Types.Threading.Monitor.SynchronizedBlock;
using SystemThreading = System.Threading;

namespace Microsoft.Coyote.Rewriting.Types.Threading
{
    /// <summary>
    /// Provides a replacement for <see cref="SystemThreading.Lock"/> that can be controlled
    /// during testing.
    /// </summary>
    /// <remarks>This type is intended for compiler use rather than use directly in code.</remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public sealed class Lock
    {
        /// <summary>
        /// The runtime <see cref="SystemThreading.Lock"/> used when the scheduler is not in
        /// interleaving mode, so that we fall back to the real lock semantics.
        /// </summary>
        private readonly SystemThreading.Lock Instance = new SystemThreading.Lock();

        /// <summary>
        /// Factory invoked by the IL rewriter in place of <c>newobj System.Threading.Lock::.ctor()</c>.
        /// The rewriter converts constructor invocations to a static <c>Create</c> call, so every
        /// shimmed type that is constructable must expose one.
        /// </summary>
        public static Lock Create() => new Lock();

        /// <summary>
        /// Enters the lock, blocking if the lock is already held by another thread.
        /// </summary>
        public void Enter()
        {
            var runtime = CoyoteRuntime.Current;
            if (runtime.SchedulingPolicy is SchedulingPolicy.Interleaving)
            {
                SynchronizedBlock.Lock(this);
            }
            else
            {
                if (runtime.SchedulingPolicy is SchedulingPolicy.Fuzzing &&
                    runtime.TryGetExecutingOperation(out ControlledOperation current))
                {
                    runtime.DelayOperation(current);
                }

                this.Instance.Enter();
            }
        }

        /// <summary>
        /// Releases the lock. Throws <see cref="SystemThreading.SynchronizationLockException"/>
        /// if the lock is not held by the current operation.
        /// </summary>
        public void Exit()
        {
            var runtime = CoyoteRuntime.Current;
            if (runtime.SchedulingPolicy is SchedulingPolicy.Interleaving)
            {
                var block = SynchronizedBlock.Find(this) ??
                    throw new SystemThreading.SynchronizationLockException();
                block.Exit();
            }
            else
            {
                this.Instance.Exit();
            }
        }

        /// <summary>
        /// Enters the lock and returns a <see cref="Scope"/> that releases the lock when disposed.
        /// This is what the C# compiler emits for <c>lock (lockObj)</c> when the variable is
        /// typed as <see cref="SystemThreading.Lock"/>.
        /// </summary>
        public Scope EnterScope()
        {
            this.Enter();
            return new Scope(this);
        }

        /// <summary>
        /// Releases a <see cref="Lock"/> when disposed. Mirrors <see cref="SystemThreading.Lock.Scope"/>.
        /// </summary>
        public readonly ref struct Scope
        {
            private readonly Lock Owner;

            internal Scope(Lock owner)
            {
                this.Owner = owner;
            }

            /// <summary>
            /// Releases the lock associated with this scope.
            /// </summary>
            public void Dispose() => this.Owner.Exit();
        }
    }
}
#endif
