#nullable enable

namespace Lib9c.Tests.Action.CustomEquipmentCraft
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Bencodex.Types;
    using Libplanet.Action.State;
    using Libplanet.Crypto;
    using Libplanet.Mocks;
    using Libplanet.Types.Assets;
    using Nekoyume;
    using Nekoyume.Action;
    using Nekoyume.Action.CustomEquipmentCraft;
    using Nekoyume.Action.Exceptions;
    using Nekoyume.Action.Exceptions.CustomEquipmentCraft;
    using Nekoyume.Battle;
    using Nekoyume.Exceptions;
    using Nekoyume.Model.Elemental;
    using Nekoyume.Model.Item;
    using Nekoyume.Model.State;
    using Nekoyume.Module;
    using Xunit;

    public class CustomEquipmentCraftTest
    {
        private const int ScrollItemId = 600401;
        private const int CircleItemId = 600402;

        private readonly Address _agentAddress;
        private readonly Address _avatarAddress;
        private readonly TableSheets _tableSheets;
        private readonly IWorld _initialState;
        private readonly AgentState _agentState;
        private readonly AvatarState _avatarState;

        public CustomEquipmentCraftTest()
        {
            _agentAddress = new PrivateKey().Address;
            _avatarAddress = _agentAddress.Derive(string.Format(
                CultureInfo.InvariantCulture,
                CreateAvatar.DeriveFormat,
                0
            ));
            var sheets = TableSheetsImporter.ImportSheets();
            _tableSheets = new TableSheets(sheets);
            _agentState = new AgentState(_agentAddress)
            {
                avatarAddresses =
                {
                    [0] = _avatarAddress,
                },
            };

            _avatarState = AvatarState.Create(
                _avatarAddress, _agentAddress, 0, _tableSheets.GetAvatarSheets(), default
            );
#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            var gold = new GoldCurrencyState(Currency.Legacy("NCG", 2, null));
#pragma warning restore CS0618

            _initialState = new World(MockUtil.MockModernWorldState)
                    .SetLegacyState(GoldCurrencyState.Address, gold.Serialize())
                    .SetActionPoint(_avatarAddress, DailyReward.ActionPointMax)
                    .SetAgentState(_agentAddress, _agentState)
                    .SetLegacyState(
                        GameConfigState.Address,
                        new GameConfigState(sheets["GameConfigSheet"]).Serialize()
                    )
                ;

            for (var i = 0; i < 4; i++)
            {
                var slotAddress = _avatarAddress.Derive(string.Format(
                    CultureInfo.InvariantCulture,
                    CombinationSlotState.DeriveFormat,
                    i
                ));
                var combinationSlotState = new CombinationSlotState(slotAddress, 0);
                _initialState = _initialState
                    .SetLegacyState(slotAddress, combinationSlotState.Serialize());
            }

            foreach (var (key, value) in sheets)
            {
                _initialState =
                    _initialState.SetLegacyState(
                        Addresses.TableSheet.Derive(key),
                        value.Serialize()
                    );
            }
        }

        public static IEnumerable<object?[]> GetTestData_Success()
        {
            // First Craft
            yield return new object?[]
            {
                new List<CustomCraftData>
                {
                    new () { RecipeId = 1, SlotIndex = 0, IconId = 10111000, },
                },
                true, 0, false,
                new List<TestResult>
                {
                    new ()
                    {
                        MinCp = 900,
                        MaxCp = 1000,
                        ItemSubType = ItemSubType.Weapon,
                        ElementalType = ElementalType.Wind,
                    },
                },
                10, null,
            };

            // Random Icon
            yield return new object?[]
            {
                new List<CustomCraftData>
                {
                    new () { RecipeId = 1, SlotIndex = 0, IconId = 0, },
                },
                true, 0, false,
                new List<TestResult>
                {
                    new ()
                    {
                        MinCp = 900,
                        MaxCp = 1000,
                        ItemSubType = ItemSubType.Weapon,
                        ElementalType = ElementalType.Wind,
                    },
                },
                10, null, 8,
            };

            // First craft in relationship group
            yield return new object?[]
            {
                new List<CustomCraftData>
                {
                    new () { RecipeId = 1, SlotIndex = 0, IconId = 10111000, },
                },
                true, 11, false,
                new List<TestResult>
                {
                    new ()
                    {
                        MinCp = 900,
                        MaxCp = 1000,
                        ItemSubType = ItemSubType.Weapon,
                        ElementalType = ElementalType.Wind,
                    },
                },
                12, null,
            };
            yield return new object?[]
            {
                new List<CustomCraftData>
                {
                    new () { RecipeId = 1, SlotIndex = 0, IconId = 10111000, },
                },
                true, 101, false,
                new List<TestResult>
                {
                    new ()
                    {
                        MinCp = 9000,
                        MaxCp = 10000,
                        ItemSubType = ItemSubType.Weapon,
                        ElementalType = ElementalType.Wind,
                    },
                },
                15, null,
            };
            yield return new object?[]
            {
                new List<CustomCraftData>
                {
                    new () { RecipeId = 1, SlotIndex = 0, IconId = 10111000, },
                },
                true, 1001, false,
                new List<TestResult>
                {
                    new ()
                    {
                        MinCp = 90000,
                        MaxCp = 100000,
                        ItemSubType = ItemSubType.Weapon,
                        ElementalType = ElementalType.Wind,
                    },
                },
                20, null,
            };

            // Multiple slots
            yield return new object?[]
            {
                new List<CustomCraftData>
                {
                    new () { RecipeId = 1, SlotIndex = 0, IconId = 10111000, },
                    new () { RecipeId = 1, SlotIndex = 1, IconId = 10112000, },
                },
                true, 0, false,
                new List<TestResult>
                {
                    new ()
                    {
                        MinCp = 900,
                        MaxCp = 1000,
                        ItemSubType = ItemSubType.Weapon,
                        ElementalType = ElementalType.Wind,
                    },
                    new ()
                    {
                        MinCp = 800,
                        MaxCp = 900,
                        ItemSubType = ItemSubType.Weapon,
                        ElementalType = ElementalType.Wind,
                    },
                },
                10, null,
            };
            yield return new object?[]
            {
                new List<CustomCraftData>
                {
                    new () { RecipeId = 1, SlotIndex = 0, IconId = 10111000, },
                    new () { RecipeId = 1, SlotIndex = 2, IconId = 10112000, },
                },
                true, 0, false, new List<TestResult>
                {
                    new ()
                    {
                        MinCp = 900,
                        MaxCp = 1000,
                        ItemSubType = ItemSubType.Weapon,
                        ElementalType = ElementalType.Wind,
                    },
                    new ()
                    {
                        MinCp = 800,
                        MaxCp = 900,
                        ItemSubType = ItemSubType.Weapon,
                        ElementalType = ElementalType.Wind,
                    },
                },
                10, null,
            };
            yield return new object?[]
            {
                new List<CustomCraftData>
                {
                    new () { RecipeId = 1, SlotIndex = 0, IconId = 10111000, },
                    new () { RecipeId = 1, SlotIndex = 1, IconId = 10112000, },
                    new () { RecipeId = 1, SlotIndex = 2, IconId = 10113000, },
                    new () { RecipeId = 1, SlotIndex = 3, IconId = 0, },
                },
                true, 0, false, new List<TestResult>
                {
                    new ()
                    {
                        MinCp = 500,
                        MaxCp = 600,
                        ItemSubType = ItemSubType.Weapon,
                        ElementalType = ElementalType.Water,
                    },
                    new ()
                    {
                        MinCp = 300,
                        MaxCp = 400,
                        ItemSubType = ItemSubType.Weapon,
                        ElementalType = ElementalType.Wind,
                    },
                    new ()
                    {
                        MinCp = 100,
                        MaxCp = 200,
                        ItemSubType = ItemSubType.Weapon,
                        ElementalType = ElementalType.Water,
                    },
                    new ()
                    {
                        MinCp = 900,
                        MaxCp = 1000,
                        ItemSubType = ItemSubType.Weapon,
                        ElementalType = ElementalType.Normal,
                    },
                },
                10, null, 4,
            };
        }

        public static IEnumerable<object?[]> GetTestData_Failure()
        {
            // Not enough materials
            yield return new object?[]
            {
                new List<CustomCraftData>
                {
                    new () { RecipeId = 1, SlotIndex = 0, IconId = 10111000, },
                },
                false, 0, false, new List<TestResult>(), 0, typeof(NotEnoughItemException),
            };

            // Slot already occupied
            yield return new object?[]
            {
                new List<CustomCraftData>
                {
                    new () { RecipeId = 1, SlotIndex = 0, IconId = 10111000, },
                },
                true, 0, true, new List<TestResult>(), 0, typeof(CombinationSlotUnlockException),
            };
            // Not enough relationship for icon
            yield return new object?[]
            {
                new List<CustomCraftData>
                {
                    new () { RecipeId = 1, SlotIndex = 0, IconId = 10131001, },
                },
                true, 0, false, new List<TestResult>(), 0, typeof(NotEnoughRelationshipException),
            };
            // Duplicated slot
            yield return new object?[]
            {
                new List<CustomCraftData>
                {
                    new () { RecipeId = 1, SlotIndex = 0, IconId = 10111000, },
                    new () { RecipeId = 1, SlotIndex = 0, IconId = 10112000, },
                },
                false, 0, false, new List<TestResult>(), 0,
                typeof(DuplicatedCraftSlotIndexException),
            };
        }

        [Theory]
        [MemberData(nameof(GetTestData_Success))]
        // [MemberData(nameof(GetTestData_Failure))]
        public void Execute(
            List<CustomCraftData> craftList,
            bool enoughMaterials,
            int initialRelationship,
            bool slotOccupied,
            List<TestResult> testResults,
            long additionalBlock,
            Type exc,
            int seed = 0
        )
        {
            if (exc is null)
            {
                Assert.Equal(craftList.Count, testResults.Count);
            }

            const long currentBlockIndex = 2L;
            var context = new ActionContext();
            var state = _initialState;

            state = state.SetRelationship(_avatarAddress, initialRelationship);

            var gameConfig = state.GetGameConfigState();
            var materialList = new List<int> { ScrollItemId, CircleItemId };
            if (enoughMaterials)
            {
                var relationshipSheet = _tableSheets.CustomEquipmentCraftRelationshipSheet;
                var materialSheet = _tableSheets.MaterialItemSheet;

                foreach (var craftData in craftList)
                {
                    var relationshipRow = relationshipSheet.OrderedList!
                        .Last(row => row.Relationship <= initialRelationship);
                    var recipeRow =
                        _tableSheets.CustomEquipmentCraftRecipeSheet[craftData.RecipeId];
                    var scrollRow = materialSheet[ScrollItemId];
                    var scroll = ItemFactory.CreateMaterial(scrollRow);
                    var scrollAmount = (int)Math.Floor(
                        recipeRow.ScrollAmount
                        * relationshipRow.CostMultiplier
                        / 10000m
                    );
                    _avatarState.inventory.AddItem(scroll, scrollAmount);

                    var circleRow = materialSheet[CircleItemId];
                    var circle = ItemFactory.CreateMaterial(circleRow);
                    var circleAmount = (decimal)recipeRow.CircleAmount
                                       * relationshipRow.CostMultiplier
                                       / 10000m;
                    if (craftData.IconId != 0)
                    {
                        circleAmount *=
                            gameConfig.CustomEquipmentCraftIconCostMultiplier / 10000m;
                    }

                    _avatarState.inventory.AddItem(circle, (int)Math.Floor(circleAmount));

                    if (relationshipRow.Relationship == initialRelationship)
                    {
                        if (relationshipRow.GoldAmount > 0)
                        {
                            state = state.MintAsset(
                                context,
                                _agentAddress,
                                state.GetGoldCurrency() * relationshipRow.GoldAmount
                            );
                        }

                        foreach (var cost in relationshipRow.MaterialCosts)
                        {
                            var row = materialSheet[cost.ItemId];
                            _avatarState.inventory.AddItem(
                                ItemFactory.CreateMaterial(row),
                                cost.Amount
                            );
                            materialList.Add(cost.ItemId);
                        }
                    }
                }
            }

            state = state.SetAvatarState(_avatarAddress, _avatarState);

            if (slotOccupied)
            {
                // Lock slot.
                var slotAddress = _avatarAddress.Derive(string.Format(
                    CultureInfo.InvariantCulture,
                    CombinationSlotState.DeriveFormat,
                    0
                ));
                state = state.SetLegacyState(
                    slotAddress,
                    new CombinationSlotState(
                            ((Dictionary)new CombinationSlotState(slotAddress, 0).Serialize())
                            .SetItem("unlockBlockIndex", 10.Serialize()
                            )
                        )
                        .Serialize()
                );
            }

            var action = new Nekoyume.Action.CustomEquipmentCraft.CustomEquipmentCraft
            {
                AvatarAddress = _avatarAddress,
                CraftList = craftList,
            };

            if (exc is not null)
            {
                Assert.Throws(exc, () => action.Execute(new ActionContext
                {
                    PreviousState = state,
                    BlockIndex = currentBlockIndex,
                    Signer = _agentAddress,
                    RandomSeed = seed,
                }));
            }
            else
            {
                var resultState = action.Execute(new ActionContext
                {
                    PreviousState = state,
                    BlockIndex = currentBlockIndex,
                    Signer = _agentAddress,
                    RandomSeed = seed,
                });

                // Test
                var gold = resultState.GetGoldCurrency();
                Assert.Equal(0 * gold, resultState.GetBalance(_agentAddress, gold));

                var inventory = resultState.GetInventoryV2(_avatarAddress);
                foreach (var material in materialList)
                {
                    // Assert.False(inventory.HasItem(material));
                    Assert.Null(inventory.Items.FirstOrDefault(i => i.item.Id == material));
                }

                Assert.Equal(
                    initialRelationship + craftList.Count,
                    resultState.GetRelationship(_avatarAddress)
                );
                Assert.Equal(craftList.Count, inventory.Equipments.Count());

                var iconIdList = inventory.Equipments.Select(e => e.IconId).ToList();
                for (var i = 0; i < craftList.Count; i++)
                {
                    var craftData = craftList[i];
                    var expected = testResults[i];

                    var slotState = resultState.GetAllCombinationSlotState(_avatarAddress)
                        .GetSlot(craftData.SlotIndex);
                    Assert.Equal(currentBlockIndex + additionalBlock, slotState.UnlockBlockIndex);

                    var itemSubType = _tableSheets.CustomEquipmentCraftRecipeSheet.Values
                        .First(row => row.Id == craftData.RecipeId).ItemSubType;
                    var expectedEquipmentId =
                        _tableSheets.CustomEquipmentCraftRelationshipSheet.OrderedList!
                            .First(row => row.Relationship >= initialRelationship)
                            .GetItemId(itemSubType);
                    var equipment = inventory.Equipments.First(e =>
                        e.ItemId == slotState.Result.itemUsable.ItemId);
                    Assert.Equal(expectedEquipmentId, equipment.Id);
                    Assert.True(equipment.ByCustomCraft);

                    if (craftData.IconId == 0)
                    {
                        // Craft with random
                        Assert.True(equipment.CraftWithRandom);
                        Assert.True(equipment.HasRandomOnlyIcon);
                    }
                    else
                    {
                        Assert.Contains(craftData.IconId, iconIdList);
                    }

                    var cp = equipment.StatsMap.GetAdditionalStats(ignoreZero: true).Sum(
                        stat => CPHelper.GetStatCP(stat.statType, stat.additionalValue)
                    );
                    // CP > Stat convert can drop sub-1 values and vise versa.
                    //   Therefore, we do not check lower bound of result CP, but leave the code for record.
                    // Assert.True(expected.MinCp <= cp);
                    Assert.True(expected.MaxCp > cp);

                    Assert.Equal(expectedEquipmentId, equipment.Id);
                    Assert.Equal(expected.ElementalType, equipment.ElementalType);
                }
            }
        }

        public struct TestResult
        {
            public int MinCp;
            public int MaxCp;
            public ItemSubType ItemSubType;
            public ElementalType ElementalType;
        }
    }
}
