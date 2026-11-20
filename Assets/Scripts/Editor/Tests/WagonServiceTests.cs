using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Relayout
{
    public sealed class WagonServiceTests
    {
        GameObject host;
        PuzzleInventoryService service;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("WagonTestService") { hideFlags = HideFlags.HideAndDontSave };
            service = host.AddComponent<PuzzleInventoryService>();
            R.Call(service, "UseStockSeed", 12345);
            R.Call(service, "Initialize");
            R.Set(service, "Funds", 5000L);
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PurchaseThenMove_KeepsTrainConsistent()
        {
            var products = R.List(service, "WagonProductsHere");
            Assert.GreaterOrEqual(products.Count, 1, "Expected at least one wagon product in starting city");
            var product = products[0];
            string productId = R.Get<string>(product, "id");
            long productPrice = R.Get<long>(product, "price");

            int initialCount = service.WagonCount;
            var originalFirst = service.TrainWagons[0];
            long fundsBefore = service.Funds;

            Assert.IsTrue(R.CallOutString(service, "TryPurchaseWagon", productId, out string failReason), failReason);
            Assert.AreEqual(initialCount + 1, service.WagonCount);
            Assert.AreEqual(fundsBefore - productPrice, service.Funds);

            int newIndex = service.WagonCount - 1;
            var purchased = service.TrainWagons[newIndex];
            Assert.AreNotSame(originalFirst, purchased);

            // Select original first wagon, then move purchased wagon to index 0
            Assert.IsTrue(R.Call<bool>(service, "SelectWagon", 0));
            Assert.AreSame(originalFirst, service.Train);

            Assert.IsTrue(R.Call<bool>(service, "TryMoveWagon", newIndex, 0));
            Assert.AreSame(purchased, service.TrainWagons[0]);
            Assert.AreSame(originalFirst, service.TrainWagons[1]);
            // CurrentWagonIndex must follow the previously selected wagon
            Assert.AreSame(originalFirst, service.Train);
            Assert.AreEqual(1, service.CurrentWagonIndex);
        }

        [Test]
        public void TryMoveWagon_RejectsInvalidAndSameIndex()
        {
            var product = R.List(service, "WagonProductsHere")[0];
            string productId = R.Get<string>(product, "id");
            Assert.IsTrue(R.CallOutString(service, "TryPurchaseWagon", productId, out string failReason), failReason);

            Assert.IsFalse(R.Call<bool>(service, "TryMoveWagon", 0, 0));
            Assert.IsFalse(R.Call<bool>(service, "TryMoveWagon", -1, 0));
            Assert.IsFalse(R.Call<bool>(service, "TryMoveWagon", 0, service.WagonCount));
        }

        [Test]
        public void TrySellWagon_SellsEmptyWagon_RejectsLoadedOrLastWagon()
        {
            var product = R.List(service, "WagonProductsHere")[0];
            string productId = R.Get<string>(product, "id");
            Assert.IsTrue(R.CallOutString(service, "TryPurchaseWagon", productId, out string failReason), failReason);
            Assert.AreEqual(2, service.WagonCount);

            // Wagon 0 has starting cargo -> cannot sell
            Assert.Greater(R.Get<int>(service.TrainWagons[0], "OccupiedCells"), 0);
            Assert.IsFalse(R.CallOutString(service, "TrySellWagon", 0, out string loadedFail));
            Assert.AreEqual("saleLoaded", loadedFail);

            // Wagon 1 is empty -> can sell
            var emptyWagon = service.TrainWagons[1];
            long expectedSale = R.Get<long>(emptyWagon, "WagonSalePrice");
            long fundsBefore = service.Funds;
            Assert.IsTrue(R.Call<bool>(service, "SelectWagon", 0));
            Assert.IsTrue(R.Call<bool>(service, "SelectWagon", 1));

            Assert.IsTrue(R.CallOutString(service, "TrySellWagon", 1, out string sellFail), sellFail);
            Assert.AreEqual(1, service.WagonCount);
            Assert.AreEqual(fundsBefore + expectedSale, service.Funds);
            Assert.AreEqual(0, service.CurrentWagonIndex);
            Assert.IsNotNull(service.Train);

            // Last wagon cannot be sold even if empty
            var placements = R.List(service.TrainWagons[0], "Placements");
            foreach (var p in placements)
            {
                R.Call(service.TrainWagons[0], "Remove", p);
            }
            Assert.AreEqual(0, R.Get<int>(service.TrainWagons[0], "OccupiedCells"));
            Assert.IsFalse(R.CallOutString(service, "TrySellWagon", 0, out string lastFail));
            Assert.AreEqual("saleLastWagon", lastFail);
        }
    }
}
