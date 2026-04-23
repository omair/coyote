// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET9_0_OR_GREATER
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
                using (sync.EnterScope())
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
                    using (a.EnterScope())
                    {
                        using (b.EnterScope())
                        {
                        }
                    }
                });

                Task t2 = Task.Run(() =>
                {
                    using (b.EnterScope())
                    {
                        using (a.EnterScope())
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
    }
}
#endif
