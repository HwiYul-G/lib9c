namespace Lib9c.Tests;

using System;
using System.Collections.Generic;
using System.Reflection;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Nekoyume;
using Xunit;

public class AddressesTest
{
    [Fact]
    public void There_Are_No_Conflicts_In_Addresses()
    {
        var addressesInTypes = new List<Address>();
        InsertStaticAddressFieldValues(typeof(ReservedAddresses));
        InsertStaticAddressFieldValues(typeof(Addresses));

        var addresses = new HashSet<Address>();
        foreach (var address in addressesInTypes)
        {
            Assert.DoesNotContain(address, addresses);
            Assert.False(Addresses.IsArenaParticipantAccountAddress(address));
            addresses.Add(address);
        }

        return;

        void InsertStaticAddressFieldValues(Type type)
        {
            var addressFields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var field in addressFields)
            {
                if (field.FieldType != typeof(Address))
                {
                    continue;
                }

                // GetValue(null) because the fields are static
                var address = (Address)field.GetValue(null)!;
                addressesInTypes.Add(address);
            }
        }
    }

    [Theory]
    [InlineData(0, 0, "0100000000000000000000000000000000000000")]
    [InlineData(1, 1, "0100000000000000000000000000000000000101")]
    [InlineData(int.MaxValue, 99, "0100000000000000000000000000214748364799")]
    public void GetArenaParticipantAccountAddressTest(int championshipId, int round, string expectedHex)
    {
        var address = Addresses.GetArenaParticipantAccountAddress(championshipId, round);
        Assert.Equal(expectedHex, address.ToHex());
    }
}
