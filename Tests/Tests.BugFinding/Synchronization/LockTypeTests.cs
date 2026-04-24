// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET9_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Coyote.Specifications;
using Xunit;
using Xunit.Abstractions;
using Lock = System.Threading.Lock;

namespace Microsoft.Coyote.BugFinding.Tests
{
    public class LockTypeTests : BaseBugFindingTest
    {
        public LockTypeTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact(Timeout = 5000)]
        public void TestLockEnterExit()
        {
            this.Test(() =>
            {
                int value = 0;
                Lock sync = new Lock();
                lock (sync)
                {
                    value++;
                }

                Specification.Assert(value == 1, "Value is {0} instead of 1.", value);
            });
        }

        [Fact(Timeout = 5000)]
        public void TestLockDeadlock()
        {
            this.TestWithError(async () =>
            {
                Lock a = new Lock();
                Lock b = new Lock();

                Task t1 = Task.Run(() =>
                {
                    lock (a)
                    {
                        lock (b)
                        {
                        }
                    }
                });

                Task t2 = Task.Run(() =>
                {
                    lock (b)
                    {
                        lock (a)
                        {
                        }
                    }
                });

                await t1;
                await t2;
            },
            errorChecker: (e) =>
            {
                Assert.StartsWith("Deadlock detected.", e);
            },
            configuration: this.GetConfiguration().WithLockAccessRaceCheckingEnabled().WithTestingIterations(100));
        }

        [Fact(Timeout = 5000)]
        public void TestLockTryEnter()
        {
            this.Test(() =>
            {
                int value = 0;
                Lock sync = new Lock();
                bool taken = sync.TryEnter();
                Specification.Assert(taken, "TryEnter on a free Lock returned false.");
                try
                {
                    value++;
                }
                finally
                {
                    sync.Exit();
                }

                Specification.Assert(value == 1, "Value is {0} instead of 1.", value);
            });
        }

        [Fact(Timeout = 5000)]
        public void TestLockTryEnterWithTimeout()
        {
            this.Test(() =>
            {
                Lock sync = new Lock();
                Specification.Assert(sync.TryEnter(0), "TryEnter(0ms) on a free Lock returned false.");
                sync.Exit();

                Specification.Assert(sync.TryEnter(TimeSpan.Zero), "TryEnter(TimeSpan.Zero) on a free Lock returned false.");
                sync.Exit();
            });
        }

        [Fact(Timeout = 5000)]
        public void TestLockIsHeldByCurrentThread()
        {
            this.Test(() =>
            {
                Lock sync = new Lock();
                Specification.Assert(!sync.IsHeldByCurrentThread, "Lock unexpectedly held before Enter.");
                lock (sync)
                {
                    Specification.Assert(sync.IsHeldByCurrentThread, "Lock not reported as held inside lock block.");
                }

                Specification.Assert(!sync.IsHeldByCurrentThread, "Lock still reported as held after lock block exit.");
            });
        }

        [Fact(Timeout = 5000)]
        public void TestLockExitWithoutEnter()
        {
            this.TestWithException<SynchronizationLockException>(() =>
            {
                Lock sync = new Lock();
                sync.Exit();
            },
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestLockReentrantAcquire()
        {
            this.Test(() =>
            {
                int value = 0;
                Lock sync = new Lock();
                lock (sync)
                {
                    value++;
                    lock (sync)
                    {
                        value++;
                    }
                }

                Specification.Assert(value == 2, "Value is {0} instead of 2.", value);
            });
        }

        [Fact(Timeout = 5000)]
        public void TestLockUnderFuzzing()
        {
            this.Test(() =>
            {
                int value = 0;
                Lock sync = new Lock();
                lock (sync)
                {
                    value++;
                }

                Specification.Assert(value == 1, "Value is {0} instead of 1.", value);
            },
            configuration: this.GetConfiguration().WithSystematicFuzzingEnabled(true).WithTestingIterations(10));
        }

        [Fact(Timeout = 5000)]
        public void TestLockConcurrentIncrement()
        {
            this.Test(async () =>
            {
                const int iterations = 10;
                int counter = 0;
                Lock sync = new Lock();

                Task t1 = Task.Run(() =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        lock (sync)
                        {
                            counter++;
                        }
                    }
                });

                Task t2 = Task.Run(() =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        lock (sync)
                        {
                            counter++;
                        }
                    }
                });

                await t1;
                await t2;

                Specification.Assert(counter == iterations * 2,
                    "Counter is {0} instead of {1}.", counter, iterations * 2);
            },
            configuration: this.GetConfiguration().WithLockAccessRaceCheckingEnabled().WithTestingIterations(50));
        }
    }
}
#endif
