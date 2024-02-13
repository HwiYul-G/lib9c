namespace Lib9c.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Lib9c.Tests.Action;
    using Libplanet.Action;
    using Nekoyume.Arena;
    using Nekoyume.Model;
    using Nekoyume.Model.BattleStatus.Arena;
    using Nekoyume.Model.Item;
    using Nekoyume.Model.Skill;
    using Nekoyume.Model.Stat;
    using Nekoyume.Model.State;
    using Xunit;

    public class ArenaSimulatorTest
    {
        private readonly TableSheets _tableSheets;
        private readonly IRandom _random;
        private readonly AvatarState _avatarState1;
        private readonly AvatarState _avatarState2;

        private readonly ArenaAvatarState _arenaAvatarState1;
        private readonly ArenaAvatarState _arenaAvatarState2;

        public ArenaSimulatorTest()
        {
            _tableSheets = new TableSheets(TableSheetsImporter.ImportSheets());
            _random = new TestRandom();

            _avatarState1 = new AvatarState(
                default,
                default,
                0,
                _tableSheets.GetAvatarSheets(),
                new GameConfigState(),
                default
            );

            _avatarState2 = new AvatarState(
                default,
                default,
                0,
                _tableSheets.GetAvatarSheets(),
                new GameConfigState(),
                default
            );

            _arenaAvatarState1 = new ArenaAvatarState(_avatarState1);
            _arenaAvatarState2 = new ArenaAvatarState(_avatarState2);
        }

        [Fact]
        public void Simulate()
        {
            var simulator = new ArenaSimulator(_random, 10);
            var myDigest = new ArenaPlayerDigest(_avatarState1, _arenaAvatarState1);
            var enemyDigest = new ArenaPlayerDigest(_avatarState2, _arenaAvatarState2);
            var arenaSheets = _tableSheets.GetArenaSimulatorSheets();
            var log = simulator.Simulate(
                myDigest,
                enemyDigest,
                arenaSheets,
                new List<StatModifier>
                {
                    new (StatType.ATK, StatModifier.OperationType.Add, 1),
                },
                new List<StatModifier>
                {
                    new (StatType.DEF, StatModifier.OperationType.Add, 1),
                }
            );

            Assert.Equal(_random, simulator.Random);

            var turn = log.Events.OfType<ArenaTurnEnd>().Count();
            Assert.Equal(simulator.Turn, turn);

            var players = log.Events.OfType<ArenaSpawnCharacter>();
            var arenaCharacters = new List<ArenaCharacter>();
            foreach (var player in players)
            {
                if (player.Character is ArenaCharacter arenaCharacter)
                {
                    arenaCharacters.Add(arenaCharacter);
                }
            }

            Assert.Equal(2, players.Count());
            Assert.Equal(2, arenaCharacters.Count);
            var challenger = arenaCharacters.Single(a => !a.IsEnemy);
            var enemy = arenaCharacters.Single(a => a.IsEnemy);
            Assert.Equal(enemy.ATK + 1, challenger.ATK);
            Assert.Equal(challenger.DEF + 1, enemy.DEF);

            var dead = log.Events.OfType<ArenaDead>();
            Assert.Single(dead);
            var deadCharacter = dead.First().Character;
            Assert.True(deadCharacter.IsDead);
            Assert.Equal(0, deadCharacter.CurrentHP);
            if (log.Result == ArenaLog.ArenaResult.Win)
            {
                Assert.True(deadCharacter.IsEnemy);
            }
            else
            {
                Assert.False(deadCharacter.IsEnemy);
            }
        }

        [Theory]
        [InlineData(10)]
        [InlineData(5)]
        [InlineData(null)]
        public void HpIncreasingModifier(int? modifier)
        {
            var simulator = modifier.HasValue ? new ArenaSimulator(_random, modifier.Value) : new ArenaSimulator(_random);
            var myDigest = new ArenaPlayerDigest(_avatarState1, _arenaAvatarState1);
            var enemyDigest = new ArenaPlayerDigest(_avatarState2, _arenaAvatarState2);
            var arenaSheets = _tableSheets.GetArenaSimulatorSheets();
            var log = simulator.Simulate(myDigest, enemyDigest, arenaSheets, new List<StatModifier>(), new List<StatModifier>());
            var expectedHpModifier = modifier ?? 2;

            Assert.Equal(_random, simulator.Random);
            Assert.Equal(expectedHpModifier, simulator.HpModifier);

            var turn = log.Events.OfType<ArenaTurnEnd>().Count();
            Assert.Equal(simulator.Turn, turn);

            var players = log.Events
                .OfType<ArenaSpawnCharacter>()
                .Select(p => p.Character)
                .ToList();
            Assert.Equal(2, players.Count);
            foreach (var player in players)
            {
                Assert.Equal(player.Stats.BaseHP * expectedHpModifier, player.CurrentHP);
            }
        }

        [Fact]
        public void Thorns()
        {
            var random = new TestRandom();
            var equipmentRow =
                _tableSheets.EquipmentItemSheet.Values.First(r => r.Stat.StatType == StatType.HP);
            var skillId = 270000;
            var skillRow = _tableSheets.SkillSheet[skillId];
            var skill = SkillFactory.Get(skillRow, 0, 100, 700, StatType.HP);
            var equipment = (Equipment)ItemFactory.CreateItem(equipmentRow, random);
            equipment.Skills.Add(skill);
            var equipmentRow2 =
                _tableSheets.EquipmentItemSheet.Values.First(r => r.Stat.StatType == StatType.HIT);
            var equipment2 = (Equipment)ItemFactory.CreateItem(equipmentRow2, random);
            equipment2.Skills.Add(skill);
            var avatarState1 = _avatarState1;
            var avatarState2 = _avatarState2;
            avatarState1.inventory.AddItem(equipment);
            avatarState2.inventory.AddItem(equipment2);

            var arenaAvatarState1 = new ArenaAvatarState(avatarState1);
            arenaAvatarState1.UpdateEquipment(new List<Guid>
            {
                equipment.ItemId,
            });
            var arenaAvatarState2 = new ArenaAvatarState(avatarState2);
            arenaAvatarState2.UpdateEquipment(new List<Guid>
            {
                equipment2.ItemId,
            });

            var simulator = new ArenaSimulator(_random);
            var myDigest = new ArenaPlayerDigest(avatarState1, arenaAvatarState1);
            var enemyDigest = new ArenaPlayerDigest(avatarState2, arenaAvatarState2);
            var arenaSheets = _tableSheets.GetArenaSimulatorSheets();
            var log = simulator.Simulate(myDigest, enemyDigest, arenaSheets, new List<StatModifier>(), new List<StatModifier>(), true);
            var ticks = log.Events
                .OfType<ArenaTickDamage>()
                .ToList();
            var challengerTick = ticks.First(r => !r.Character.IsEnemy);
            var enemyTick = ticks.First(r => r.Character.IsEnemy);
            Assert.True(challengerTick.Character.HP > enemyTick.Character.HP);
            Assert.True(enemyTick.SkillInfos.First().Effect > challengerTick.SkillInfos.First().Effect);
        }
    }
}
