using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Relayout
{
    public sealed class QuestRuleTests
    {
        GameObject host;
        PuzzleInventoryService service;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("QuestRuleTestService") { hideFlags = HideFlags.HideAndDontSave };
            service = host.AddComponent<PuzzleInventoryService>();
            R.Call(service, "UseStockSeed", 12345);
            R.Call(service, "Initialize");
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        static bool TryFindPlacementSlot(PuzzleInventory _wagon, object _def,
            out Vector2Int _grabbedCell, out Vector2Int _targetCell)
        {
            var cells = R.List(_def, "Cells").Cast<Vector2Int>().ToList();
            _grabbedCell = cells[0];
            for (int y = 0; y < _wagon.Height; y++)
            {
                for (int x = 0; x < _wagon.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (R.Call<bool>(_wagon, "CanPlaceAt", _def, _grabbedCell, cell, 0))
                    {
                        _targetCell = cell;
                        return true;
                    }
                }
            }
            _targetCell = default;
            return false;
        }

        static void TravelToCity(PuzzleInventoryService _service, string _targetCityId)
        {
            var planResult = R.Call(_service, "PlanRoute", _targetCityId);
            Assert.AreEqual("None", planResult.ToString(), "Route plan to quest target failed");
            int safety = 30;
            while (!string.Equals(_service.CurrentCityId, _targetCityId, StringComparison.Ordinal) && safety-- > 0)
            {
                var routePlan = R.Get(_service, "RoutePlan");
                string nextCity = routePlan != null ? R.Get<string>(routePlan, "NextCityId") : null;
                Assert.IsFalse(string.IsNullOrEmpty(nextCity), "Expected next city in route plan");
                var travelResult = R.Call(_service, "Travel", nextCity);
                Assert.IsTrue(R.Get<bool>(travelResult, "IsSuccess"),
                    $"Travel to {nextCity} failed: {R.Get(travelResult, "Failure")}");
            }
            Assert.AreEqual(_targetCityId, _service.CurrentCityId);
        }

        [Test]
        public void AcceptAtChosenPlacement_ChargesDepositAndMatchesAutoPlacementState()
        {
            GameObject autoHost = null;
            try
            {
                int chosenSeed = 12345;
                for (int seed = 100; seed < 200; seed++)
                {
                    var probeGo = new GameObject("Probe") { hideFlags = HideFlags.HideAndDontSave };
                    var probe = probeGo.AddComponent<PuzzleInventoryService>();
                    R.Call(probe, "UseStockSeed", seed);
                    R.Call(probe, "Initialize");
                    var defs = R.List(probe, "ItemDefinitions");
                    var offers = R.List(probe, "QuestOffers");
                    bool hasFresh = offers.Any(_o =>
                    {
                        string pid = R.Get<string>(_o, "PieceId");
                        var def = defs.FirstOrDefault(_d => R.Get<string>(_d, "Id") == pid);
                        return def != null && R.Get<int>(def, "ShelfLifeHours") > 0;
                    });
                    UnityEngine.Object.DestroyImmediate(probeGo);
                    if (hasFresh)
                    {
                        chosenSeed = seed;
                        break;
                    }
                }

                UnityEngine.Object.DestroyImmediate(host);
                host = new GameObject("QuestRuleTestService") { hideFlags = HideFlags.HideAndDontSave };
                service = host.AddComponent<PuzzleInventoryService>();
                R.Call(service, "UseStockSeed", chosenSeed);
                R.Call(service, "Initialize");
                R.Set(service, "Funds", 5000L);

                autoHost = new GameObject("AutoQuestTestService") { hideFlags = HideFlags.HideAndDontSave };
                var autoHostService = autoHost.AddComponent<PuzzleInventoryService>();
                R.Call(autoHostService, "UseStockSeed", chosenSeed);
                R.Call(autoHostService, "Initialize");
                R.Set(autoHostService, "Funds", 5000L);

                var itemDefs = R.List(service, "ItemDefinitions");
                var questOffers = R.List(service, "QuestOffers");
                var offer = questOffers.FirstOrDefault(_o =>
                {
                    string pid = R.Get<string>(_o, "PieceId");
                    var def = itemDefs.FirstOrDefault(_d => R.Get<string>(_d, "Id") == pid);
                    return def != null && R.Get<int>(def, "ShelfLifeHours") > 0;
                }) ?? questOffers[0];

                string offerPieceId = R.Get<string>(offer, "PieceId");
                string offerTarget = R.Get<string>(offer, "TargetCityId");
                long offerDeposit = R.Get<long>(offer, "Deposit");

                var autoOffer = R.List(autoHostService, "QuestOffers")
                    .First(_o => R.Get<string>(_o, "PieceId") == offerPieceId && R.Get<string>(_o, "TargetCityId") == offerTarget);

                var definition = itemDefs.First(_d => R.Get<string>(_d, "Id") == offerPieceId);
                Assert.IsTrue(TryFindPlacementSlot(service.Train, definition, out var grabbedCell, out var targetCell));

                long fundsBefore = service.Funds;
                var acceptResult = R.Call(service, "TryAcceptQuestAt", offer, service.Train, grabbedCell, targetCell, 0);
                Assert.AreEqual("None", acceptResult.ToString());
                Assert.AreEqual(fundsBefore - offerDeposit, service.Funds);

                var autoAcceptResult = R.Call(autoHostService, "TryAcceptQuest", autoOffer);
                Assert.AreEqual("None", autoAcceptResult.ToString());

                var quest = R.List(service, "Quests").Single();
                var autoQuest = R.List(autoHostService, "Quests").Single();
                int cargoId = R.Get<int>(quest, "CargoInstanceId");
                int autoCargoId = R.Get<int>(autoQuest, "CargoInstanceId");

                var cargo = R.List(service.Train, "Placements").Single(_p => R.Get<int>(_p, "InstanceId") == cargoId);
                var autoCargo = R.List(autoHostService.Train, "Placements").Single(_p => R.Get<int>(_p, "InstanceId") == autoCargoId);

                var pose = R.Get<PuzzlePose>(cargo, "Pose");
                var occupied = pose.Cells.Select(_c => _c.Position).ToArray();
                CollectionAssert.Contains(occupied, targetCell);
                Assert.GreaterOrEqual(R.Get<long>(cargo, "OwnedHours"), 0L);
                Assert.AreEqual(Convert.ToDouble(R.Get(autoCargo, "FreshnessPercent")),
                    Convert.ToDouble(R.Get(cargo, "FreshnessPercent")), 0.001);
                Assert.AreEqual(R.Get<long>(autoCargo, "OwnedHours"), R.Get<long>(cargo, "OwnedHours"));
                Assert.AreEqual(Convert.ToDecimal(R.Get(autoCargo, "ActualValue")),
                    Convert.ToDecimal(R.Get(cargo, "ActualValue")));
            }
            finally
            {
                if (autoHost != null)
                {
                    UnityEngine.Object.DestroyImmediate(autoHost);
                }
            }
        }

        [Test]
        public void ArrivalDoesNotAutoComplete_DropCompletesWithPayoutAndXp_NoDoublePayout()
        {
            R.Set(service, "Funds", 5000L);
            var offer = R.List(service, "QuestOffers")[0];
            string pieceId = R.Get<string>(offer, "PieceId");
            var definition = R.List(service, "ItemDefinitions").First(_d => R.Get<string>(_d, "Id") == pieceId);
            Assert.IsTrue(TryFindPlacementSlot(service.Train, definition, out var grabbedCell, out var targetCell));

            var acceptResult = R.Call(service, "TryAcceptQuestAt", offer, service.Train, grabbedCell, targetCell, 0);
            Assert.AreEqual("None", acceptResult.ToString());

            var quest = R.List(service, "Quests").Single();
            string targetCityId = R.Get<string>(quest, "TargetCityId");
            string profTag = R.Get<string>(quest, "ProficiencyTag");
            long profXp = R.Get<long>(quest, "ProficiencyXp");
            long reward = R.Get<long>(quest, "Reward");
            long deposit = R.Get<long>(quest, "Deposit");
            int cargoId = R.Get<int>(quest, "CargoInstanceId");
            long xpBefore = R.Call<long>(service, "ProficiencyXp", profTag);

            TravelToCity(service, targetCityId);

            // Arrival must NOT auto-complete
            Assert.AreEqual("Active", R.Get(quest, "State").ToString());
            var cargo = R.List(service.Train, "Placements").FirstOrDefault(_p => R.Get<int>(_p, "InstanceId") == cargoId);
            Assert.IsNotNull(cargo, "Quest cargo should still be on the train after arrival");

            long fundsBeforeDrop = service.Funds;
            decimal roundedValue = Math.Round(R.Get<decimal>(cargo, "FreshnessAdjustedValue"), MidpointRounding.AwayFromZero);
            long expectedCargoValue = roundedValue > 0 ? (long)roundedValue : 0;
            long expectedPayout = reward + expectedCargoValue + deposit;

            Assert.IsTrue(R.CallOutString(service, "TryCompleteQuestWithCargo", cargo, out string failReason), failReason);
            Assert.AreEqual("Completed", R.Get(quest, "State").ToString());
            Assert.AreEqual(fundsBeforeDrop + expectedPayout, service.Funds);
            Assert.AreEqual(xpBefore + profXp, R.Call<long>(service, "ProficiencyXp", profTag));
            Assert.IsFalse(R.List(service.Train, "Placements").Any(_p => R.Get<int>(_p, "InstanceId") == cargoId));

            // No double payout
            long fundsAfterComplete = service.Funds;
            Assert.IsFalse(R.CallOutString(service, "TryCompleteQuestWithCargo", cargo, out _));
            Assert.IsFalse(R.CallOutString(service, "TryCompleteQuest", quest, out _));
            Assert.AreEqual(fundsAfterComplete, service.Funds);
        }

        [Test]
        public void WagonQuestDrop_GrantsWagonWithoutLevelUp_OnlyOnce()
        {
            var existing = R.List(service, "QuestOffers")[0];
            var offer = Activator.CreateInstance(existing.GetType(),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new object[]
                {
                    "courier.wagon4", R.Get<string>(existing, "PieceId"), service.CurrentCityId,
                    service.CurrentCityId, "penalty.deposit", 0L, 0L, 0L, 72L,
                    R.Get<string>(existing, "ProficiencyTag"), 0L, "questReward.4"
                }, null);
            ((System.Collections.IList)R.Get(service, "questOffers")).Add(offer);
            int before = service.TrainWagons.Count;
            Assert.Less(before, R.Get<int>(service, "MaxWagonCount"));
            Assert.AreEqual("None", R.Call(service, "TryAcceptQuest", offer).ToString());
            var quest = R.List(service, "Quests").Single();
            int cargoId = R.Get<int>(quest, "CargoInstanceId");
            var cargo = service.TrainWagons.SelectMany(_w => R.List(_w, "Placements"))
                .Single(_p => R.Get<int>(_p, "InstanceId") == cargoId);

            Assert.IsTrue(R.CallOutString(service, "TryCompleteQuestWithCargo", cargo, out string failure), failure);
            Assert.AreEqual(before + 1, service.TrainWagons.Count);
            Assert.IsTrue(R.Get<bool>(quest, "RewardWagonReceived"));
            Assert.AreEqual("questReward.4", R.Get<string>(R.Get(service.TrainWagons.Last(), "WagonProduct"), "id"));
            Assert.IsFalse(R.CallOutString(service, "TryCompleteQuest", quest, out _));
            Assert.AreEqual(before + 1, service.TrainWagons.Count);
        }

        [Test]
        public void WrongCityAndNonQuestCargo_AreRejected()
        {
            R.Set(service, "Funds", 5000L);
            var offer = R.List(service, "QuestOffers")[0];
            string pieceId = R.Get<string>(offer, "PieceId");
            var definition = R.List(service, "ItemDefinitions").First(_d => R.Get<string>(_d, "Id") == pieceId);
            Assert.IsTrue(TryFindPlacementSlot(service.Train, definition, out var grabbedCell, out var targetCell));

            Assert.AreEqual("None",
                R.Call(service, "TryAcceptQuestAt", offer, service.Train, grabbedCell, targetCell, 0).ToString());

            var quest = R.List(service, "Quests").Single();
            int cargoId = R.Get<int>(quest, "CargoInstanceId");
            string targetCityId = R.Get<string>(quest, "TargetCityId");
            var placements = R.List(service.Train, "Placements");
            var questCargo = placements.Single(_p => R.Get<int>(_p, "InstanceId") == cargoId);
            var nonQuestCargo = placements.First(_p => R.Get<int>(_p, "InstanceId") != cargoId);

            // Wrong city
            Assert.AreNotEqual(targetCityId, service.CurrentCityId);
            Assert.IsFalse(R.CallOutString(service, "TryCompleteQuestWithCargo", questCargo, out string wrongCityReason));
            Assert.IsFalse(string.IsNullOrEmpty(wrongCityReason));
            Assert.AreEqual("Active", R.Get(quest, "State").ToString());

            // Non-quest cargo rejected at target city
            TravelToCity(service, targetCityId);
            Assert.IsFalse(R.CallOutString(service, "TryCompleteQuestWithCargo", nonQuestCargo, out string nonQuestReason));
            Assert.IsFalse(string.IsNullOrEmpty(nonQuestReason));
            Assert.AreEqual("Active", R.Get(quest, "State").ToString());
        }

        [Test]
        public void FinalSettlement_CompletesQuestsAlreadyAtTarget()
        {
            R.Set(service, "Funds", 5000L);
            while (service.Time.RemainingHours > 30)
            {
                int turns = Math.Min(4, (int)((service.Time.RemainingHours - 30) / 6));
                if (turns <= 0) break;
                Assert.IsTrue(R.Call<bool>(service, "Rest", turns));
            }

            // Pick an offer whose target can be reached within remaining hours before deadline
            var offers = R.List(service, "QuestOffers");
            object offer = offers[0];
            foreach (var candidate in offers)
            {
                string candTarget = R.Get<string>(candidate, "TargetCityId");
                long candDeadline = R.Get<long>(candidate, "DeadlineHours");
                if (candDeadline >= service.Time.RemainingHours)
                {
                    offer = candidate;
                    break;
                }
            }
            string pieceId = R.Get<string>(offer, "PieceId");
            var definition = R.List(service, "ItemDefinitions").First(_d => R.Get<string>(_d, "Id") == pieceId);
            Assert.IsTrue(TryFindPlacementSlot(service.Train, definition, out var grabbedCell, out var targetCell));
            Assert.AreEqual("None",
                R.Call(service, "TryAcceptQuestAt", offer, service.Train, grabbedCell, targetCell, 0).ToString());

            var quest = R.List(service, "Quests").Single();
            string targetCityId = R.Get<string>(quest, "TargetCityId");
            int cargoId = R.Get<int>(quest, "CargoInstanceId");

            TravelToCity(service, targetCityId);
            Assert.AreEqual("Active", R.Get(quest, "State").ToString());

            long fundsBeforeSettlement = service.Funds;
            int safety = 20;
            while (service.RunStatus == PuzzleRunStatus.InProgress && safety-- > 0)
            {
                int remainingTurns = (int)(service.Time.RemainingHours / 6);
                int restTurns = Math.Clamp(remainingTurns, 1, 4);
                Assert.IsTrue(R.Call<bool>(service, "Rest", restTurns));
            }

            Assert.AreNotEqual(PuzzleRunStatus.InProgress, service.RunStatus);
            Assert.AreEqual("Completed", R.Get(quest, "State").ToString());
            Assert.Greater(service.Funds, fundsBeforeSettlement);
            Assert.IsFalse(R.List(service.Train, "Placements").Any(_p => R.Get<int>(_p, "InstanceId") == cargoId));
        }
    }
}
