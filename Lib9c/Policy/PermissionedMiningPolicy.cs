using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Libplanet;

namespace Nekoyume.BlockChain.Policy
{
    public struct PermissionedMiningPolicy
    {
        public PermissionedMiningPolicy(ISet<Address> miners, long startIndex, long? endIndex)
        {
            if (endIndex is long ei && ei < startIndex)
            {
                throw new ArgumentOutOfRangeException(
                    $"Non-null {nameof(endIndex)} cannot be less than {nameof(startIndex)}.");
            }
            else if (miners.Count == 0)
            {
                throw new ArgumentException(
                    $"Set {nameof(miners)} cannot be empty.");
            }

            Miners = miners;
            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        public ISet<Address> Miners { get; private set; }

        public long StartIndex { get; private set; }

        public long? EndIndex { get; private set; }

        public bool IsTargetBlockIndex(long index)
        {
            return StartIndex <= index
                && (EndIndex is long endIndex && index <= endIndex);
        }

        public static PermissionedMiningPolicy Mainnet => new PermissionedMiningPolicy()
        {
            Miners = new[]
            {
                new Address("ab1dce17dCE1Db1424BB833Af6cC087cd4F5CB6d"),
                new Address("3217f757064Cd91CAba40a8eF3851F4a9e5b4985"),
                new Address("474CB59Dea21159CeFcC828b30a8D864e0b94a6B"),
                new Address("636d187B4d434244A92B65B06B5e7da14b3810A9"),
            }.ToImmutableHashSet(),
            StartIndex = 2_225_500,
            EndIndex = null,
        };
    }
}
